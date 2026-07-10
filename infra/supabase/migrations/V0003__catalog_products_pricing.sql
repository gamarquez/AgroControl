create table if not exists app.product_categories (
    category_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    name text not null,
    description text,
    is_active boolean not null default true,
    created_at timestamptz not null default timezone('utc', now()),
    updated_at timestamptz not null default timezone('utc', now()),
    constraint uq_product_categories_organization_name unique (organization_id, name)
);

create table if not exists app.brands (
    brand_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    name text not null,
    description text,
    is_active boolean not null default true,
    created_at timestamptz not null default timezone('utc', now()),
    updated_at timestamptz not null default timezone('utc', now()),
    constraint uq_brands_organization_name unique (organization_id, name)
);

create table if not exists app.units_of_measure (
    unit_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    name text not null,
    code text not null,
    symbol text not null,
    allows_fraction boolean not null default false,
    is_active boolean not null default true,
    created_at timestamptz not null default timezone('utc', now()),
    updated_at timestamptz not null default timezone('utc', now()),
    constraint uq_units_of_measure_organization_code unique (organization_id, code)
);

create table if not exists app.price_lists (
    price_list_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    name text not null,
    code text not null,
    is_default boolean not null default false,
    is_active boolean not null default true,
    created_at timestamptz not null default timezone('utc', now()),
    updated_at timestamptz not null default timezone('utc', now()),
    constraint uq_price_lists_organization_code unique (organization_id, code)
);

create table if not exists app.products (
    product_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    category_id uuid references app.product_categories (category_id),
    brand_id uuid references app.brands (brand_id),
    base_unit_id uuid not null references app.units_of_measure (unit_id),
    name text not null,
    description text,
    internal_code text not null,
    sku text,
    barcode text,
    is_active boolean not null default true,
    allows_fraction boolean not null default false,
    sales_unit_label text,
    created_at timestamptz not null default timezone('utc', now()),
    updated_at timestamptz not null default timezone('utc', now()),
    deleted_at timestamptz,
    constraint uq_products_organization_internal_code unique (organization_id, internal_code)
);

create table if not exists app.product_prices (
    product_price_id uuid primary key,
    product_id uuid not null references app.products (product_id) on delete cascade,
    price_list_id uuid not null references app.price_lists (price_list_id) on delete cascade,
    cost_amount numeric(18, 2) not null,
    margin_percent numeric(9, 2),
    sale_amount numeric(18, 2) not null,
    currency_code text not null,
    effective_from timestamptz not null default timezone('utc', now()),
    updated_at timestamptz not null default timezone('utc', now()),
    constraint uq_product_prices_product_price_list unique (product_id, price_list_id)
);

create unique index if not exists ix_products_organization_sku_not_null
    on app.products (organization_id, sku)
    where sku is not null;

create unique index if not exists ix_products_organization_barcode_not_null
    on app.products (organization_id, barcode)
    where barcode is not null;

create index if not exists ix_products_organization_name on app.products (organization_id, name);
create index if not exists ix_products_organization_is_active on app.products (organization_id, is_active);
create index if not exists ix_products_organization_category on app.products (organization_id, category_id);
create index if not exists ix_products_organization_brand on app.products (organization_id, brand_id);
create index if not exists ix_product_prices_price_list on app.product_prices (price_list_id);
create unique index if not exists ix_price_lists_one_default_per_organization
    on app.price_lists (organization_id)
    where is_default;

insert into app.permissions (permission_id, module, action)
values
    ('00000000-0000-0000-0000-000000000301'::uuid, 'catalog', 'read'),
    ('00000000-0000-0000-0000-000000000302'::uuid, 'catalog', 'write'),
    ('00000000-0000-0000-0000-000000000303'::uuid, 'pricing', 'read'),
    ('00000000-0000-0000-0000-000000000304'::uuid, 'pricing', 'write')
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
            ('catalog', 'read'),
            ('catalog', 'write'),
            ('pricing', 'read'),
            ('pricing', 'write')
        )
    ) or (
        r.code in ('seller', 'cashier', 'viewer')
        and (p.module, p.action) in (
            ('catalog', 'read'),
            ('pricing', 'read')
        )
    )
on conflict (role_id, permission_id) do nothing;

insert into app.units_of_measure (unit_id, organization_id, name, code, symbol, allows_fraction, is_active)
select gen_random_uuid(), o.organization_id, seed.name, seed.code, seed.symbol, seed.allows_fraction, true
from app.organizations o
cross join (
    values
        ('Unidad', 'unit', 'u', false),
        ('Kilogramo', 'kg', 'kg', true),
        ('Gramo', 'g', 'g', true),
        ('Litro', 'litre', 'l', true),
        ('Mililitro', 'ml', 'ml', true),
        ('Bolsa', 'bag', 'bag', false)
) as seed(name, code, symbol, allows_fraction)
where not exists (
    select 1
    from app.units_of_measure u
    where u.organization_id = o.organization_id
      and u.code = seed.code
);

insert into app.price_lists (price_list_id, organization_id, name, code, is_default, is_active)
select gen_random_uuid(), o.organization_id, 'Lista estandar', 'standard', true, true
from app.organizations o
where not exists (
    select 1
    from app.price_lists p
    where p.organization_id = o.organization_id
      and p.code = 'standard'
);

update app.price_lists p
set is_default = (p.code = 'standard'),
    updated_at = timezone('utc', now())
where p.code = 'standard'
  and p.is_default is distinct from true;

create or replace function app.create_product(
    p_organization_id uuid,
    p_category_id uuid,
    p_brand_id uuid,
    p_base_unit_id uuid,
    p_name text,
    p_description text,
    p_internal_code text,
    p_sku text,
    p_barcode text,
    p_allows_fraction boolean,
    p_sales_unit_label text,
    p_cost_amount numeric,
    p_margin_percent numeric,
    p_sale_amount numeric,
    p_currency_code text,
    p_price_list_id uuid default null
)
returns uuid
language plpgsql
as $$
declare
    v_product_id uuid := gen_random_uuid();
    v_price_list_id uuid;
begin
    /*
    Contract:
    - Creates a product and its current price in one transaction.
    - Uses the default active price list when p_price_list_id is null.
    - Validates organization ownership for category, brand, unit and price list.
    - Returns the created product_id.
    */
    select pl.price_list_id
      into v_price_list_id
      from app.price_lists pl
     where pl.organization_id = p_organization_id
       and pl.is_active
       and (
            (p_price_list_id is not null and pl.price_list_id = p_price_list_id)
            or (p_price_list_id is null and pl.is_default)
       )
     limit 1;

    if v_price_list_id is null then
        raise exception 'No active default price list was found for the organization.';
    end if;

    if p_category_id is not null and not exists (
        select 1 from app.product_categories c
        where c.category_id = p_category_id
          and c.organization_id = p_organization_id
    ) then
        raise exception 'The informed category does not belong to the active organization.';
    end if;

    if p_brand_id is not null and not exists (
        select 1 from app.brands b
        where b.brand_id = p_brand_id
          and b.organization_id = p_organization_id
    ) then
        raise exception 'The informed brand does not belong to the active organization.';
    end if;

    if not exists (
        select 1 from app.units_of_measure u
        where u.unit_id = p_base_unit_id
          and u.organization_id = p_organization_id
          and u.is_active
    ) then
        raise exception 'The informed unit does not belong to the active organization.';
    end if;

    insert into app.products (
        product_id,
        organization_id,
        category_id,
        brand_id,
        base_unit_id,
        name,
        description,
        internal_code,
        sku,
        barcode,
        is_active,
        allows_fraction,
        sales_unit_label,
        created_at,
        updated_at)
    values (
        v_product_id,
        p_organization_id,
        p_category_id,
        p_brand_id,
        p_base_unit_id,
        p_name,
        p_description,
        p_internal_code,
        nullif(p_sku, ''),
        nullif(p_barcode, ''),
        true,
        p_allows_fraction,
        nullif(p_sales_unit_label, ''),
        timezone('utc', now()),
        timezone('utc', now()));

    insert into app.product_prices (
        product_price_id,
        product_id,
        price_list_id,
        cost_amount,
        margin_percent,
        sale_amount,
        currency_code,
        effective_from,
        updated_at)
    values (
        gen_random_uuid(),
        v_product_id,
        v_price_list_id,
        p_cost_amount,
        p_margin_percent,
        p_sale_amount,
        upper(trim(p_currency_code)),
        timezone('utc', now()),
        timezone('utc', now()));

    return v_product_id;
end;
$$;

create or replace function app.update_product(
    p_organization_id uuid,
    p_product_id uuid,
    p_category_id uuid,
    p_brand_id uuid,
    p_base_unit_id uuid,
    p_name text,
    p_description text,
    p_internal_code text,
    p_sku text,
    p_barcode text,
    p_is_active boolean,
    p_allows_fraction boolean,
    p_sales_unit_label text,
    p_cost_amount numeric,
    p_margin_percent numeric,
    p_sale_amount numeric,
    p_currency_code text,
    p_price_list_id uuid default null
)
returns uuid
language plpgsql
as $$
declare
    v_price_list_id uuid;
begin
    /*
    Contract:
    - Updates product data and the current price for one price list in one transaction.
    - Uses the default active price list when p_price_list_id is null.
    - Applies soft deactivation through is_active/deleted_at.
    - Returns the updated product_id.
    */
    if not exists (
        select 1 from app.products p
        where p.product_id = p_product_id
          and p.organization_id = p_organization_id
    ) then
        raise exception 'The informed product does not belong to the active organization.';
    end if;

    select pl.price_list_id
      into v_price_list_id
      from app.price_lists pl
     where pl.organization_id = p_organization_id
       and pl.is_active
       and (
            (p_price_list_id is not null and pl.price_list_id = p_price_list_id)
            or (p_price_list_id is null and pl.is_default)
       )
     limit 1;

    if v_price_list_id is null then
        raise exception 'No active default price list was found for the organization.';
    end if;

    if p_category_id is not null and not exists (
        select 1 from app.product_categories c
        where c.category_id = p_category_id
          and c.organization_id = p_organization_id
    ) then
        raise exception 'The informed category does not belong to the active organization.';
    end if;

    if p_brand_id is not null and not exists (
        select 1 from app.brands b
        where b.brand_id = p_brand_id
          and b.organization_id = p_organization_id
    ) then
        raise exception 'The informed brand does not belong to the active organization.';
    end if;

    if not exists (
        select 1 from app.units_of_measure u
        where u.unit_id = p_base_unit_id
          and u.organization_id = p_organization_id
          and u.is_active
    ) then
        raise exception 'The informed unit does not belong to the active organization.';
    end if;

    update app.products
       set category_id = p_category_id,
           brand_id = p_brand_id,
           base_unit_id = p_base_unit_id,
           name = p_name,
           description = p_description,
           internal_code = p_internal_code,
           sku = nullif(p_sku, ''),
           barcode = nullif(p_barcode, ''),
           is_active = p_is_active,
           allows_fraction = p_allows_fraction,
           sales_unit_label = nullif(p_sales_unit_label, ''),
           updated_at = timezone('utc', now()),
           deleted_at = case when p_is_active then null else timezone('utc', now()) end
     where product_id = p_product_id
       and organization_id = p_organization_id;

    insert into app.product_prices (
        product_price_id,
        product_id,
        price_list_id,
        cost_amount,
        margin_percent,
        sale_amount,
        currency_code,
        effective_from,
        updated_at)
    values (
        gen_random_uuid(),
        p_product_id,
        v_price_list_id,
        p_cost_amount,
        p_margin_percent,
        p_sale_amount,
        upper(trim(p_currency_code)),
        timezone('utc', now()),
        timezone('utc', now()))
    on conflict (product_id, price_list_id) do update
       set cost_amount = excluded.cost_amount,
           margin_percent = excluded.margin_percent,
           sale_amount = excluded.sale_amount,
           currency_code = excluded.currency_code,
           effective_from = excluded.effective_from,
           updated_at = excluded.updated_at;

    return p_product_id;
end;
$$;
