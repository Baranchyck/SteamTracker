# Модель даних — ER-діаграма

Локальний кеш даних Steam Web API (SQLite, ADO.NET). БД не є джерелом правди —
кожен рядок має поле `FetchedAt`/`LastFetchedAt` для TTL-інвалідації кешу
(див. `Services` — методи спершу читають з БД, і лише при простроченому TTL
чи відсутності рядка йдуть у Steam API, після чого upsert-ять результат назад).

```mermaid
erDiagram
  PLAYER ||--o{ OWNED_GAME : owns
  GAME ||--o{ OWNED_GAME : owned_as
  GAME ||--o{ ACHIEVEMENT : defines
  PLAYER ||--o{ PLAYER_ACHIEVEMENT : unlocks
  ACHIEVEMENT ||--o{ PLAYER_ACHIEVEMENT : tracked_by
  PLAYER ||--o{ FRIENDSHIP : owner_side
  PLAYER ||--o{ FRIENDSHIP : friend_side

  PLAYER {
    bigint SteamId64 PK
    varchar PersonaName
    varchar AvatarUrl
    int VisibilityState
    bigint TimeCreated
    bool IsLocalUser
    datetime LastFetchedAt
  }
  GAME {
    int AppId PK
    varchar Title
    int CurrentPriceCents
    varchar Currency
    datetime PriceFetchedAt
  }
  OWNED_GAME {
    bigint PlayerId PK_FK
    int AppId PK_FK
    int PlayTimeForever
    int PlayTimeLastTwoWeeks
    bigint LastPlayed
    datetime FetchedAt
  }
  ACHIEVEMENT {
    int AchievementId PK
    int AppId FK
    varchar ApiName UK
    varchar DisplayName
    real GlobalPercent
    datetime PercentFetchedAt
  }
  PLAYER_ACHIEVEMENT {
    bigint PlayerId PK_FK
    int AchievementId PK_FK
    bool Achieved
    bigint UnlockTime
  }
  FRIENDSHIP {
    bigint OwnerId PK_FK
    bigint FriendId PK_FK
  }
```

## Ключові рішення

- **`Player`/`Game`/`OwnedGame`/`Friendship`** — натуральні ключі
  (`SteamId64`, `AppId`), без сурогатів.
- Поля, які Steam API повертає не завжди (`PlayTimeLastTwoWeeks`,
  `TimeCreated` для приватних профілів, `CurrentPriceCents` з `appdetails`) —
  навмисно nullable.
- `CHECK (VisibilityState IN (1,3))` та `CHECK (FriendId != OwnerId)` —
  додаються прямо в `CREATE TABLE` (див. `docs/schema.sql`).