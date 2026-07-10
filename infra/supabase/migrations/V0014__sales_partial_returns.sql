alter table app.sales
    drop constraint if exists ck_sales_status;

alter table app.sales
    add constraint ck_sales_status check (status in ('confirmed', 'partially_returned', 'reversed'));

create table if not exists app.sale_returns (
    sale_return_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    sale_id uuid not null references app.sales (sale_id) on delete cascade,
    cash_session_id uuid references app.cash_sessions (cash_session_id),
    customer_account_movement_id uuid references app.customer_account_movements (customer_account_movement_id),
    returned_by_user_id uuid not null references app.users (user_id),
    return_total_amount numeric(18, 2) not null,
    refunded_paid_amount numeric(18, 2) not null,
    credited_account_amount numeric(18, 2) not null,
    notes text,
    created_at timestamptz not null default timezone('utc', now()),
    constraint ck_sale_returns_amounts_non_negative check (
        return_total_amount > 0
        and refunded_paid_amount >= 0
        and credited_account_amount >= 0
    )
);

create index if not exists ix_sale_returns_sale_created_at
    on app.sale_returns (sale_id, created_at desc);

create table if not exists app.sale_return_items (
    sale_return_item_id uuid primary key,
    sale_return_id uuid not null references app.sale_returns (sale_return_id) on delete cascade,
    sale_item_id uuid not null references app.sale_items (sale_item_id) on delete cascade,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    product_id uuid not null references app.products (product_id),
    quantity numeric(18, 3) not null,
    unit_price numeric(18, 2) not null,
    line_total numeric(18, 2) not null,
    created_at timestamptz not null default timezone('utc', now()),
    constraint ck_sale_return_items_quantity_positive check (quantity > 0),
    constraint ck_sale_return_items_amounts_non_negative check (unit_price >= 0 and line_total >= 0)
);

create index if not exists ix_sale_return_items_return_id
    on app.sale_return_items (sale_return_id, created_at asc);

create index if not exists ix_sale_return_items_sale_item_id
    on app.sale_return_items (sale_item_id, created_at asc);

create or replace function app.return_sale_items(
    p_organization_id uuid,
    p_sale_id uuid,
    p_cash_session_id uuid,
    p_returned_by_user_id uuid,
    p_items jsonb,
    p_notes text default null
)
returns uuid
language plpgsql
as $$
declare
    v_sale record;
    v_item jsonb;
    v_sale_item record;
    v_sale_return_id uuid := gen_random_uuid();
    v_customer_credit_movement_id uuid;
    v_reference_document text;
    v_warehouse_id uuid := app.get_default_warehouse_id(p_organization_id);
    v_current_cash_balance numeric(18, 2);
    v_resulting_cash_balance numeric(18, 2);
    v_current_account_balance numeric(18, 2);
    v_resulting_account_balance numeric(18, 2);
    v_return_total_amount numeric(18, 2) := 0;
    v_refunded_paid_amount numeric(18, 2) := 0;
    v_credited_account_amount numeric(18, 2) := 0;
    v_requested_quantity numeric(18, 3);
    v_previously_returned_quantity numeric(18, 3);
    v_available_quantity numeric(18, 3);
    v_line_total numeric(18, 2);
    v_resulting_quantity numeric(18, 3);
    v_remaining_credit_amount numeric(18, 2);
    v_open_amount numeric(18, 2);
    v_applied_amount numeric(18, 2);
    v_debit record;
    v_payment record;
    v_payment_refund_amount numeric(18, 2);
    v_remaining_paid_refund numeric(18, 2);
begin
    if p_items is null or jsonb_typeof(p_items) <> 'array' or jsonb_array_length(p_items) = 0 then
        raise exception 'At least one return item is required.';
    end if;

    select
        s.sale_id,
        s.ticket_number,
        s.customer_id,
        s.customer_name,
        s.status,
        s.total_amount,
        s.paid_amount,
        s.account_balance_amount,
        s.due_date
      into v_sale
      from app.sales s
     where s.organization_id = p_organization_id
       and s.sale_id = p_sale_id
     for update;

    if v_sale.sale_id is null then
        raise exception 'The informed sale does not belong to the active organization.';
    end if;

    if v_sale.status = 'reversed' then
        raise exception 'Only confirmed sales can receive partial returns.';
    end if;

    for v_item in select value from jsonb_array_elements(p_items)
    loop
        if (v_item ->> 'saleItemId') is null then
            raise exception 'Each return item must include a valid sale item.';
        end if;

        v_requested_quantity := (v_item ->> 'quantity')::numeric;

        if v_requested_quantity is null or v_requested_quantity <= 0 then
            raise exception 'Each return item quantity must be greater than zero.';
        end if;

        select
            si.sale_item_id,
            si.product_id,
            si.product_name,
            si.quantity,
            si.unit_price
          into v_sale_item
          from app.sale_items si
         where si.sale_id = p_sale_id
           and si.sale_item_id = (v_item ->> 'saleItemId')::uuid;

        if v_sale_item.sale_item_id is null then
            raise exception 'One of the informed sale items does not belong to the sale.';
        end if;

        select coalesce(sum(sri.quantity), 0)
          into v_previously_returned_quantity
          from app.sale_return_items sri
         where sri.sale_item_id = v_sale_item.sale_item_id;

        v_available_quantity := v_sale_item.quantity - v_previously_returned_quantity;

        if v_requested_quantity > v_available_quantity then
            raise exception 'The informed return quantity exceeds the available quantity for the sale item.';
        end if;

        v_line_total := round(v_sale_item.unit_price * v_requested_quantity, 2);
        v_return_total_amount := v_return_total_amount + v_line_total;
    end loop;

    if v_return_total_amount <= 0 then
        raise exception 'The partial return total must be greater than zero.';
    end if;

    if v_sale.total_amount <= 0 then
        raise exception 'The informed sale cannot be partially returned.';
    end if;

    v_refunded_paid_amount := round(v_return_total_amount * (v_sale.paid_amount / v_sale.total_amount), 2);
    v_credited_account_amount := v_return_total_amount - v_refunded_paid_amount;

    if v_refunded_paid_amount > 0 then
        if p_cash_session_id is null then
            raise exception 'An open cash session is required to refund collected amounts.';
        end if;

        select cs.current_balance
          into v_current_cash_balance
          from app.cash_sessions cs
         where cs.organization_id = p_organization_id
           and cs.cash_session_id = p_cash_session_id
           and cs.status = 'open'
         for update;

        if v_current_cash_balance is null then
            raise exception 'The informed cash session is not open for the active organization.';
        end if;

        v_resulting_cash_balance := v_current_cash_balance - v_refunded_paid_amount;

        if v_resulting_cash_balance < 0 then
            raise exception 'There is not enough balance to refund the informed partial return.';
        end if;

        update app.cash_sessions
           set current_balance = v_resulting_cash_balance
         where cash_session_id = p_cash_session_id;
    end if;

    if v_credited_account_amount > 0 then
        if v_sale.customer_id is null then
            raise exception 'The informed sale does not support account credit adjustments.';
        end if;

        select coalesce(cam.resulting_balance, 0)
          into v_current_account_balance
          from app.customer_account_movements cam
         where cam.organization_id = p_organization_id
           and cam.customer_id = v_sale.customer_id
         order by cam.created_at desc, cam.customer_account_movement_id desc
         limit 1;

        v_resulting_account_balance := coalesce(v_current_account_balance, 0) - v_credited_account_amount;

        if v_resulting_account_balance < 0 then
            raise exception 'The partial return credit exceeds the outstanding customer balance.';
        end if;

        insert into app.customer_account_movements (
            customer_account_movement_id,
            organization_id,
            customer_id,
            sale_id,
            cash_session_id,
            movement_type,
            concept,
            reference_document,
            debit_amount,
            credit_amount,
            resulting_balance,
            due_date,
            notes,
            performed_by_user_id,
            created_at
        )
        values (
            gen_random_uuid(),
            p_organization_id,
            v_sale.customer_id,
            p_sale_id,
            null,
            'credit_note',
            'Nota de credito por devolucion parcial',
            concat('SALE-', lpad(v_sale.ticket_number::text, 8, '0')),
            0,
            v_credited_account_amount,
            v_resulting_account_balance,
            null,
            nullif(p_notes, ''),
            p_returned_by_user_id,
            timezone('utc', now())
        )
        returning customer_account_movement_id into v_customer_credit_movement_id;

        v_remaining_credit_amount := v_credited_account_amount;

        for v_debit in
            select
                cam.customer_account_movement_id,
                cam.debit_amount,
                coalesce(sum(cpa.applied_amount), 0) as applied_amount
            from app.customer_account_movements cam
            left join app.customer_payment_allocations cpa
                on cpa.debit_movement_id = cam.customer_account_movement_id
            where cam.organization_id = p_organization_id
              and cam.customer_id = v_sale.customer_id
              and cam.movement_type = 'sale_debit'
            group by cam.customer_account_movement_id, cam.debit_amount, cam.due_date, cam.created_at
            having cam.debit_amount > coalesce(sum(cpa.applied_amount), 0)
            order by cam.due_date asc nulls first, cam.created_at asc, cam.customer_account_movement_id asc
        loop
            exit when v_remaining_credit_amount <= 0;

            v_open_amount := v_debit.debit_amount - v_debit.applied_amount;
            v_applied_amount := least(v_open_amount, v_remaining_credit_amount);

            insert into app.customer_payment_allocations (
                customer_payment_allocation_id,
                organization_id,
                payment_movement_id,
                debit_movement_id,
                applied_amount,
                created_at
            )
            values (
                gen_random_uuid(),
                p_organization_id,
                v_customer_credit_movement_id,
                v_debit.customer_account_movement_id,
                v_applied_amount,
                timezone('utc', now())
            );

            v_remaining_credit_amount := v_remaining_credit_amount - v_applied_amount;
        end loop;
    end if;

    insert into app.sale_returns (
        sale_return_id,
        organization_id,
        sale_id,
        cash_session_id,
        customer_account_movement_id,
        returned_by_user_id,
        return_total_amount,
        refunded_paid_amount,
        credited_account_amount,
        notes,
        created_at
    )
    values (
        v_sale_return_id,
        p_organization_id,
        p_sale_id,
        p_cash_session_id,
        v_customer_credit_movement_id,
        p_returned_by_user_id,
        v_return_total_amount,
        v_refunded_paid_amount,
        v_credited_account_amount,
        nullif(p_notes, ''),
        timezone('utc', now())
    );

    for v_item in select value from jsonb_array_elements(p_items)
    loop
        select
            si.sale_item_id,
            si.product_id,
            si.product_name,
            si.quantity,
            si.unit_price
          into v_sale_item
          from app.sale_items si
         where si.sale_id = p_sale_id
           and si.sale_item_id = (v_item ->> 'saleItemId')::uuid;

        v_requested_quantity := (v_item ->> 'quantity')::numeric;
        v_line_total := round(v_sale_item.unit_price * v_requested_quantity, 2);

        insert into app.sale_return_items (
            sale_return_item_id,
            sale_return_id,
            sale_item_id,
            organization_id,
            product_id,
            quantity,
            unit_price,
            line_total,
            created_at
        )
        values (
            gen_random_uuid(),
            v_sale_return_id,
            v_sale_item.sale_item_id,
            p_organization_id,
            v_sale_item.product_id,
            v_requested_quantity,
            v_sale_item.unit_price,
            v_line_total,
            timezone('utc', now())
        );

        insert into app.stock_balances (
            stock_balance_id,
            organization_id,
            warehouse_id,
            product_id,
            on_hand_quantity,
            min_quantity,
            max_quantity,
            reorder_point,
            version_number,
            updated_at
        )
        values (
            gen_random_uuid(),
            p_organization_id,
            v_warehouse_id,
            v_sale_item.product_id,
            0,
            null,
            null,
            null,
            1,
            timezone('utc', now())
        )
        on conflict (organization_id, warehouse_id, product_id) do nothing;

        update app.stock_balances
           set on_hand_quantity = on_hand_quantity + v_requested_quantity,
               version_number = version_number + 1,
               updated_at = timezone('utc', now())
         where organization_id = p_organization_id
           and warehouse_id = v_warehouse_id
           and product_id = v_sale_item.product_id
        returning on_hand_quantity into v_resulting_quantity;

        insert into app.stock_movements (
            stock_movement_id,
            organization_id,
            warehouse_id,
            product_id,
            movement_type,
            quantity,
            quantity_delta,
            resulting_quantity,
            reason,
            reference_document,
            notes,
            performed_by_user_id,
            created_at
        )
        values (
            gen_random_uuid(),
            p_organization_id,
            v_warehouse_id,
            v_sale_item.product_id,
            'return_inbound',
            v_requested_quantity,
            v_requested_quantity,
            v_resulting_quantity,
            'Devolucion parcial de venta',
            p_sale_id::text,
            nullif(p_notes, ''),
            p_returned_by_user_id,
            timezone('utc', now())
        );
    end loop;

    if v_refunded_paid_amount > 0 then
        v_reference_document := concat('SALE-', lpad(v_sale.ticket_number::text, 8, '0'));
        v_remaining_paid_refund := v_refunded_paid_amount;

        for v_payment in
            select
                sp.sale_payment_id,
                sp.payment_method,
                sp.amount,
                sp.reference,
                sp.provider_name
            from app.sale_payments sp
            where sp.sale_id = p_sale_id
              and sp.payment_method <> 'account'
            order by sp.created_at asc, sp.sale_payment_id asc
        loop
            if v_remaining_paid_refund <= 0 then
                exit;
            end if;

            if v_payment.amount >= v_remaining_paid_refund then
                v_payment_refund_amount := v_remaining_paid_refund;
            else
                v_payment_refund_amount := v_payment.amount;
            end if;

            insert into app.cash_movements (
                cash_movement_id,
                organization_id,
                cash_session_id,
                movement_type,
                category_code,
                concept,
                payment_method,
                amount,
                signed_amount,
                resulting_balance,
                reference_document,
                notes,
                performed_by_user_id,
                created_at
            )
            values (
                gen_random_uuid(),
                p_organization_id,
                p_cash_session_id,
                'cash_out',
                'sale_partial_return',
                'Devolucion parcial de venta',
                v_payment.payment_method,
                v_payment_refund_amount,
                -v_payment_refund_amount,
                v_resulting_cash_balance,
                v_reference_document,
                nullif(p_notes, ''),
                p_returned_by_user_id,
                timezone('utc', now())
            );

            v_remaining_paid_refund := v_remaining_paid_refund - v_payment_refund_amount;
        end loop;
    end if;

    update app.sales
       set status = 'partially_returned'
     where sale_id = p_sale_id;

    return v_sale_return_id;
end;
$$;
