using System.ComponentModel.DataAnnotations;

namespace InboxPriorityQueue.InboxPoll;

public class InboxPollConfiguration
{
    public int PollIntervalMs { get; set; } = 2_500;
    [Range(1, 1_000)]
    public int WorkerCount { get; set; } = 10;

    public int CleanUpIntervalMs { get; set; } = 120_000;
}