namespace AgroControl.Contracts.Customers;

public sealed record CustomerAccountMovementListResponse(
    IReadOnlyList<CustomerAccountMovementResponse> Items,
    int Page,
    int PageSize,
    int Total);
