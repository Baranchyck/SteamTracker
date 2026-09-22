# Use Case Diagram — основні сценарії користувача

Діаграма показує ключові сценарії взаємодії гравця із SteamTracker.
Усі сценарії передбачають попередню авторизацію (UC-1) та взаємодію
із зовнішньою системою Steam Web API.

```mermaid
flowchart LR
    Player([fa:fa-user Гравець])
    Steam([fa:fa-server Steam Web API])

    subgraph SteamTracker ["Десктопний додаток SteamTracker"]
        UC1(["UC-1: Авторизуватися через Steam"])
        UC2(["UC-2: Переглянути профіль"])
        UC3(["UC-3: Achievement Planner"])
        UC4(["UC-4: Порівняти час гри з друзями"])
        UC5(["UC-5: Друзі-радар"])
        UC6(["UC-6: Co-op Matchmaker"])
        UC7(["UC-7: Ціна за годину"])
    end

    Player --- UC1
    Player --- UC2
    Player --- UC3
    Player --- UC4
    Player --- UC5
    Player --- UC6
    Player --- UC7

    UC2 -.->|include| UC1
    UC3 -.->|include| UC1
    UC4 -.->|include| UC1
    UC5 -.->|include| UC1
    UC6 -.->|include| UC1
    UC7 -.->|include| UC1

    UC1 --- Steam
    UC2 --- Steam
    UC3 --- Steam
    UC4 --- Steam
    UC5 --- Steam
    UC6 --- Steam
    UC7 --- Steam
```

## Опис сценаріїв

| Use Case | Опис |
|---|---|
| UC-1: Авторизуватися через Steam | Steam OpenID через системний браузер + локальний HttpListener, збереження SteamID64 локально |
| UC-2: Переглянути профіль | Нік, аватар, статус приватності через `GetPlayerSummaries` |
| UC-3: Achievement Planner | Roadmap до 100% ачівок із сортуванням за глобальним % виконання |
| UC-4: Порівняти час гри з друзями | Рейтинг playtime серед друзів по обраній грі |
| UC-5: Друзі-радар | Список друзів онлайн/у грі зараз |
| UC-6: Co-op Matchmaker | Перетин бібліотек кількох гравців |
| UC-7: Ціна за годину | Розрахунок $/год на основі playtime і поточної ціни в Steam Store |

## Примітки

- Усі сценарії (UC-2 – UC-7) включають UC-1 як обов'язкову передумову — без авторизації жоден інший сценарій недоступний.
- Steam Web API — зовнішня система (non-human actor), з якою взаємодіє кожен сценарій для отримання даних.