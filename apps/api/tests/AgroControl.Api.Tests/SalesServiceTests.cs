using AgroControl.Application.Auth;
using AgroControl.Application.Sales;

namespace AgroControl.Api.Tests;

public sealed class SalesServiceTests
{
    [Fact]
    public async Task CreateCashSaleAsync_WithCashierRole_WritesAudit()
    {
        var repository = new FakeSalesRepository();
        var auditRepository = new FakeAuditLogRepository();
        var service = new SalesService(repository, auditRepository);

        var sale = await service.CreateCashSaleAsync(
            new IdentityContext(Guid.NewGuid(), repository.OrganizationId, null, "cashier@demo.local", ["cashier"]),
            new CreateCashSaleCommand(
                repository.Sale.CashSessionId!.Value,
                [new CreateSaleItemCommand(repository.Product.ProductId, 2)],
                "Venta mostrador"),
            CancellationToken.None);

        _ = Assert.Single(sale.Items);
        Assert.Contains(auditRepository.Entries, entry => entry.Action == "cash_sale_created");
    }

    [Fact]
    public async Task CreateCashSaleAsync_WithViewerRole_ThrowsAuthorizationException()
    {
        var service = new SalesService(new FakeSalesRepository(), new FakeAuditLogRepository());

        await Assert.ThrowsAsync<AuthorizationException>(() =>
            service.CreateCashSaleAsync(
                new IdentityContext(Guid.NewGuid(), Guid.NewGuid(), null, "viewer@demo.local", ["viewer"]),
                new CreateCashSaleCommand(Guid.NewGuid(), [new CreateSaleItemCommand(Guid.NewGuid(), 1)], null),
                CancellationToken.None));
    }

    [Fact]
    public async Task ReverseCashSaleAsync_WithAdministratorRole_WritesAudit()
    {
        var repository = new FakeSalesRepository();
        var auditRepository = new FakeAuditLogRepository();
        var service = new SalesService(repository, auditRepository);

        var sale = await service.ReverseCashSaleAsync(
            CreateAdministratorIdentity(repository.OrganizationId),
            repository.Sale.SaleId,
            new ReverseCashSaleCommand(repository.Sale.CashSessionId!.Value, "Reversa operativa"),
            CancellationToken.None);

        Assert.Equal("reversed", sale.Status);
        Assert.Contains(auditRepository.Entries, entry => entry.Action == "cash_sale_reversed");
    }

    [Fact]
    public async Task ReturnSaleAsync_WithAdministratorRole_WritesAudit()
    {
        var repository = new FakeSalesRepository();
        var auditRepository = new FakeAuditLogRepository();
        var service = new SalesService(repository, auditRepository);

        var sale = await service.ReturnSaleAsync(
            CreateAdministratorIdentity(repository.OrganizationId),
            repository.Sale.SaleId,
            new ReturnSaleCommand(
                repository.Sale.CashSessionId,
                [new ReturnSaleItemCommand(repository.Sale.Items[0].SaleItemId, 0.5m)],
                "Devolucion parcial"),
            CancellationToken.None);

        Assert.Equal("partially_returned", sale.Status);
        Assert.Contains(auditRepository.Entries, entry => entry.Action == "sale_items_returned");
    }

    [Fact]
    public async Task ReturnSaleAsync_WhenItemsAreFullyReturned_MarksSaleAsFullyReturned()
    {
        var repository = new FakeSalesRepository();
        var auditRepository = new FakeAuditLogRepository();
        var service = new SalesService(repository, auditRepository);

        var sale = await service.ReturnSaleAsync(
            CreateAdministratorIdentity(repository.OrganizationId),
            repository.Sale.SaleId,
            new ReturnSaleCommand(
                repository.Sale.CashSessionId,
                [new ReturnSaleItemCommand(repository.Sale.Items[0].SaleItemId, repository.Sale.Items[0].Quantity)],
                "Devolucion total por items"),
            CancellationToken.None);

        Assert.Equal("fully_returned", sale.Status);
        Assert.Equal(0, sale.Items[0].AvailableToReturnQuantity);
    }

    [Fact]
    public async Task CreateAccountSaleAsync_WithSellerRole_WritesAudit()
    {
        var repository = new FakeSalesRepository();
        var auditRepository = new FakeAuditLogRepository();
        var service = new SalesService(repository, auditRepository);

        var sale = await service.CreateAccountSaleAsync(
            new IdentityContext(Guid.NewGuid(), repository.OrganizationId, null, "seller@demo.local", ["seller"]),
            new CreateAccountSaleCommand(
                repository.CustomerId,
                [new CreateSaleItemCommand(repository.Product.ProductId, 1)],
                DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(15)),
                "Venta a cuenta"),
            CancellationToken.None);

        Assert.Equal("pos_account", sale.SaleChannel);
        Assert.Equal(repository.CustomerId, sale.CustomerId);
        Assert.Contains(auditRepository.Entries, entry => entry.Action == "account_sale_created");
    }

    [Fact]
    public async Task CreateCheckoutSaleAsync_WithMixedPayments_WritesAudit()
    {
        var repository = new FakeSalesRepository();
        var auditRepository = new FakeAuditLogRepository();
        var service = new SalesService(repository, auditRepository);

        var sale = await service.CreateCheckoutSaleAsync(
            new IdentityContext(Guid.NewGuid(), repository.OrganizationId, null, "seller@demo.local", ["seller"]),
            new CreateCheckoutSaleCommand(
                repository.Sale.CashSessionId,
                repository.CustomerId,
                [new CreateSaleItemCommand(repository.Product.ProductId, 1)],
                [
                    new CreateSalePaymentCommand("cash", 6000, null, null),
                    new CreateSalePaymentCommand("account", 6000, null, null)
                ],
                DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(30)),
                "Checkout mixto"),
            CancellationToken.None);

        Assert.Equal("pos_checkout", sale.SaleChannel);
        Assert.Equal(6000, sale.PaidAmount);
        Assert.Equal(6000, sale.AccountBalanceAmount);
        Assert.Equal(2, sale.Payments.Count);
        Assert.Contains(auditRepository.Entries, entry => entry.Action == "checkout_sale_created");
    }

    [Fact]
    public async Task ListPosProductsAsync_NormalizesPaginationAndSearch()
    {
        var repository = new FakeSalesRepository();
        var service = new SalesService(repository, new FakeAuditLogRepository());

        _ = await service.ListPosProductsAsync(
            CreateAdministratorIdentity(repository.OrganizationId),
            new PosProductListQuery("  balanceado  ", 0, 1000),
            CancellationToken.None);

        Assert.NotNull(repository.LastQuery);
        Assert.Equal("balanceado", repository.LastQuery!.Search);
        Assert.Equal(1, repository.LastQuery.Page);
        Assert.Equal(100, repository.LastQuery.PageSize);
    }

    private static IdentityContext CreateAdministratorIdentity(Guid organizationId)
        => new(Guid.NewGuid(), organizationId, null, "admin@demo.local", ["administrator"]);

    private sealed class FakeSalesRepository : ISalesRepository
    {
        public Guid OrganizationId { get; } = Guid.NewGuid();

        public PosProductRecord Product { get; } = new(
            Guid.NewGuid(),
            "Balanceado Premium",
            "BAL-001",
            null,
            null,
            true,
            "kg",
            20,
            12000,
            "ARS");

        public SaleRecord Sale { get; private set; }

        public PosProductListQuery? LastQuery { get; private set; }

        public Guid CustomerId { get; } = Guid.NewGuid();

        public FakeSalesRepository()
        {
            Sale = new SaleRecord(
                Guid.NewGuid(),
                1,
                OrganizationId,
                Guid.NewGuid(),
                null,
                Guid.NewGuid(),
                "pos_cash",
                "confirmed",
                "Consumidor final",
                12000,
                0,
            12000,
            12000,
            0,
            0,
            null,
            "ARS",
                null,
                null,
                null,
                "Venta semilla",
                null,
                DateTimeOffset.UtcNow,
                [
                    new SaleItemRecord(
                        Guid.NewGuid(),
                        Product.ProductId,
                        Product.Name,
                        Product.UnitSymbol,
                        1,
                        0,
                        1,
                        Product.SaleAmount,
                        Product.SaleAmount)
                ],
                [
                    new SalePaymentRecord(Guid.NewGuid(), "cash", 12000, null, null, DateTimeOffset.UtcNow)
                ],
                []);
        }

        public Task<PosProductListResult> ListPosProductsAsync(Guid organizationId, PosProductListQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult(new PosProductListResult([Product], query.Page, query.PageSize, 1));
        }

        public Task<SaleListResult> ListSalesAsync(Guid organizationId, SaleListQuery query, CancellationToken cancellationToken)
            => Task.FromResult(new SaleListResult([], query.Page, query.PageSize, 0));

        public Task<SaleRecord?> GetSaleAsync(Guid organizationId, Guid saleId, CancellationToken cancellationToken)
            => Task.FromResult<SaleRecord?>(saleId == Sale.SaleId ? Sale : null);

        public Task<SaleRecord> CreateCashSaleAsync(Guid organizationId, Guid actorUserId, CreateCashSaleCommand command, CancellationToken cancellationToken)
        {
            Sale = new SaleRecord(
                Guid.NewGuid(),
                1,
                organizationId,
                command.CashSessionId,
                null,
                actorUserId,
                "pos_cash",
                "confirmed",
                "Consumidor final",
                Product.SaleAmount * command.Items[0].Quantity,
                0,
                Product.SaleAmount * command.Items[0].Quantity,
                Product.SaleAmount * command.Items[0].Quantity,
                0,
                0,
                null,
                "ARS",
                null,
                null,
                null,
                command.Notes,
                null,
                DateTimeOffset.UtcNow,
                [
                    new SaleItemRecord(
                        Guid.NewGuid(),
                        Product.ProductId,
                        Product.Name,
                        Product.UnitSymbol,
                        command.Items[0].Quantity,
                        0,
                        command.Items[0].Quantity,
                        Product.SaleAmount,
                        Product.SaleAmount * command.Items[0].Quantity)
                ],
                [
                    new SalePaymentRecord(Guid.NewGuid(), "cash", Product.SaleAmount * command.Items[0].Quantity, null, null, DateTimeOffset.UtcNow)
                ],
                []);

            return Task.FromResult(Sale);
        }

        public Task<SaleRecord> CreateAccountSaleAsync(Guid organizationId, Guid actorUserId, CreateAccountSaleCommand command, CancellationToken cancellationToken)
        {
            Sale = new SaleRecord(
                Guid.NewGuid(),
                2,
                organizationId,
                null,
                command.CustomerId,
                actorUserId,
                "pos_account",
                "confirmed",
                "Cliente cuenta corriente",
                Product.SaleAmount * command.Items[0].Quantity,
                0,
                Product.SaleAmount * command.Items[0].Quantity,
                0,
                Product.SaleAmount * command.Items[0].Quantity,
                0,
                command.DueDate,
                "ARS",
                null,
                null,
                null,
                command.Notes,
                null,
                DateTimeOffset.UtcNow,
                [
                    new SaleItemRecord(
                        Guid.NewGuid(),
                        Product.ProductId,
                        Product.Name,
                        Product.UnitSymbol,
                        command.Items[0].Quantity,
                        0,
                        command.Items[0].Quantity,
                        Product.SaleAmount,
                        Product.SaleAmount * command.Items[0].Quantity)
                ],
                [
                    new SalePaymentRecord(Guid.NewGuid(), "account", Product.SaleAmount * command.Items[0].Quantity, null, null, DateTimeOffset.UtcNow)
                ],
                []);

            return Task.FromResult(Sale);
        }

        public Task<SaleRecord> CreateCheckoutSaleAsync(Guid organizationId, Guid actorUserId, CreateCheckoutSaleCommand command, CancellationToken cancellationToken)
        {
            var total = Product.SaleAmount * command.Items[0].Quantity;
            var paidAmount = command.Payments.Where(payment => payment.PaymentMethod != "account").Sum(payment => payment.Amount);
            var accountAmount = command.Payments.Where(payment => payment.PaymentMethod == "account").Sum(payment => payment.Amount);

            Sale = new SaleRecord(
                Guid.NewGuid(),
                3,
                organizationId,
                command.CashSessionId,
                command.CustomerId,
                actorUserId,
                "pos_checkout",
                "confirmed",
                command.CustomerId.HasValue ? "Cliente checkout mixto" : "Consumidor final",
                total,
                0,
                total,
                paidAmount,
                accountAmount,
                0,
                command.DueDate,
                "ARS",
                null,
                null,
                null,
                command.Notes,
                null,
                DateTimeOffset.UtcNow,
                [
                    new SaleItemRecord(
                        Guid.NewGuid(),
                        Product.ProductId,
                        Product.Name,
                        Product.UnitSymbol,
                        command.Items[0].Quantity,
                        0,
                        command.Items[0].Quantity,
                        Product.SaleAmount,
                        Product.SaleAmount * command.Items[0].Quantity)
                ],
                command.Payments.Select(payment => new SalePaymentRecord(
                    Guid.NewGuid(),
                    payment.PaymentMethod,
                    payment.Amount,
                    payment.Reference,
                    payment.ProviderName,
                    DateTimeOffset.UtcNow)).ToArray(),
                []);

            return Task.FromResult(Sale);
        }

        public Task<SaleRecord> ReverseCashSaleAsync(Guid organizationId, Guid actorUserId, Guid saleId, ReverseCashSaleCommand command, CancellationToken cancellationToken)
        {
            Sale = Sale with
            {
                Status = "reversed",
                ReversalCashSessionId = command.CashSessionId,
                ReversedByUserId = actorUserId,
                ReversedAt = DateTimeOffset.UtcNow,
                ReversalNotes = command.ReversalNotes
            };

            return Task.FromResult(Sale);
        }

        public Task<SaleRecord> ReturnSaleAsync(Guid organizationId, Guid actorUserId, Guid saleId, ReturnSaleCommand command, CancellationToken cancellationToken)
        {
            var currentItem = Sale.Items[0];
            var returnedQuantity = currentItem.ReturnedQuantity + command.Items[0].Quantity;
            var availableToReturnQuantity = currentItem.Quantity - returnedQuantity;
            var returnTotal = currentItem.UnitPrice * command.Items[0].Quantity;
            var saleReturn = new SaleReturnRecord(
                Guid.NewGuid(),
                saleId,
                command.CashSessionId,
                Sale.CustomerId is null ? null : Guid.NewGuid(),
                actorUserId,
                returnTotal,
                Sale.PaidAmount > 0 ? returnTotal : 0,
                Sale.AccountBalanceAmount > 0 ? 0 : 0,
                command.Notes,
                DateTimeOffset.UtcNow,
                [
                    new SaleReturnItemRecord(
                        Guid.NewGuid(),
                        currentItem.SaleItemId,
                        currentItem.ProductId,
                        command.Items[0].Quantity,
                        currentItem.UnitPrice,
                        returnTotal,
                        DateTimeOffset.UtcNow)
                ]);

            Sale = Sale with
            {
                Status = availableToReturnQuantity == 0 ? "fully_returned" : "partially_returned",
                Items =
                [
                    currentItem with
                    {
                        ReturnedQuantity = returnedQuantity,
                        AvailableToReturnQuantity = availableToReturnQuantity
                    }
                ],
                Returns = [saleReturn, .. Sale.Returns]
            };

            return Task.FromResult(Sale);
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
