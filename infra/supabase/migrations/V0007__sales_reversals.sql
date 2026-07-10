alter table app.sales
    drop constraint if exists ck_sales_status;

alter table app.sales
    add column if not exists reversed_at timestamptz,
    add column if not exists reversed_by_user_id uuid references app.users (user_id),
    add column if not exists reversal_cash_session_id uuid references app.cash_sessions (cash_session_id),
    add column if not exists reversal_notes text;

alter table app.sales
    add constraint ck_sales_status check (status in ('confirmed', 'reversed'));

create index if not exists ix_sales_organization_status_created_at
    on app.sales (organization_id, status, created_at desc);

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
begin
    /*
    Contract:
    - Reverses one confirmed cash sale for the active organization.
    - Restores stock and records one compensating cash out movement in an open cash session.
    - Marks the original sale as reversed without deleting prior movements.
    - Returns the reversed sale_id.
    */
    select
        s.sale_id,
        s.ticket_number,
        s.total_amount,
        s.status
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
        select
            si.product_id,
            si.quantity,
            si.product_name
        from app.sale_items si
        where si.sale_id = p_sale_id
        order by si.created_at asc
    loop
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
            v_item.product_id,
            0,
            null,
            null,
            null,
            1,
            timezone('utc', now())
        )
        on conflict (organization_id, product_id) do nothing;

        update app.stock_balances
           set on_hand_quantity = on_hand_quantity + v_item.quantity,
               version_number = version_number + 1,
               updated_at = timezone('utc', now())
         where organization_id = p_organization_id
           and product_id = v_item.product_id
        returning on_hand_quantity into v_resulting_quantity;

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
