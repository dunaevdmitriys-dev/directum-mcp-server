---
description: "Генерация тестовых данных для любых сущностей Directum RX через прямые SQL INSERT в PostgreSQL"
---

# Generate Test Data — Генерация тестовых данных

Генерирует реалистичные тестовые данные для указанных сущностей Directum RX, вставляя записи напрямую в PostgreSQL через C# консольное приложение (Npgsql).

> ⚠️ **ОБЯЗАТЕЛЬНО перед INSERT**: определи имя таблицы через SQL, НЕ угадывай формулу:
> ```sql
> SELECT tablename FROM pg_tables WHERE tablename ILIKE '%deal%';
> ```
> Или через MCP: `map_db_schema entity=Deal`
>
> **Status** может быть числовым enum (discriminator), не строкой 'Active'. Проверь: `\d {tablename}`
> Многие таблицы RX содержат колонку `entitytype` (GUID типа). Проверь схему перед INSERT.

## Входные данные

Спроси у пользователя (если не указано):
- **entity_type** — тип сущности (Deal, Lead, Counterparty, Task, etc.)
- **module_path** — путь к модулю (где лежит `source/` с .mtd файлами)
- **count** — количество записей (по умолчанию 20)
- **connection_string** — строка подключения PostgreSQL (или определи из конфига стенда)
- **employee_login** — логин сотрудника-владельца данных (для полей Responsible, Author и т.д.)
- **distribution** — тип распределения данных: `uniform` (равномерное), `funnel` (воронка — убывающее), `random`

## Алгоритм

### 1. Исследуй структуру сущности

Прочитай `.mtd` файл сущности и определи:
```
- Имя таблицы БД (из NameGuid + discriminator)
- Все свойства с их типами (string, int, long, DateTime, NavigationProperty, enum)
- Обязательные поля (IsRequired: true)
- Навигационные свойства (FK на другие сущности)
- Enum-значения (для статусов, типов и т.д.)
- Discriminator GUID сущности
```

**Как определить имя таблицы:**
- Формат: `{company}_{module}_{entity}` в нижнем регистре
- Пример: `DirRX.CRMSales.Deal` → `dirrx_crmsale_deal`
- Модуль может укорачиваться (CRMSales → crmsale, CRMMarketing → crmmrktg)
- Лучше проверить через SQL: `SELECT tablename FROM pg_tables WHERE tablename LIKE '%deal%'`

**Как определить discriminator:**
- Это `NameGuid` из `.mtd` файла верхнего уровня
- Пример: `"NameGuid": "a7f05f7d-19a3-4733-9432-1eb0ff68b56d"` → discriminator для Deal

### 2. Исследуй зависимости

Определи какие связанные данные нужны:
```sql
-- Найди существующие записи для FK-полей
SELECT id, name FROM {related_table} WHERE status = 'Active' LIMIT 20;

-- Найди ID сотрудника по логину
SELECT r.id FROM sungero_core_recipient r
JOIN sungero_core_login l ON r.login = l.id
WHERE l.loginname = '{login}' AND r.discriminator = 'b7905516-2be5-4931-961c-cb38d5677565'
LIMIT 1;
```

### 3. Определи стратегию генерации ID

```sql
-- Найди максимальный ID по всем затронутым таблицам
SELECT COALESCE(MAX(id), 0) FROM {table};
-- Начни с MAX + 100 или минимум 50000 для безопасности
```

**ВАЖНО:** НЕ используй sequences (они могут не существовать). Всегда используй `MAX(id) + offset`.

### 4. Сгенерируй C# программу

Создай .NET Console App с Npgsql:

```
Путь: /tmp/test_data_gen/TestDataGen/
```

**Структура Program.cs:**

```csharp
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;

try
{
    var connStr = "{connection_string}";
    using var conn = new NpgsqlConnection(connStr);
    conn.Open();

    // Helpers
    int Exec(string sql)
    {
        using var c = new NpgsqlCommand(sql, conn);
        return c.ExecuteNonQuery();
    }

    long GetMax(string table)
    {
        using var c = new NpgsqlCommand($"SELECT COALESCE(MAX(id), 0) FROM {table}", conn);
        return (long)c.ExecuteScalar()!;
    }

    // 1. Очистка предыдущих тестовых данных (опционально)
    // Exec("DELETE FROM {table} WHERE id >= 50000");

    // 2. Определение начального ID
    long nextId = Math.Max(GetMax("{table}") + 100, 50000);

    // 3. Получение зависимостей
    long employeeId = ...;  // через SELECT по логину
    var relatedIds = new List<long>(); // FK записи

    // 4. Генерация данных
    var rng = new Random(42); // фиксированный seed для воспроизводимости
    var data = new[] { ... }; // массив тестовых данных

    foreach (var item in data)
    {
        var id = nextId++;
        Exec($@"INSERT INTO {table}
            (id, discriminator, entityversion, status, ...)
            VALUES ({id}, '{discriminator}'::uuid, 1, 'Active', ...)");
        Console.WriteLine($"  Created: {item.Name} (ID={id})");
    }

    // 5. Сводка
    Console.WriteLine($"\nDONE! Created {data.Length} records.");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
}
```

### 5. Правила генерации данных

**Обязательные поля для ВСЕХ сущностей:**
```sql
id              -- long, уникальный
discriminator   -- uuid, GUID типа сущности (из .mtd NameGuid)
entityversion   -- int, всегда 1
status          -- varchar, обычно 'Active'
```

**Типы данных:**
| Тип в .mtd | Тип в SQL | Генерация |
|------------|-----------|-----------|
| string | varchar | Реалистичные русские названия |
| int / long | integer / bigint | Числовые значения по контексту |
| DateTime | timestamp | Даты в формате 'yyyy-MM-dd' |
| double | double precision | Суммы, проценты |
| bool | boolean | true/false (НЕ True/False!) |
| NavigationProperty | bigint (FK) | ID существующей записи |
| Enumeration | varchar | Значение из enum (код .mtd) |

**Правила для строк в SQL:**
- Экранировать апострофы: `name.Replace("'", "''")`
- NULL значения: `"NULL"` без кавычек, не `'NULL'`
- Boolean: `true`/`false` строчными (PostgreSQL), НЕ `True`/`False` (C#)

**Распределения:**
- `uniform`: равное количество записей по категориям
- `funnel`: убывающее (7, 4, 3, 3, 2) — для стадий воронки, статусов
- `random`: случайное с seed=42

**Реалистичность данных:**
- Используй русские названия компаний, проектов, описаний
- Даты: разбрасывай создание за последние 30-60 дней
- Суммы: реалистичные (100K-1M для сделок)
- Для "срочных" записей — дедлайны в ближайшие 1-7 дней
- Для "просроченных" — дедлайны в прошлом

### 6. Запусти и проверь

```bash
cd /tmp/test_data_gen/TestDataGen
dotnet run
```

### 7. Верификация

После генерации выведи сводку:
```sql
SELECT '{entity}', COUNT(*),
       COUNT(CASE WHEN id >= 50000 THEN 1 END) as test_records
FROM {table} WHERE status = 'Active';
```

## Примеры тестовых данных по типам сущностей

### Сделки (Deal)
```
Внедрение CRM для Альфа-Банка, 350000, стадия: Первый контакт
Поставка ПО для Сбербанка, 280000, стадия: Квалификация
Консалтинг ИБ для Газпрома, 150000, стадия: Предложение
```

### Лиды (Lead)
```
Запрос на демо от ПАО Лукойл, канал: Сайт, статус: Новый
Входящий звонок от ООО Ромашка, канал: Телефон, статус: В работе
```

### Контрагенты (Counterparty)
```
ООО "Альфа Технологии", ИНН: 7712345678, тип: Юр.лицо
ИП Сидоров А.В., ИНН: 771234567890, тип: ИП
```

### Задачи/Задания
```
Согласование договора №123, срок: +3 дня, приоритет: Высокий
Подготовка отчёта за Q1, срок: +7 дней, приоритет: Обычный
```

## Проект .csproj (шаблон)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Npgsql" Version="8.0.0" />
  </ItemGroup>
</Project>
```

## Валидация

- [ ] Все ID уникальны и не конфликтуют с существующими
- [ ] Discriminator корректен (из .mtd NameGuid)
- [ ] FK-ссылки указывают на существующие записи
- [ ] Boolean значения: `true`/`false` (не `True`/`False`)
- [ ] NULL значения без кавычек
- [ ] Строки с апострофами экранированы
- [ ] Даты в формате PostgreSQL ('yyyy-MM-dd')
- [ ] Программа выводит сводку с количеством созданных записей

## Типичные ошибки и решения

| Ошибка | Причина | Решение |
|--------|---------|---------|
| duplicate key | ID уже существует | Начинай с MAX(id) + 100 |
| violates foreign key | FK на несуществующую запись | Проверь SELECT перед INSERT |
| column does not exist | Неверное имя колонки | Проверь `\d {table}` в psql |
| invalid input for uuid | GUID без ::uuid | Добавь `'{guid}'::uuid` |
| sequence not found | Нет sequence для таблицы | Не используй sequences, используй MAX(id) |

## Определение строки подключения

Если не указана, ищи в конфиге стенда:
```bash
# Из _ConfigSettings.xml
# Из Docker PostgreSQL (стандартное подключение):
# Host=localhost;Port=5432;Database=directum;Username=directum;Password=directum

# Или из конфига launcher:
grep -r "ConnectionString" 26.1.20260320.1846/DirectumLauncher/etc/_services_config/
```

Стандартный формат: `Host={host};Port=5432;Database={db};Username={user};Password={pass}`

