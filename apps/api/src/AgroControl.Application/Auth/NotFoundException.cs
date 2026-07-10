namespace AgroControl.Application.Auth;

public sealed class NotFoundException(string message) : Exception(message);
