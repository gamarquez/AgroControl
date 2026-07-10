alter table app.sales
    add column if not exists due_date date;

alter table app.customer_account_movements
    add column if not exists due_date date;

create table if not exists app.customer_payment_allocations (
    customer_payment_allocation_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    payment_movement_id uuid not null references app.customer_account_movements (customer_account_movement_id) on delete cascade,
    debit_movement_id uuid not null references app.customer_account_movements (customer_account_movement_id) on delete cascade,
    applied_amount numeric(18, 2) not null,
    created_at timestamptz not null default timezone('utc', now()),
    constraint ck_customer_payment_allocations_amount_positive check (applied_amount > 0),
    constraint uq_customer_payment_allocations_pair unique (payment_movement_id, debit_movement_id)
);

create index if not exists ix_customer_payment_allocations_payment
    on app.customer_payment_allocations (payment_movement_id, created_at asc);

create index if not exists ix_customer_payment_allocations_debit
    on app.customer_payment_allocations (debit_movement_id, created_at asc);

create or replace function app.create_account_sale(
    p_organization_id uuid,
    p_customer_id uuid,
    p_sold_by_user_id uuid,
    p_items jsonb,
    p_due_date date,
    p_notes text default null
)
returns uuid
language plpgsql
as $$
declare
    v_sale_id uuid := gen_random_uuid();
    v_warehouse_id uuid := app.get_default_warehouse_id(p_organization_id);
    v_item jsonb;
    v_product_id uuid;
    v_quantity numeric(18, 3);
    v_product_name text;
    v_unit_symbol text;
    v_sale_amount numeric(18, 2);
    v_allows_fraction boolean;
    v_current_quantity numeric(18, 3);
    v_resulting_quantity numeric(18, 3);
    v_subtotal numeric(18, 2) := 0;
    v_customer_name text;
    v_credit_limit numeric(18, 2);
    v_current_account_balance numeric(18, 2);
    v_resulting_account_balance numeric(18, 2);
begin
    if p_items is null or jsonb_typeof(p_items) <> 'array' or jsonb_array_length(p_items) = 0 then
        raise exception 'At least one sale item is required.';
    end if;

    select c.display_name, c.credit_limit_amount
      into v_customer_name, v_credit_limit
      from app.customers c
     where c.organization_id = p_organization_id
       and c.customer_id = p_customer_id
       and c.is_active = true;

    if v_customer_name is null then
        raise exception 'The informed customer does not belong to the active organization.';
    end if;

    if p_due_date is not null and p_due_date < current_date then
        raise exception 'The informed due date cannot be in the past.';
    end if;

    insert into app.sales (
        sale_id,
        organization_id,
        cash_session_id,
        customer_id,
        sold_by_user_id,
        sale_channel,
        status,
        customer_name,
        subtotal_amount,
        discount_amount,
        total_amount,
        paid_amount,
        account_balance_amount,
        currency_code,
        due_date,
        notes,
        created_at
    )
    values (
        v_sale_id,
        p_organization_id,
        null,
        p_customer_id,
        p_sold_by_user_id,
        'pos_account',
        'confirmed',
        v_customer_name,
        0,
        0,
        0,
        0,
        0,
        'ARS',
        p_due_date,
        nullif(p_notes, ''),
        timezone('utc', now())
    );

    for v_item in select value from jsonb_array_elements(p_items)
    loop
        v_product_id := (v_item ->> 'productId')::uuid;
        v_quantity := (v_item ->> 'quantity')::numeric;

        if v_product_id is null then
            raise exception 'Each sale item must include a valid product.';
        end if;

        if v_quantity is null or v_quantity <= 0 then
            raise exception 'Each sale item quantity must be greater than zero.';
        end if;

        select p.name, u.symbol, pp.sale_amount, p.allows_fraction
          into v_product_name, v_unit_symbol, v_sale_amount, v_allows_fraction
          from app.products p
          join app.units_of_measure u on u.unit_id = p.base_unit_id
          join app.price_lists pl on pl.organization_id = p.organization_id and pl.is_default and pl.is_active
          join app.product_prices pp on pp.product_id = p.product_id and pp.price_list_id = pl.price_list_id
         where p.organization_id = p_organization_id
           and p.product_id = v_product_id
           and p.is_active = true;

        if v_product_name is null then
            raise exception 'One of the informed products is not available for sale in the active organization.';
        end if;

        if not v_allows_fraction and v_quantity <> trunc(v_quantity) then
            raise exception 'One of the informed products does not allow fractioned quantities.';
        end if;

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
            v_product_id,
            0,
            null,
            null,
            null,
            1,
            timezone('utc', now())
        )
        on conflict (organization_id, warehouse_id, product_id) do nothing;

        select sb.on_hand_quantity
          into v_current_quantity
          from app.stock_balances sb
         where sb.organization_id = p_organization_id
           and sb.warehouse_id = v_warehouse_id
           and sb.product_id = v_product_id
         for update;

        v_resulting_quantity := v_current_quantity - v_quantity;

        if v_resulting_quantity < 0 then
            raise exception 'There is not enough stock to confirm the sale.';
        end if;

        update app.stock_balances
           set on_hand_quantity = v_resulting_quantity,
               version_number = version_number + 1,
               updated_at = timezone('utc', now())
         where organization_id = p_organization_id
           and warehouse_id = v_warehouse_id
           and product_id = v_product_id;

        insert into app.sale_items (
            sale_item_id,
            sale_id,
            organization_id,
            product_id,
            product_name,
            unit_symbol,
            quantity,
            unit_price,
            line_total,
            created_at
        )
        values (
            gen_random_uuid(),
            v_sale_id,
            p_organization_id,
            v_product_id,
            v_product_name,
            v_unit_symbol,
            v_quantity,
            v_sale_amount,
            round(v_sale_amount * v_quantity, 2),
            timezone('utc', now())
        );

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
            v_product_id,
            'sale_outbound',
            v_quantity,
            -v_quantity,
            v_resulting_quantity,
            'Venta cuenta corriente',
            v_sale_id::text,
            nullif(p_notes, ''),
            p_sold_by_user_id,
            timezone('utc', now())
        );

        v_subtotal := v_subtotal + round(v_sale_amount * v_quantity, 2);
    end loop;

    select coalesce(cam.resulting_balance, 0)
      into v_current_account_balance
      from app.customer_account_movements cam
     where cam.organization_id = p_organization_id
       and cam.customer_id = p_customer_id
     order by cam.created_at desc, cam.customer_account_movement_id desc
     limit 1;

    v_resulting_account_balance := coalesce(v_current_account_balance, 0) + v_subtotal;

    if v_credit_limit > 0 and v_resulting_account_balance > v_credit_limit then
        raise exception 'The customer would exceed the configured credit limit.';
    end if;

    update app.sales
       set subtotal_amount = v_subtotal,
           total_amount = v_subtotal,
           paid_amount = 0,
           account_balance_amount = v_subtotal
     where sale_id = v_sale_id;

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
        p_customer_id,
        v_sale_id,
        null,
        'sale_debit',
        'Venta en cuenta corriente',
        v_sale_id::text,
        v_subtotal,
        0,
        v_resulting_account_balance,
        p_due_date,
        nullif(p_notes, ''),
        p_sold_by_user_id,
        timezone('utc', now())
    );

    return v_sale_id;
end;
$$;

create or replace function app.create_checkout_sale(
    p_organization_id uuid,
    p_cash_session_id uuid,
    p_customer_id uuid,
    p_sold_by_user_id uuid,
    p_items jsonb,
    p_payments jsonb,
    p_due_date date,
    p_notes text default null
)
returns uuid
language plpgsql
as $$
declare
    v_sale_id uuid := gen_random_uuid();
    v_warehouse_id uuid := app.get_default_warehouse_id(p_organization_id);
    v_item jsonb;
    v_payment jsonb;
    v_product_id uuid;
    v_quantity numeric(18, 3);
    v_product_name text;
    v_unit_symbol text;
    v_sale_amount numeric(18, 2);
    v_allows_fraction boolean;
    v_current_quantity numeric(18, 3);
    v_resulting_quantity numeric(18, 3);
    v_subtotal numeric(18, 2) := 0;
    v_payment_method text;
    v_payment_amount numeric(18, 2);
    v_payment_reference text;
    v_provider_name text;
    v_collected_total numeric(18, 2) := 0;
    v_account_total numeric(18, 2) := 0;
    v_current_cash_balance numeric(18, 2);
    v_resulting_cash_balance numeric(18, 2);
    v_customer_name text := 'Consumidor final';
    v_credit_limit numeric(18, 2) := 0;
    v_current_account_balance numeric(18, 2) := 0;
    v_resulting_account_balance numeric(18, 2);
begin
    if p_items is null or jsonb_typeof(p_items) <> 'array' or jsonb_array_length(p_items) = 0 then
        raise exception 'At least one sale item is required.';
    end if;

    if p_payments is null or jsonb_typeof(p_payments) <> 'array' or jsonb_array_length(p_payments) = 0 then
        raise exception 'At least one payment component is required.';
    end if;

    if p_customer_id is not null then
        select c.display_name, c.credit_limit_amount
          into v_customer_name, v_credit_limit
          from app.customers c
         where c.organization_id = p_organization_id
           and c.customer_id = p_customer_id
           and c.is_active = true;

        if v_customer_name is null then
            raise exception 'The informed customer does not belong to the active organization.';
        end if;
    end if;

    if p_due_date is not null and p_due_date < current_date then
        raise exception 'The informed due date cannot be in the past.';
    end if;

    for v_payment in select value from jsonb_array_elements(p_payments)
    loop
        v_payment_method := lower(trim(coalesce(v_payment ->> 'paymentMethod', '')));
        v_payment_amount := round((v_payment ->> 'amount')::numeric, 2);

        if v_payment_method not in ('cash', 'transfer', 'qr', 'card', 'account') then
            raise exception 'One of the informed payment methods is not supported.';
        end if;

        if v_payment_amount is null or v_payment_amount <= 0 then
            raise exception 'Each payment amount must be greater than zero.';
        end if;

        if v_payment_method = 'account' then
            v_account_total := v_account_total + v_payment_amount;
        else
            v_collected_total := v_collected_total + v_payment_amount;
        end if;
    end loop;

    if v_collected_total > 0 then
        if p_cash_session_id is null then
            raise exception 'The informed cash session is required for collected payment methods.';
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
    end if;

    if v_account_total > 0 and p_customer_id is null then
        raise exception 'A customer is required to use account payment.';
    end if;

    if v_account_total > 0 and p_due_date is null then
        raise exception 'A due date is required when the sale includes account balance.';
    end if;

    insert into app.sales (
        sale_id,
        organization_id,
        cash_session_id,
        customer_id,
        sold_by_user_id,
        sale_channel,
        status,
        customer_name,
        subtotal_amount,
        discount_amount,
        total_amount,
        paid_amount,
        account_balance_amount,
        currency_code,
        due_date,
        notes,
        created_at
    )
    values (
        v_sale_id,
        p_organization_id,
        p_cash_session_id,
        p_customer_id,
        p_sold_by_user_id,
        'pos_checkout',
        'confirmed',
        v_customer_name,
        0,
        0,
        0,
        0,
        0,
        'ARS',
        p_due_date,
        nullif(p_notes, ''),
        timezone('utc', now())
    );

    for v_item in select value from jsonb_array_elements(p_items)
    loop
        v_product_id := (v_item ->> 'productId')::uuid;
        v_quantity := (v_item ->> 'quantity')::numeric;
        if v_product_id is null then
            raise exception 'Each sale item must include a valid product.';
        end if;
        if v_quantity is null or v_quantity <= 0 then
            raise exception 'Each sale item quantity must be greater than zero.';
        end if;
        select p.name, u.symbol, pp.sale_amount, p.allows_fraction
          into v_product_name, v_unit_symbol, v_sale_amount, v_allows_fraction
          from app.products p
          join app.units_of_measure u on u.unit_id = p.base_unit_id
          join app.price_lists pl on pl.organization_id = p.organization_id and pl.is_default and pl.is_active
          join app.product_prices pp on pp.product_id = p.product_id and pp.price_list_id = pl.price_list_id
         where p.organization_id = p_organization_id
           and p.product_id = v_product_id
           and p.is_active = true;
        if v_product_name is null then
            raise exception 'One of the informed products is not available for sale in the active organization.';
        end if;
        if not v_allows_fraction and v_quantity <> trunc(v_quantity) then
            raise exception 'One of the informed products does not allow fractioned quantities.';
        end if;
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
            v_product_id,
            0,
            null,
            null,
            null,
            1,
            timezone('utc', now())
        )
        on conflict (organization_id, warehouse_id, product_id) do nothing;
        select sb.on_hand_quantity
          into v_current_quantity
          from app.stock_balances sb
         where sb.organization_id = p_organization_id
           and sb.warehouse_id = v_warehouse_id
           and sb.product_id = v_product_id
         for update;
        v_resulting_quantity := v_current_quantity - v_quantity;
        if v_resulting_quantity < 0 then
            raise exception 'There is not enough stock to confirm the sale.';
        end if;
        update app.stock_balances
           set on_hand_quantity = v_resulting_quantity,
               version_number = version_number + 1,
               updated_at = timezone('utc', now())
         where organization_id = p_organization_id
           and warehouse_id = v_warehouse_id
           and product_id = v_product_id;
        insert into app.sale_items (
            sale_item_id,
            sale_id,
            organization_id,
            product_id,
            product_name,
            unit_symbol,
            quantity,
            unit_price,
            line_total,
            created_at
        )
        values (
            gen_random_uuid(),
            v_sale_id,
            p_organization_id,
            v_product_id,
            v_product_name,
            v_unit_symbol,
            v_quantity,
            v_sale_amount,
            round(v_sale_amount * v_quantity, 2),
            timezone('utc', now())
        );
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
            v_product_id,
            'sale_outbound',
            v_quantity,
            -v_quantity,
            v_resulting_quantity,
            'Venta POS checkout',
            v_sale_id::text,
            nullif(p_notes, ''),
            p_sold_by_user_id,
            timezone('utc', now())
        );
        v_subtotal := v_subtotal + round(v_sale_amount * v_quantity, 2);
    end loop;

    if round(v_collected_total + v_account_total, 2) <> round(v_subtotal, 2) then
        raise exception 'The total amount of payments does not match the sale total.';
    end if;

    if v_account_total > 0 then
        select coalesce(cam.resulting_balance, 0)
          into v_current_account_balance
          from app.customer_account_movements cam
         where cam.organization_id = p_organization_id
           and cam.customer_id = p_customer_id
         order by cam.created_at desc, cam.customer_account_movement_id desc
         limit 1;

        v_resulting_account_balance := coalesce(v_current_account_balance, 0) + v_account_total;

        if v_credit_limit > 0 and v_resulting_account_balance > v_credit_limit then
            raise exception 'The customer would exceed the configured credit limit.';
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
            p_customer_id,
            v_sale_id,
            null,
            'sale_debit',
            'Venta POS checkout en cuenta corriente',
            v_sale_id::text,
            v_account_total,
            0,
            v_resulting_account_balance,
            p_due_date,
            nullif(p_notes, ''),
            p_sold_by_user_id,
            timezone('utc', now())
        );
    end if;

    for v_payment in select value from jsonb_array_elements(p_payments)
    loop
        v_payment_method := lower(trim(coalesce(v_payment ->> 'paymentMethod', '')));
        v_payment_amount := round((v_payment ->> 'amount')::numeric, 2);
        v_payment_reference := nullif(trim(coalesce(v_payment ->> 'reference', '')), '');
        v_provider_name := nullif(trim(coalesce(v_payment ->> 'providerName', '')), '');
        insert into app.sale_payments (
            sale_payment_id,
            organization_id,
            sale_id,
            payment_method,
            amount,
            reference,
            provider_name,
            created_at
        )
        values (
            gen_random_uuid(),
            p_organization_id,
            v_sale_id,
            v_payment_method,
            v_payment_amount,
            v_payment_reference,
            v_provider_name,
            timezone('utc', now())
        );
        if v_payment_method <> 'account' then
            v_resulting_cash_balance := v_current_cash_balance + v_payment_amount;
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
                'cash_in',
                'sale',
                'Venta mostrador checkout',
                v_payment_method,
                v_payment_amount,
                v_payment_amount,
                v_resulting_cash_balance,
                coalesce(v_payment_reference, v_sale_id::text),
                nullif(p_notes, ''),
                p_sold_by_user_id,
                timezone('utc', now())
            );
            v_current_cash_balance := v_resulting_cash_balance;
        end if;
    end loop;

    if v_collected_total > 0 then
        update app.cash_sessions
           set current_balance = v_current_cash_balance
         where cash_session_id = p_cash_session_id;
    end if;

    update app.sales
       set subtotal_amount = v_subtotal,
           total_amount = v_subtotal,
           paid_amount = v_collected_total,
           account_balance_amount = v_account_total,
           sale_channel = case
               when v_account_total = v_subtotal then 'pos_account'
               when v_account_total = 0 and jsonb_array_length(p_payments) = 1 and lower(trim(coalesce(p_payments -> 0 ->> 'paymentMethod', ''))) = 'cash' then 'pos_cash'
               else 'pos_checkout'
           end
     where sale_id = v_sale_id;

    return v_sale_id;
end;
$$;

create or replace function app.record_customer_payment(
    p_organization_id uuid,
    p_customer_id uuid,
    p_cash_session_id uuid,
    p_amount numeric,
    p_notes text,
    p_performed_by_user_id uuid
)
returns uuid
language plpgsql
as $$
declare
    v_movement_id uuid := gen_random_uuid();
    v_customer_name text;
    v_current_account_balance numeric(18, 2);
    v_resulting_account_balance numeric(18, 2);
    v_current_cash_balance numeric(18, 2);
    v_resulting_cash_balance numeric(18, 2);
    v_remaining_amount numeric(18, 2);
    v_debit record;
    v_open_amount numeric(18, 2);
    v_applied_amount numeric(18, 2);
begin
    if p_amount is null or p_amount <= 0 then
        raise exception 'The informed payment amount must be greater than zero.';
    end if;

    select c.display_name
      into v_customer_name
      from app.customers c
     where c.organization_id = p_organization_id
       and c.customer_id = p_customer_id
       and c.is_active = true;

    if v_customer_name is null then
        raise exception 'The informed customer does not belong to the active organization.';
    end if;

    select coalesce(cam.resulting_balance, 0)
      into v_current_account_balance
      from app.customer_account_movements cam
     where cam.organization_id = p_organization_id
       and cam.customer_id = p_customer_id
     order by cam.created_at desc, cam.customer_account_movement_id desc
     limit 1;

    if coalesce(v_current_account_balance, 0) <= 0 then
        raise exception 'The informed customer does not have an outstanding balance.';
    end if;

    v_resulting_account_balance := coalesce(v_current_account_balance, 0) - p_amount;

    if v_resulting_account_balance < 0 then
        raise exception 'The informed payment exceeds the outstanding customer balance.';
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

    v_resulting_cash_balance := v_current_cash_balance + p_amount;

    update app.cash_sessions
       set current_balance = v_resulting_cash_balance
     where cash_session_id = p_cash_session_id;

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
        'cash_in',
        'customer_payment',
        'Cobranza cuenta corriente',
        'cash',
        p_amount,
        p_amount,
        v_resulting_cash_balance,
        p_customer_id::text,
        nullif(p_notes, ''),
        p_performed_by_user_id,
        timezone('utc', now())
    );

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
        v_movement_id,
        p_organization_id,
        p_customer_id,
        null,
        p_cash_session_id,
        'payment_credit',
        'Cobranza cuenta corriente',
        p_customer_id::text,
        0,
        p_amount,
        v_resulting_account_balance,
        null,
        nullif(p_notes, ''),
        p_performed_by_user_id,
        timezone('utc', now())
    );

    v_remaining_amount := p_amount;

    for v_debit in
        select
            cam.customer_account_movement_id,
            cam.debit_amount,
            coalesce(sum(cpa.applied_amount), 0) as applied_amount
        from app.customer_account_movements cam
        left join app.customer_payment_allocations cpa
            on cpa.debit_movement_id = cam.customer_account_movement_id
        where cam.organization_id = p_organization_id
          and cam.customer_id = p_customer_id
          and cam.movement_type = 'sale_debit'
        group by cam.customer_account_movement_id, cam.debit_amount, cam.due_date, cam.created_at
        having cam.debit_amount > coalesce(sum(cpa.applied_amount), 0)
        order by cam.due_date asc nulls first, cam.created_at asc, cam.customer_account_movement_id asc
    loop
        exit when v_remaining_amount <= 0;

        v_open_amount := v_debit.debit_amount - v_debit.applied_amount;
        v_applied_amount := least(v_open_amount, v_remaining_amount);

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
            v_movement_id,
            v_debit.customer_account_movement_id,
            v_applied_amount,
            timezone('utc', now())
        );

        v_remaining_amount := v_remaining_amount - v_applied_amount;
    end loop;

    return v_movement_id;
end;
$$;
