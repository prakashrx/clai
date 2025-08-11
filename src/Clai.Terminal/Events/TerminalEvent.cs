namespace Clai.Terminal.Events;

/// <summary>
/// Base class for all terminal events
/// </summary>
public abstract record TerminalEvent
{
    /// <summary>
    /// When the event occurred
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    
    /// <summary>
    /// Source of the event
    /// </summary>
    public EventSource Source { get; init; } = EventSource.Terminal;
}

/// <summary>
/// Source of a terminal event
/// </summary>
public enum EventSource
{
    Terminal,   // From ConPTY output
    User,       // User input
    System      // System generated (like prompt detection)
}