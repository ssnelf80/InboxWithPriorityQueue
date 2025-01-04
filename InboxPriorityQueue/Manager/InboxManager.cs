using System.Data;
using System.Text;
using Dapper;
using InboxPriorityQueue.Context;
using InboxPriorityQueue.Models;
using InboxPriorityQueue.Processors;
using Microsoft.Extensions.Primitives;

namespace InboxPriorityQueue.Manager;

public class InboxManager
{
    private readonly InboxContext _context;
    private readonly IInboxProcessor _processor;

    private bool _isCycleProcessing;
    private readonly object _sync = new();

    public bool IsCycleProcessing => _isCycleProcessing;

    public InboxManager(InboxContext context, IInboxProcessor processor)
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
        var sb = new StringBuilder();
        sb.Append(@"insert into ""InboxItems"" (""Item"", ""Status"", ""Priority"") values ");
        foreach (var value in values.Distinct())
            sb.Append(@$"('{value}', {(short)Status.Pending}, {(short)priority}),");
        sb.Length--;
        sb.Append(
            @$" on conflict (""ItemHash"", ""Item"") do update set ""Priority"" = excluded.""Priority"", ""Status"" = excluded.""Status""
        where ""InboxItems"".""Status"" = {(short)Status.Done} 
        or (""InboxItems"".""Status"" = {(short)Status.Pending} and ""InboxItems"".""Priority"" < excluded.""Priority"")"
            );

        return await db.ExecuteAsync(sb.ToString(), cancellationToken);
    }

    public async Task CycleProcessingAsync(CancellationToken cancellationToken = default)
    {
        if (!TryStartCycleProcessing())
            return;
        try
        {
            var result = ProcessResult.Success;
            while (result != ProcessResult.EmptyQueue)
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

    public async Task<bool> IsEmptyQueueAsync(CancellationToken cancellationToken = default)
    {
        using var db = _context.OpenConnection();
        var item = await db.QueryFirstOrDefaultAsync<InboxItem>(
            @$"select * from ""InboxItems""
            where ""Priority"" != {(short)Priority.Ignore} and ""Status"" = {(short)Status.Pending}
            order by ""Priority"" desc
            limit 1
            for update skip locked"
            , cancellationToken);
        return item is null;
    }

    public async Task<ProcessResult> ProcessAsync(CancellationToken cancellationToken = default)
    {
        var item = await DequeueOrNullAsync(cancellationToken);
        if (item is null)
            return ProcessResult.EmptyQueue;
        var result = await _processor.Process(item.Value, cancellationToken);
        if (result)
        {
            await SetCompletedAsync(item.Id, cancellationToken);
            return ProcessResult.Success;
        }
        else
        {
            await ReturnToQueryAsync(item, cancellationToken);
            return ProcessResult.Failure;
        }
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
        var item = await db.QueryFirstOrDefaultAsync<InboxItem>(
            @$"select * from ""InboxItems""
            where ""Priority"" != {(short)Priority.Ignore} and ""Status"" = {(short)Status.Pending}
            order by ""Priority"" desc
            limit 1
            for update skip locked"
            , cancellationToken);

        if (item is null)
            return null;

        var changeStatusQuery = @$"Update ""InboxItems"" set ""Status"" = {(short)Status.Progress},
                          ""Priority"" = {(short)Priority.Ignore}                      
                      where ""Id"" = @Id";

        await db.ExecuteAsync(changeStatusQuery, item);
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return item;
    }

    private async Task<bool> SetCompletedAsync(int Id, CancellationToken cancellationToken = default)
    {
        using var db = _context.OpenConnection();
        using var transaction = db.BeginTransaction(IsolationLevel.RepeatableRead);
        var item = await db.QueryFirstOrDefaultAsync<InboxItem>(
            @$"select * from ""InboxItems""
            where ""Id"" = {Id} and ""Status"" = {(short)Status.Progress}           
            limit 1"
            , cancellationToken);

        if (item is null)
            return false;

        var changeStatusQuery = @$"Update ""InboxItems"" set ""Status"" = {(short)Status.Done},
                          ""Priority"" = {(short)Priority.Ignore}                      
                      where ""Id"" = @Id";

        await db.ExecuteAsync(changeStatusQuery, item);
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return true;
    }

    private async Task<bool> ReturnToQueryAsync(InboxItem inboxItem, CancellationToken cancellationToken = default)
    {
        using var db = _context.OpenConnection();
        using var transaction = db.BeginTransaction(IsolationLevel.RepeatableRead);
        var item = await db.QueryFirstOrDefaultAsync<InboxItem>(
            @$"select * from ""InboxItems""
            where ""Id"" = {inboxItem.Id} and ""Status"" = {(short)Status.Progress}           
            limit 1"
            , cancellationToken);

        if (item is null)
            return false;

        var changeStatusQuery = @$"Update ""InboxItems"" set ""Status"" = {(short)inboxItem.Status},
                          ""Priority"" = {(short)inboxItem.Priority}                      
                      where ""Id"" = @Id";

        await db.ExecuteAsync(changeStatusQuery, item);
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return true;
    }
}