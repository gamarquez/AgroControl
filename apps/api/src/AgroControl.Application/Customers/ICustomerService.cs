using AgroControl.Application.Auth;

namespace AgroControl.Application.Customers;

public interface ICustomerService
{
    Task<CustomerListResult> ListCustomersAsync(IdentityContext identity, CustomerListQuery query, CancellationToken cancellationToken);

    Task<CustomerRecord> GetCustomerAsync(IdentityContext identity, Guid customerId, CancellationToken cancellationToken);

    Task<CustomerRecord> CreateCustomerAsync(IdentityContext identity, CreateCustomerCommand command, CancellationToken cancellationToken);

    Task<CustomerRecord> UpdateCustomerAsync(IdentityContext identity, Guid customerId, UpdateCustomerCommand command, CancellationToken cancellationToken);

    Task<CustomerAccountMovementListResult> ListAccountMovementsAsync(IdentityContext identity, Guid customerId, int page, int pageSize, CancellationToken cancellationToken);

    Task<CustomerAccountStatementRecord> GetAccountStatementAsync(IdentityContext identity, Guid customerId, int limit, CancellationToken cancellationToken);

    Task<RecordCustomerPaymentResult> RecordPaymentAsync(IdentityContext identity, Guid customerId, RecordCustomerPaymentCommand command, CancellationToken cancellationToken);

    Task<RecordCustomerCreditNoteResult> RecordCreditNoteAsync(IdentityContext identity, Guid customerId, RecordCustomerCreditNoteCommand command, CancellationToken cancellationToken);
}
