using AgroControl.Application.Auth;
using AgroControl.Application.Catalog;

namespace AgroControl.Api.Tests;

public sealed class CatalogServiceTests
{
    [Fact]
    public async Task CreateProductAsync_WithAdministratorRole_CreatesProductAndWritesAudit()
    {
        var repository = new FakeCatalogRepository();
        var auditRepository = new FakeAuditLogRepository();
        var service = new CatalogService(repository, auditRepository);
        var identity = CreateAdministratorIdentity();

        var product = await service.CreateProductAsync(
            identity,
            new CreateProductCommand(
                null,
                null,
                repository.Unit.UnitId,
                "Balanceado Premium",
                "Uso general",
                "BAL-001",
                null,
                null,
                false,
                "bolsa",
                100,
                25,
                125,
                "ars",
                repository.PriceList.PriceListId),
            CancellationToken.None);

        Assert.Equal("Balanceado Premium", product.Name);
        Assert.Contains(auditRepository.Entries, entry => entry.Action == "product_created");
    }

    [Fact]
    public async Task CreateProductAsync_WithSellerRole_ThrowsAuthorizationException()
    {
        var service = new CatalogService(new FakeCatalogRepository(), new FakeAuditLogRepository());

        await Assert.ThrowsAsync<AuthorizationException>(() =>
            service.CreateProductAsync(
                new IdentityContext(Guid.NewGuid(), Guid.NewGuid(), null, "seller@demo.local", ["seller"]),
                new CreateProductCommand(null, null, Guid.NewGuid(), "Producto", null, "PROD-1", null, null, false, null, 1, null, 2, "ARS", null),
                CancellationToken.None));
    }

    [Fact]
    public async Task ListProductsAsync_NormalizesPaginationAndSearch()
    {
        var repository = new FakeCatalogRepository();
        var service = new CatalogService(repository, new FakeAuditLogRepository());

        _ = await service.ListProductsAsync(
            CreateAdministratorIdentity(),
            new ProductListQuery("  alfa  ", null, null, null, 0, 200),
            CancellationToken.None);

        Assert.NotNull(repository.LastListQuery);
        Assert.Equal("alfa", repository.LastListQuery!.Search);
        Assert.Equal(1, repository.LastListQuery.Page);
        Assert.Equal(100, repository.LastListQuery.PageSize);
    }

    [Fact]
    public async Task UpdateProductAsync_WhenProductBecomesInactive_WritesDeactivationAudit()
    {
        var repository = new FakeCatalogRepository();
        var auditRepository = new FakeAuditLogRepository();
        var service = new CatalogService(repository, auditRepository);

        var product = await service.UpdateProductAsync(
            CreateAdministratorIdentity(),
            repository.Product.ProductId,
            new UpdateProductCommand(
                null,
                null,
                repository.Unit.UnitId,
                repository.Product.Name,
                null,
                repository.Product.InternalCode,
                null,
                null,
                false,
                false,
                null,
                50,
                20,
                60,
                "ARS",
                repository.PriceList.PriceListId),
            CancellationToken.None);

        Assert.False(product.IsActive);
        Assert.Contains(auditRepository.Entries, entry => entry.Action == "product_deactivated");
        Assert.Contains(auditRepository.Entries, entry => entry.Action == "price_updated");
    }

    [Fact]
    public async Task UpdatePriceListAsync_WhenNotFound_ThrowsNotFoundException()
    {
        var service = new CatalogService(new FakeCatalogRepository(), new FakeAuditLogRepository());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdatePriceListAsync(
                CreateAdministratorIdentity(),
                Guid.NewGuid(),
                new UpdatePriceListCommand("Mayorista", "wholesale", false, true),
                CancellationToken.None));
    }

    private static IdentityContext CreateAdministratorIdentity()
        => new(Guid.NewGuid(), Guid.NewGuid(), null, "admin@demo.local", ["administrator"]);

    private sealed class FakeCatalogRepository : ICatalogRepository
    {
        public UnitOfMeasureRecord Unit { get; } = new(Guid.NewGuid(), Guid.NewGuid(), "Unidad", "unit", "u", false, true);

        public PriceListRecord PriceList { get; } = new(Guid.NewGuid(), Guid.NewGuid(), "Lista estandar", "standard", true, true);

        public ProductRecord Product { get; }

        public ProductListQuery? LastListQuery { get; private set; }

        public FakeCatalogRepository()
        {
            Product = new ProductRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Producto base",
                null,
                "PROD-001",
                null,
                null,
                true,
                false,
                null,
                null,
                null,
                Unit,
                new ProductPriceRecord(PriceList.PriceListId, PriceList.Name, PriceList.Code, 10, 10, 11, "ARS", DateTimeOffset.UtcNow),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);
        }

        public Task<ProductListResult> ListProductsAsync(Guid organizationId, ProductListQuery query, CancellationToken cancellationToken)
        {
            LastListQuery = query;
            return Task.FromResult(new ProductListResult([Product], query.Page, query.PageSize, 1));
        }

        public Task<ProductRecord?> GetProductAsync(Guid organizationId, Guid productId, CancellationToken cancellationToken)
            => Task.FromResult(productId == Product.ProductId ? Product : null);

        public Task<ProductRecord> CreateProductAsync(Guid organizationId, CreateProductCommand command, CancellationToken cancellationToken)
            => Task.FromResult(Product with
            {
                Name = command.Name,
                InternalCode = command.InternalCode,
                CurrentPrice = Product.CurrentPrice with
                {
                    CostAmount = command.CostAmount,
                    MarginPercent = command.MarginPercent,
                    SaleAmount = command.SaleAmount,
                    CurrencyCode = command.CurrencyCode
                }
            });

        public Task<ProductRecord?> UpdateProductAsync(Guid organizationId, Guid productId, UpdateProductCommand command, CancellationToken cancellationToken)
            => Task.FromResult(productId == Product.ProductId
                ? Product with
                {
                    Name = command.Name,
                    IsActive = command.IsActive,
                    CurrentPrice = Product.CurrentPrice with
                    {
                        CostAmount = command.CostAmount,
                        MarginPercent = command.MarginPercent,
                        SaleAmount = command.SaleAmount,
                        CurrencyCode = command.CurrencyCode
                    }
                }
                : null);

        public Task<IReadOnlyList<ProductCategoryRecord>> ListCategoriesAsync(Guid organizationId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ProductCategoryRecord>>([]);

        public Task<ProductCategoryRecord> CreateCategoryAsync(Guid organizationId, CreateCategoryCommand command, CancellationToken cancellationToken)
            => Task.FromResult(new ProductCategoryRecord(Guid.NewGuid(), organizationId, command.Name, command.Description, true));

        public Task<ProductCategoryRecord?> UpdateCategoryAsync(Guid organizationId, Guid categoryId, UpdateCategoryCommand command, CancellationToken cancellationToken)
            => Task.FromResult<ProductCategoryRecord?>(new ProductCategoryRecord(categoryId, organizationId, command.Name, command.Description, command.IsActive));

        public Task<IReadOnlyList<BrandRecord>> ListBrandsAsync(Guid organizationId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<BrandRecord>>([]);

        public Task<BrandRecord> CreateBrandAsync(Guid organizationId, CreateBrandCommand command, CancellationToken cancellationToken)
            => Task.FromResult(new BrandRecord(Guid.NewGuid(), organizationId, command.Name, command.Description, true));

        public Task<BrandRecord?> UpdateBrandAsync(Guid organizationId, Guid brandId, UpdateBrandCommand command, CancellationToken cancellationToken)
            => Task.FromResult<BrandRecord?>(new BrandRecord(brandId, organizationId, command.Name, command.Description, command.IsActive));

        public Task<IReadOnlyList<UnitOfMeasureRecord>> ListUnitsAsync(Guid organizationId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<UnitOfMeasureRecord>>([Unit]);

        public Task<IReadOnlyList<PriceListRecord>> ListPriceListsAsync(Guid organizationId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PriceListRecord>>([PriceList]);

        public Task<PriceListRecord> CreatePriceListAsync(Guid organizationId, CreatePriceListCommand command, CancellationToken cancellationToken)
            => Task.FromResult(new PriceListRecord(Guid.NewGuid(), organizationId, command.Name, command.Code, command.IsDefault, command.IsActive));

        public Task<PriceListRecord?> UpdatePriceListAsync(Guid organizationId, Guid priceListId, UpdatePriceListCommand command, CancellationToken cancellationToken)
            => Task.FromResult<PriceListRecord?>(null);
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
