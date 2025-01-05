using System.Data;
using System.Text;
using Dapper;
using InboxPriorityQueue.Context;
using InboxPriorityQueue.Models;
using InboxPriorityQueue.Processors;
using Microsoft.Extensions.Primitives;

namespace InboxPriorityQueue.Manager;

public class InboxWorker
{
    private readonly InboxContext _context;
    private readonly IInboxProcessor _processor;

    private bool _isCycleProcessing;
    private readonly object _sync = new();

    public bool IsCycleProcessing => _isCycleProcessing;

    public InboxWorker(InboxContext context, IInboxProcessor processor)
    {
        _context = context;
        _processor = processor;
    }

    public Task<int> AddOrUpdateInboxItemAsync(string value, Priority priority = Priority.Low,
        CancellationToken cancellationToken = default)
        => AddOrUpdateInboxItemsAsync([value], priority, cancellationToken);

    public async Task<int> AddOrUpdateInboxItemsAsync(string[] values, Priority priority = Priority.Low,
        CancellationToken cancellationToken = default)
    {
        if (values is null || values.Length == 0)
            return 0;
        
        using var db = _context.OpenConnection();
        return await db.ExecuteAsync(QueryBuilder.GetAddOrUpdateQuery(values, priority), cancellationToken);
    }

    /// <summary>
    /// Запуск цикличного потребления очереди воркером
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task CycleProcessingAsync(CancellationToken cancellationToken = default)
    {
        if (!TryStartCycleProcessing())
            return;
        try
        {
            var result = true;
            while (result)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result = await ProcessAsync(cancellationToken);
            }
        }
        finally
        {
            _isCycleProcessing = false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<bool> IsEmptyQueueAsync(CancellationToken cancellationToken = default)
    {
        using var db = _context.OpenConnection();

        var item = await db.QueryFirstOrDefaultAsync<InboxItem>(QueryBuilder.GetFirstPending, cancellationToken);
        return item is null;
    }

    /// <summary>
    /// Забирает элемент из очереди (переводя в статус Progress) - выполняет действие процессора.
    /// В случае успешного выполнения действия процессора - переводит в статус Done, иначе возвращает элемент в очередь
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>Возвращает false - если очередь пуста, иначе - true</returns>
    public async Task<bool> ProcessAsync(CancellationToken cancellationToken = default)
    {
        var item = await DequeueOrNullAsync(cancellationToken);
        if (item is null)
            return false;
        
        await SetCompletedAsync(item, await _processor.Process(item.Item, cancellationToken), cancellationToken);
        return true;
    }

    private bool TryStartCycleProcessing()
    {
        if (_isCycleProcessing)
            return false;
        lock (_sync)
        {
            if (_isCycleProcessing)
                return false;
            _isCycleProcessing = true;
            return true;
        }
    }

    private async Task<InboxItem?> DequeueOrNullAsync(CancellationToken cancellationToken = default)
    {
        using var db = _context.OpenConnection();
        using var transaction = db.BeginTransaction(IsolationLevel.RepeatableRead);
        var item = await db.QueryFirstOrDefaultAsync<InboxItem>(QueryBuilder.GetFirstPending, cancellationToken);

        if (item is null)
            return null;

        await db.ExecuteAsync(QueryBuilder.SetProgressStatus, item);
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return item;
    }

    private async Task<bool> SetCompletedAsync(InboxItem inboxItem, bool isSuccessProcessing, CancellationToken cancellationToken = default)
    {
        using var db = _context.OpenConnection();
        using var transaction = db.BeginTransaction(IsolationLevel.RepeatableRead);
        var item = await db.QueryFirstOrDefaultAsync<InboxItem>(QueryBuilder.GetInProgressById, inboxItem);

        if (item is null)
            return false;

        await db.ExecuteAsync(isSuccessProcessing ? QueryBuilder.SetStatusDone : QueryBuilder.RollbackStatusQuery, inboxItem);
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return true;
    }
}