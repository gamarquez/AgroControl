create table if not exists app.fiscal_settings (
    organization_id uuid primary key references app.organizations (organization_id) on delete cascade,
    provider text not null,
    environment text not null,
    taxpayer_id text not null,
    point_of_sale integer not null,
    service_name text not null default 'wsfe',
    default_document_type text not null default 'invoice_c',
    is_enabled boolean not null default false,
    updated_at timestamptz not null default timezone('utc', now()),
    constraint ck_fiscal_settings_provider check (provider in ('disabled', 'arca_wsfev1')),
    constraint ck_fiscal_settings_environment check (environment in ('homologation', 'production')),
    constraint ck_fiscal_settings_point_of_sale check (point_of_sale between 1 and 99998)
);

create table if not exists app.fiscal_documents (
    fiscal_document_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    sale_id uuid not null references app.sales (sale_id) on delete restrict,
    document_kind text not null,
    provider text not null,
    environment text not null,
    service_name text not null,
    taxpayer_id text not null,
    point_of_sale integer not null,
    status text not null,
    document_number bigint,
    cae text,
    cae_expires_on date,
    external_reference text,
    last_error text,
    attempts_count integer not null default 0,
    last_attempt_at timestamptz,
    created_at timestamptz not null default timezone('utc', now()),
    updated_at timestamptz not null default timezone('utc', now()),
    constraint uq_fiscal_documents_sale_invoice unique (sale_id, document_kind),
    constraint ck_fiscal_documents_kind check (document_kind in ('invoice', 'credit_note')),
    constraint ck_fiscal_documents_provider check (provider in ('disabled', 'arca_wsfev1')),
    constraint ck_fiscal_documents_environment check (environment in ('homologation', 'production')),
    constraint ck_fiscal_documents_status check (status in ('pending', 'authorized', 'rejected'))
);

create index if not exists ix_fiscal_documents_organization_status_created_at
    on app.fiscal_documents (organization_id, status, created_at desc);

create index if not exists ix_fiscal_documents_sale_id
    on app.fiscal_documents (sale_id);

create table if not exists app.fiscal_request_logs (
    fiscal_request_log_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    fiscal_document_id uuid references app.fiscal_documents (fiscal_document_id) on delete cascade,
    request_kind text not null,
    request_payload jsonb,
    response_payload jsonb,
    is_success boolean not null,
    error_message text,
    created_at timestamptz not null default timezone('utc', now()),
    constraint ck_fiscal_request_logs_kind check (request_kind in ('queue', 'probe'))
);

create index if not exists ix_fiscal_request_logs_document_created_at
    on app.fiscal_request_logs (fiscal_document_id, created_at desc);

insert into app.permissions (permission_id, module, action)
values
    ('00000000-0000-0000-0000-000000001001'::uuid, 'fiscal', 'read'),
    ('00000000-0000-0000-0000-000000001002'::uuid, 'fiscal', 'write')
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
            ('fiscal', 'read'),
            ('fiscal', 'write')
        )
    ) or (
        r.code in ('seller', 'cashier', 'viewer')
        and (p.module, p.action) in (
            ('fiscal', 'read')
        )
    )
on conflict (role_id, permission_id) do nothing;

insert into app.fiscal_settings (
    organization_id,
    provider,
    environment,
    taxpayer_id,
    point_of_sale,
    service_name,
    default_document_type,
    is_enabled,
    updated_at
)
select
    os.organization_id,
    'disabled',
    'homologation',
    os.tax_id,
    1,
    'wsfe',
    'invoice_c',
    false,
    timezone('utc', now())
from app.organization_settings os
where not exists (
    select 1
    from app.fiscal_settings fs
    where fs.organization_id = os.organization_id
);
