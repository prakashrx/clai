namespace Clai.Terminal.Events;

/// <summary>
/// A command prompt was detected (heuristic)
/// </summary>
public record PromptDetectedEvent : TerminalEvent
{
    /// <summary>
    /// The prompt text
    /// </summary>
    public required string PromptText { get; init; }
    
    /// <summary>
    /// The working directory in the prompt (if detected)
    /// </summary>
    public string? WorkingDirectory { get; init; }
    
    public PromptDetectedEvent()
    {
        Source = EventSource.System;
    }
}

/// <summary>
/// Command execution completed (heuristic)
/// </summary>
public record CommandCompletedEvent : TerminalEvent
{
    /// <summary>
    /// The command that was executed
    /// </summary>
    public string? Command { get; init; }
    
    /// <summary>
    /// How long the command took
    /// </summary>
    public TimeSpan? Duration { get; init; }
    
    /// <summary>
    /// Exit code if available
    /// </summary>
    public int? ExitCode { get; init; }
    
    public CommandCompletedEvent()
    {
        Source = EventSource.System;
    }
}

/// <summary>
/// Terminal bell/beep
/// </summary>
public record BellEvent : TerminalEvent
{
}

/// <summary>
/// Terminal title changed
/// </summary>
public record TitleChangedEvent : TerminalEvent
{
    /// <summary>
    /// New title
    /// </summary>
    public required string Title { get; init; }
}