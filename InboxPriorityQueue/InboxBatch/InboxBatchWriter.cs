using System.Threading.Tasks.Dataflow;
using InboxPriorityQueue.Worker;

namespace InboxPriorityQueue.InboxBatch;

public class InboxBatchWriter : IDisposable, IAsyncDisposable
{
    private readonly InboxWorker _inboxWorker;
    private readonly BatchBlock<string> _batchValuesBlock;
    private readonly ActionBlock<string[]> _insertValuesBlock;
    private readonly Timer _timer;

    public InboxBatchWriter(InboxWorker inboxWorker, int batchSize = 5000, int batchDelayMs = 1000)
    {
        _inboxWorker = inboxWorker;
        _batchValuesBlock = new BatchBlock<string>(batchSize);
        _insertValuesBlock = new ActionBlock<string[]>(InsertItemsAsync);
        
        _batchValuesBlock.LinkTo(_insertValuesBlock);
        _timer = new Timer(TimerCallback, null, batchDelayMs, batchDelayMs);
    }
    
    public void Enqueue(string value) => _batchValuesBlock.Post(value);

    private void TimerCallback(object? state)
    {
        _batchValuesBlock.TriggerBatch();
    }

    private Task InsertItemsAsync(string[] values) => _inboxWorker.AddOrUpdateInboxItemsAsync(values);
    
    public void Dispose()
    {
        _timer.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _timer.DisposeAsync();
    }
}