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


# Sequence Diagram — розрахунок складності ачівок в Achievement Planner

Флоу показує, як формується roadmap: недосягнуті ачівки сортуються
за глобальним % виконання (найлегші — зверху).

```mermaid
sequenceDiagram
    actor User
    participant UI as SteamTracker.UI
    participant Service as AchievementService
    participant Client as ISteamApiClient
    participant Steam as Steam Web API

    User->>UI: Обрати гру → "Achievement Planner"
    UI->>Service: GetRoadmap(steamId, appId)

    Service->>Client: GetPlayerAchievements(steamId, appId)
    Client->>Steam: HTTP GET /ISteamUserStats/GetPlayerAchievements
    Steam-->>Client: Achievements[] (achieved: true/false)
    Client-->>Service: PlayerAchievements

    Service->>Client: GetGlobalAchievementPercentagesForApp(appId)
    Client->>Steam: HTTP GET /ISteamUserStats/GetGlobalAchievementPercentagesForApp
    Steam-->>Client: GlobalPercentages[]
    Client-->>Service: GlobalAchievementStats

    Service->>Service: Відфільтрувати недосягнуті ачівки
    Service->>Service: Приєднати глобальний % до кожної
    Service->>Service: Сортувати за % спадання (найлегші — зверху)
    Service->>Service: Побудувати Roadmap (скільки ачівок закрити для +N%)

    Service-->>UI: Roadmap (сортований список + прогноз)
    UI-->>User: Показати відсортований список ачівок
```

## Примітки

- Якщо `GetPlayerAchievements` повертає порожній результат — профіль приватний або гра не має статистики; обробляється як окремий UI-стан.
- Розрахунок "скільки ачівок для +N% проходження" — проста арифметика: `N% * totalAchievements / 100`, округлення в бік найближчих найлегших ачівок зі списку.