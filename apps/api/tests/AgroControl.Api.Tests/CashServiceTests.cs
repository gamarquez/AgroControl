using AgroControl.Application.Auth;
using AgroControl.Application.Cash;

namespace AgroControl.Api.Tests;

public sealed class CashServiceTests
{
    [Fact]
    public async Task OpenSessionAsync_WithCashierRole_WritesAudit()
    {
        var repository = new FakeCashRepository();
        var auditRepository = new FakeAuditLogRepository();
        var service = new CashService(repository, auditRepository);

        var session = await service.OpenSessionAsync(
            new IdentityContext(Guid.NewGuid(), repository.OrganizationId, null, "cashier@demo.local", ["cashier"]),
            new OpenCashSessionCommand("main", 15000, "Fondo inicial"),
            CancellationToken.None);

        Assert.Equal(15000, session.OpeningAmount);
        Assert.Contains(auditRepository.Entries, entry => entry.Action == "cash_session_opened");
    }

    [Fact]
    public async Task CreateMovementAsync_WithSellerRole_ThrowsAuthorizationException()
    {
        var service = new CashService(new FakeCashRepository(), new FakeAuditLogRepository());

        await Assert.ThrowsAsync<AuthorizationException>(() =>
            service.CreateMovementAsync(
                new IdentityContext(Guid.NewGuid(), Guid.NewGuid(), null, "seller@demo.local", ["seller"]),
                new CreateCashMovementCommand(Guid.NewGuid(), "cash_out", "expense", "Compra de limpieza", "cash", 500, null, null),
                CancellationToken.None));
    }

    [Fact]
    public async Task ListMovementsAsync_NormalizesPagination()
    {
        var repository = new FakeCashRepository();
        var service = new CashService(repository, new FakeAuditLogRepository());

        _ = await service.ListMovementsAsync(
            CreateAdministratorIdentity(repository.OrganizationId),
            0,
            500,
            CancellationToken.None);

        Assert.Equal(1, repository.LastPage);
        Assert.Equal(100, repository.LastPageSize);
    }

    [Fact]
    public async Task CloseSessionAsync_WritesAudit()
    {
        var repository = new FakeCashRepository();
        var auditRepository = new FakeAuditLogRepository();
        var service = new CashService(repository, auditRepository);

        var session = await service.CloseSessionAsync(
            CreateAdministratorIdentity(repository.OrganizationId),
            repository.Session.CashSessionId,
            new CloseCashSessionCommand(12000, "Cierre de turno"),
            CancellationToken.None);

        Assert.Equal("closed", session.Status);
        Assert.Contains(auditRepository.Entries, entry => entry.Action == "cash_session_closed");
    }

    private static IdentityContext CreateAdministratorIdentity(Guid organizationId)
        => new(Guid.NewGuid(), organizationId, null, "admin@demo.local", ["administrator"]);

    private sealed class FakeCashRepository : ICashRepository
    {
        public Guid OrganizationId { get; } = Guid.NewGuid();

        public CashRegisterRecord Register { get; }

        public CashSessionRecord Session { get; private set; }

        public int LastPage { get; private set; }

        public int LastPageSize { get; private set; }

        public FakeCashRepository()
        {
            Register = new CashRegisterRecord(Guid.NewGuid(), OrganizationId, "Caja principal", "main", true);
            Session = new CashSessionRecord(
                Guid.NewGuid(),
                OrganizationId,
                Register,
                Guid.NewGuid(),
                null,
                10000,
                null,
                null,
                10000,
                "open",
                null,
                null,
                DateTimeOffset.UtcNow.AddHours(-2),
                null);
        }

        public Task<CashOverviewRecord> GetOverviewAsync(Guid organizationId, CancellationToken cancellationToken)
            => Task.FromResult(new CashOverviewRecord(Register, Session, []));

        public Task<CashMovementListResult> ListMovementsAsync(Guid organizationId, int page, int pageSize, CancellationToken cancellationToken)
        {
            LastPage = page;
            LastPageSize = pageSize;
            return Task.FromResult(new CashMovementListResult([], page, pageSize, 0));
        }

        public Task<CashSessionRecord?> GetSessionAsync(Guid organizationId, Guid cashSessionId, CancellationToken cancellationToken)
            => Task.FromResult<CashSessionRecord?>(cashSessionId == Session.CashSessionId ? Session : null);

        public Task<CashSessionRecord> OpenSessionAsync(Guid organizationId, Guid actorUserId, OpenCashSessionCommand command, CancellationToken cancellationToken)
        {
            Session = new CashSessionRecord(
                Guid.NewGuid(),
                organizationId,
                Register,
                actorUserId,
                null,
                command.OpeningAmount,
                null,
                null,
                command.OpeningAmount,
                "open",
                command.OpeningNotes,
                null,
                DateTimeOffset.UtcNow,
                null);

            return Task.FromResult(Session);
        }

        public Task<CashMovementRecord> CreateMovementAsync(Guid organizationId, Guid actorUserId, CreateCashMovementCommand command, CancellationToken cancellationToken)
            => Task.FromResult(new CashMovementRecord(
                Guid.NewGuid(),
                command.CashSessionId,
                command.MovementType,
                command.CategoryCode,
                command.Concept,
                command.PaymentMethod,
                command.Amount,
                command.MovementType == "cash_out" ? -command.Amount : command.Amount,
                Session.CurrentBalance + (command.MovementType == "cash_out" ? -command.Amount : command.Amount),
                command.ReferenceDocument,
                command.Notes,
                actorUserId,
                DateTimeOffset.UtcNow));

        public Task<CashSessionRecord> CloseSessionAsync(Guid organizationId, Guid actorUserId, Guid cashSessionId, CloseCashSessionCommand command, CancellationToken cancellationToken)
        {
            Session = Session with
            {
                ClosedByUserId = actorUserId,
                ClosingAmount = command.ClosingAmount,
                DifferenceAmount = command.ClosingAmount - Session.CurrentBalance,
                ClosingNotes = command.ClosingNotes,
                ClosedAt = DateTimeOffset.UtcNow,
                Status = "closed"
            };

            return Task.FromResult(Session);
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
