namespace SteamTracker.Domain.Exceptions;

public class AuthFailedException : Exception
{
    public AuthFailedException(string message) : base(message) { }

    public AuthFailedException(string message, Exception innerException)
        : base(message, innerException) { }
}