namespace AgroControl.Application.Customers;

public sealed record RecordCustomerPaymentResult(
    CustomerRecord Customer,
    CustomerAccountMovementRecord Movement);
