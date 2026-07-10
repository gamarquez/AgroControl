create table if not exists app.cash_registers (
    cash_register_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    name text not null,
    code text not null,
    is_active boolean not null default true,
    created_at timestamptz not null default timezone('utc', now()),
    constraint uq_cash_registers_organization_code unique (organization_id, code)
);

create table if not exists app.cash_sessions (
    cash_session_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    cash_register_id uuid not null references app.cash_registers (cash_register_id) on delete restrict,
    opened_by_user_id uuid not null references app.users (user_id),
    closed_by_user_id uuid references app.users (user_id),
    opening_amount numeric(18, 2) not null,
    closing_amount numeric(18, 2),
    difference_amount numeric(18, 2),
    current_balance numeric(18, 2) not null,
    status text not null,
    opening_notes text,
    closing_notes text,
    opened_at timestamptz not null default timezone('utc', now()),
    closed_at timestamptz,
    constraint ck_cash_sessions_amounts_non_negative check (
        opening_amount >= 0
        and (closing_amount is null or closing_amount >= 0)
    ),
    constraint ck_cash_sessions_status check (status in ('open', 'closed'))
);

create unique index if not exists ix_cash_sessions_single_open_per_register
    on app.cash_sessions (organization_id, cash_register_id)
    where status = 'open';

create index if not exists ix_cash_sessions_organization_opened_at
    on app.cash_sessions (organization_id, opened_at desc);

create table if not exists app.cash_movements (
    cash_movement_id uuid primary key,
    organization_id uuid not null references app.organizations (organization_id) on delete cascade,
    cash_session_id uuid not null references app.cash_sessions (cash_session_id) on delete restrict,
    movement_type text not null,
    category_code text not null,
    concept text not null,
    payment_method text not null,
    amount numeric(18, 2) not null,
    signed_amount numeric(18, 2) not null,
    resulting_balance numeric(18, 2) not null,
    reference_document text,
    notes text,
    performed_by_user_id uuid references app.users (user_id),
    created_at timestamptz not null default timezone('utc', now()),
    constraint ck_cash_movements_type check (
        movement_type in ('opening', 'cash_in', 'cash_out')
    ),
    constraint ck_cash_movements_amount_positive check (amount > 0)
);

create index if not exists ix_cash_movements_organization_session_created_at
    on app.cash_movements (organization_id, cash_session_id, created_at desc);

create index if not exists ix_cash_movements_organization_type_created_at
    on app.cash_movements (organization_id, movement_type, created_at desc);

insert into app.permissions (permission_id, module, action)
values
    ('00000000-0000-0000-0000-000000000501'::uuid, 'cash', 'read'),
    ('00000000-0000-0000-0000-000000000502'::uuid, 'cash', 'write')
on conflict (permission_id) do update
set module = excluded.module,
    action = excluded.action;

insert into app.role_permissions (role_id, permission_id)
select r.role_id, p.permission_id
from app.roles r
join app.permissions p
    on (
        r.code in ('administrator', 'manager', 'cashier')
        and (p.module, p.action) in (
            ('cash', 'read'),
            ('cash', 'write')
        )
    ) or (
        r.code in ('seller', 'viewer')
        and (p.module, p.action) in (
            ('cash', 'read')
        )
    )
on conflict (role_id, permission_id) do nothing;

insert into app.cash_registers (
    cash_register_id,
    organization_id,
    name,
    code,
    is_active,
    created_at
)
select
    gen_random_uuid(),
    o.organization_id,
    'Caja principal',
    'main',
    true,
    timezone('utc', now())
from app.organizations o
where not exists (
    select 1
    from app.cash_registers cr
    where cr.organization_id = o.organization_id
      and cr.code = 'main'
);

create or replace function app.open_cash_session(
    p_organization_id uuid,
    p_cash_register_code text,
    p_opening_amount numeric,
    p_opening_notes text,
    p_opened_by_user_id uuid
)
returns uuid
language plpgsql
as $$
declare
    v_cash_session_id uuid := gen_random_uuid();
    v_cash_movement_id uuid := gen_random_uuid();
    v_cash_register_id uuid;
begin
    /*
    Contract:
    - Opens one cash session for the informed organization and register code.
    - Prevents more than one active session per register.
    - Creates an opening movement in the same transaction.
    - Returns the created cash_session_id.
    */
    if p_opening_amount is null or p_opening_amount < 0 then
        raise exception 'The opening amount cannot be negative.';
    end if;

    select cr.cash_register_id
      into v_cash_register_id
      from app.cash_registers cr
     where cr.organization_id = p_organization_id
       and cr.code = coalesce(nullif(trim(p_cash_register_code), ''), 'main')
       and cr.is_active = true;

    if v_cash_register_id is null then
        raise exception 'The informed cash register is not available for the active organization.';
    end if;

    if exists (
        select 1
        from app.cash_sessions cs
        where cs.organization_id = p_organization_id
          and cs.cash_register_id = v_cash_register_id
          and cs.status = 'open'
    ) then
        raise exception 'There is already an open cash session for the informed register.';
    end if;

    insert into app.cash_sessions (
        cash_session_id,
        organization_id,
        cash_register_id,
        opened_by_user_id,
        opening_amount,
        current_balance,
        status,
        opening_notes,
        opened_at
    )
    values (
        v_cash_session_id,
        p_organization_id,
        v_cash_register_id,
        p_opened_by_user_id,
        p_opening_amount,
        p_opening_amount,
        'open',
        nullif(p_opening_notes, ''),
        timezone('utc', now())
    );

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
        v_cash_session_id,
        'opening',
        'opening',
        'Apertura de caja',
        'cash',
        p_opening_amount,
        p_opening_amount,
        p_opening_amount,
        null,
        nullif(p_opening_notes, ''),
        p_opened_by_user_id,
        timezone('utc', now())
    );

    return v_cash_session_id;
end;
$$;

create or replace function app.record_cash_movement(
    p_organization_id uuid,
    p_cash_session_id uuid,
    p_movement_type text,
    p_category_code text,
    p_concept text,
    p_payment_method text,
    p_amount numeric,
    p_reference_document text,
    p_notes text,
    p_performed_by_user_id uuid default null
)
returns uuid
language plpgsql
as $$
declare
    v_cash_movement_id uuid := gen_random_uuid();
    v_current_balance numeric(18, 2);
    v_resulting_balance numeric(18, 2);
    v_signed_amount numeric(18, 2);
begin
    /*
    Contract:
    - Records one immutable manual movement against an open cash session.
    - Updates current_balance transactionally.
    - Prevents negative resulting balance.
    - Returns the created cash_movement_id.
    */
    if p_amount is null or p_amount <= 0 then
        raise exception 'The informed cash amount must be greater than zero.';
    end if;

    v_signed_amount := case p_movement_type
        when 'cash_in' then p_amount
        when 'cash_out' then -p_amount
        else null
    end;

    if v_signed_amount is null then
        raise exception 'The informed cash movement type is not supported.';
    end if;

    select cs.current_balance
      into v_current_balance
      from app.cash_sessions cs
     where cs.organization_id = p_organization_id
       and cs.cash_session_id = p_cash_session_id
       and cs.status = 'open'
     for update;

    if v_current_balance is null then
        raise exception 'The informed cash session is not open for the active organization.';
    end if;

    v_resulting_balance := v_current_balance + v_signed_amount;

    if v_resulting_balance < 0 then
        raise exception 'There is not enough balance to register the requested cash out movement.';
    end if;

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
        p_movement_type,
        trim(p_category_code),
        trim(p_concept),
        coalesce(nullif(trim(p_payment_method), ''), 'cash'),
        p_amount,
        v_signed_amount,
        v_resulting_balance,
        nullif(p_reference_document, ''),
        nullif(p_notes, ''),
        p_performed_by_user_id,
        timezone('utc', now())
    );

    return v_cash_movement_id;
end;
$$;

create or replace function app.close_cash_session(
    p_organization_id uuid,
    p_cash_session_id uuid,
    p_closing_amount numeric,
    p_closing_notes text,
    p_closed_by_user_id uuid
)
returns uuid
language plpgsql
as $$
declare
    v_current_balance numeric(18, 2);
begin
    /*
    Contract:
    - Closes one open cash session for the active organization.
    - Stores the declared closing amount and difference against current balance.
    - Returns the affected cash_session_id.
    */
    if p_closing_amount is null or p_closing_amount < 0 then
        raise exception 'The closing amount cannot be negative.';
    end if;

    select cs.current_balance
      into v_current_balance
      from app.cash_sessions cs
     where cs.organization_id = p_organization_id
       and cs.cash_session_id = p_cash_session_id
       and cs.status = 'open'
     for update;

    if v_current_balance is null then
        raise exception 'The informed cash session is not open for the active organization.';
    end if;

    update app.cash_sessions
       set closing_amount = p_closing_amount,
           difference_amount = p_closing_amount - v_current_balance,
           closed_by_user_id = p_closed_by_user_id,
           closing_notes = nullif(p_closing_notes, ''),
           closed_at = timezone('utc', now()),
           status = 'closed'
     where cash_session_id = p_cash_session_id;

    return p_cash_session_id;
end;
$$;
