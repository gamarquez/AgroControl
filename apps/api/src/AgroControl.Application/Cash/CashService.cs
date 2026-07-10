using AgroControl.Application.Auth;

namespace AgroControl.Application.Cash;

internal sealed class CashService(
    ICashRepository cashRepository,
    IAuditLogRepository auditLogRepository) : ICashService
{
    public Task<CashOverviewRecord> GetOverviewAsync(IdentityContext identity, CancellationToken cancellationToken)
        => cashRepository.GetOverviewAsync(identity.OrganizationId, cancellationToken);

    public Task<CashMovementListResult> ListMovementsAsync(IdentityContext identity, int page, int pageSize, CancellationToken cancellationToken)
        => cashRepository.ListMovementsAsync(
            identity.OrganizationId,
            CashValidation.ValidatePage(page),
            CashValidation.ValidatePageSize(pageSize),
            cancellationToken);

    public async Task<CashSessionRecord> OpenSessionAsync(IdentityContext identity, OpenCashSessionCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var normalized = new OpenCashSessionCommand(
            CashValidation.ValidateCashRegisterCode(command.CashRegisterCode),
            CashValidation.ValidateNonNegativeAmount(command.OpeningAmount, "monto de apertura"),
            CashValidation.NormalizeOptional(command.OpeningNotes));

        var session = await cashRepository.OpenSessionAsync(identity.OrganizationId, identity.UserId, normalized, cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "cash_sessions",
            session.CashSessionId.ToString(),
            "cash_session_opened",
            new Dictionary<string, object?>
            {
                ["cashRegisterCode"] = session.CashRegister.Code,
                ["openingAmount"] = session.OpeningAmount
            },
            cancellationToken);

        return session;
    }

    public async Task<CashMovementRecord> CreateMovementAsync(IdentityContext identity, CreateCashMovementCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var normalized = new CreateCashMovementCommand(
            CashValidation.ValidateRequiredGuid(command.CashSessionId, "sesion de caja"),
            CashValidation.ValidateMovementType(command.MovementType),
            CashValidation.ValidateCategoryCode(command.CategoryCode),
            CashValidation.ValidateConcept(command.Concept),
            CashValidation.ValidatePaymentMethod(command.PaymentMethod),
            CashValidation.ValidatePositiveAmount(command.Amount, "importe"),
            CashValidation.NormalizeOptional(command.ReferenceDocument),
            CashValidation.NormalizeOptional(command.Notes));

        var movement = await cashRepository.CreateMovementAsync(identity.OrganizationId, identity.UserId, normalized, cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "cash_movements",
            movement.CashMovementId.ToString(),
            "cash_movement_recorded",
            new Dictionary<string, object?>
            {
                ["cashSessionId"] = movement.CashSessionId,
                ["movementType"] = movement.MovementType,
                ["categoryCode"] = movement.CategoryCode,
                ["paymentMethod"] = movement.PaymentMethod,
                ["amount"] = movement.Amount,
                ["resultingBalance"] = movement.ResultingBalance
            },
            cancellationToken);

        return movement;
    }

    public async Task<CashSessionRecord> CloseSessionAsync(IdentityContext identity, Guid cashSessionId, CloseCashSessionCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var session = await cashRepository.CloseSessionAsync(
            identity.OrganizationId,
            identity.UserId,
            CashValidation.ValidateRequiredGuid(cashSessionId, "sesion de caja"),
            new CloseCashSessionCommand(
                CashValidation.ValidateNonNegativeAmount(command.ClosingAmount, "monto de cierre"),
                CashValidation.NormalizeOptional(command.ClosingNotes)),
            cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "cash_sessions",
            session.CashSessionId.ToString(),
            "cash_session_closed",
            new Dictionary<string, object?>
            {
                ["closingAmount"] = session.ClosingAmount,
                ["differenceAmount"] = session.DifferenceAmount,
                ["currentBalance"] = session.CurrentBalance
            },
            cancellationToken);

        return session;
    }

    private static void EnsureWriter(IdentityContext identity)
    {
        if (!identity.Roles.Contains("administrator", StringComparer.OrdinalIgnoreCase) &&
            !identity.Roles.Contains("manager", StringComparer.OrdinalIgnoreCase) &&
            !identity.Roles.Contains("cashier", StringComparer.OrdinalIgnoreCase))
        {
            throw new AuthorizationException("No cuenta con permisos para operar caja.");
        }
    }
}
