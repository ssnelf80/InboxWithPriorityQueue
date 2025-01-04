namespace InboxPriorityQueue.Models;

public class InboxItem
{
    public int Id { get; init; }
    public string Value { get; init; }
    public Status Status { get; init; }
    public Priority Priority { get; init; }
}