namespace SteamTracker.Domain.Exceptions;

public class AuthCancelledException : Exception
{
    public AuthCancelledException(string message) : base(message) { }

    public AuthCancelledException(string message, Exception innerException)
        : base(message, innerException) { }
}