# SteamTracker
[![CI](https://github.com/Baranchyck/SteamTracker/actions/workflows/ci.yml/badge.svg)](https://github.com/Baranchyck/SteamTracker/actions/workflows/ci.yml)

Настільний застосунок для Windows, який перетворює дані Steam-акаунту на просту аналітику: які досягнення закрити далі, у що зіграти з друзями і як використовується власна бібліотека.

---

## Проблема і цільові користувачі

Steam зберігає багато даних про гравця, але показує їх сирими списками. Щоб зрозуміти, яке досягнення закрити першим, з ким із друзів зіграти чи які куплені ігри так і не запускались, доводиться порівнювати все вручну.

**Цільові користувачі:**
- гравці зі Steam-бібліотекою, які хочуть бачити, як вони її використовують;
- ачівмент-хантери, яким потрібен пріоритет невиконаних досягнень;
- гравці, які шукають, у що зіграти разом із друзями.

**Мета:** дати простий шар аналітики поверх даних Steam Web API, який відповідає на три питання: у що грати далі, які досягнення взяти наступними і з ким сьогодні зіграти.

---

## Можливості

| Модуль | Опис | Стан |
|---|---|---|
| Вхід через Steam | Steam OpenID у браузері, пароль застосунок не бачить; збереження сесії; вихід | ✅ UI і сервіс готові; справжній вхід чекає `GetPlayerSummaryAsync` |
| Бібліотека | Список ігор, фільтр «ніколи не запускались», сортування | 🟡 сервіс готовий, екран у роботі |
| Планувальник досягнень | Невиконані досягнення, відсортовані за глобальним % отримання | ⏳ заплановано (ядро MVP) |
| Час гри з друзями | Лідерборд за годинами серед друзів | ⏳ заплановано |
| Друзі-радар | Хто онлайн і у що грає | ⏳ заплановано |
| Co-op Matchmaker | Спільні ігри в бібліотеках кількох гравців | ⏳ заплановано |

Детальніше: [специфікація MVP](docs/steam-tracker-doc.md).

---

## Команда

| Учасник | Зона відповідальності |
|---|---|
| [@Baranchyck](https://github.com/Baranchyck) | бекенд: сервіси (`AuthService`, `LibraryService`), Steam OpenID, DI-контейнер, дошка задач |
| [@Yulian-Karpiak](https://github.com/Yulian-Karpiak) | дані й інфраструктура: репозиторії SQLite, конфігурація Steam API ключа, CI, екран бібліотеки |
| [@paaarl](https://github.com/paaarl) | UI: каркас вікна і навігація, вхід, картка профілю, фейкові сервіси; документація архітектури |

---

## Технології

| Шар | Технологія |
|---|---|
| Мова і платформа | C#, .NET 8 |
| Інтерфейс | WPF (XAML) |
| Дані | SQLite, ADO.NET (`Microsoft.Data.Sqlite`) |
| Steam | Steam Web API через `HttpClient`, вхід через Steam OpenID |
| Залежності | `Microsoft.Extensions.DependencyInjection` |
| Кеш | `MemoryCache` (бібліотека ігор, 5 хв), щоб не перевищувати ліміти Steam API |
| Тести | xUnit, Moq |
| CI | GitHub Actions: збірка і тести на кожен push і PR у `main` |

---

## Запуск

### Що потрібно

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Швидкий запуск без Steam (фейкові дані)

Так можна запустити застосунок без Steam API ключа й без інтернету: замість Steam використовуються тестові дані з `SteamTracker.UI/Fakes/Data/`.

1. Склонуй репозиторій:

   ```bash
   git clone https://github.com/Baranchyck/SteamTracker.git
   ```

2. Створи файл `SteamTracker/SteamTracker.UI/appsettings.json`:

   ```json
   { "UseFakes": true }
   ```

3. З кореня репозиторію запусти:

   ```bash
   dotnet run --project SteamTracker/SteamTracker.UI
   ```

Відкриється вікно входу. Після «Увійти через Steam» (≈1,5 с) відкривається головне вікно з тестовим користувачем `test_player`.

Додаткові прапорці фейків в `appsettings.json` (шаблон — [appsettings.example.json](SteamTracker/SteamTracker.UI/appsettings.example.json)):

| Ключ | Що робить |
|---|---|
| `Fakes:HasSavedSession` | `true` — при старті користувач уже залогінений |
| `Fakes:SimulateFailure` | `true` — вхід завершується помилкою |

### Запуск зі справжнім Steam

Потрібен Steam Web API key: https://steamcommunity.com/dev/apikey (потрібен акаунт Steam і будь-який домен, наприклад `localhost`).

**Ключ ніколи не комітимо.** Без ключа (і без `"UseFakes": true`) застосунок при старті покаже помилку «Задай Steam:ApiKey».

**Для розробки — user-secrets.** З папки `SteamTracker/`:

```bash
dotnet user-secrets set "Steam:ApiKey" "<твій ключ>" --project SteamTracker.UI
```

Секрет зберігається поза репозиторієм, у `%APPDATA%\Microsoft\UserSecrets\`.

**Для зібраного `.exe` — `appsettings.json`.** Скопіюй `SteamTracker.UI/appsettings.example.json` в `appsettings.json` поруч із `.exe` (або в `SteamTracker.UI/` перед збіркою), впиши ключ і постав `"UseFakes": false`. `appsettings.json` є в `.gitignore`. Якщо ключ задано в обох місцях, user-secrets мають пріоритет.

### Збірка

```bash
dotnet build SteamTracker/SteamTracker.sln
```

---

## Тестування

```bash
dotnet test SteamTracker/SteamTracker.sln
```

- **Модульні тести** (xUnit) перевіряють бізнес-логіку сервісів (`AuthService`, `LibraryService`), розбір відповідей Steam OpenID і конфігурацію. Залежності (`ISteamApiClient`, репозиторії) підмінюються через Moq, тож тести не ходять у мережу.
- **Інтеграційні тести** репозиторіїв працюють зі справжньою SQLite у тимчасовому файлі.
- **CI** ([ci.yml](.github/workflows/ci.yml)) збирає рішення і проганяє всі тести на `windows-latest` для кожного push і PR у `main`. Steam API ключ для тестів не потрібен.
- **UI** перевіряється вручну на фейкових сервісах (`"UseFakes": true`), де можна відтворити всі стани: успішний вхід, скасування, помилку, збережену сесію.

---

## Архітектура

Шарова архітектура (Clean/Onion): UI не звертається до Steam API чи бази напряму, а лише через сервіси; шари спілкуються через інтерфейси з `Domain`.

```text
┌──────────────────────────────────────────────────────────┐
│                     SteamTracker.UI                      │
│      (WPF / XAML, DI-контейнер, вікна, екрани, фейки)    │
└────────────────────────────┬─────────────────────────────┘
                             │ calls
                             ▼
┌──────────────────────────────────────────────────────────┐
│                  SteamTracker.Services                   │
│              (бізнес-логіка, кеш, конфігурація)          │
└──────────────┬─────────────────────────────┬─────────────┘
               │ uses                        │ uses
               ▼                             ▼
┌─────────────────────────────┐ ┌──────────────────────────┐
│   SteamTracker.DataAccess   │ │ SteamTracker.Integrations│
│   (ADO.NET, репозиторії)    │ │  (Steam Web API, OpenID) │
└──────────────┬──────────────┘ └────────────┬─────────────┘
               │ references                  │ references
               ▼                             ▼
┌──────────────────────────────────────────────────────────┐
│                   SteamTracker.Domain                    │
│          (моделі, інтерфейси, винятки)                   │
└──────────────────────────────────────────────────────────┘
```

Детальніше: [документ архітектури](docs/architecture.md).

### Структура репозиторію

```text
SteamTracker/
├── .github/workflows/ci.yml      # CI: збірка + тести
├── docs/                         # документація проєкту
├── README.md
└── SteamTracker/
    ├── SteamTracker.sln
    ├── SteamTracker.Domain/          # моделі, інтерфейси, винятки
    ├── SteamTracker.Services/        # бізнес-логіка (Auth, Library), конфігурація
    ├── SteamTracker.Integrations/    # Steam Web API (HttpClient), Steam OpenID
    ├── SteamTracker.DataAccess/      # SQLite через ADO.NET, репозиторії
    ├── SteamTracker.UI/              # WPF-клієнт
    │   ├── Windows/                  #   вікна: вхід, головне
    │   ├── Views/                    #   екрани модулів усередині головного вікна
    │   ├── Styles/                   #   кольори, текст, контроли
    │   ├── Fakes/                    #   фейкові сервіси і тестові дані
    │   └── ServiceCollectionExtensions.cs  # реєстрація залежностей (DI)
    └── SteamTracker.Tests/           # xUnit + Moq
```

---

## Документація

| Документ | Що всередині |
|---|---|
| [Специфікація MVP](docs/steam-tracker-doc.md) | стек, MVP, функціональні та нефункціональні вимоги, ризики |
| [Історії користувача](docs/steam-tracker-user-stories.md) | сценарії й критерії приймання |
| [Use Case діаграма](docs/diagrams/use-case-diagram.md) | сценарії використання |
| [Sequence діаграми](docs/diagrams/sequence-diagrams.md) | вхід і кешування профілю, планувальник досягнень |
| [UI Flow](docs/UI_FLOW.md) | стани вікон і переходи між ними |
| [Архітектура](docs/architecture.md) | компоненти, взаємодія, ключові рішення |
| [Модель даних](docs/db-model.md) | ER-діаграма; [SQL-схема](docs/schema.sql) |
| [Презентація](docs/Steam_Tracker.pptx) | проблема, ідея, конкуренти |

## Дизайн

[Макет у Figma](https://www.figma.com/design/6Z4XOLw3wlAcGxuiU5hB24/Steam-Tracker-%E2%80%94-MVP-UI?node-id=0-1&t=vKiXKCWZm9q1LLVx-1)
