using System.Windows.Media;

namespace SteamTracker.UI.Views.Achievements
{
    /// <summary>Пункт вибору гри: назва і скільки ачівок уже закрито.</summary>
    public record GameOption(int AppId, string Title)
    {
        // Шаблон SortComboBox показує вибраний пункт через ToString()
        public override string ToString() => Title;
    }

    /// <summary>Рядок списку «Незакриті досягнення»: вже відформатовані значення для показу.</summary>
    /// <param name="InRoadmap">true — ачівка входить у план калькулятора цілі, рядок підсвічується.</param>
    public record AchievementRow(
        string Name,
        string Description,
        string PercentText,
        double BarWidth,
        Brush BarBrush,
        bool InRoadmap);
}
