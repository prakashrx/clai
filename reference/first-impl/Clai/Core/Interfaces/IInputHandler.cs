namespace Clai.Core;

public interface IInputHandler
{
    Task<string> GetInputAsync(CancellationToken cancellationToken = default);
    void AddToHistory(string input);
    IReadOnlyList<string> GetHistory();
}