-- SteamTracker — SQLite DDL
-- Виконується один раз при першому запуску застосунку (DataAccess-шар,
-- CREATE TABLE IF NOT EXISTS), якщо файл БД ще не існує.
--
-- ВАЖЛИВО: PRAGMA нижче встановлюється на кожному з'єднанні окремо
-- (SqliteConnection.Open() -> ExecuteNonQuery("PRAGMA foreign_keys = ON;")),
-- сам він у схему не "зберігається".

PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Player (
    SteamId64        INTEGER PRIMARY KEY,
    PersonaName      TEXT    NOT NULL,
    AvatarUrl        TEXT    NOT NULL,
    VisibilityState  INTEGER NOT NULL,
    TimeCreated      INTEGER,                 -- nullable: відсутнє для приватних профілів
    IsLocalUser      INTEGER NOT NULL DEFAULT 0, -- 0/1, SQLite не має рідного BOOL
    LastFetchedAt    TEXT    NOT NULL,         -- ISO-8601, TTL кешу профілю
    CHECK (VisibilityState IN (1, 3))
);

CREATE TABLE IF NOT EXISTS Game (
    AppId             INTEGER PRIMARY KEY,
    Title             TEXT    NOT NULL,
    CurrentPriceCents INTEGER,                -- nullable: appdetails неофіційний, може бути недоступний
    Currency          TEXT,                   -- nullable: разом з CurrentPriceCents
    PriceFetchedAt    TEXT                    -- nullable: NULL, доки ціну жодного разу не стягнуто
);

CREATE TABLE IF NOT EXISTS OwnedGame (
    PlayerId             INTEGER NOT NULL,
    AppId                INTEGER NOT NULL,
    PlayTimeForever      INTEGER NOT NULL DEFAULT 0,
    PlayTimeLastTwoWeeks INTEGER,             -- nullable: Steam не повертає, якщо не грали останні 2 тижні
    LastPlayed           INTEGER,             -- nullable: unix timestamp, відсутнє якщо не грали ніколи
    FetchedAt            TEXT    NOT NULL,    -- ISO-8601, TTL кешу playtime
    PRIMARY KEY (PlayerId, AppId),
    FOREIGN KEY (PlayerId) REFERENCES Player (SteamId64) ON DELETE CASCADE,
    FOREIGN KEY (AppId)    REFERENCES Game   (AppId)     ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS Achievement (
    AchievementId    INTEGER PRIMARY KEY,     -- сурогат: rowid-аліас, автоінкремент "з коробки"
    AppId            INTEGER NOT NULL,
    ApiName          TEXT    NOT NULL,        -- натуральний ідентифікатор ачівки в межах гри
    DisplayName      TEXT    NOT NULL,
    GlobalPercent    REAL,                    -- nullable: приховані ачівки / невдалий фетч
    PercentFetchedAt TEXT,                    -- nullable
    FOREIGN KEY (AppId) REFERENCES Game (AppId) ON DELETE CASCADE,
    UNIQUE (AppId, ApiName)                   -- для upsert: ON CONFLICT (AppId, ApiName) DO UPDATE ...
);

CREATE TABLE IF NOT EXISTS PlayerAchievement (
    PlayerId      INTEGER NOT NULL,
    AchievementId INTEGER NOT NULL,
    Achieved      INTEGER NOT NULL DEFAULT 0, -- 0/1
    UnlockTime    INTEGER,                    -- nullable: заповнюється лише коли Achieved = 1
    PRIMARY KEY (PlayerId, AchievementId),
    FOREIGN KEY (PlayerId)      REFERENCES Player      (SteamId64)     ON DELETE CASCADE,
    FOREIGN KEY (AchievementId) REFERENCES Achievement (AchievementId) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS Friendship (
    OwnerId  INTEGER NOT NULL,
    FriendId INTEGER NOT NULL,
    PRIMARY KEY (OwnerId, FriendId),
    FOREIGN KEY (OwnerId)  REFERENCES Player (SteamId64) ON DELETE CASCADE,
    FOREIGN KEY (FriendId) REFERENCES Player (SteamId64) ON DELETE CASCADE,
    CHECK (FriendId != OwnerId)
);