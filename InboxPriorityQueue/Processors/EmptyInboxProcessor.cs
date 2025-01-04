namespace InboxPriorityQueue.Processors;

public class EmptyInboxProcessor : IInboxProcessor
{
    public Task<bool> Process(string value, CancellationToken cancellationToken) => Task.FromResult(true);
}