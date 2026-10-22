namespace SteamTracker.Domain.Exceptions;

public class SteamApiException : Exception
{
    public SteamApiException(string message) : base(message) { }

    public SteamApiException(string message, Exception innerException)
        : base(message, innerException) { }
}