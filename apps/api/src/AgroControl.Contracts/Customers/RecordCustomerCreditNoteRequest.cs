namespace AgroControl.Contracts.Customers;

public sealed record RecordCustomerCreditNoteRequest(
    decimal Amount,
    string Concept,
    string? ReferenceDocument,
    string? Notes);
