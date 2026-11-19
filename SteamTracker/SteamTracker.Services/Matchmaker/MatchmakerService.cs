using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;
using SteamTracker.Services.Common;

namespace SteamTracker.Services.Matchmaker;

public class MatchmakerService : IMatchmakerService
{
    // Скільки запитів у Steam (резолв ніків, бібліотеки) летить одночасно
    public const int MaxParallelRequests = 4;

    // Вводів може бути більше за учасників (дублікати, сам користувач), але не безмежно —
    // інакше одна форма генерує сотні запитів у Steam
    public const int MaxInputs = IMatchmakerService.MaxParticipants * 2;

    private readonly ISteamApiClient _steamApi;
    private readonly IAuthService _auth;
    private readonly ISteamIdResolver _resolver;

    public MatchmakerService(ISteamApiClient steamApi, IAuthService auth, ISteamIdResolver resolver)
    {
        _steamApi = steamApi;
        _auth = auth;
        _resolver = resolver;
    }

    public async Task<ServiceResult<MatchResult>> FindCommonGamesAsync(
        IReadOnlyList<string> participantInputs, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(participantInputs);

        var user = _auth.CurrentUser;
        if (user is null)
            return ServiceCall.NotSignedIn<MatchResult>();

        // Порожні рядки — просто незаповнені поля форми, а не помилка вводу
        var inputs = participantInputs.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();

        if (inputs.Count > MaxInputs)
            return ServiceCall.Error<MatchResult>(
                $"Забагато учасників: максимум {IMatchmakerService.MaxParticipants} разом з вами.");

        return await ServiceCall.RunAsync<MatchResult>(async () =>
        {
            using var throttle = new SemaphoreSlim(MaxParallelRequests);

            var resolved = await RunThrottledAsync(inputs, i => _resolver.ResolveAsync(i, ct), throttle, ct);
            var invalid = resolved.Where(r => !r.IsSuccess).ToList();

            // Поточний користувач першим; повтори (і сам користувач у вводі) прибираються
            var ids = new List<string> { user.SteamId };
            foreach (var r in resolved.Where(r => r.IsSuccess))
            {
                if (!ids.Contains(r.SteamId!))
                    ids.Add(r.SteamId!);
            }

            // Валідний за форматом SteamID, якого не існує, Steam просто не повертає в профілях
            var profiles = (await _steamApi.GetPlayerSummariesAsync(ids, ct))
                .ToDictionary(p => p.SteamId);

            foreach (var id in ids.Skip(1).Where(id => !profiles.ContainsKey(id)).ToList())
            {
                ids.Remove(id);
                var input = resolved.First(r => r.SteamId == id).Input;
                invalid.Add(SteamIdResolveResult.Fail(input, SteamIdResolveError.NotFound, "Профіль не знайдено."));
            }

            if (ids.Count < IMatchmakerService.MinParticipants)
                return ServiceCall.Error<MatchResult>(invalid.Count > 0
                    ? $"Немає жодного іншого учасника: {invalid[0].Message}"
                    : "Додайте хоча б одного учасника.");

            if (ids.Count > IMatchmakerService.MaxParticipants)
                return ServiceCall.Error<MatchResult>(
                    $"Забагато учасників: максимум {IMatchmakerService.MaxParticipants} разом з вами.");

            var libraries = await RunThrottledAsync(ids, id => _steamApi.GetOwnedGamesAsync(id, ct), throttle, ct);

            var participants = ids.Select((id, i) => ToParticipant(
                    id,
                    profiles.GetValueOrDefault(id),
                    user,
                    libraryHidden: libraries[i] is null))
                .ToList();

            var visible = ids.Zip(libraries)
                .Where(x => x.Second is not null)
                .Select(x => (SteamId: x.First, Games: x.Second!))
                .ToList();

            var hidden = participants.Where(p => p.LibraryHidden).ToList();

            // Прихована бібліотека не обнуляє перетин — учасник просто не бере в ньому участі.
            // Але з однією відкритою бібліотекою "спільних" ігор не буває.
            string? message = null;
            IReadOnlyList<CommonGame> common;
            if (visible.Count < IMatchmakerService.MinParticipants)
            {
                common = Array.Empty<CommonGame>();
                message = "Недостатньо відкритих бібліотек, щоб знайти спільні ігри.";
            }
            else
            {
                common = Intersect(visible);
            }

            var result = new MatchResult(participants, common, invalid, hidden);
            return new ServiceResult<MatchResult>(result, DataStatus.Available, message);
        }, "Не вдалося отримати дані зі Steam. Спробуйте пізніше.");
    }

    private static IReadOnlyList<CommonGame> Intersect(
        IReadOnlyList<(string SteamId, IReadOnlyList<OwnedGame> Games)> libraries)
    {
        // Словник на кожного учасника: appid → гра (дублікатів у бібліотеці Steam немає, але не падаємо)
        var byApp = libraries
            .Select(l => (l.SteamId, Games: l.Games
                .GroupBy(g => g.AppId)
                .ToDictionary(g => g.Key, g => g.First())))
            .ToList();

        var commonIds = byApp
            .Select(l => (IEnumerable<int>)l.Games.Keys)
            .Aggregate((acc, keys) => acc.Intersect(keys))
            .ToList();

        return commonIds
            .Select(appId =>
            {
                var first = byApp[0].Games[appId];
                var playtimes = byApp.ToDictionary(l => l.SteamId, l => l.Games[appId].PlaytimeForeverMinutes);
                return new CommonGame(
                    AppId: appId,
                    Name: first.Name,
                    IconUrl: first.IconUrl,
                    AveragePlaytimeMinutes: Math.Round((decimal)playtimes.Values.Average(), 1),
                    PlaytimeMinutesBySteamId: playtimes);
            })
            .OrderByDescending(g => g.AveragePlaytimeMinutes)
            .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static MatchParticipant ToParticipant(
        string steamId, PlayerPresence? profile, SteamProfile user, bool libraryHidden)
    {
        bool isUser = steamId == user.SteamId;
        return new MatchParticipant(
            SteamId: steamId,
            PersonaName: profile?.PersonaName ?? (isUser ? user.PersonaName : steamId),
            AvatarUrl: profile?.AvatarUrl ?? (isUser ? user.AvatarUrl : string.Empty),
            PersonaState: profile?.PersonaState ?? PersonaState.Offline,
            IsCurrentUser: isUser,
            LibraryHidden: libraryHidden);
    }

    // Порядок результатів = порядок вхідних елементів
    private static async Task<T[]> RunThrottledAsync<TIn, T>(
        IEnumerable<TIn> items, Func<TIn, Task<T>> action, SemaphoreSlim throttle, CancellationToken ct)
    {
        return await Task.WhenAll(items.Select(async item =>
        {
            await throttle.WaitAsync(ct);
            try
            {
                return await action(item);
            }
            finally
            {
                throttle.Release();
            }
        }));
    }
}
