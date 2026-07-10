namespace AgroControl.Application.Auth;

public interface IRefreshTokenProtector
{
    string GenerateToken();

    string Hash(string token);
}
