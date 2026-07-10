namespace AgroControl.Application.Customers;

public interface ICustomerRepository
{
    Task<CustomerListResult> ListCustomersAsync(Guid organizationId, CustomerListQuery query, CancellationToken cancellationToken);

    Task<CustomerRecord?> GetCustomerAsync(Guid organizationId, Guid customerId, CancellationToken cancellationToken);

    Task<CustomerRecord> CreateCustomerAsync(Guid organizationId, CreateCustomerCommand command, CancellationToken cancellationToken);

    Task<CustomerRecord?> UpdateCustomerAsync(Guid organizationId, Guid customerId, UpdateCustomerCommand command, CancellationToken cancellationToken);

    Task<CustomerAccountMovementListResult> ListAccountMovementsAsync(Guid organizationId, Guid customerId, int page, int pageSize, CancellationToken cancellationToken);

    Task<CustomerAccountStatementRecord> GetAccountStatementAsync(Guid organizationId, Guid customerId, int limit, CancellationToken cancellationToken);

    Task<RecordCustomerPaymentResult> RecordPaymentAsync(Guid organizationId, Guid customerId, Guid actorUserId, RecordCustomerPaymentCommand command, CancellationToken cancellationToken);

    Task<RecordCustomerCreditNoteResult> RecordCreditNoteAsync(Guid organizationId, Guid customerId, Guid actorUserId, RecordCustomerCreditNoteCommand command, CancellationToken cancellationToken);
}
