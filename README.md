# Sender — Real-time Chat Application

Cross-platform desktop чат-додаток клієнт-серверної архітектури з підтримкою особистих та групових повідомлень.

## Можливості

- Реєстрація та авторизація користувачів (BCrypt хешування паролів)
- Особисті повідомлення в реальному часі
- Групові чати (створення, додавання учасників)
- Управління контактами (додавання, редагування, видалення)
- Чорний список (блокування та розблокування користувачів)
- Збереження історії повідомлень у базі даних

## Технології

| Компонент | Технологія |
|---|---|
| Мова | C# |
| Рамка | .NET 10.0 |
| UI | [Avalonia UI](https://avaloniaui.net/) 12.0.4 (Fluent Theme) |
| База даних | PostgreSQL (Entity Framework Core 10) |
| Мережа | TCP sockets (JSON protocol) |
| Хешування | BCrypt.Net-Next |

## Вимоги

- [.NET 10.0 SDK](https://dotnet.microsoft.com/)
- PostgreSQL

## Налаштування

Конфігурація знаходиться у файлі `appsettings.json`:

```json
{
  "Server": {
    "Host": "127.0.0.1",
    "Port": 5000
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=postgres;Username=anton;Password=00000"
  }
}
```

Змініть параметри підключення до бази даних та адресу сервера відповідно до вашого середовища.

## Запуск

### Підготовка бази даних

```bash
dotnet ef database update
```

### Запуск сервера

```bash
dotnet run server
```

Запускає TCP-сервер на вказаному хості та порті (за замовчуванням `127.0.0.1:5000`).

### Запуск клієнта (GUI)

```bash
dotnet run
```

Відкриває вікно десктопного додатку. Клієнт автоматично підключається до сервера.

### Збірка

```bash
dotnet build
```

### Публікація

```bash
dotnet publish -c Release
```

## Архітектура

```
┌──────────────────────────────────────────────┐
│            ChatServer (TCP :5000)             │
│  Маршрутизація JSON-повідомлень між          │
│  підключеними клієнтами                      │
└──────────┬──────────────────┬────────────────┘
           │ TCP              │ TCP
┌──────────▼────────┐  ┌─────▼──────────────┐
│  Клієнт (User A)  │  │  Клієнт (User B)   │
│  Avalonia UI       │  │  Avalonia UI        │
└──────────┬────────┘  └─────┬──────────────┘
           │                  │
           ▼                  ▼
┌──────────────────────────────────────────────┐
│           PostgreSQL Database                 │
│  Users, Messages, Contacts, Groups,          │
│  GroupMembers                                │
└──────────────────────────────────────────────┘
```

## Протокол

Текстовий JSON-протокол поверх TCP (кожне повідомлення — окремий рядок):

| Тип | Формат |
|---|---|
| Авторизація | `{"type":"Auth","name":"username"}` |
| Особисте повідомлення | `{"type":"Message","from":"alice","to":"bob","text":"hello"}` |
| Групове повідомлення | `{"type":"GroupMessage","from":"alice","groupId":1,"text":"hello"}` |

## Структура проекту

```
├── Program.cs           # Точка входу (режим сервера або клієнта)
├── ChatServer.cs        # TCP-сервер
├── ChatClient.cs        # TCP-клієнт
├── AppContext.cs         # EF Core DbContext
├── GroupService.cs       # Сервіс груп
├── MainWindow.axaml(.cs) # Основне вікно додатку
├── App.axaml(.cs)        # Avalonia Application
├── Models/               # Моделі даних
│   ├── User.cs
│   ├── Message.cs
│   ├── Contact.cs
│   ├── Group.cs
│   └── GroupMember.cs
├── Migrations/           # Міграції БД
└── appsettings.json      # Конфігурація
```
