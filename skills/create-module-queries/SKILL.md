---
description: "Создать ModuleQueries.xml с SQL-запросами для PostgreSQL и MSSQL в модуле Directum RX"
---

# Создание ModuleQueries.xml

Создаёт файл SQL-запросов с вариантами для PostgreSQL и MSSQL.

## Когда использовать
- Нужен сложный SQL (JOIN, агрегация, оконные функции) — не покрывается LINQ
- Нужна DB-специфичная оптимизация
- Работа с временными таблицами, CTE

## Алгоритм

### 1. Создать файл
`{ModuleName}.Server/ModuleQueries.xml`

### 2. Структура файла
```xml
<?xml version="1.0" encoding="utf-8"?>
<Queries xmlns="http://www.sungero.com/queries">
  
  <Query name="GetDealsByPeriod">
    <postgres>
      SELECT d.id, d.name, d.amount, s.name as stage_name
      FROM dirrx_crmsales_deal d
      JOIN dirrx_crmsales_stage s ON d.stage_dirrx = s.id
      WHERE d.created_date_dirrx >= @startDate
        AND d.created_date_dirrx &lt;= @endDate
      ORDER BY d.amount DESC
    </postgres>
    <mssql>
      SELECT d.Id, d.Name, d.Amount, s.Name as StageName
      FROM DirRX_CRMSales_Deal d
      JOIN DirRX_CRMSales_Stage s ON d.Stage_DirRX = s.Id
      WHERE d.CreatedDate_DirRX >= @startDate
        AND d.CreatedDate_DirRX &lt;= @endDate
      ORDER BY d.Amount DESC
    </mssql>
  </Query>

</Queries>
```

### 3. Использование в C#
```csharp
using (var command = SQL.GetCurrentConnection().CreateCommand())
{
    command.CommandText = Queries.Module.GetDealsByPeriod;
    SQL.AddParameter(command, "@startDate", startDate, DbType.DateTime);
    SQL.AddParameter(command, "@endDate", endDate, DbType.DateTime);
    
    using (var reader = command.ExecuteReader())
    {
        while (reader.Read())
        {
            var id = reader.GetInt64(0);
            var name = reader.GetString(1);
            // ...
        }
    }
}
```

### 4. Правила
- ВСЕГДА параметризованные запросы (NpgsqlParameter) — НИКОГДА string.Format
- XML-escaping: `<` → `&lt;`, `>` → `&gt;`, `&` → `&amp;`
- PostgreSQL: snake_case имена таблиц/колонок
- MSSQL: PascalCase имена
- Суффикс `_dirrx` для кастомных колонок (PostgreSQL)

## Reference
- Agile: `ModuleQueries.xml` → GetTicketMoveHistory
- Targets: `DirRX.KPI.Server/ModuleQueries.xml` → GetOlapMetricActualValueData
