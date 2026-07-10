create table if not exists app.customers (
    customer_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    display_name text not null,
    tax_id text,
    phone text,
    email text,
    address text,
    credit_limit_amount numeric(18, 2) not null default 0,
    is_active boolean not null default true,
    notes text,
    created_at timestamptz not null default timezone('utc', now()),
    updated_at timestamptz not null default timezone('utc', now())
);

create unique index if not exists ix_customers_organization_display_name
    on app.customers (organization_id, lower(display_name));

create index if not exists ix_customers_organization_active_name
    on app.customers (organization_id, is_active, display_name asc);

create table if not exists app.customer_account_movements (
    customer_account_movement_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    customer_id uuid not null references app.customers (customer_id) on delete restrict,
    sale_id uuid references app.sales (sale_id) on delete restrict,
    cash_session_id uuid references app.cash_sessions (cash_session_id) on delete restrict,
    movement_type text not null,
    concept text not null,
    reference_document text,
    debit_amount numeric(18, 2) not null default 0,
    credit_amount numeric(18, 2) not null default 0,
    resulting_balance numeric(18, 2) not null,
    notes text,
    performed_by_user_id uuid references app.users (user_id),
    created_at timestamptz not null default timezone('utc', now()),
    constraint ck_customer_account_movement_type check (
        movement_type in ('sale_debit', 'payment_credit')
    ),
    constraint ck_customer_account_movement_non_negative check (
        debit_amount >= 0 and credit_amount >= 0
    ),
    constraint ck_customer_account_movement_direction check (
        (debit_amount > 0 and credit_amount = 0) or
        (credit_amount > 0 and debit_amount = 0)
    )
);

create index if not exists ix_customer_account_movements_customer_created_at
    on app.customer_account_movements (organization_id, customer_id, created_at desc);

create index if not exists ix_customer_account_movements_sale_id
    on app.customer_account_movements (sale_id);

alter table app.sales
    alter column cash_session_id drop not null;

alter table app.sales
    add column if not exists customer_id uuid references app.customers (customer_id),
    add column if not exists paid_amount numeric(18, 2) not null default 0,
    add column if not exists account_balance_amount numeric(18, 2) not null default 0;

alter table app.sales
    drop constraint if exists ck_sales_channel;

alter table app.sales
    add constraint ck_sales_channel check (sale_channel in ('pos_cash', 'pos_account'));

insert into app.permissions (permission_id, module, action)
values
    ('00000000-0000-0000-0000-000000000801'::uuid, 'customers', 'read'),
    ('00000000-0000-0000-0000-000000000802'::uuid, 'customers', 'write'),
    ('00000000-0000-0000-0000-000000000803'::uuid, 'accounts', 'read'),
    ('00000000-0000-0000-0000-000000000804'::uuid, 'accounts', 'write')
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
            ('customers', 'read'),
            ('customers', 'write'),
            ('accounts', 'read'),
            ('accounts', 'write')
        )
    ) or (
        r.code in ('seller', 'cashier')
        and (p.module, p.action) in (
            ('customers', 'read'),
            ('accounts', 'read'),
            ('accounts', 'write')
        )
    ) or (
        r.code = 'viewer'
        and (p.module, p.action) in (
            ('customers', 'read'),
            ('accounts', 'read')
        )
    )
on conflict (role_id, permission_id) do nothing;

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
    /*
    Contract:
    - Creates one confirmed sale on customer account for the active organization.
    - Persists sale and items, decreases stock, and records one customer account debit atomically.
    - Rejects the sale if the customer does not belong to the organization or exceeds the credit limit.
    - Returns the created sale_id.
    */
    if p_items is null or jsonb_typeof(p_items) <> 'array' or jsonb_array_length(p_items) = 0 then
        raise exception 'At least one sale item is required.';
    end if;

    select
        c.display_name,
        c.credit_limit_amount
    into
        v_customer_name,
        v_credit_limit
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
    /*
    Contract:
    - Records one payment against customer account in the active organization.
    - Decreases the customer account balance and increases the open cash session balance transactionally.
    - Rejects overpayments and invalid sessions.
    - Returns the created customer_account_movement_id.
    */
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
        nullif(p_notes, ''),
        p_performed_by_user_id,
        timezone('utc', now())
    );

    return v_movement_id;
end;
$$;
