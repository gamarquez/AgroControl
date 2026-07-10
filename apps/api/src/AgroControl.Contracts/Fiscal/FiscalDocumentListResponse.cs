namespace AgroControl.Contracts.Fiscal;

public sealed record FiscalDocumentListResponse(
    IReadOnlyList<FiscalDocumentResponse> Items,
    int Total);
