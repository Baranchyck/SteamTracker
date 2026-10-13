namespace SteamTracker.Domain.Exceptions;

public class ConfigurationMissingException : Exception
{
    public ConfigurationMissingException(string message) : base(message) { }
}
