using AgroControl.Application.Auth;

namespace AgroControl.Application.Catalog;

public interface ICatalogRepository
{
    Task<ProductListResult> ListProductsAsync(Guid organizationId, ProductListQuery query, CancellationToken cancellationToken);

    Task<ProductRecord?> GetProductAsync(Guid organizationId, Guid productId, CancellationToken cancellationToken);

    Task<ProductRecord> CreateProductAsync(Guid organizationId, CreateProductCommand command, CancellationToken cancellationToken);

    Task<ProductRecord?> UpdateProductAsync(Guid organizationId, Guid productId, UpdateProductCommand command, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductCategoryRecord>> ListCategoriesAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<ProductCategoryRecord> CreateCategoryAsync(Guid organizationId, CreateCategoryCommand command, CancellationToken cancellationToken);

    Task<ProductCategoryRecord?> UpdateCategoryAsync(Guid organizationId, Guid categoryId, UpdateCategoryCommand command, CancellationToken cancellationToken);

    Task<IReadOnlyList<BrandRecord>> ListBrandsAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<BrandRecord> CreateBrandAsync(Guid organizationId, CreateBrandCommand command, CancellationToken cancellationToken);

    Task<BrandRecord?> UpdateBrandAsync(Guid organizationId, Guid brandId, UpdateBrandCommand command, CancellationToken cancellationToken);

    Task<IReadOnlyList<UnitOfMeasureRecord>> ListUnitsAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PriceListRecord>> ListPriceListsAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<PriceListRecord> CreatePriceListAsync(Guid organizationId, CreatePriceListCommand command, CancellationToken cancellationToken);

    Task<PriceListRecord?> UpdatePriceListAsync(Guid organizationId, Guid priceListId, UpdatePriceListCommand command, CancellationToken cancellationToken);
}
