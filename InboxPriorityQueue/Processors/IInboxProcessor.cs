namespace InboxPriorityQueue.Processors;

public interface IInboxProcessor
{
    Task<bool> Process(string value, CancellationToken cancellationToken);
}