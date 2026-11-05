using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;

namespace SteamTracker.UI.Fakes
{
    /// <summary>
    /// Фейкова реалізація IAchievementService: кілька ігор «в процесі» з недосягнутими ачівками прямо в коді.
    /// </summary>
    public class FakeAchievementService : IAchievementService
    {
        private static readonly TimeSpan LoadDelay = TimeSpan.FromMilliseconds(500);

        private static readonly IReadOnlyList<FakeGame> Games = new[]
        {
            new FakeGame(new AchievementGameProgress(900001, "Starfall Tactics", "", 34, 49), new[]
            {
                A("first_step", "Перший крок", "Завершіть навчальну кампанію", 78.4),
                A("collector", "Колекціонер", "Зберіть 50 артефактів", 52.1),
                A("no_scratch", "Без жодної подряпини", "Переможіть боса, не втративши жодного юніта", 31.7),
                A("night_shift", "Нічна зміна", "Виграйте 10 матчів поспіль", 18.9),
                A("tactics_master", "Майстер тактики", "Пройдіть кампанію на складності «Експерт»", 7.3),
                A("explorer", "Дослідник", "Відкрийте всі сектори карти", 44.6),
                A("engineer", "Інженер", "Побудуйте 20 оборонних споруд", 39.2),
                A("diplomat", "Дипломат", "Укладіть мир з усіма фракціями", 26.5),
                A("speedrun", "Блискавка", "Пройдіть місію швидше ніж за 5 хвилин", 12.8),
                A("pacifist", "Пацифіст", "Завершіть місію без жодної атаки", 9.4),
                A("hoarder", "Скарбник", "Накопичте 100 000 кредитів", 22.0),
                A("veteran", "Ветеран", "Зіграйте 100 матчів", 15.1),
                A("perfectionist", "Перфекціоніст", "Отримайте три зірки в усіх місіях", 3.2),
                A("secret_ending", "Інший фінал", null, 5.6),
                A("legacy", "Спадщина", "Досягнення з оновлення 1.4", null)
            }),
            new FakeGame(new AchievementGameProgress(1145360, "Hades", "", 41, 49), new[]
            {
                A("hades_pact", "Пакт покари", "Пройдіть з 32 Жаром", 4.1),
                A("hades_family", "Сімейна справа", "Завершіть основну історію", 21.3),
                A("hades_weapons", "Арсенал", "Відкрийте всі аспекти зброї", 11.7),
                A("hades_keepsakes", "Пам'ятки", "Зберіть усі пам'ятки", 16.2),
                A("hades_fish", "Рибалка", "Впіймайте кожен вид риби", 9.8),
                A("hades_house", "Майстер дому", "Придбайте всі прикраси дому", 6.5),
                A("hades_prophecies", "Пророцтва", "Виконайте всі пророцтва", 3.9),
                A("hades_bond", "Узи", "Максимальна близькість з усіма персонажами", 7.4)
            }),
            new FakeGame(new AchievementGameProgress(367520, "Hollow Knight", "", 40, 46), new[]
            {
                A("hk_grimm", "Трупа Ґрімма", "Пройдіть ритуал до кінця", 27.6),
                A("hk_pantheon", "Пантеон", "Пройдіть Пантеон Халлоунесту", 2.8),
                A("hk_map", "Картограф", "Купіть усі карти", 34.9),
                A("hk_charms", "Талісмани", "Зберіть усі талісмани", 12.5),
                A("hk_steel", "Сталева душа", "Пройдіть гру в режимі сталевої душі", 1.9),
                A("hk_speed", "Спідран", "Пройдіть гру менш ніж за 5 годин", 3.3)
            }),
            new FakeGame(new AchievementGameProgress(504230, "Celeste", "", 30, 32), new[]
            {
                A("celeste_farewell", "Прощання", "Пройдіть розділ 9", 8.7),
                A("celeste_golden", "Золота полуниця", "Пройдіть розділ без смертей", 2.1)
            })
        };

        /// <summary>Який результат повертати: дані, приватний профіль або помилку.</summary>
        public DataStatus Status { get; set; } = DataStatus.Available;

        /// <summary>true — при Status = Available немає жодної гри «в процесі» (стан «Порожньо»).</summary>
        public bool ReturnEmpty { get; set; }

        public async Task<ServiceResult<IReadOnlyList<AchievementGameProgress>>> GetGamesInProgressAsync(CancellationToken ct = default)
        {
            await Task.Delay(LoadDelay, ct).ConfigureAwait(false);

            if (Failure<IReadOnlyList<AchievementGameProgress>>() is { } failure)
            {
                return failure;
            }

            IReadOnlyList<AchievementGameProgress> games = ReturnEmpty
                ? Array.Empty<AchievementGameProgress>()
                : Games.Select(g => g.Progress).ToList();

            return new ServiceResult<IReadOnlyList<AchievementGameProgress>>(games, DataStatus.Available);
        }

        public async Task<ServiceResult<AchievementPlan>> GetPlanAsync(int appId, CancellationToken ct = default)
        {
            await Task.Delay(LoadDelay, ct).ConfigureAwait(false);

            if (Failure<AchievementPlan>() is { } failure)
            {
                return failure;
            }

            var game = Games.FirstOrDefault(g => g.Progress.AppId == appId);
            if (game is null)
            {
                // Як у справжньому сервісі: гра без ачівок — Available з порожнім планом
                return new ServiceResult<AchievementPlan>(
                    new AchievementPlan(appId, 0, 0, Array.Empty<PlannedAchievement>()), DataStatus.Available);
            }

            var plan = new AchievementPlan(appId, game.Progress.Achieved, game.Progress.Total, SortByEase(game.Remaining));
            return new ServiceResult<AchievementPlan>(plan, DataStatus.Available);
        }

        // Та сама формула, що в AchievementService: +N пунктів = N% від усіх ачівок, округлення вгору
        public RoadmapResult BuildRoadmap(AchievementPlan plan, int targetPointsUp)
        {
            int needed = (int)Math.Ceiling(targetPointsUp * plan.Total / 100m);
            var picked = SortByEase(plan.Remaining).Take(Math.Min(needed, plan.Remaining.Count)).ToList();
            var after = plan with { Achieved = plan.Achieved + picked.Count };

            return new RoadmapResult(targetPointsUp, picked, plan.Percent, after.Percent);
        }

        private ServiceResult<T>? Failure<T>() => Status switch
        {
            DataStatus.Private => new ServiceResult<T>(default, DataStatus.Private, "Подробиці ігор у профілі приховані."),
            DataStatus.Error => new ServiceResult<T>(default, DataStatus.Error, "Не вдалося завантажити досягнення."),
            _ => null
        };

        // Найлегші (найбільший глобальний %) — зверху, без % — у кінці
        private static IReadOnlyList<PlannedAchievement> SortByEase(IEnumerable<PlannedAchievement> achievements) =>
            achievements
                .OrderBy(a => a.GlobalPercent is null)
                .ThenByDescending(a => a.GlobalPercent)
                .ThenBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static PlannedAchievement A(string apiName, string name, string? description, double? globalPercent) =>
            new(apiName, name, description, globalPercent);

        private sealed record FakeGame(AchievementGameProgress Progress, IReadOnlyList<PlannedAchievement> Remaining);
    }
}
