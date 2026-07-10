create table if not exists app.warehouses (
    warehouse_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    name text not null,
    code text not null,
    is_default boolean not null default false,
    is_active boolean not null default true,
    created_at timestamptz not null default timezone('utc', now()),
    updated_at timestamptz not null default timezone('utc', now())
);

create unique index if not exists uq_warehouses_organization_code
    on app.warehouses (organization_id, lower(code));

create unique index if not exists uq_warehouses_single_default
    on app.warehouses (organization_id)
    where is_default;

create index if not exists ix_warehouses_organization_active_name
    on app.warehouses (organization_id, is_active, name asc);

insert into app.warehouses (
    warehouse_id,
    organization_id,
    name,
    code,
    is_default,
    is_active,
    created_at,
    updated_at
)
select
    gen_random_uuid(),
    o.organization_id,
    'Deposito principal',
    'MAIN',
    true,
    true,
    timezone('utc', now()),
    timezone('utc', now())
from app.organizations o
where not exists (
    select 1
    from app.warehouses w
    where w.organization_id = o.organization_id
);

alter table app.stock_balances
    add column if not exists warehouse_id uuid references app.warehouses (warehouse_id);

alter table app.stock_movements
    add column if not exists warehouse_id uuid references app.warehouses (warehouse_id);

with default_warehouses as (
    select distinct on (w.organization_id)
        w.organization_id,
        w.warehouse_id
    from app.warehouses w
    where w.is_default = true
    order by w.organization_id, w.created_at asc
)
update app.stock_balances sb
set warehouse_id = dw.warehouse_id
from default_warehouses dw
where sb.organization_id = dw.organization_id
  and sb.warehouse_id is null;

with default_warehouses as (
    select distinct on (w.organization_id)
        w.organization_id,
        w.warehouse_id
    from app.warehouses w
    where w.is_default = true
    order by w.organization_id, w.created_at asc
)
update app.stock_movements sm
set warehouse_id = dw.warehouse_id
from default_warehouses dw
where sm.organization_id = dw.organization_id
  and sm.warehouse_id is null;

alter table app.stock_balances
    alter column warehouse_id set not null;

alter table app.stock_movements
    alter column warehouse_id set not null;

drop index if exists ix_stock_balances_organization_updated_at;
alter table app.stock_balances
    drop constraint if exists uq_stock_balances_organization_product;

create unique index if not exists uq_stock_balances_organization_warehouse_product
    on app.stock_balances (organization_id, warehouse_id, product_id);

create index if not exists ix_stock_balances_organization_warehouse_updated_at
    on app.stock_balances (organization_id, warehouse_id, updated_at desc);

drop index if exists ix_stock_movements_organization_product_created_at;
create index if not exists ix_stock_movements_organization_warehouse_product_created_at
    on app.stock_movements (organization_id, warehouse_id, product_id, created_at desc);

create index if not exists ix_stock_movements_organization_warehouse_type_created_at
    on app.stock_movements (organization_id, warehouse_id, movement_type, created_at desc);

create table if not exists app.physical_inventory_counts (
    physical_inventory_count_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    warehouse_id uuid not null references app.warehouses (warehouse_id) on delete restrict,
    product_id uuid not null references app.products (product_id) on delete restrict,
    stock_movement_id uuid references app.stock_movements (stock_movement_id) on delete set null,
    expected_quantity numeric(18, 3) not null,
    counted_quantity numeric(18, 3) not null,
    difference_quantity numeric(18, 3) not null,
    reason text not null,
    notes text,
    performed_by_user_id uuid references app.users (user_id),
    created_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_physical_inventory_counts_organization_created_at
    on app.physical_inventory_counts (organization_id, created_at desc);

create index if not exists ix_physical_inventory_counts_product_warehouse_created_at
    on app.physical_inventory_counts (organization_id, product_id, warehouse_id, created_at desc);

create or replace function app.get_default_warehouse_id(
    p_organization_id uuid
)
returns uuid
language plpgsql
as $$
declare
    v_warehouse_id uuid;
begin
    select w.warehouse_id
      into v_warehouse_id
      from app.warehouses w
     where w.organization_id = p_organization_id
       and w.is_default = true
       and w.is_active = true
     order by w.created_at asc
     limit 1;

    if v_warehouse_id is null then
        raise exception 'The active organization does not have a default warehouse configured.';
    end if;

    return v_warehouse_id;
end;
$$;

create or replace function app.upsert_warehouse(
    p_organization_id uuid,
    p_warehouse_id uuid,
    p_name text,
    p_code text,
    p_is_default boolean,
    p_is_active boolean
)
returns uuid
language plpgsql
as $$
declare
    v_warehouse_id uuid := coalesce(p_warehouse_id, gen_random_uuid());
begin
    if nullif(trim(coalesce(p_name, '')), '') is null then
        raise exception 'The warehouse name is required.';
    end if;

    if nullif(trim(coalesce(p_code, '')), '') is null then
        raise exception 'The warehouse code is required.';
    end if;

    if p_warehouse_id is not null and not exists (
        select 1
        from app.warehouses w
        where w.organization_id = p_organization_id
          and w.warehouse_id = p_warehouse_id
    ) then
        raise exception 'The informed warehouse does not belong to the active organization.';
    end if;

    if p_is_default then
        update app.warehouses
           set is_default = false,
               updated_at = timezone('utc', now())
         where organization_id = p_organization_id
           and warehouse_id <> v_warehouse_id
           and is_default = true;
    elsif not exists (
        select 1
        from app.warehouses w
        where w.organization_id = p_organization_id
          and w.warehouse_id <> v_warehouse_id
          and w.is_default = true
    ) then
        p_is_default := true;
    end if;

    insert into app.warehouses (
        warehouse_id,
        organization_id,
        name,
        code,
        is_default,
        is_active,
        created_at,
        updated_at
    )
    values (
        v_warehouse_id,
        p_organization_id,
        trim(p_name),
        upper(trim(p_code)),
        p_is_default,
        p_is_active,
        timezone('utc', now()),
        timezone('utc', now())
    )
    on conflict (warehouse_id) do update
       set name = excluded.name,
           code = excluded.code,
           is_default = excluded.is_default,
           is_active = excluded.is_active,
           updated_at = excluded.updated_at;

    return v_warehouse_id;
end;
$$;

create or replace function app.update_stock_policy(
    p_organization_id uuid,
    p_product_id uuid,
    p_warehouse_id uuid,
    p_min_quantity numeric,
    p_max_quantity numeric,
    p_reorder_point numeric
)
returns uuid
language plpgsql
as $$
declare
    v_balance_id uuid;
begin
    if not exists (
        select 1
        from app.products p
        where p.product_id = p_product_id
          and p.organization_id = p_organization_id
    ) then
        raise exception 'The informed product does not belong to the active organization.';
    end if;

    if not exists (
        select 1
        from app.warehouses w
        where w.organization_id = p_organization_id
          and w.warehouse_id = p_warehouse_id
          and w.is_active = true
    ) then
        raise exception 'The informed warehouse does not belong to the active organization.';
    end if;

    if p_min_quantity is not null and p_min_quantity < 0 then
        raise exception 'The minimum quantity cannot be negative.';
    end if;

    if p_max_quantity is not null and p_max_quantity < 0 then
        raise exception 'The maximum quantity cannot be negative.';
    end if;

    if p_reorder_point is not null and p_reorder_point < 0 then
        raise exception 'The reorder point cannot be negative.';
    end if;

    if p_min_quantity is not null and p_max_quantity is not null and p_max_quantity < p_min_quantity then
        raise exception 'The maximum quantity cannot be lower than the minimum quantity.';
    end if;

    if p_reorder_point is not null and p_min_quantity is not null and p_reorder_point < p_min_quantity then
        raise exception 'The reorder point cannot be lower than the minimum quantity.';
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
        p_warehouse_id,
        p_product_id,
        0,
        p_min_quantity,
        p_max_quantity,
        p_reorder_point,
        1,
        timezone('utc', now())
    )
    on conflict (organization_id, warehouse_id, product_id) do update
       set min_quantity = excluded.min_quantity,
           max_quantity = excluded.max_quantity,
           reorder_point = excluded.reorder_point,
           version_number = app.stock_balances.version_number + 1,
           updated_at = excluded.updated_at
    returning stock_balance_id into v_balance_id;

    return v_balance_id;
end;
$$;

create or replace function app.record_stock_movement(
    p_organization_id uuid,
    p_product_id uuid,
    p_warehouse_id uuid,
    p_movement_type text,
    p_quantity numeric,
    p_reason text,
    p_reference_document text,
    p_notes text,
    p_performed_by_user_id uuid default null
)
returns uuid
language plpgsql
as $$
declare
    v_stock_movement_id uuid := gen_random_uuid();
    v_balance_id uuid;
    v_current_quantity numeric(18, 3);
    v_resulting_quantity numeric(18, 3);
    v_quantity_delta numeric(18, 3);
    v_allows_fraction boolean;
begin
    if p_quantity is null or p_quantity <= 0 then
        raise exception 'The informed quantity must be greater than zero.';
    end if;

    if not exists (
        select 1
        from app.warehouses w
        where w.organization_id = p_organization_id
          and w.warehouse_id = p_warehouse_id
          and w.is_active = true
    ) then
        raise exception 'The informed warehouse does not belong to the active organization.';
    end if;

    select p.allows_fraction
      into v_allows_fraction
      from app.products p
     where p.product_id = p_product_id
       and p.organization_id = p_organization_id;

    if v_allows_fraction is null then
        raise exception 'The informed product does not belong to the active organization.';
    end if;

    if not v_allows_fraction and p_quantity <> trunc(p_quantity) then
        raise exception 'The informed product does not allow fractioned stock quantities.';
    end if;

    v_quantity_delta := case p_movement_type
        when 'purchase_inbound' then p_quantity
        when 'adjustment_increase' then p_quantity
        when 'return_inbound' then p_quantity
        when 'sale_outbound' then -p_quantity
        when 'adjustment_decrease' then -p_quantity
        when 'loss' then -p_quantity
        when 'broken' then -p_quantity
        when 'expired' then -p_quantity
        else null
    end;

    if v_quantity_delta is null then
        raise exception 'The informed stock movement type is not supported.';
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
        p_warehouse_id,
        p_product_id,
        0,
        null,
        null,
        null,
        1,
        timezone('utc', now())
    )
    on conflict (organization_id, warehouse_id, product_id) do nothing;

    select sb.stock_balance_id, sb.on_hand_quantity
      into v_balance_id, v_current_quantity
      from app.stock_balances sb
     where sb.organization_id = p_organization_id
       and sb.warehouse_id = p_warehouse_id
       and sb.product_id = p_product_id
     for update;

    v_resulting_quantity := v_current_quantity + v_quantity_delta;

    if v_resulting_quantity < 0 then
        raise exception 'There is not enough stock to register the requested outbound movement.';
    end if;

    update app.stock_balances
       set on_hand_quantity = v_resulting_quantity,
           version_number = version_number + 1,
           updated_at = timezone('utc', now())
     where stock_balance_id = v_balance_id;

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
        v_stock_movement_id,
        p_organization_id,
        p_warehouse_id,
        p_product_id,
        p_movement_type,
        p_quantity,
        v_quantity_delta,
        v_resulting_quantity,
        p_reason,
        nullif(p_reference_document, ''),
        nullif(p_notes, ''),
        p_performed_by_user_id,
        timezone('utc', now())
    );

    return v_stock_movement_id;
end;
$$;

create or replace function app.record_physical_inventory_count(
    p_organization_id uuid,
    p_warehouse_id uuid,
    p_product_id uuid,
    p_counted_quantity numeric,
    p_reason text,
    p_notes text,
    p_performed_by_user_id uuid default null
)
returns uuid
language plpgsql
as $$
declare
    v_physical_inventory_count_id uuid := gen_random_uuid();
    v_stock_movement_id uuid;
    v_balance_id uuid;
    v_current_quantity numeric(18, 3);
    v_difference_quantity numeric(18, 3);
    v_resulting_quantity numeric(18, 3);
    v_allows_fraction boolean;
    v_movement_type text;
begin
    if p_counted_quantity is null or p_counted_quantity < 0 then
        raise exception 'The counted quantity cannot be negative.';
    end if;

    if not exists (
        select 1
        from app.warehouses w
        where w.organization_id = p_organization_id
          and w.warehouse_id = p_warehouse_id
          and w.is_active = true
    ) then
        raise exception 'The informed warehouse does not belong to the active organization.';
    end if;

    select p.allows_fraction
      into v_allows_fraction
      from app.products p
     where p.product_id = p_product_id
       and p.organization_id = p_organization_id;

    if v_allows_fraction is null then
        raise exception 'The informed product does not belong to the active organization.';
    end if;

    if not v_allows_fraction and p_counted_quantity <> trunc(p_counted_quantity) then
        raise exception 'The informed product does not allow fractioned stock quantities.';
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
        p_warehouse_id,
        p_product_id,
        0,
        null,
        null,
        null,
        1,
        timezone('utc', now())
    )
    on conflict (organization_id, warehouse_id, product_id) do nothing;

    select sb.stock_balance_id, sb.on_hand_quantity
      into v_balance_id, v_current_quantity
      from app.stock_balances sb
     where sb.organization_id = p_organization_id
       and sb.warehouse_id = p_warehouse_id
       and sb.product_id = p_product_id
     for update;

    v_difference_quantity := p_counted_quantity - v_current_quantity;
    v_resulting_quantity := p_counted_quantity;

    update app.stock_balances
       set on_hand_quantity = v_resulting_quantity,
           version_number = version_number + 1,
           updated_at = timezone('utc', now())
     where stock_balance_id = v_balance_id;

    if v_difference_quantity <> 0 then
        v_movement_type := case
            when v_difference_quantity > 0 then 'adjustment_increase'
            else 'adjustment_decrease'
        end;

        v_stock_movement_id := gen_random_uuid();

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
            v_stock_movement_id,
            p_organization_id,
            p_warehouse_id,
            p_product_id,
            v_movement_type,
            abs(v_difference_quantity),
            v_difference_quantity,
            v_resulting_quantity,
            p_reason,
            'physical-count',
            nullif(p_notes, ''),
            p_performed_by_user_id,
            timezone('utc', now())
        );
    end if;

    insert into app.physical_inventory_counts (
        physical_inventory_count_id,
        organization_id,
        warehouse_id,
        product_id,
        stock_movement_id,
        expected_quantity,
        counted_quantity,
        difference_quantity,
        reason,
        notes,
        performed_by_user_id,
        created_at
    )
    values (
        v_physical_inventory_count_id,
        p_organization_id,
        p_warehouse_id,
        p_product_id,
        v_stock_movement_id,
        v_current_quantity,
        p_counted_quantity,
        v_difference_quantity,
        p_reason,
        nullif(p_notes, ''),
        p_performed_by_user_id,
        timezone('utc', now())
    );

    return v_physical_inventory_count_id;
end;
$$;

create or replace function app.create_cash_sale(
    p_organization_id uuid,
    p_cash_session_id uuid,
    p_sold_by_user_id uuid,
    p_items jsonb,
    p_notes text default null
)
returns uuid
language plpgsql
as $$
declare
    v_sale_id uuid := gen_random_uuid();
    v_cash_movement_id uuid := gen_random_uuid();
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
    v_current_balance numeric(18, 2);
    v_resulting_balance numeric(18, 2);
    v_subtotal numeric(18, 2) := 0;
    v_ticket_number bigint;
    v_reference_document text;
begin
    if p_items is null or jsonb_typeof(p_items) <> 'array' or jsonb_array_length(p_items) = 0 then
        raise exception 'At least one sale item is required.';
    end if;

    if not exists (
        select 1
        from app.cash_sessions cs
        where cs.organization_id = p_organization_id
          and cs.cash_session_id = p_cash_session_id
          and cs.status = 'open'
    ) then
        raise exception 'The informed cash session is not open for the active organization.';
    end if;

    insert into app.sales (
        sale_id,
        organization_id,
        cash_session_id,
        sold_by_user_id,
        sale_channel,
        status,
        customer_name,
        subtotal_amount,
        discount_amount,
        total_amount,
        currency_code,
        notes,
        created_at
    )
    values (
        v_sale_id,
        p_organization_id,
        p_cash_session_id,
        p_sold_by_user_id,
        'pos_cash',
        'confirmed',
        'Consumidor final',
        0,
        0,
        0,
        'ARS',
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
            'Venta mostrador',
            v_sale_id::text,
            nullif(p_notes, ''),
            p_sold_by_user_id,
            timezone('utc', now())
        );

        v_subtotal := v_subtotal + round(v_sale_amount * v_quantity, 2);
    end loop;

    update app.sales
       set subtotal_amount = v_subtotal,
           total_amount = v_subtotal
     where sale_id = v_sale_id
    returning ticket_number into v_ticket_number;

    select cs.current_balance
      into v_current_balance
      from app.cash_sessions cs
     where cs.organization_id = p_organization_id
       and cs.cash_session_id = p_cash_session_id
       and cs.status = 'open'
     for update;

    v_resulting_balance := v_current_balance + v_subtotal;
    v_reference_document := concat('SALE-', lpad(v_ticket_number::text, 8, '0'));

    update app.cash_sessions
       set current_balance = v_resulting_balance
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
        v_cash_movement_id,
        p_organization_id,
        p_cash_session_id,
        'cash_in',
        'sale',
        'Venta mostrador',
        'cash',
        v_subtotal,
        v_subtotal,
        v_resulting_balance,
        v_reference_document,
        nullif(p_notes, ''),
        p_sold_by_user_id,
        timezone('utc', now())
    );

    return v_sale_id;
end;
$$;

create or replace function app.reverse_cash_sale(
    p_organization_id uuid,
    p_sale_id uuid,
    p_cash_session_id uuid,
    p_reversed_by_user_id uuid,
    p_reversal_notes text default null
)
returns uuid
language plpgsql
as $$
declare
    v_sale record;
    v_item record;
    v_current_balance numeric(18, 2);
    v_resulting_balance numeric(18, 2);
    v_reference_document text;
    v_resulting_quantity numeric(18, 3);
    v_warehouse_id uuid := app.get_default_warehouse_id(p_organization_id);
begin
    select s.sale_id, s.ticket_number, s.total_amount, s.status
      into v_sale
      from app.sales s
     where s.organization_id = p_organization_id
       and s.sale_id = p_sale_id
     for update;

    if v_sale.sale_id is null then
        raise exception 'The informed sale does not belong to the active organization.';
    end if;

    if v_sale.status <> 'confirmed' then
        raise exception 'Only confirmed sales can be reversed.';
    end if;

    if not exists (
        select 1
        from app.cash_sessions cs
        where cs.organization_id = p_organization_id
          and cs.cash_session_id = p_cash_session_id
          and cs.status = 'open'
    ) then
        raise exception 'The informed cash session is not open for the active organization.';
    end if;

    select cs.current_balance
      into v_current_balance
      from app.cash_sessions cs
     where cs.organization_id = p_organization_id
       and cs.cash_session_id = p_cash_session_id
       and cs.status = 'open'
     for update;

    v_resulting_balance := v_current_balance - v_sale.total_amount;

    if v_resulting_balance < 0 then
        raise exception 'There is not enough balance to reverse the informed sale.';
    end if;

    for v_item in
        select si.product_id, si.quantity, si.product_name
          from app.sale_items si
         where si.sale_id = p_sale_id
         order by si.created_at asc
    loop
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
            v_item.product_id,
            0,
            null,
            null,
            null,
            1,
            timezone('utc', now())
        )
        on conflict (organization_id, warehouse_id, product_id) do nothing;

        update app.stock_balances
           set on_hand_quantity = on_hand_quantity + v_item.quantity,
               version_number = version_number + 1,
               updated_at = timezone('utc', now())
         where organization_id = p_organization_id
           and warehouse_id = v_warehouse_id
           and product_id = v_item.product_id
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
            v_item.product_id,
            'return_inbound',
            v_item.quantity,
            v_item.quantity,
            v_resulting_quantity,
            'Reversa de venta mostrador',
            p_sale_id::text,
            nullif(p_reversal_notes, ''),
            p_reversed_by_user_id,
            timezone('utc', now())
        );
    end loop;

    update app.cash_sessions
       set current_balance = v_resulting_balance
     where cash_session_id = p_cash_session_id;

    v_reference_document := concat('SALE-', lpad(v_sale.ticket_number::text, 8, '0'));

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
        'sale_reversal',
        'Reversa de venta mostrador',
        'cash',
        v_sale.total_amount,
        -v_sale.total_amount,
        v_resulting_balance,
        v_reference_document,
        nullif(p_reversal_notes, ''),
        p_reversed_by_user_id,
        timezone('utc', now())
    );

    update app.sales
       set status = 'reversed',
           reversed_at = timezone('utc', now()),
           reversed_by_user_id = p_reversed_by_user_id,
           reversal_cash_session_id = p_cash_session_id,
           reversal_notes = nullif(p_reversal_notes, '')
     where sale_id = p_sale_id;

    return p_sale_id;
end;
$$;

create or replace function app.create_account_sale(
    p_organization_id uuid,
    p_customer_id uuid,
    p_sold_by_user_id uuid,
    p_items jsonb,
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
