using AgroControl.Application.Fiscal;
using AgroControl.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;

namespace AgroControl.Infrastructure.Fiscal;

internal sealed class ArcaFiscalService(
    IOptions<ArcaOptions> options) : IArcaFiscalService
{
    private static readonly XNamespace WsfeNamespace = "http://ar.gov.afip.dif.FEV1/";
    private readonly ArcaOptions _options = options.Value;

    public async Task<FiscalProbeResult> ProbeAsync(FiscalSettingsRecord settings, CancellationToken cancellationToken)
    {
        if (!settings.IsEnabled || !string.Equals(settings.Provider, "arca_wsfev1", StringComparison.OrdinalIgnoreCase))
        {
            return new FiscalProbeResult(
                false,
                false,
                settings.Provider,
                settings.Environment,
                "La integracion fiscal esta deshabilitada para esta organizacion.",
                null,
                null,
                null,
                null);
        }

        if (string.IsNullOrWhiteSpace(_options.CertificatePath) || string.IsNullOrWhiteSpace(_options.CertificatePassword))
        {
            return new FiscalProbeResult(
                true,
                false,
                settings.Provider,
                settings.Environment,
                "Faltan `Arca:CertificatePath` o `Arca:CertificatePassword` para validar la conectividad fiscal.",
                null,
                null,
                null,
                null);
        }

        try
        {
            _ = LoadCertificate();
            var dummy = await CallDummyAsync(settings, cancellationToken);

            return new FiscalProbeResult(
                true,
                true,
                settings.Provider,
                settings.Environment,
                "El certificado configurado cargo correctamente y FEDummy respondio en el entorno seleccionado.",
                null,
                dummy.AuthServer,
                dummy.AppServer,
                dummy.DbServer);
        }
        catch (Exception exception)
        {
            return new FiscalProbeResult(
                true,
                false,
                settings.Provider,
                settings.Environment,
                exception.Message,
                null,
                null,
                null,
                null);
        }
    }

    private async Task<DummyResponse> CallDummyAsync(FiscalSettingsRecord settings, CancellationToken cancellationToken)
    {
        const string soapBody = """
            <?xml version="1.0" encoding="utf-8"?>
            <soap:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <FEDummy xmlns="http://ar.gov.afip.dif.FEV1/" />
              </soap:Body>
            </soap:Envelope>
            """;

        using var request = new HttpRequestMessage(HttpMethod.Post, GetWsfeUrl(settings.Environment))
        {
            Content = new StringContent(soapBody, Encoding.UTF8, "text/xml"),
        };
        request.Headers.Add("SOAPAction", "\"http://ar.gov.afip.dif.FEV1/FEDummy\"");

        using var client = CreateHttpClient();
        using var response = await client.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var document = XDocument.Parse(responseContent);
        var result = document.Descendants(WsfeNamespace + "FEDummyResult").FirstOrDefault()
            ?? throw new InvalidOperationException("FEDummy no devolvio `FEDummyResult`.");

        return new DummyResponse(
            result.Element(WsfeNamespace + "AppServer")?.Value ?? "unknown",
            result.Element(WsfeNamespace + "DbServer")?.Value ?? "unknown",
            result.Element(WsfeNamespace + "AuthServer")?.Value ?? "unknown");
    }

    private HttpClient CreateHttpClient()
        => new()
        {
            Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds),
        };

    private X509Certificate2 LoadCertificate()
    {
        var certificatePath = _options.CertificatePath
            ?? throw new InvalidOperationException("No se configuro la ruta del certificado digital ARCA.");

        if (!File.Exists(certificatePath))
        {
            throw new InvalidOperationException("No encontramos el certificado digital configurado para ARCA.");
        }

        return X509CertificateLoader.LoadPkcs12FromFile(
            certificatePath,
            _options.CertificatePassword,
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
    }

    private static string GetWsfeUrl(string environment)
        => string.Equals(environment, "production", StringComparison.OrdinalIgnoreCase)
            ? "https://servicios1.afip.gov.ar/wsfev1/service.asmx"
            : "https://wswhomo.afip.gov.ar/wsfev1/service.asmx";

    private sealed record DummyResponse(
        string AppServer,
        string DbServer,
        string AuthServer);
}
