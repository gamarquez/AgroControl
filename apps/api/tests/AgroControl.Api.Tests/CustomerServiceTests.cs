using AgroControl.Application.Auth;
using AgroControl.Application.Customers;

namespace AgroControl.Api.Tests;

public sealed class CustomerServiceTests
{
    [Fact]
    public async Task CreateCustomerAsync_WithAdministratorRole_WritesAudit()
    {
        var repository = new FakeCustomerRepository();
        var auditRepository = new FakeAuditLogRepository();
        var service = new CustomerService(repository, auditRepository);

        var customer = await service.CreateCustomerAsync(
            CreateAdministratorIdentity(repository.OrganizationId),
            new CreateCustomerCommand("Cliente Rural", null, null, null, null, 50000, null),
            CancellationToken.None);

        Assert.Equal("Cliente Rural", customer.DisplayName);
        Assert.Contains(auditRepository.Entries, entry => entry.Action == "customer_created");
    }

    [Fact]
    public async Task RecordPaymentAsync_WithCashierRole_WritesAudit()
    {
        var repository = new FakeCustomerRepository();
        var auditRepository = new FakeAuditLogRepository();
        var service = new CustomerService(repository, auditRepository);

        var result = await service.RecordPaymentAsync(
            new IdentityContext(Guid.NewGuid(), repository.OrganizationId, null, "cashier@demo.local", ["cashier"]),
            repository.Customer.CustomerId,
            new RecordCustomerPaymentCommand(Guid.NewGuid(), 1000, "Cobranza mostrador"),
            CancellationToken.None);

        Assert.Equal(1000, result.Movement.CreditAmount);
        Assert.Contains(auditRepository.Entries, entry => entry.Action == "customer_payment_recorded");
    }

    [Fact]
    public async Task RecordCreditNoteAsync_WithManagerRole_WritesAudit()
    {
        var repository = new FakeCustomerRepository();
        var auditRepository = new FakeAuditLogRepository();
        var service = new CustomerService(repository, auditRepository);

        var result = await service.RecordCreditNoteAsync(
            new IdentityContext(Guid.NewGuid(), repository.OrganizationId, null, "manager@demo.local", ["manager"]),
            repository.Customer.CustomerId,
            new RecordCustomerCreditNoteCommand(500, "Bonificacion comercial", "NC-INT-1", "Ajuste"),
            CancellationToken.None);

        Assert.Equal(500, result.Movement.CreditAmount);
        Assert.Equal("credit_note", result.Movement.MovementType);
        Assert.Contains(auditRepository.Entries, entry => entry.Action == "customer_credit_note_recorded");
    }

    [Fact]
    public async Task ListCustomersAsync_NormalizesPaginationAndSearch()
    {
        var repository = new FakeCustomerRepository();
        var service = new CustomerService(repository, new FakeAuditLogRepository());

        _ = await service.ListCustomersAsync(
            CreateAdministratorIdentity(repository.OrganizationId),
            new CustomerListQuery("  rural  ", null, 0, 500),
            CancellationToken.None);

        Assert.NotNull(repository.LastQuery);
        Assert.Equal("rural", repository.LastQuery!.Search);
        Assert.Equal(1, repository.LastQuery.Page);
        Assert.Equal(100, repository.LastQuery.PageSize);
    }

    private static IdentityContext CreateAdministratorIdentity(Guid organizationId)
        => new(Guid.NewGuid(), organizationId, null, "admin@demo.local", ["administrator"]);

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        public Guid OrganizationId { get; } = Guid.NewGuid();

        public CustomerRecord Customer { get; private set; }

        public CustomerListQuery? LastQuery { get; private set; }

        public FakeCustomerRepository()
        {
            Customer = new CustomerRecord(
                Guid.NewGuid(),
                OrganizationId,
                "Cliente Base",
                null,
                null,
                null,
                null,
                10000,
                2500,
                500,
                DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7)),
                true,
                null,
                DateTimeOffset.UtcNow.AddDays(-10),
                DateTimeOffset.UtcNow.AddDays(-1));
        }

        public Task<CustomerListResult> ListCustomersAsync(Guid organizationId, CustomerListQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult(new CustomerListResult([Customer], query.Page, query.PageSize, 1));
        }

        public Task<CustomerRecord?> GetCustomerAsync(Guid organizationId, Guid customerId, CancellationToken cancellationToken)
            => Task.FromResult<CustomerRecord?>(customerId == Customer.CustomerId ? Customer : null);

        public Task<CustomerRecord> CreateCustomerAsync(Guid organizationId, CreateCustomerCommand command, CancellationToken cancellationToken)
        {
            Customer = Customer with
            {
                CustomerId = Guid.NewGuid(),
                DisplayName = command.DisplayName,
                CreditLimitAmount = command.CreditLimitAmount,
                CurrentBalance = 0
            };

            return Task.FromResult(Customer);
        }

        public Task<CustomerRecord?> UpdateCustomerAsync(Guid organizationId, Guid customerId, UpdateCustomerCommand command, CancellationToken cancellationToken)
        {
            if (customerId != Customer.CustomerId)
            {
                return Task.FromResult<CustomerRecord?>(null);
            }

            Customer = Customer with
            {
                DisplayName = command.DisplayName,
                CreditLimitAmount = command.CreditLimitAmount,
                IsActive = command.IsActive,
                Notes = command.Notes
            };

            return Task.FromResult<CustomerRecord?>(Customer);
        }

        public Task<CustomerAccountMovementListResult> ListAccountMovementsAsync(Guid organizationId, Guid customerId, int page, int pageSize, CancellationToken cancellationToken)
            => Task.FromResult(new CustomerAccountMovementListResult([], page, pageSize, 0));

        public Task<CustomerAccountStatementRecord> GetAccountStatementAsync(Guid organizationId, Guid customerId, int limit, CancellationToken cancellationToken)
            => Task.FromResult(new CustomerAccountStatementRecord(Customer, []));

        public Task<RecordCustomerPaymentResult> RecordPaymentAsync(Guid organizationId, Guid customerId, Guid actorUserId, RecordCustomerPaymentCommand command, CancellationToken cancellationToken)
        {
            Customer = Customer with { CurrentBalance = Customer.CurrentBalance - command.Amount };
            var movement = new CustomerAccountMovementRecord(
                Guid.NewGuid(),
                customerId,
                null,
                command.CashSessionId,
                "payment_credit",
                "Cobranza cuenta corriente",
                null,
                0,
                command.Amount,
                0,
                false,
                null,
                Customer.CurrentBalance,
                command.Notes,
                actorUserId,
                DateTimeOffset.UtcNow);

            return Task.FromResult(new RecordCustomerPaymentResult(Customer, movement));
        }

        public Task<RecordCustomerCreditNoteResult> RecordCreditNoteAsync(Guid organizationId, Guid customerId, Guid actorUserId, RecordCustomerCreditNoteCommand command, CancellationToken cancellationToken)
        {
            Customer = Customer with { CurrentBalance = Customer.CurrentBalance - command.Amount };
            var movement = new CustomerAccountMovementRecord(
                Guid.NewGuid(),
                customerId,
                null,
                null,
                "credit_note",
                command.Concept,
                command.ReferenceDocument,
                0,
                command.Amount,
                0,
                false,
                null,
                Customer.CurrentBalance,
                command.Notes,
                actorUserId,
                DateTimeOffset.UtcNow);

            return Task.FromResult(new RecordCustomerCreditNoteResult(Customer, movement));
        }
    }

    private sealed class FakeAuditLogRepository : IAuditLogRepository
    {
        public List<(string EntityName, string Action)> Entries { get; } = [];

        public Task WriteAsync(Guid organizationId, Guid? actorUserId, string entityName, string entityId, string action, IReadOnlyDictionary<string, object?> metadata, CancellationToken cancellationToken)
        {
            Entries.Add((entityName, action));
            return Task.CompletedTask;
        }
    }
}
