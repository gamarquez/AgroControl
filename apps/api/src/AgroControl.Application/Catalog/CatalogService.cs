using AgroControl.Application.Auth;

namespace AgroControl.Application.Catalog;

internal sealed class CatalogService(
    ICatalogRepository catalogRepository,
    IAuditLogRepository auditLogRepository) : ICatalogService
{
    public async Task<ProductListResult> ListProductsAsync(IdentityContext identity, ProductListQuery query, CancellationToken cancellationToken)
    {
        return await catalogRepository.ListProductsAsync(
            identity.OrganizationId,
            CatalogValidation.ValidateListQuery(query),
            cancellationToken);
    }

    public async Task<ProductRecord> GetProductAsync(IdentityContext identity, Guid productId, CancellationToken cancellationToken)
    {
        return await catalogRepository.GetProductAsync(identity.OrganizationId, productId, cancellationToken)
               ?? throw new NotFoundException("Producto no encontrado.");
    }

    public async Task<ProductRecord> CreateProductAsync(IdentityContext identity, CreateProductCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var normalized = new CreateProductCommand(
            command.CategoryId,
            command.BrandId,
            CatalogValidation.ValidateRequiredGuid(command.BaseUnitId, "unidad base"),
            CatalogValidation.ValidateName(command.Name, "nombre"),
            CatalogValidation.NormalizeOptional(command.Description),
            CatalogValidation.ValidateCode(command.InternalCode, "codigo interno"),
            CatalogValidation.NormalizeOptional(command.Sku),
            CatalogValidation.NormalizeOptional(command.Barcode),
            command.AllowsFraction,
            CatalogValidation.NormalizeOptional(command.SalesUnitLabel),
            CatalogValidation.ValidateAmount(command.CostAmount, "costo"),
            CatalogValidation.ValidateMargin(command.MarginPercent),
            CatalogValidation.ValidateAmount(command.SaleAmount, "precio de venta"),
            CatalogValidation.ValidateCurrencyCode(command.CurrencyCode),
            command.PriceListId);

        var product = await catalogRepository.CreateProductAsync(identity.OrganizationId, normalized, cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "products",
            product.ProductId.ToString(),
            "product_created",
            new Dictionary<string, object?>
            {
                ["internalCode"] = product.InternalCode,
                ["saleAmount"] = product.CurrentPrice.SaleAmount
            },
            cancellationToken);

        return product;
    }

    public async Task<ProductRecord> UpdateProductAsync(IdentityContext identity, Guid productId, UpdateProductCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var normalized = new UpdateProductCommand(
            command.CategoryId,
            command.BrandId,
            CatalogValidation.ValidateRequiredGuid(command.BaseUnitId, "unidad base"),
            CatalogValidation.ValidateName(command.Name, "nombre"),
            CatalogValidation.NormalizeOptional(command.Description),
            CatalogValidation.ValidateCode(command.InternalCode, "codigo interno"),
            CatalogValidation.NormalizeOptional(command.Sku),
            CatalogValidation.NormalizeOptional(command.Barcode),
            command.IsActive,
            command.AllowsFraction,
            CatalogValidation.NormalizeOptional(command.SalesUnitLabel),
            CatalogValidation.ValidateAmount(command.CostAmount, "costo"),
            CatalogValidation.ValidateMargin(command.MarginPercent),
            CatalogValidation.ValidateAmount(command.SaleAmount, "precio de venta"),
            CatalogValidation.ValidateCurrencyCode(command.CurrencyCode),
            command.PriceListId);

        var product = await catalogRepository.UpdateProductAsync(identity.OrganizationId, productId, normalized, cancellationToken)
                      ?? throw new NotFoundException("Producto no encontrado.");

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "products",
            product.ProductId.ToString(),
            product.IsActive ? "product_updated" : "product_deactivated",
            new Dictionary<string, object?>
            {
                ["internalCode"] = product.InternalCode,
                ["saleAmount"] = product.CurrentPrice.SaleAmount,
                ["isActive"] = product.IsActive
            },
            cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "product_prices",
            product.ProductId.ToString(),
            "price_updated",
            new Dictionary<string, object?>
            {
                ["priceListCode"] = product.CurrentPrice.PriceListCode,
                ["saleAmount"] = product.CurrentPrice.SaleAmount,
                ["costAmount"] = product.CurrentPrice.CostAmount
            },
            cancellationToken);

        return product;
    }

    public Task<IReadOnlyList<ProductCategoryRecord>> ListCategoriesAsync(IdentityContext identity, CancellationToken cancellationToken)
        => catalogRepository.ListCategoriesAsync(identity.OrganizationId, cancellationToken);

    public async Task<ProductCategoryRecord> CreateCategoryAsync(IdentityContext identity, CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var category = await catalogRepository.CreateCategoryAsync(
            identity.OrganizationId,
            new CreateCategoryCommand(
                CatalogValidation.ValidateName(command.Name, "nombre"),
                CatalogValidation.NormalizeOptional(command.Description)),
            cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "product_categories",
            category.CategoryId.ToString(),
            "category_created",
            new Dictionary<string, object?> { ["name"] = category.Name },
            cancellationToken);

        return category;
    }

    public async Task<ProductCategoryRecord> UpdateCategoryAsync(IdentityContext identity, Guid categoryId, UpdateCategoryCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var category = await catalogRepository.UpdateCategoryAsync(
            identity.OrganizationId,
            categoryId,
            new UpdateCategoryCommand(
                CatalogValidation.ValidateName(command.Name, "nombre"),
                CatalogValidation.NormalizeOptional(command.Description),
                command.IsActive),
            cancellationToken)
            ?? throw new NotFoundException("Categoria no encontrada.");

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "product_categories",
            category.CategoryId.ToString(),
            "category_updated",
            new Dictionary<string, object?> { ["name"] = category.Name, ["isActive"] = category.IsActive },
            cancellationToken);

        return category;
    }

    public Task<IReadOnlyList<BrandRecord>> ListBrandsAsync(IdentityContext identity, CancellationToken cancellationToken)
        => catalogRepository.ListBrandsAsync(identity.OrganizationId, cancellationToken);

    public async Task<BrandRecord> CreateBrandAsync(IdentityContext identity, CreateBrandCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var brand = await catalogRepository.CreateBrandAsync(
            identity.OrganizationId,
            new CreateBrandCommand(
                CatalogValidation.ValidateName(command.Name, "nombre"),
                CatalogValidation.NormalizeOptional(command.Description)),
            cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "brands",
            brand.BrandId.ToString(),
            "brand_created",
            new Dictionary<string, object?> { ["name"] = brand.Name },
            cancellationToken);

        return brand;
    }

    public async Task<BrandRecord> UpdateBrandAsync(IdentityContext identity, Guid brandId, UpdateBrandCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var brand = await catalogRepository.UpdateBrandAsync(
            identity.OrganizationId,
            brandId,
            new UpdateBrandCommand(
                CatalogValidation.ValidateName(command.Name, "nombre"),
                CatalogValidation.NormalizeOptional(command.Description),
                command.IsActive),
            cancellationToken)
            ?? throw new NotFoundException("Marca no encontrada.");

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "brands",
            brand.BrandId.ToString(),
            "brand_updated",
            new Dictionary<string, object?> { ["name"] = brand.Name, ["isActive"] = brand.IsActive },
            cancellationToken);

        return brand;
    }

    public Task<IReadOnlyList<UnitOfMeasureRecord>> ListUnitsAsync(IdentityContext identity, CancellationToken cancellationToken)
        => catalogRepository.ListUnitsAsync(identity.OrganizationId, cancellationToken);

    public Task<IReadOnlyList<PriceListRecord>> ListPriceListsAsync(IdentityContext identity, CancellationToken cancellationToken)
        => catalogRepository.ListPriceListsAsync(identity.OrganizationId, cancellationToken);

    public async Task<PriceListRecord> CreatePriceListAsync(IdentityContext identity, CreatePriceListCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var priceList = await catalogRepository.CreatePriceListAsync(
            identity.OrganizationId,
            new CreatePriceListCommand(
                CatalogValidation.ValidateName(command.Name, "nombre"),
                CatalogValidation.ValidateCode(command.Code, "codigo"),
                command.IsDefault,
                command.IsActive),
            cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "price_lists",
            priceList.PriceListId.ToString(),
            "price_list_created",
            new Dictionary<string, object?> { ["code"] = priceList.Code, ["isDefault"] = priceList.IsDefault },
            cancellationToken);

        return priceList;
    }

    public async Task<PriceListRecord> UpdatePriceListAsync(IdentityContext identity, Guid priceListId, UpdatePriceListCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var priceList = await catalogRepository.UpdatePriceListAsync(
            identity.OrganizationId,
            priceListId,
            new UpdatePriceListCommand(
                CatalogValidation.ValidateName(command.Name, "nombre"),
                CatalogValidation.ValidateCode(command.Code, "codigo"),
                command.IsDefault,
                command.IsActive),
            cancellationToken)
            ?? throw new NotFoundException("Lista de precios no encontrada.");

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "price_lists",
            priceList.PriceListId.ToString(),
            "price_list_updated",
            new Dictionary<string, object?> { ["code"] = priceList.Code, ["isDefault"] = priceList.IsDefault, ["isActive"] = priceList.IsActive },
            cancellationToken);

        return priceList;
    }

    private static void EnsureWriter(IdentityContext identity)
    {
        if (!identity.Roles.Contains("administrator", StringComparer.OrdinalIgnoreCase) &&
            !identity.Roles.Contains("manager", StringComparer.OrdinalIgnoreCase))
        {
            throw new AuthorizationException("No cuenta con permisos para editar catalogo o precios.");
        }
    }
}
