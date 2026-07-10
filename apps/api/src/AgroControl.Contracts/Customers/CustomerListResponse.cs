namespace AgroControl.Contracts.Customers;

public sealed record CustomerListResponse(
    IReadOnlyList<CustomerResponse> Items,
    int Page,
    int PageSize,
    int Total);
