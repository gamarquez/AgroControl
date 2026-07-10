using AgroControl.Application.Auth;
using AgroControl.Application.Catalog;
using AgroControl.Contracts.Catalog;
using System.Security.Claims;

namespace AgroControl.Api.Infrastructure;

internal static class CatalogApiEndpoints
{
    public static IEndpointRouteBuilder MapCatalogApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1/catalog").RequireAuthorization();

        api.MapGet(
                "/products",
                async (
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICatalogService catalogService,
                    string? search,
                    Guid? categoryId,
                    Guid? brandId,
                    bool? isActive,
                    int? page,
                    int? pageSize,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var result = await catalogService.ListProductsAsync(
                        identity,
                        new ProductListQuery(search, categoryId, brandId, isActive, page ?? 1, pageSize ?? 20),
                        cancellationToken);

                    return TypedResults.Ok(new ProductListResponse(
                        result.Items.Select(ToProductSummaryResponse).ToArray(),
                        result.Page,
                        result.PageSize,
                        result.Total));
                })
            .WithName("ListProducts");

        api.MapGet(
                "/products/{productId:guid}",
                async (
                    Guid productId,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICatalogService catalogService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var product = await catalogService.GetProductAsync(identity, productId, cancellationToken);
                    return TypedResults.Ok(ToProductDetailResponse(product));
                })
            .WithName("GetProduct");

        api.MapPost(
                "/products",
                async (
                    CreateProductRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICatalogService catalogService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var product = await catalogService.CreateProductAsync(
                        identity,
                        new CreateProductCommand(
                            request.CategoryId,
                            request.BrandId,
                            request.BaseUnitId,
                            request.Name,
                            request.Description,
                            request.InternalCode,
                            request.Sku,
                            request.Barcode,
                            request.AllowsFraction,
                            request.SalesUnitLabel,
                            request.CostAmount,
                            request.MarginPercent,
                            request.SaleAmount,
                            request.CurrencyCode,
                            request.PriceListId),
                        cancellationToken);

                    return TypedResults.Created($"/api/v1/catalog/products/{product.ProductId}", ToProductDetailResponse(product));
                })
            .WithName("CreateProduct");

        api.MapPatch(
                "/products/{productId:guid}",
                async (
                    Guid productId,
                    UpdateProductRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICatalogService catalogService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var product = await catalogService.UpdateProductAsync(
                        identity,
                        productId,
                        new UpdateProductCommand(
                            request.CategoryId,
                            request.BrandId,
                            request.BaseUnitId,
                            request.Name,
                            request.Description,
                            request.InternalCode,
                            request.Sku,
                            request.Barcode,
                            request.IsActive,
                            request.AllowsFraction,
                            request.SalesUnitLabel,
                            request.CostAmount,
                            request.MarginPercent,
                            request.SaleAmount,
                            request.CurrencyCode,
                            request.PriceListId),
                        cancellationToken);

                    return TypedResults.Ok(ToProductDetailResponse(product));
                })
            .WithName("UpdateProduct");

        api.MapGet(
                "/categories",
                async (
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICatalogService catalogService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var categories = await catalogService.ListCategoriesAsync(identity, cancellationToken);
                    return TypedResults.Ok(new ProductCategoryListResponse(categories.Select(ToCategoryResponse).ToArray()));
                })
            .WithName("ListCategories");

        api.MapPost(
                "/categories",
                async (
                    CreateCategoryRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICatalogService catalogService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var category = await catalogService.CreateCategoryAsync(
                        identity,
                        new CreateCategoryCommand(request.Name, request.Description),
                        cancellationToken);

                    return TypedResults.Created($"/api/v1/catalog/categories/{category.CategoryId}", ToCategoryResponse(category));
                })
            .WithName("CreateCategory");

        api.MapPatch(
                "/categories/{categoryId:guid}",
                async (
                    Guid categoryId,
                    UpdateCategoryRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICatalogService catalogService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var category = await catalogService.UpdateCategoryAsync(
                        identity,
                        categoryId,
                        new UpdateCategoryCommand(request.Name, request.Description, request.IsActive),
                        cancellationToken);

                    return TypedResults.Ok(ToCategoryResponse(category));
                })
            .WithName("UpdateCategory");

        api.MapGet(
                "/brands",
                async (
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICatalogService catalogService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var brands = await catalogService.ListBrandsAsync(identity, cancellationToken);
                    return TypedResults.Ok(new BrandListResponse(brands.Select(ToBrandResponse).ToArray()));
                })
            .WithName("ListBrands");

        api.MapPost(
                "/brands",
                async (
                    CreateBrandRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICatalogService catalogService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var brand = await catalogService.CreateBrandAsync(
                        identity,
                        new CreateBrandCommand(request.Name, request.Description),
                        cancellationToken);

                    return TypedResults.Created($"/api/v1/catalog/brands/{brand.BrandId}", ToBrandResponse(brand));
                })
            .WithName("CreateBrand");

        api.MapPatch(
                "/brands/{brandId:guid}",
                async (
                    Guid brandId,
                    UpdateBrandRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICatalogService catalogService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var brand = await catalogService.UpdateBrandAsync(
                        identity,
                        brandId,
                        new UpdateBrandCommand(request.Name, request.Description, request.IsActive),
                        cancellationToken);

                    return TypedResults.Ok(ToBrandResponse(brand));
                })
            .WithName("UpdateBrand");

        api.MapGet(
                "/units",
                async (
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICatalogService catalogService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var units = await catalogService.ListUnitsAsync(identity, cancellationToken);
                    return TypedResults.Ok(new UnitOfMeasureListResponse(units.Select(ToUnitResponse).ToArray()));
                })
            .WithName("ListUnits");

        api.MapGet(
                "/price-lists",
                async (
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICatalogService catalogService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var priceLists = await catalogService.ListPriceListsAsync(identity, cancellationToken);
                    return TypedResults.Ok(new PriceListListResponse(priceLists.Select(ToPriceListResponse).ToArray()));
                })
            .WithName("ListPriceLists");

        api.MapPost(
                "/price-lists",
                async (
                    CreatePriceListRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICatalogService catalogService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var priceList = await catalogService.CreatePriceListAsync(
                        identity,
                        new CreatePriceListCommand(request.Name, request.Code, request.IsDefault, request.IsActive),
                        cancellationToken);

                    return TypedResults.Created($"/api/v1/catalog/price-lists/{priceList.PriceListId}", ToPriceListResponse(priceList));
                })
            .WithName("CreatePriceList");

        api.MapPatch(
                "/price-lists/{priceListId:guid}",
                async (
                    Guid priceListId,
                    UpdatePriceListRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICatalogService catalogService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var priceList = await catalogService.UpdatePriceListAsync(
                        identity,
                        priceListId,
                        new UpdatePriceListCommand(request.Name, request.Code, request.IsDefault, request.IsActive),
                        cancellationToken);

                    return TypedResults.Ok(ToPriceListResponse(priceList));
                })
            .WithName("UpdatePriceList");

        return app;
    }

    private static ProductSummaryResponse ToProductSummaryResponse(ProductRecord product)
        => new(
            product.ProductId,
            product.Name,
            product.Description,
            product.InternalCode,
            product.Sku,
            product.Barcode,
            product.IsActive,
            product.AllowsFraction,
            product.SalesUnitLabel,
            product.Category is null ? null : ToCategoryResponse(product.Category),
            product.Brand is null ? null : ToBrandResponse(product.Brand),
            ToUnitResponse(product.BaseUnit),
            ToPriceResponse(product.CurrentPrice));

    private static ProductDetailResponse ToProductDetailResponse(ProductRecord product)
        => new(
            product.ProductId,
            product.Name,
            product.Description,
            product.InternalCode,
            product.Sku,
            product.Barcode,
            product.IsActive,
            product.AllowsFraction,
            product.SalesUnitLabel,
            product.Category is null ? null : ToCategoryResponse(product.Category),
            product.Brand is null ? null : ToBrandResponse(product.Brand),
            ToUnitResponse(product.BaseUnit),
            ToPriceResponse(product.CurrentPrice),
            product.CreatedAt,
            product.UpdatedAt);

    private static ProductCategoryResponse ToCategoryResponse(ProductCategoryRecord category)
        => new(category.CategoryId, category.Name, category.Description, category.IsActive);

    private static BrandResponse ToBrandResponse(BrandRecord brand)
        => new(brand.BrandId, brand.Name, brand.Description, brand.IsActive);

    private static UnitOfMeasureResponse ToUnitResponse(UnitOfMeasureRecord unit)
        => new(unit.UnitId, unit.Name, unit.Code, unit.Symbol, unit.AllowsFraction, unit.IsActive);

    private static PriceListResponse ToPriceListResponse(PriceListRecord priceList)
        => new(priceList.PriceListId, priceList.Name, priceList.Code, priceList.IsDefault, priceList.IsActive);

    private static ProductPriceResponse ToPriceResponse(ProductPriceRecord price)
        => new(
            price.PriceListId,
            price.PriceListName,
            price.PriceListCode,
            price.CostAmount,
            price.MarginPercent,
            price.SaleAmount,
            price.CurrencyCode,
            price.EffectiveFrom);
}
