namespace Clai.Core;

public interface ICommandProcessor
{
    Task<CommandResult> ProcessAsync(string input, CancellationToken cancellationToken = default);
}

public record CommandResult(
    string Output,
    bool IsSuccess,
    bool ShouldExit = false,
    CommandType Type = CommandType.AI
);

public enum CommandType
{
    AI,
    Manual,
    System
}