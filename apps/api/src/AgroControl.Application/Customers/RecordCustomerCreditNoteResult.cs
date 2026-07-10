namespace AgroControl.Application.Customers;

public sealed record RecordCustomerCreditNoteResult(
    CustomerRecord Customer,
    CustomerAccountMovementRecord Movement);
