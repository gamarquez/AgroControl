using AgroControl.Application.Auth;

namespace AgroControl.Application.Catalog;

public interface ICatalogService
{
    Task<ProductListResult> ListProductsAsync(IdentityContext identity, ProductListQuery query, CancellationToken cancellationToken);

    Task<ProductRecord> GetProductAsync(IdentityContext identity, Guid productId, CancellationToken cancellationToken);

    Task<ProductRecord> CreateProductAsync(IdentityContext identity, CreateProductCommand command, CancellationToken cancellationToken);

    Task<ProductRecord> UpdateProductAsync(IdentityContext identity, Guid productId, UpdateProductCommand command, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductCategoryRecord>> ListCategoriesAsync(IdentityContext identity, CancellationToken cancellationToken);

    Task<ProductCategoryRecord> CreateCategoryAsync(IdentityContext identity, CreateCategoryCommand command, CancellationToken cancellationToken);

    Task<ProductCategoryRecord> UpdateCategoryAsync(IdentityContext identity, Guid categoryId, UpdateCategoryCommand command, CancellationToken cancellationToken);

    Task<IReadOnlyList<BrandRecord>> ListBrandsAsync(IdentityContext identity, CancellationToken cancellationToken);

    Task<BrandRecord> CreateBrandAsync(IdentityContext identity, CreateBrandCommand command, CancellationToken cancellationToken);

    Task<BrandRecord> UpdateBrandAsync(IdentityContext identity, Guid brandId, UpdateBrandCommand command, CancellationToken cancellationToken);

    Task<IReadOnlyList<UnitOfMeasureRecord>> ListUnitsAsync(IdentityContext identity, CancellationToken cancellationToken);

    Task<IReadOnlyList<PriceListRecord>> ListPriceListsAsync(IdentityContext identity, CancellationToken cancellationToken);

    Task<PriceListRecord> CreatePriceListAsync(IdentityContext identity, CreatePriceListCommand command, CancellationToken cancellationToken);

    Task<PriceListRecord> UpdatePriceListAsync(IdentityContext identity, Guid priceListId, UpdatePriceListCommand command, CancellationToken cancellationToken);
}
