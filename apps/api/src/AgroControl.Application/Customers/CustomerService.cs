using AgroControl.Application.Auth;

namespace AgroControl.Application.Customers;

internal sealed class CustomerService(
    ICustomerRepository customerRepository,
    IAuditLogRepository auditLogRepository) : ICustomerService
{
    public Task<CustomerListResult> ListCustomersAsync(IdentityContext identity, CustomerListQuery query, CancellationToken cancellationToken)
    {
        EnsureReader(identity);
        return customerRepository.ListCustomersAsync(identity.OrganizationId, CustomerValidation.ValidateListQuery(query), cancellationToken);
    }

    public async Task<CustomerRecord> GetCustomerAsync(IdentityContext identity, Guid customerId, CancellationToken cancellationToken)
    {
        EnsureReader(identity);

        return await customerRepository.GetCustomerAsync(
                   identity.OrganizationId,
                   CustomerValidation.ValidateRequiredGuid(customerId, "cliente"),
                   cancellationToken)
               ?? throw new NotFoundException("Cliente no encontrado.");
    }

    public async Task<CustomerRecord> CreateCustomerAsync(IdentityContext identity, CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var normalized = new CreateCustomerCommand(
            CustomerValidation.ValidateDisplayName(command.DisplayName),
            CustomerValidation.NormalizeOptional(command.TaxId),
            CustomerValidation.NormalizeOptional(command.Phone),
            CustomerValidation.NormalizeOptional(command.Email),
            CustomerValidation.NormalizeOptional(command.Address),
            CustomerValidation.ValidateCreditLimit(command.CreditLimitAmount),
            CustomerValidation.NormalizeOptional(command.Notes));

        var customer = await customerRepository.CreateCustomerAsync(identity.OrganizationId, normalized, cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "customers",
            customer.CustomerId.ToString(),
            "customer_created",
            new Dictionary<string, object?>
            {
                ["displayName"] = customer.DisplayName,
                ["creditLimitAmount"] = customer.CreditLimitAmount
            },
            cancellationToken);

        return customer;
    }

    public async Task<CustomerRecord> UpdateCustomerAsync(IdentityContext identity, Guid customerId, UpdateCustomerCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var customer = await customerRepository.UpdateCustomerAsync(
            identity.OrganizationId,
            CustomerValidation.ValidateRequiredGuid(customerId, "cliente"),
            new UpdateCustomerCommand(
                CustomerValidation.ValidateDisplayName(command.DisplayName),
                CustomerValidation.NormalizeOptional(command.TaxId),
                CustomerValidation.NormalizeOptional(command.Phone),
                CustomerValidation.NormalizeOptional(command.Email),
                CustomerValidation.NormalizeOptional(command.Address),
                CustomerValidation.ValidateCreditLimit(command.CreditLimitAmount),
                command.IsActive,
                CustomerValidation.NormalizeOptional(command.Notes)),
            cancellationToken)
            ?? throw new NotFoundException("Cliente no encontrado.");

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "customers",
            customer.CustomerId.ToString(),
            "customer_updated",
            new Dictionary<string, object?>
            {
                ["isActive"] = customer.IsActive,
                ["creditLimitAmount"] = customer.CreditLimitAmount,
                ["currentBalance"] = customer.CurrentBalance
            },
            cancellationToken);

        return customer;
    }

    public Task<CustomerAccountMovementListResult> ListAccountMovementsAsync(IdentityContext identity, Guid customerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        EnsureReader(identity);
        return customerRepository.ListAccountMovementsAsync(
            identity.OrganizationId,
            CustomerValidation.ValidateRequiredGuid(customerId, "cliente"),
            CustomerValidation.ValidatePage(page),
            CustomerValidation.ValidatePageSize(pageSize),
            cancellationToken);
    }

    public Task<CustomerAccountStatementRecord> GetAccountStatementAsync(IdentityContext identity, Guid customerId, int limit, CancellationToken cancellationToken)
    {
        EnsureReader(identity);
        return customerRepository.GetAccountStatementAsync(
            identity.OrganizationId,
            CustomerValidation.ValidateRequiredGuid(customerId, "cliente"),
            limit <= 0 ? 50 : Math.Min(limit, 200),
            cancellationToken);
    }

    public async Task<RecordCustomerPaymentResult> RecordPaymentAsync(IdentityContext identity, Guid customerId, RecordCustomerPaymentCommand command, CancellationToken cancellationToken)
    {
        EnsureAccountWriter(identity);

        var result = await customerRepository.RecordPaymentAsync(
            identity.OrganizationId,
            CustomerValidation.ValidateRequiredGuid(customerId, "cliente"),
            identity.UserId,
            new RecordCustomerPaymentCommand(
                CustomerValidation.ValidateRequiredGuid(command.CashSessionId, "sesion de caja"),
                CustomerValidation.ValidateAmount(command.Amount),
                CustomerValidation.NormalizeOptional(command.Notes)),
            cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "customer_account_movements",
            result.Movement.CustomerAccountMovementId.ToString(),
            "customer_payment_recorded",
            new Dictionary<string, object?>
            {
                ["customerId"] = result.Customer.CustomerId,
                ["amount"] = result.Movement.CreditAmount,
                ["resultingBalance"] = result.Movement.ResultingBalance
            },
            cancellationToken);

        return result;
    }

    public async Task<RecordCustomerCreditNoteResult> RecordCreditNoteAsync(IdentityContext identity, Guid customerId, RecordCustomerCreditNoteCommand command, CancellationToken cancellationToken)
    {
        EnsureAdjustmentWriter(identity);

        var result = await customerRepository.RecordCreditNoteAsync(
            identity.OrganizationId,
            CustomerValidation.ValidateRequiredGuid(customerId, "cliente"),
            identity.UserId,
            new RecordCustomerCreditNoteCommand(
                CustomerValidation.ValidateAmount(command.Amount),
                CustomerValidation.ValidateConcept(command.Concept),
                CustomerValidation.NormalizeOptional(command.ReferenceDocument),
                CustomerValidation.NormalizeOptional(command.Notes)),
            cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "customer_account_movements",
            result.Movement.CustomerAccountMovementId.ToString(),
            "customer_credit_note_recorded",
            new Dictionary<string, object?>
            {
                ["customerId"] = result.Customer.CustomerId,
                ["amount"] = result.Movement.CreditAmount,
                ["concept"] = result.Movement.Concept,
                ["resultingBalance"] = result.Movement.ResultingBalance
            },
            cancellationToken);

        return result;
    }

    private static void EnsureReader(IdentityContext identity)
    {
        if (!identity.Roles.Any(role =>
                role.Equals("administrator", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("manager", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("seller", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("cashier", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("viewer", StringComparison.OrdinalIgnoreCase)))
        {
            throw new AuthorizationException("No cuenta con permisos para consultar clientes.");
        }
    }

    private static void EnsureWriter(IdentityContext identity)
    {
        if (!identity.Roles.Any(role =>
                role.Equals("administrator", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("manager", StringComparison.OrdinalIgnoreCase)))
        {
            throw new AuthorizationException("No cuenta con permisos para administrar clientes.");
        }
    }

    private static void EnsureAccountWriter(IdentityContext identity)
    {
        if (!identity.Roles.Any(role =>
                role.Equals("administrator", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("manager", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("seller", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("cashier", StringComparison.OrdinalIgnoreCase)))
        {
            throw new AuthorizationException("No cuenta con permisos para operar cuenta corriente.");
        }
    }

    private static void EnsureAdjustmentWriter(IdentityContext identity)
    {
        if (!identity.Roles.Any(role =>
                role.Equals("administrator", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("manager", StringComparison.OrdinalIgnoreCase)))
        {
            throw new AuthorizationException("No cuenta con permisos para registrar notas de credito.");
        }
    }
}
