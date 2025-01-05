namespace InboxPriorityQueue.Processors;

/// <summary>
/// Обработчик значений из очереди
/// </summary>
public interface IInboxProcessor
{
    /// <summary>
    /// Метод обработки значения из очереди
    /// </summary>
    /// <param name="value">Значение из очереди</param>
    /// <param name="cancellationToken"></param>
    /// <returns>true - в случае успешной обработки, false - в случае ошибки</returns>
    Task<bool> Process(string value, CancellationToken cancellationToken);
}