alter table app.customer_account_movements
    drop constraint if exists ck_customer_account_movement_type;

alter table app.customer_account_movements
    add constraint ck_customer_account_movement_type check (
        movement_type in ('sale_debit', 'payment_credit', 'credit_note')
    );

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
    v_remaining_amount numeric(18, 2);
    v_debit record;
    v_open_amount numeric(18, 2);
    v_applied_amount numeric(18, 2);
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

    if coalesce(v_current_account_balance, 0) <= 0 then
        raise exception 'The informed customer does not have an outstanding balance.';
    end if;

    v_resulting_account_balance := coalesce(v_current_account_balance, 0) - p_amount;

    if v_resulting_account_balance < 0 then
        raise exception 'The informed credit note exceeds the outstanding customer balance.';
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

    v_remaining_amount := p_amount;

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
            v_movement_id,
            v_debit.customer_account_movement_id,
            v_applied_amount,
            timezone('utc', now())
        );

        v_remaining_amount := v_remaining_amount - v_applied_amount;
    end loop;

    return v_movement_id;
end;
$$;
