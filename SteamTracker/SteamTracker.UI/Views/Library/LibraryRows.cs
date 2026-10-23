namespace SteamTracker.UI.Views.Library
{
    /// <summary>Рядок списку «Усі ігри»: вже відформатовані значення для показу.</summary>
    public record GameRow(string Name, string IconUrl, string LastPlayedText, string HoursText, double BarWidth);

    /// <summary>Рядок списку «Майже не грав».</summary>
    public record BarelyPlayedRow(string Name, string IconUrl, bool NeverPlayed, string MinutesText);
}
