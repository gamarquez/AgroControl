namespace AgroControl.Contracts.Customers;

public sealed record CustomerAccountStatementResponse(
    CustomerResponse Customer,
    IReadOnlyList<CustomerAccountMovementResponse> Movements);
