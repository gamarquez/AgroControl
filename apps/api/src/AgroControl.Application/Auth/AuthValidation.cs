namespace AgroControl.Application.Auth;

public static class AuthValidation
{
    public static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ValidationException("El email es obligatorio.");
        }

        return email.Trim().ToLowerInvariant();
    }

    public static string ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ValidationException("La contraseña es obligatoria.");
        }

        if (password.Length < 8)
        {
            throw new ValidationException("La contraseña debe tener al menos 8 caracteres.");
        }

        return password;
    }

    public static string ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ValidationException("El nombre a mostrar es obligatorio.");
        }

        return displayName.Trim();
    }

    public static IReadOnlyList<Guid> ValidateRoleIds(IReadOnlyList<Guid> roleIds)
    {
        if (roleIds.Count == 0)
        {
            throw new ValidationException("Debe seleccionar al menos un rol.");
        }

        if (roleIds.Any(roleId => roleId == Guid.Empty))
        {
            throw new ValidationException("Los roles informados no son válidos.");
        }

        return roleIds.Distinct().ToArray();
    }
}
