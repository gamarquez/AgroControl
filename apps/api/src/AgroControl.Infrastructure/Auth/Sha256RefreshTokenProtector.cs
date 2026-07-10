using System.Security.Cryptography;
using System.Text;
using AgroControl.Application.Auth;

namespace AgroControl.Infrastructure.Auth;

internal sealed class Sha256RefreshTokenProtector : IRefreshTokenProtector
{
    public string GenerateToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    }

    public string Hash(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(bytes);
    }
}
