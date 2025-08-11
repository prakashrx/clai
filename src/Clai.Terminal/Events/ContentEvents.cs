namespace Clai.Terminal.Events;

/// <summary>
/// Text was written to the terminal
/// </summary>
public record TextWrittenEvent : TerminalEvent
{
    /// <summary>
    /// The text that was written
    /// </summary>
    public required string Text { get; init; }
    
    /// <summary>
    /// The cells with color/attribute information
    /// </summary>
    public TerminalCell[]? Cells { get; init; }
    
    /// <summary>
    /// Current cursor X position
    /// </summary>
    public int CursorX { get; init; }
    
    /// <summary>
    /// Current cursor Y position
    /// </summary>
    public int CursorY { get; init; }
}

/// <summary>
/// A complete line was written or updated
/// </summary>
public record LineUpdatedEvent : TerminalEvent
{
    /// <summary>
    /// The line number that was updated
    /// </summary>
    public required int LineNumber { get; init; }
    
    /// <summary>
    /// The complete line content
    /// </summary>
    public required string Text { get; init; }
    
    /// <summary>
    /// The cells for the entire line with colors
    /// </summary>
    public TerminalCell[]? Cells { get; init; }
}

/// <summary>
/// Terminal output a newline
/// </summary>
public record NewLineEvent : TerminalEvent
{
}