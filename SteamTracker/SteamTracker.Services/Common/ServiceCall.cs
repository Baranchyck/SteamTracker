using Microsoft.Extensions.Logging;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;

namespace SteamTracker.Services.Common;

internal static class ServiceCall
{
    public const string NotSignedInMessage = "Спочатку увійдіть через Steam.";

    public static ServiceResult<T> NotSignedIn<T>() => Error<T>(NotSignedInMessage);

    public static ServiceResult<T> Error<T>(string message) => new(default, DataStatus.Error, message);

    public static ServiceResult<T> Private<T>(string message) => new(default, DataStatus.Private, message);

    // OperationCanceledException не ловимо: скасування має дійти до UI.
    // Збій Steam логуємо як Error разом із винятком (#40); errorMessage — для UI, logMessage — для файлу логів
    public static async Task<ServiceResult<T>> RunAsync<T>(
        Func<Task<ServiceResult<T>>> call,
        string errorMessage,
        ILogger logger,
        string logMessage,
        params object?[] logArgs)
    {
        try
        {
            return await call();
        }
        catch (SteamApiException ex)
        {
            logger.LogError(ex, logMessage, logArgs);
            return Error<T>(errorMessage);
        }
    }
}
