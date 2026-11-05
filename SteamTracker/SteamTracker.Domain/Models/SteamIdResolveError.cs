namespace SteamTracker.Domain.Models;

public enum SteamIdResolveError
{
    Empty,           // порожній рядок або самі пробіли
    TooLong,         // довший за допустиму межу — сміття, не відправляємо в Steam
    InvalidFormat,   // не схоже ні на SteamID64, ні на посилання, ні на нік
    InvalidSteamId,  // 17 цифр, але не індивідуальний акаунт (без префікса 7656119)
    ForeignHost,     // посилання не на steamcommunity.com
    NotFound,        // vanity-ніку не існує
    SteamUnavailable // Steam не відповів — ввід міг бути правильним
}
