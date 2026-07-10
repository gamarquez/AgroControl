create extension if not exists pgcrypto;

alter table app.users
    add column if not exists password_hash text,
    add column if not exists last_login_at timestamptz,
    add column if not exists is_locked boolean not null default false,
    add column if not exists must_change_password boolean not null default false;

alter table app.organization_settings
    add column if not exists trade_name text,
    add column if not exists tax_id text;

create table if not exists app.refresh_sessions (
    session_id uuid primary key,
    user_id uuid not null references app.users (user_id) on delete cascade,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    token_hash text not null,
    created_at timestamptz not null default timezone('utc', now()),
    expires_at timestamptz not null,
    revoked_at timestamptz,
    revoked_reason text,
    replaced_by_session_id uuid references app.refresh_sessions (session_id),
    constraint uq_refresh_sessions_token_hash unique (token_hash)
);

create index if not exists ix_refresh_sessions_user_id on app.refresh_sessions (user_id);
create index if not exists ix_refresh_sessions_organization_id on app.refresh_sessions (organization_id);
create index if not exists ix_refresh_sessions_active on app.refresh_sessions (organization_id, user_id, revoked_at, expires_at desc);
create unique index if not exists ix_users_organization_email_normalized on app.users (organization_id, lower(email));

update app.users
set email = lower(trim(email))
where email <> lower(trim(email));

insert into app.organizations (organization_id, name, tax_id)
select
    '11111111-1111-1111-1111-111111111111'::uuid,
    'AgroControl Demo',
    '30-00000000-0'
where not exists (select 1 from app.organizations);

insert into app.organization_settings (
    organization_id,
    legal_name,
    trade_name,
    tax_id,
    time_zone,
    currency_code,
    fiscal_environment)
select
    o.organization_id,
    o.name,
    o.name,
    coalesce(o.tax_id, ''),
    'America/Argentina/Buenos_Aires',
    'ARS',
    'homologation'
from app.organizations o
where not exists (
    select 1
    from app.organization_settings s
    where s.organization_id = o.organization_id
);

insert into app.permissions (permission_id, module, action)
values
    ('00000000-0000-0000-0000-000000000201'::uuid, 'auth', 'session'),
    ('00000000-0000-0000-0000-000000000202'::uuid, 'auth', 'refresh'),
    ('00000000-0000-0000-0000-000000000203'::uuid, 'users', 'read'),
    ('00000000-0000-0000-0000-000000000204'::uuid, 'users', 'write'),
    ('00000000-0000-0000-0000-000000000205'::uuid, 'settings', 'read'),
    ('00000000-0000-0000-0000-000000000206'::uuid, 'settings', 'write')
on conflict (permission_id) do update
set module = excluded.module,
    action = excluded.action;

insert into app.roles (role_id, organization_id, name, code)
select gen_random_uuid(), o.organization_id, role_seed.name, role_seed.code
from app.organizations o
cross join (
    values
        ('Administrador', 'administrator'),
        ('Encargado', 'manager'),
        ('Vendedor', 'seller'),
        ('Cajero', 'cashier'),
        ('Consulta', 'viewer')
) as role_seed(name, code)
where not exists (
    select 1
    from app.roles r
    where r.organization_id = o.organization_id
      and r.code = role_seed.code
);

insert into app.role_permissions (role_id, permission_id)
select r.role_id, p.permission_id
from app.roles r
join app.permissions p
    on (
        r.code = 'administrator'
        and (p.module, p.action) in (
            ('auth', 'session'),
            ('auth', 'refresh'),
            ('users', 'read'),
            ('users', 'write'),
            ('settings', 'read'),
            ('settings', 'write')
        )
    ) or (
        r.code = 'manager'
        and (p.module, p.action) in (
            ('auth', 'session'),
            ('auth', 'refresh'),
            ('users', 'read'),
            ('settings', 'read')
        )
    ) or (
        r.code in ('seller', 'cashier', 'viewer')
        and (p.module, p.action) in (
            ('auth', 'session'),
            ('auth', 'refresh')
        )
    )
on conflict (role_id, permission_id) do nothing;
