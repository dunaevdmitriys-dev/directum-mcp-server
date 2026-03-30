using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DirectumMcp.RuntimeTools.Prompts;

[McpServerPromptType]
public class RuntimePrompts
{
    [McpServerPrompt, Description("Анализ загрузки подразделения: задания, просрочки, bottleneck, рекомендации.")]
    public static IEnumerable<PromptMessage> AnalyzeWorkload(
        [Description("Название или ID подразделения")] string department)
    {
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock
            {
                Text = $"""
                    Проанализируй загрузку подразделения: {department}

                    Прочитай ресурсы:
                    - directum-rx://knowledge/analytics-patterns
                    - directum-rx://knowledge/task-workflow

                    ## Шаги анализа:
                    1. team_workload — получи загрузку каждого сотрудника
                    2. overdue_report — просроченные задания подразделения
                    3. bottleneck_detect — найди узкие места
                    4. pending_approvals — зависшие согласования

                    ## Выходной отчёт:
                    - Таблица: Сотрудник | В работе | Просрочено | % просрочки
                    - Топ-3 bottleneck'а с рекомендациями
                    - Зависшие согласования (>3 дней без действия)
                    - Общая оценка загрузки: Норма / Перегрузка / Критично
                    """
            }
        };
    }

    [McpServerPrompt, Description("Расследование просрочек: причины, ответственные, рекомендации по исправлению.")]
    public static IEnumerable<PromptMessage> InvestigateOverdue(
        [Description("Период анализа (например: '7 дней' или '2024-01-01..2024-01-31')")] string period = "7 дней")
    {
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock
            {
                Text = $"""
                    Расследуй просрочки за период: {period}

                    Прочитай: directum-rx://knowledge/analytics-patterns

                    ## Шаги:
                    1. overdue_report — все просроченные задания
                    2. Для каждого просроченного: summarize — получи контекст задачи
                    3. process_stats — статистика по типам задач
                    4. deadline_risk — задания с рисками просрочки в ближайшие дни

                    ## Выходной отчёт:
                    - Количество просрочек по подразделениям
                    - Топ-5 самых длительных просрочек с описанием
                    - Паттерны: какие типы задач чаще просрочиваются
                    - Рекомендации: перераспределение, эскалация, изменение сроков
                    """
            }
        };
    }

    [McpServerPrompt, Description("Найти и обработать документы: поиск, массовое выполнение, формирование реестра.")]
    public static IEnumerable<PromptMessage> ProcessDocuments(
        [Description("Что нужно найти/обработать (например: 'все договоры с просроченными дедлайнами')")] string query)
    {
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock
            {
                Text = $"""
                    Обработай документы: {query}

                    Прочитай: directum-rx://knowledge/document-operations

                    ## Шаги:
                    1. search или find_docs — найди документы по критерию
                    2. Для каждого: summarize — краткое содержание
                    3. Сформируй реестр (таблица: Название | Автор | Дата | Статус)
                    4. Предложи действия: завершить, продлить, переназначить

                    ВАЖНО: перед массовыми операциями (bulk_complete) — покажи список и подожди подтверждения!
                    """
            }
        };
    }

    [McpServerPrompt, Description("Быстрый дашборд: ключевые метрики за сегодня/неделю.")]
    public static IEnumerable<PromptMessage> QuickDashboard(
        [Description("Период: today, week, month")] string period = "today")
    {
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock
            {
                Text = $"""
                    Покажи дашборд за: {period}

                    Прочитай: directum-rx://knowledge/analytics-patterns

                    ## Метрики:
                    1. my_tasks — мои задания (в работе, новые)
                    2. pending_approvals — ожидающие согласования
                    3. deadline_risk — рисковые дедлайны
                    4. process_stats — статистика процессов

                    ## Формат:
                    - Задания: X в работе, Y новых, Z просроченных
                    - Согласования: X ожидают
                    - Риски: X заданий с дедлайном в ближайшие 2 дня
                    - Совет дня: что сделать в первую очередь
                    """
            }
        };
    }
}
