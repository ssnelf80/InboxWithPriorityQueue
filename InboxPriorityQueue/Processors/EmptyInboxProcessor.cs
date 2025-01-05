namespace InboxPriorityQueue.Processors;

/// <summary>
/// <inheritdoc cref="IInboxProcessor"/>
/// </summary>
public class EmptyInboxProcessor : IInboxProcessor
{
    /// <summary>
    /// <inheritdoc />
    /// </summary>
    public Task<bool> Process(string value, CancellationToken cancellationToken) => Task.FromResult(true);
}