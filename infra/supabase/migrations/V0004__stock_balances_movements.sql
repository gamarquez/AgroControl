create table if not exists app.stock_balances (
    stock_balance_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    product_id uuid not null references app.products (product_id) on delete cascade,
    on_hand_quantity numeric(18, 3) not null default 0,
    min_quantity numeric(18, 3),
    max_quantity numeric(18, 3),
    reorder_point numeric(18, 3),
    version_number integer not null default 1,
    updated_at timestamptz not null default timezone('utc', now()),
    constraint uq_stock_balances_organization_product unique (organization_id, product_id)
);

create table if not exists app.stock_movements (
    stock_movement_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    product_id uuid not null references app.products (product_id) on delete cascade,
    movement_type text not null,
    quantity numeric(18, 3) not null,
    quantity_delta numeric(18, 3) not null,
    resulting_quantity numeric(18, 3) not null,
    reason text not null,
    reference_document text,
    notes text,
    performed_by_user_id uuid references app.users (user_id),
    created_at timestamptz not null default timezone('utc', now()),
    constraint ck_stock_movements_type check (
        movement_type in (
            'purchase_inbound',
            'sale_outbound',
            'adjustment_increase',
            'adjustment_decrease',
            'return_inbound',
            'loss',
            'broken',
            'expired'
        )
    ),
    constraint ck_stock_movements_quantity_positive check (quantity > 0)
);

create index if not exists ix_stock_balances_organization_updated_at
    on app.stock_balances (organization_id, updated_at desc);

create index if not exists ix_stock_movements_organization_product_created_at
    on app.stock_movements (organization_id, product_id, created_at desc);

create index if not exists ix_stock_movements_organization_type_created_at
    on app.stock_movements (organization_id, movement_type, created_at desc);

insert into app.permissions (permission_id, module, action)
values
    ('00000000-0000-0000-0000-000000000401'::uuid, 'stock', 'read'),
    ('00000000-0000-0000-0000-000000000402'::uuid, 'stock', 'write')
on conflict (permission_id) do update
set module = excluded.module,
    action = excluded.action;

insert into app.role_permissions (role_id, permission_id)
select r.role_id, p.permission_id
from app.roles r
join app.permissions p
    on (
        r.code in ('administrator', 'manager')
        and (p.module, p.action) in (
            ('stock', 'read'),
            ('stock', 'write')
        )
    ) or (
        r.code in ('seller', 'cashier', 'viewer')
        and (p.module, p.action) in (
            ('stock', 'read')
        )
    )
on conflict (role_id, permission_id) do nothing;

insert into app.stock_balances (
    stock_balance_id,
    organization_id,
    product_id,
    on_hand_quantity,
    min_quantity,
    max_quantity,
    reorder_point,
    version_number,
    updated_at
)
select
    gen_random_uuid(),
    p.organization_id,
    p.product_id,
    0,
    null,
    null,
    null,
    1,
    timezone('utc', now())
from app.products p
where not exists (
    select 1
    from app.stock_balances sb
    where sb.organization_id = p.organization_id
      and sb.product_id = p.product_id
);

create or replace function app.update_stock_policy(
    p_organization_id uuid,
    p_product_id uuid,
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
    /*
    Contract:
    - Creates or updates the single-location stock policy for a product.
    - Ensures the product belongs to the active organization.
    - Keeps current on_hand_quantity untouched and increments version_number on updates.
    - Returns the affected stock_balance_id.
    */
    if not exists (
        select 1
        from app.products p
        where p.product_id = p_product_id
          and p.organization_id = p_organization_id
    ) then
        raise exception 'The informed product does not belong to the active organization.';
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
        p_product_id,
        0,
        p_min_quantity,
        p_max_quantity,
        p_reorder_point,
        1,
        timezone('utc', now())
    )
    on conflict (organization_id, product_id) do update
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
    /*
    Contract:
    - Records one immutable stock movement and updates the single-location balance transactionally.
    - Derives quantity_delta from the movement type and prevents negative stock.
    - Rejects fractional quantities for products that do not allow fractioned operation.
    - Returns the created stock_movement_id.
    */
    if p_quantity is null or p_quantity <= 0 then
        raise exception 'The informed quantity must be greater than zero.';
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
        p_product_id,
        0,
        null,
        null,
        null,
        1,
        timezone('utc', now())
    )
    on conflict (organization_id, product_id) do nothing;

    select sb.stock_balance_id, sb.on_hand_quantity
      into v_balance_id, v_current_quantity
      from app.stock_balances sb
     where sb.organization_id = p_organization_id
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
