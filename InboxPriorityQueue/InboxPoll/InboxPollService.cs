using InboxPriorityQueue.Context;
using InboxPriorityQueue.Manager;
using InboxPriorityQueue.Processors;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace InboxPriorityQueue.InboxPoll;

public class InboxPollService : IHostedService, IDisposable, IAsyncDisposable
{
    private readonly Timer _timerPoll;
    private readonly Timer _timerCleanUp;
    private readonly int _pollDelayMs;
    private readonly int _cleanUpDelayMs;
    private readonly int _workerCount;

    private readonly InboxWorker[] _inboxWorkers;
    private readonly HashSet<int> _previousProcesses = new();
    
    public InboxPollService(IOptions<InboxPollConfiguration> configOptions, InboxContext context, IInboxProcessor inboxProcessor)
    {
        var config = configOptions.Value;
        _pollDelayMs = config.PollIntervalMs;
        _cleanUpDelayMs = config.CleanUpIntervalMs;
        _inboxWorkers = new InboxWorker[config.WorkerCount];
        
        for (var i = 0; i < _inboxWorkers.Length; i++)
            _inboxWorkers[i] = new InboxWorker(context, inboxProcessor);
        
        _timerPoll = new Timer(PollCallback, null, Timeout.Infinite, Timeout.Infinite);
        _timerCleanUp = new Timer(CleanUpCallback, null, Timeout.Infinite, Timeout.Infinite);
    }
    private void PollCallback(object? state)
    {
        Task.Run(async () =>
        {
            try
            {
                if (await _inboxWorkers[0].IsEmptyQueueAsync())
                    return;

                Parallel.ForEach(_inboxWorkers, inboxManager =>
                {
                    if (inboxManager.IsCycleProcessing)
                        return;
                    _ = inboxManager.CycleProcessingAsync();
                });
               
            }
            finally
            {
                _timerPoll.Change(_pollDelayMs, Timeout.Infinite);
            }
        });
    }
    
    private void CleanUpCallback(object? state)
    {
        Task.Run(async () =>
        {
            try
            {
                await _inboxWorkers[0].DeleteDoneItemsAsync();
                var currentProcesses = new HashSet<int>(await _inboxWorkers[0].GetProgressIdsAsync());

                if (_previousProcesses.Count != 0)
                {
                    _previousProcesses.IntersectWith(currentProcesses);

                    if (_previousProcesses.Count != 0)
                    {
                        await _inboxWorkers[0].ReturnZombieToQueueAsync(_previousProcesses);
                        _previousProcesses.Clear();
                    }
                }
                
                _previousProcesses.UnionWith(currentProcesses);
            }
            finally
            {
                _timerCleanUp.Change(_cleanUpDelayMs, Timeout.Infinite);
            }
        });
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timerPoll.Change(0, Timeout.Infinite);
        _timerCleanUp.Change(0, Timeout.Infinite);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timerPoll.Change(Timeout.Infinite, Timeout.Infinite);
        _timerCleanUp.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timerPoll.Dispose();
        _timerCleanUp.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _timerPoll.DisposeAsync();
        await _timerCleanUp.DisposeAsync();
    }
}

