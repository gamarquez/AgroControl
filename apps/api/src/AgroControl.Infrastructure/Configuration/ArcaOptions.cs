using Microsoft.Extensions.Configuration;

namespace AgroControl.Infrastructure.Configuration;

public sealed record ArcaOptions(
    string? CertificatePath,
    string? CertificatePassword,
    int TimeoutSeconds)
{
    public static ArcaOptions FromConfiguration(IConfiguration configuration)
    {
        return new ArcaOptions(
            configuration["Arca:CertificatePath"]?.Trim(),
            configuration["Arca:CertificatePassword"],
            int.TryParse(configuration["Arca:TimeoutSeconds"], out var timeoutSeconds) && timeoutSeconds > 0
                ? timeoutSeconds
                : 20);
    }
}
