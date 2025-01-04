using InboxPriorityQueue.Context;
using InboxPriorityQueue.Manager;
using InboxPriorityQueue.Processors;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace InboxPriorityQueue.InboxPoll;

public class InboxPollService : IHostedService, IDisposable, IAsyncDisposable
{
    private readonly Timer _timer;
    private readonly int _pollDelayMs;
    private readonly int _workerCount;

    private readonly InboxManager[] _inboxManagers;
    
    public InboxPollService(IOptions<InboxPollConfiguration> configOptions, InboxContext context, IInboxProcessor inboxProcessor)
    {
        var config = configOptions.Value ?? new InboxPollConfiguration();
        _pollDelayMs = config.PollIntervalMs;
        _inboxManagers = new InboxManager[config.WorkerCount];
        
        for (var i = 0; i < _inboxManagers.Length; i++)
            _inboxManagers[i] = new InboxManager(context, inboxProcessor);
        
        _timer = new Timer(PollCallback, null, Timeout.Infinite, Timeout.Infinite);
    }
    private async void PollCallback(object? state)
    {
        try
        {
            if (await _inboxManagers[0].IsEmptyQueueAsync())
                return;

            foreach (var inboxManager in _inboxManagers)
            {
                if (inboxManager.IsCycleProcessing)
                    continue;
                
                _ = inboxManager.CycleProcessingAsync();
                    
                if (await _inboxManagers[0].IsEmptyQueueAsync())
                    return;
            }
        }
        finally
        {
            _timer.Change(_pollDelayMs, Timeout.Infinite);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer.Change(0, Timeout.Infinite);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _timer.DisposeAsync();
    }
}

