# SteamTracker
[![CI](https://github.com/Baranchyck/SteamTracker/actions/workflows/ci.yml/badge.svg)](https://github.com/Baranchyck/SteamTracker/actions/workflows/ci.yml)

.NET desktop app for Steam library analytics — achievements, friends comparison, co-op matchmaker, price-per-hour.

---

## ✨ Features

* **🔑 Steam OpenID Authentication** — Secure profile synchronization and graceful private account handling.
* **🏆 Achievement Planner** — Optimal progression paths, completionist roadmaps, and difficulty sorting.
* **📊 Friend Comparisons** — Playtime leaderboards and activity metrics relative to friends.
* **📡 Friends Radar** — Real-time tracking of current game sessions and online availability.
* **🤝 Co-op Matchmaker** — Multi-library intersection analyzer to pick shared co-op titles effortlessly.
* **💰 Cost-Per-Hour Analytics** — Value-for-money metrics calculating price-per-hour efficiency.

---

### ⚙️ Technical Highlights
* **Storage**: Embedded **SQLite** database for zero-configuration local usage (user cache, tracked entities, app settings).
* **Performance**: In-memory caching via `.NET` native structures (`MemoryCache`) to respect Steam API rate limits.
* **Quality Assurance**: Automated testing with **xUnit** and **Moq**.
* **DevOps**: Continuous Integration workflows configured with **GitHub Actions**.
* **Distribution**: Standalone desktop executable (`.exe`).

---

## 🔑 Steam API key

Застосунку потрібен Steam Web API key. Отримати його можна тут: https://steamcommunity.com/dev/apikey (потрібен акаунт Steam і будь-який домен, напр. `localhost`).

**Ключ ніколи не комітимо.** Без ключа застосунок при старті покаже помилку «Задай Steam:ApiKey».

### Для розробки (user-secrets)

З папки `SteamTracker/`:

```bash
dotnet user-secrets set "Steam:ApiKey" "<твій ключ>" --project SteamTracker.UI
```

Секрет зберігається поза репозиторієм, у `%APPDATA%\Microsoft\UserSecrets\`.

### Для зібраного `.exe` (appsettings.json)

User-secrets працюють лише на машині розробника. Для `.exe` скопіюй `SteamTracker.UI/appsettings.example.json` в `appsettings.json` поруч із `.exe` (або в `SteamTracker.UI/` перед збіркою) і впиши свій ключ. `appsettings.json` є в `.gitignore`.

Якщо ключ задано в обох місцях, user-secrets мають пріоритет.

---

## 🏛 System Architecture & Solution Structure

The project follows a modular, layered architecture inspired by Clean/Onion principles, ensuring strict separation of concerns, testability, and maintainability.

---

## Database structure

- [Модель даних (ER-діаграма, dbdiagram.io)](https://dbdiagram.io/d/твій-id-діаграми)
- [SQL DDL](docs/schema.sql)

---

### 📐 High-Level Architecture & Dependency Flow

```text
┌──────────────────────────────────────────────────────────┐
│                     SteamTracker.UI                      │
│             (Presentation Layer: WPF / XAML)             │
└────────────────────────────┬─────────────────────────────┘
                             │ calls
                             ▼
┌──────────────────────────────────────────────────────────┐
│                  SteamTracker.Services                   │
│         (Application & Business Logic Layer)             │
└──────────────┬─────────────────────────────┬─────────────┘
               │ uses                        │ uses
               ▼                             ▼
┌─────────────────────────────┐ ┌──────────────────────────┐
│   SteamTracker.DataAccess   │ │ SteamTracker.Integrations│
│   (ADO.NET, Repositories)   │ │  (Steam Web API Client)  │
└──────────────┬──────────────┘ └────────────┬─────────────┘
               │ references                  │ references
               ▼                             ▼
┌──────────────────────────────────────────────────────────┐
│                   SteamTracker.Domain                    │
│      (Entities, Value Objects, Interfaces, Contracts)    │
└──────────────────────────────────────────────────────────┘
```

---

### 📂 Repository Tree

```text
SteamTracker/
│
├── .gitignore
├── README.md
└── SteamTracker/
    ├── SteamTracker.sln
    ├── SteamTracker.Domain/          # Core entities and abstractions
    ├── SteamTracker.Services/        # Business logic and caching
    ├── SteamTracker.Integrations/    # Steam Web API wrapper (HttpClient)
    ├── SteamTracker.DataAccess/      # ADO.NET database operations
    ├── SteamTracker.UI/              # Desktop client (WPF/WinForms)
    └── SteamTracker.Tests/           # xUnit & Moq test suite
```
## 🎨 Design

[View Figma mockup](https://www.figma.com/design/6Z4XOLw3wlAcGxuiU5hB24/Steam-Tracker-%E2%80%94-MVP-UI?node-id=0-1&t=vKiXKCWZm9q1LLVx-1)

