create table if not exists app.sales (
    sale_id uuid primary key,
    ticket_number bigint generated always as identity,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    cash_session_id uuid not null references app.cash_sessions (cash_session_id) on delete restrict,
    sold_by_user_id uuid not null references app.users (user_id),
    sale_channel text not null,
    status text not null,
    customer_name text not null default 'Consumidor final',
    subtotal_amount numeric(18, 2) not null,
    discount_amount numeric(18, 2) not null default 0,
    total_amount numeric(18, 2) not null,
    currency_code text not null default 'ARS',
    notes text,
    created_at timestamptz not null default timezone('utc', now()),
    constraint ck_sales_status check (status in ('confirmed')),
    constraint ck_sales_channel check (sale_channel in ('pos_cash')),
    constraint ck_sales_amounts_non_negative check (
        subtotal_amount >= 0
        and discount_amount >= 0
        and total_amount >= 0
    )
);

create table if not exists app.sale_items (
    sale_item_id uuid primary key,
    sale_id uuid not null references app.sales (sale_id) on delete cascade,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    product_id uuid not null references app.products (product_id) on delete restrict,
    product_name text not null,
    unit_symbol text not null,
    quantity numeric(18, 3) not null,
    unit_price numeric(18, 2) not null,
    line_total numeric(18, 2) not null,
    created_at timestamptz not null default timezone('utc', now()),
    constraint ck_sale_items_quantity_positive check (quantity > 0),
    constraint ck_sale_items_amounts_non_negative check (unit_price >= 0 and line_total >= 0)
);

create index if not exists ix_sales_organization_created_at
    on app.sales (organization_id, created_at desc);

create index if not exists ix_sales_organization_cash_session
    on app.sales (organization_id, cash_session_id, created_at desc);

create index if not exists ix_sale_items_sale_id
    on app.sale_items (sale_id);

create index if not exists ix_sale_items_product_id
    on app.sale_items (organization_id, product_id);

insert into app.permissions (permission_id, module, action)
values
    ('00000000-0000-0000-0000-000000000601'::uuid, 'sales', 'read'),
    ('00000000-0000-0000-0000-000000000602'::uuid, 'sales', 'write')
on conflict (permission_id) do update
set module = excluded.module,
    action = excluded.action;

insert into app.role_permissions (role_id, permission_id)
select r.role_id, p.permission_id
from app.roles r
join app.permissions p
    on (
        r.code in ('administrator', 'manager', 'seller', 'cashier')
        and (p.module, p.action) in (
            ('sales', 'read'),
            ('sales', 'write')
        )
    ) or (
        r.code = 'viewer'
        and (p.module, p.action) in (
            ('sales', 'read')
        )
    )
on conflict (role_id, permission_id) do nothing;

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
    /*
    Contract:
    - Creates one confirmed POS cash sale for the active organization.
    - Persists sale and items, decreases stock, and records one cash inflow atomically.
    - Requires an open cash session and a non-empty item list.
    - Returns the created sale_id.
    */
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

    for v_item in
        select value
        from jsonb_array_elements(p_items)
    loop
        v_product_id := (v_item ->> 'productId')::uuid;
        v_quantity := (v_item ->> 'quantity')::numeric;

        if v_product_id is null then
            raise exception 'Each sale item must include a valid product.';
        end if;

        if v_quantity is null or v_quantity <= 0 then
            raise exception 'Each sale item quantity must be greater than zero.';
        end if;

        select
            p.name,
            u.symbol,
            pp.sale_amount,
            p.allows_fraction
        into
            v_product_name,
            v_unit_symbol,
            v_sale_amount,
            v_allows_fraction
        from app.products p
        join app.units_of_measure u on u.unit_id = p.base_unit_id
        join app.price_lists pl
            on pl.organization_id = p.organization_id
           and pl.is_default
           and pl.is_active
        join app.product_prices pp
            on pp.product_id = p.product_id
           and pp.price_list_id = pl.price_list_id
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
            v_product_id,
            0,
            null,
            null,
            null,
            1,
            timezone('utc', now())
        )
        on conflict (organization_id, product_id) do nothing;

        select sb.on_hand_quantity
          into v_current_quantity
          from app.stock_balances sb
         where sb.organization_id = p_organization_id
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
