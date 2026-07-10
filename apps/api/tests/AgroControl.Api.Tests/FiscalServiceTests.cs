using AgroControl.Application.Auth;
using AgroControl.Application.Fiscal;
using AgroControl.Application.Sales;

namespace AgroControl.Api.Tests;

public sealed class FiscalServiceTests
{
    [Fact]
    public async Task CreateFiscalDocumentAsync_WithAdministratorRole_QueuesDocumentAndWritesAudit()
    {
        var repository = new FakeFiscalRepository();
        var salesRepository = new FakeSalesRepository(repository.OrganizationId);
        var auditRepository = new FakeAuditLogRepository();
        var service = new FiscalService(repository, salesRepository, new FakeArcaFiscalService(), auditRepository);

        var document = await service.CreateFiscalDocumentAsync(
            CreateAdministratorIdentity(repository.OrganizationId),
            new CreateFiscalDocumentCommand(salesRepository.Sale.SaleId, "invoice"),
            CancellationToken.None);

        Assert.Equal("pending", document.Status);
        Assert.Contains(auditRepository.Entries, entry => entry.Action == "fiscal_document_queued");
    }

    [Fact]
    public async Task CreateFiscalDocumentAsync_WithViewerRole_ThrowsAuthorizationException()
    {
        var repository = new FakeFiscalRepository();
        var salesRepository = new FakeSalesRepository(repository.OrganizationId);
        var service = new FiscalService(repository, salesRepository, new FakeArcaFiscalService(), new FakeAuditLogRepository());

        await Assert.ThrowsAsync<AuthorizationException>(() =>
            service.CreateFiscalDocumentAsync(
                new IdentityContext(Guid.NewGuid(), repository.OrganizationId, null, "viewer@demo.local", ["viewer"]),
                new CreateFiscalDocumentCommand(salesRepository.Sale.SaleId, "invoice"),
                CancellationToken.None));
    }

    [Fact]
    public async Task ProbeAsync_WritesRequestLogAndAudit()
    {
        var repository = new FakeFiscalRepository();
        var salesRepository = new FakeSalesRepository(repository.OrganizationId);
        var auditRepository = new FakeAuditLogRepository();
        var service = new FiscalService(repository, salesRepository, new FakeArcaFiscalService(), auditRepository);

        var probe = await service.ProbeAsync(
            CreateAdministratorIdentity(repository.OrganizationId),
            CancellationToken.None);

        Assert.True(probe.IsReachable);
        Assert.Single(repository.RequestLogs);
        Assert.Contains(auditRepository.Entries, entry => entry.Action == "fiscal_probe_executed");
    }

    private static IdentityContext CreateAdministratorIdentity(Guid organizationId)
        => new(Guid.NewGuid(), organizationId, null, "admin@demo.local", ["administrator"]);

    private sealed class FakeFiscalRepository : IFiscalRepository
    {
        public Guid OrganizationId { get; } = Guid.NewGuid();

        public FiscalSettingsRecord Settings { get; private set; }

        public List<(string RequestKind, bool IsSuccess)> RequestLogs { get; } = [];

        private FiscalDocumentRecord? _document;

        public FakeFiscalRepository()
        {
            Settings = new FiscalSettingsRecord(
                OrganizationId,
                "arca_wsfev1",
                "homologation",
                "30123456789",
                1,
                "wsfe",
                "invoice_c",
                true,
                DateTimeOffset.UtcNow);
        }

        public Task<FiscalSettingsRecord?> GetSettingsAsync(Guid organizationId, CancellationToken cancellationToken)
            => Task.FromResult<FiscalSettingsRecord?>(organizationId == OrganizationId ? Settings : null);

        public Task<FiscalSettingsRecord> UpsertSettingsAsync(FiscalSettingsRecord settings, CancellationToken cancellationToken)
        {
            Settings = settings;
            return Task.FromResult(Settings);
        }

        public Task<FiscalDocumentListResult> ListDocumentsAsync(Guid organizationId, int limit, CancellationToken cancellationToken)
            => Task.FromResult(new FiscalDocumentListResult(_document is null ? [] : [_document], _document is null ? 0 : 1));

        public Task<FiscalDocumentRecord?> GetDocumentBySaleAsync(Guid organizationId, Guid saleId, string documentKind, CancellationToken cancellationToken)
            => Task.FromResult(_document is not null && _document.SaleId == saleId && _document.DocumentKind == documentKind ? _document : null);

        public Task<FiscalDocumentRecord> CreatePendingDocumentAsync(
            Guid organizationId,
            SaleDocumentSnapshot sale,
            FiscalSettingsRecord settings,
            string documentKind,
            string? lastError,
            string requestPayload,
            CancellationToken cancellationToken)
        {
            _document = new FiscalDocumentRecord(
                Guid.NewGuid(),
                sale.SaleId,
                sale.TicketNumber,
                sale.CustomerId,
                sale.CustomerName,
                sale.TotalAmount,
                sale.CurrencyCode,
                documentKind,
                settings.Provider,
                settings.Environment,
                settings.ServiceName,
                settings.TaxpayerId,
                settings.PointOfSale,
                "pending",
                null,
                null,
                null,
                null,
                lastError,
                1,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);

            return Task.FromResult(_document);
        }

        public Task WriteRequestLogAsync(
            Guid organizationId,
            Guid? fiscalDocumentId,
            string requestKind,
            string? requestPayload,
            string? responsePayload,
            bool isSuccess,
            string? errorMessage,
            CancellationToken cancellationToken)
        {
            RequestLogs.Add((requestKind, isSuccess));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSalesRepository(Guid organizationId) : ISalesRepository
    {
        public SaleRecord Sale { get; } = new(
            Guid.NewGuid(),
            120,
            organizationId,
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            "pos_checkout",
            "confirmed",
            "Consumidor final",
            15000,
            0,
            15000,
            15000,
            0,
            0,
            null,
            "ARS",
            null,
            null,
            null,
            "Venta fiscal",
            null,
            DateTimeOffset.UtcNow,
            [],
            [],
            []);

        public Task<PosProductListResult> ListPosProductsAsync(Guid organizationId, PosProductListQuery query, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SaleListResult> ListSalesAsync(Guid organizationId, SaleListQuery query, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SaleRecord?> GetSaleAsync(Guid organizationId, Guid saleId, CancellationToken cancellationToken)
            => Task.FromResult<SaleRecord?>(saleId == Sale.SaleId ? Sale : null);

        public Task<SaleRecord> CreateCashSaleAsync(Guid organizationId, Guid actorUserId, CreateCashSaleCommand command, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SaleRecord> CreateAccountSaleAsync(Guid organizationId, Guid actorUserId, CreateAccountSaleCommand command, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SaleRecord> CreateCheckoutSaleAsync(Guid organizationId, Guid actorUserId, CreateCheckoutSaleCommand command, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SaleRecord> ReverseCashSaleAsync(Guid organizationId, Guid actorUserId, Guid saleId, ReverseCashSaleCommand command, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SaleRecord> ReturnSaleAsync(Guid organizationId, Guid actorUserId, Guid saleId, ReturnSaleCommand command, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class FakeArcaFiscalService : IArcaFiscalService
    {
        public Task<FiscalProbeResult> ProbeAsync(FiscalSettingsRecord settings, CancellationToken cancellationToken)
            => Task.FromResult(new FiscalProbeResult(
                true,
                true,
                settings.Provider,
                settings.Environment,
                "Certificado cargado y FEDummy operativo.",
                null,
                "OK",
                "OK",
                "OK"));
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
