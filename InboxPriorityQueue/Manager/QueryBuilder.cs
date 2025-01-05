using InboxPriorityQueue.Models;

namespace InboxPriorityQueue.Manager;

public static class QueryBuilder
{
    /// <summary>
    /// Ищем первый элемент у которого приоритет не Ignored и статус Pending
    /// Order by по приоритету, в теории, должен ускорять запрос, так как приоритет сортированный индекс
    /// skip locked - игнорируем заблокированные записи (в процессе перевода статуса)
    /// </summary>
    public const string GetFirstPending = @"select * from ""InboxItems""
            where ""Priority"" != 0 and ""Status"" = 2
            order by ""Priority"" desc
            limit 1
            for update skip locked";

    /// <summary>
    /// Генерация запроса для постановки в очередь
    /// </summary>
    /// <param name="values">значения</param>
    /// <param name="priority">приоритет</param>
    /// <returns></returns>
    public static string GetAddOrUpdateQuery(string[] values, Priority priority)
    {
        /// <summary>
        /// при конфликте (если элемент уже присутствует в очереди) - устанавливаем указанный приоритет и статус
        /// если Status - 0 - DONE
        /// или если Status = 2 - PENDING и имеющийся приоритет ниже
        /// </summary>
        const string OnConflict =
            @"on conflict (""ItemHash"", ""Item"") do update set ""Priority"" = excluded.""Priority"", ""Status"" = excluded.""Status""
        where ""InboxItems"".""Status"" = 0 
        or (""InboxItems"".""Status"" = 2 and ""InboxItems"".""Priority"" < excluded.""Priority"")";

        return @$"insert into ""InboxItems"" (""Item"", ""Status"", ""Priority"") values {string.Join(',',
            values
                .Distinct()
                .Select(value => $"('{value}', {(short)Status.Pending}, {(short)priority})"))} {OnConflict}";
    }

    /// <summary>
    /// выставляем для переданного ID status - Progress, приоритет - Ignore (чтобы избегать при поиске ожидающих)
    /// </summary>
    public const string SetProgressStatus = @"Update ""InboxItems"" set ""Status"" = 1,
                          ""Priority"" = 0                   
                      where ""Id"" = @Id";

    /// <summary>
    /// получаем запись по Id, при условии, что Status = Progress
    /// </summary>
    public const string GetInProgressById = @"select * from ""InboxItems""
            where ""Id"" = @Id and ""Status"" = 1           
            limit 1";

    /// <summary>
    /// Устанавливает статус в Done, а приоритет в Ingnore
    /// </summary>
    public const string SetStatusDone = @"Update ""InboxItems"" set ""Status"" = 0,
                          ""Priority"" = 0                 
                      where ""Id"" = @Id";

    /// <summary>
    /// Устанавливает статус и приоритет в исходное значение
    /// </summary>
    public const string RollbackStatusQuery = @"Update ""InboxItems"" set ""Status"" = @Status,
                          ""Priority"" = @Priority                      
                      where ""Id"" = @Id";

    /// <summary>
    /// Получаем все Id записей в статусе Progress
    /// </summary>
    public const string GetProgressIds = @"select ""Id"" from ""InboxItems"" where ""Status"" = 1";

    /// <summary>
    /// Удаляет записи со статусом Done
    /// </summary>
    public const string DeleteDoneItems = @"delete from ""InboxItems"" where ""Status"" = 0";

    /// <summary>
    /// Возвращает указанные записи в очередь с максимальным приоритетом
    /// </summary>
    /// <param name="ids">id записей</param>
    /// <returns></returns>
    public static string ReturnZombieToQueue(IEnumerable<int> ids) =>
        @$"Update ""InboxItems"" set ""Status"" = 2,
                          ""Priority"" = 3
                      where ""Id"" in ({string.Join(',', ids.Select(id => $"{id}"))})";
}