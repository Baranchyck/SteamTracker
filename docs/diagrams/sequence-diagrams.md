# Sequence Diagram — завантаження та кешування профілю гравця

Флоу показує, як застосунок вирішує: віддати профіль з локального
кешу (SQLite) чи звернутись до Steam API, якщо кеш відсутній або застарів.

```mermaid
sequenceDiagram
    actor User
    participant UI as SteamTracker.UI
    participant Service as ProfileService
    participant Repo as ProfileRepository (SQLite)
    participant Client as ISteamApiClient
    participant Steam as Steam Web API

    User->>UI: Відкрити профіль
    UI->>Service: GetProfile(steamId)
    Service->>Repo: GetCached(steamId)

    alt Кеш є і не застарів
        Repo-->>Service: CachedProfile
        Service-->>UI: Profile (з кешу)
    else Кеш відсутній або застарів
        Repo-->>Service: null
        Service->>Client: GetPlayerSummaries(steamId)
        Client->>Steam: HTTP GET /ISteamUser/GetPlayerSummaries
        Steam-->>Client: Player data (JSON)
        Client-->>Service: PlayerSummary

        alt Профіль приватний
            Service-->>UI: Стан "дані недоступні"
        else Профіль публічний
            Service->>Repo: Save(profile)
            Service-->>UI: Profile (свіжий)
        end
    end

    UI-->>User: Показати профіль / стан недоступності
```

## Примітки

- Кеш вважається застарілим за TTL, визначеним у `ProfileService` (наприклад, 15 хв).
- Приватний профіль обробляється як окремий UI-стан, не як помилка.
- Помилки мережі (Steam API недоступний) обробляються через try/catch у `SteamApiClient` з поверненням явного результату (не exception назовні UI).