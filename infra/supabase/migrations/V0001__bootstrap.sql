create schema if not exists app;

create table if not exists app.organizations (
    organization_id uuid primary key,
    name text not null,
    tax_id text,
    created_at timestamptz not null default timezone('utc', now())
);

create table if not exists app.branches (
    branch_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id),
    name text not null,
    code text not null,
    created_at timestamptz not null default timezone('utc', now()),
    constraint uq_branches_organization_code unique (organization_id, code)
);

create table if not exists app.users (
    user_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id),
    branch_id uuid references app.branches (branch_id),
    email text not null,
    display_name text not null,
    is_active boolean not null default true,
    created_at timestamptz not null default timezone('utc', now()),
    constraint uq_users_organization_email unique (organization_id, email)
);

create table if not exists app.roles (
    role_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id),
    name text not null,
    code text not null,
    created_at timestamptz not null default timezone('utc', now()),
    constraint uq_roles_organization_code unique (organization_id, code)
);

create table if not exists app.permissions (
    permission_id uuid primary key,
    module text not null,
    action text not null,
    created_at timestamptz not null default timezone('utc', now()),
    constraint uq_permissions_module_action unique (module, action)
);

create table if not exists app.role_permissions (
    role_id uuid not null references app.roles (role_id) on delete cascade,
    permission_id uuid not null references app.permissions (permission_id) on delete cascade,
    granted_at timestamptz not null default timezone('utc', now()),
    primary key (role_id, permission_id)
);

create table if not exists app.user_roles (
    user_id uuid not null references app.users (user_id) on delete cascade,
    role_id uuid not null references app.roles (role_id) on delete cascade,
    assigned_at timestamptz not null default timezone('utc', now()),
    primary key (user_id, role_id)
);

create table if not exists app.organization_settings (
    organization_id uuid primary key references app.organizations (organization_id),
    legal_name text,
    time_zone text not null default 'America/Argentina/Buenos_Aires',
    currency_code text not null default 'ARS',
    fiscal_environment text not null default 'homologation',
    created_at timestamptz not null default timezone('utc', now()),
    updated_at timestamptz not null default timezone('utc', now())
);

create table if not exists app.audit_logs (
    audit_log_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id),
    actor_user_id uuid references app.users (user_id),
    entity_name text not null,
    entity_id text not null,
    action text not null,
    metadata jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_branches_organization_id on app.branches (organization_id);
create index if not exists ix_users_organization_id on app.users (organization_id);
create index if not exists ix_users_branch_id on app.users (branch_id);
create index if not exists ix_roles_organization_id on app.roles (organization_id);
create index if not exists ix_audit_logs_organization_id_created_at on app.audit_logs (organization_id, created_at desc);
