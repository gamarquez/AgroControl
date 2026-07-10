alter table app.sales
    add column if not exists credit_balance_applied_amount numeric(18, 2) not null default 0;

create or replace function app.apply_available_customer_credits(
    p_organization_id uuid,
    p_customer_id uuid,
    p_debit_movement_id uuid,
    p_target_amount numeric
)
returns numeric
language plpgsql
as $$
declare
    v_credit record;
    v_credit_available numeric(18, 2);
    v_applied_amount numeric(18, 2);
    v_remaining_amount numeric(18, 2) := coalesce(p_target_amount, 0);
    v_total_applied numeric(18, 2) := 0;
begin
    if v_remaining_amount <= 0 then
        return 0;
    end if;

    for v_credit in
        select
            cam.customer_account_movement_id,
            cam.credit_amount,
            coalesce(sum(cpa.applied_amount), 0) as applied_amount
        from app.customer_account_movements cam
        left join app.customer_payment_allocations cpa
            on cpa.payment_movement_id = cam.customer_account_movement_id
        where cam.organization_id = p_organization_id
          and cam.customer_id = p_customer_id
          and cam.credit_amount > 0
        group by cam.customer_account_movement_id, cam.credit_amount, cam.created_at
        having cam.credit_amount > coalesce(sum(cpa.applied_amount), 0)
        order by cam.created_at asc, cam.customer_account_movement_id asc
    loop
        exit when v_remaining_amount <= 0;

        v_credit_available := v_credit.credit_amount - v_credit.applied_amount;
        v_applied_amount := least(v_credit_available, v_remaining_amount);

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
            v_credit.customer_account_movement_id,
            p_debit_movement_id,
            v_applied_amount,
            timezone('utc', now())
        );

        v_remaining_amount := v_remaining_amount - v_applied_amount;
        v_total_applied := v_total_applied + v_applied_amount;
    end loop;

    return v_total_applied;
end;
$$;

create or replace function app.allocate_credit_movement_to_open_debits(
    p_organization_id uuid,
    p_customer_id uuid,
    p_credit_movement_id uuid,
    p_credit_amount numeric
)
returns numeric
language plpgsql
as $$
declare
    v_debit record;
    v_open_amount numeric(18, 2);
    v_applied_amount numeric(18, 2);
    v_remaining_amount numeric(18, 2) := coalesce(p_credit_amount, 0);
    v_total_applied numeric(18, 2) := 0;
begin
    if v_remaining_amount <= 0 then
        return 0;
    end if;

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
            p_credit_movement_id,
            v_debit.customer_account_movement_id,
            v_applied_amount,
            timezone('utc', now())
        );

        v_remaining_amount := v_remaining_amount - v_applied_amount;
        v_total_applied := v_total_applied + v_applied_amount;
    end loop;

    return v_total_applied;
end;
$$;

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
    v_customer_movement_id uuid := gen_random_uuid();
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
    v_credit_balance_applied_amount numeric(18, 2) := 0;
    v_net_account_balance_amount numeric(18, 2) := 0;
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
        credit_balance_applied_amount,
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
        v_customer_movement_id,
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

    v_credit_balance_applied_amount := app.apply_available_customer_credits(
        p_organization_id,
        p_customer_id,
        v_customer_movement_id,
        v_subtotal);

    v_net_account_balance_amount := v_subtotal - v_credit_balance_applied_amount;

    update app.sales
       set subtotal_amount = v_subtotal,
           total_amount = v_subtotal,
           paid_amount = 0,
           account_balance_amount = v_net_account_balance_amount,
           credit_balance_applied_amount = v_credit_balance_applied_amount
     where sale_id = v_sale_id;

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
    v_customer_movement_id uuid := gen_random_uuid();
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
    v_credit_balance_applied_amount numeric(18, 2) := 0;
    v_net_account_balance_amount numeric(18, 2) := 0;
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

        select coalesce(cam.resulting_balance, 0)
          into v_current_account_balance
          from app.customer_account_movements cam
         where cam.organization_id = p_organization_id
           and cam.customer_id = p_customer_id
         order by cam.created_at desc, cam.customer_account_movement_id desc
         limit 1;
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

    v_credit_balance_applied_amount := least(greatest(-coalesce(v_current_account_balance, 0), 0), v_account_total);
    v_net_account_balance_amount := v_account_total - v_credit_balance_applied_amount;

    if v_net_account_balance_amount > 0 and p_due_date is null then
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
        credit_balance_applied_amount,
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
            v_customer_movement_id,
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

        v_credit_balance_applied_amount := app.apply_available_customer_credits(
            p_organization_id,
            p_customer_id,
            v_customer_movement_id,
            v_account_total);

        v_net_account_balance_amount := v_account_total - v_credit_balance_applied_amount;
    else
        v_credit_balance_applied_amount := 0;
        v_net_account_balance_amount := 0;
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
           account_balance_amount = v_net_account_balance_amount,
           credit_balance_applied_amount = v_credit_balance_applied_amount,
           sale_channel = case
               when v_collected_total = 0 then 'pos_account'
               when v_net_account_balance_amount = 0 and jsonb_array_length(p_payments) = 1 and lower(trim(coalesce(p_payments -> 0 ->> 'paymentMethod', ''))) = 'cash' then 'pos_cash'
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

    v_resulting_account_balance := coalesce(v_current_account_balance, 0) - p_amount;

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

    perform app.allocate_credit_movement_to_open_debits(
        p_organization_id,
        p_customer_id,
        v_movement_id,
        p_amount);

    return v_movement_id;
end;
$$;

create or replace function app.record_customer_credit_note(
    p_organization_id uuid,
    p_customer_id uuid,
    p_amount numeric,
    p_concept text,
    p_reference_document text,
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
begin
    if p_amount is null or p_amount <= 0 then
        raise exception 'The informed credit note amount must be greater than zero.';
    end if;

    if nullif(trim(coalesce(p_concept, '')), '') is null then
        raise exception 'A concept is required to record the credit note.';
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

    v_resulting_account_balance := coalesce(v_current_account_balance, 0) - p_amount;

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
        null,
        'credit_note',
        trim(p_concept),
        nullif(trim(coalesce(p_reference_document, '')), ''),
        0,
        p_amount,
        v_resulting_account_balance,
        null,
        nullif(trim(coalesce(p_notes, '')), ''),
        p_performed_by_user_id,
        timezone('utc', now())
    );

    perform app.allocate_credit_movement_to_open_debits(
        p_organization_id,
        p_customer_id,
        v_movement_id,
        p_amount);

    return v_movement_id;
end;
$$;

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
    v_customer_credit_movement_id uuid := gen_random_uuid();
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
            v_customer_credit_movement_id,
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
        );

        perform app.allocate_credit_movement_to_open_debits(
            p_organization_id,
            v_sale.customer_id,
            v_customer_credit_movement_id,
            v_credited_account_amount);
    else
        v_customer_credit_movement_id := null;
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
