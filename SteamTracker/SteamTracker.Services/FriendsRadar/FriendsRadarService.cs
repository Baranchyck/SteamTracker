using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;

namespace SteamTracker.Services.FriendsRadar;

public class FriendsRadarService : IFriendsRadarService
{
    private readonly ISteamApiClient _steamApi;
    private readonly IAuthService _auth;

    public FriendsRadarService(ISteamApiClient steamApi, IAuthService auth)
    {
        _steamApi = steamApi;
        _auth = auth;
    }

    public async Task<ServiceResult<IReadOnlyList<PlayerPresence>>> GetFriendsAsync(
        RadarFilter filter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var user = _auth.CurrentUser;
        if (user is null)
            return new(null, DataStatus.Error, "Спочатку увійдіть через Steam.");

        try
        {
            var friendIds = await _steamApi.GetFriendIdsAsync(user.SteamId, ct);
            if (friendIds is null)
                return new(null, DataStatus.Private, "Список друзів прихований налаштуваннями приватності.");

            // Друзів немає — це не помилка, а просто порожній радар
            if (friendIds.Count == 0)
                return new(Array.Empty<PlayerPresence>(), DataStatus.Available);

            var friends = await _steamApi.GetPlayerSummariesAsync(friendIds, ct);

            IEnumerable<PlayerPresence> query = friends;
            if (filter.OnlyAvailable)
                query = query.Where(IsAvailable);

            var sorted = query
                .OrderBy(Group)
                .ThenBy(f => f.PersonaName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            return new(sorted, DataStatus.Available);
        }
        catch (SteamApiException)
        {
            return new(null, DataStatus.Error, "Не вдалося отримати список друзів зі Steam. Спробуйте пізніше.");
        }
    }

    // Для сторонніх (non-Steam) ігор CurrentGameId == null, але назва гри є — це теж "в грі"
    private static bool IsInGame(PlayerPresence p) =>
        p.CurrentGameId is not null || !string.IsNullOrEmpty(p.CurrentGameName);

    // "Вільні для гри": онлайн або шукає гру, і зараз ні в що не грає
    private static bool IsAvailable(PlayerPresence p) =>
        (p.PersonaState is PersonaState.Online or PersonaState.LookingToPlay) && !IsInGame(p);

    // Порядок на радарі: в грі → онлайн → відійшов/зайнятий → офлайн
    private static int Group(PlayerPresence p)
    {
        if (IsInGame(p))
            return 0;

        return p.PersonaState switch
        {
            PersonaState.Online or PersonaState.LookingToPlay or PersonaState.LookingToTrade => 1,
            PersonaState.Away or PersonaState.Busy or PersonaState.Snooze => 2,
            _ => 3
        };
    }
}
