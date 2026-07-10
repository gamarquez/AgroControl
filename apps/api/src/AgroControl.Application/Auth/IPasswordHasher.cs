namespace AgroControl.Application.Auth;

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerificationResult Verify(string password, string hash);
}
