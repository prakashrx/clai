namespace Clai.Terminal.Events;

/// <summary>
/// Screen was cleared
/// </summary>
public record ClearScreenEvent : TerminalEvent
{
}

/// <summary>
/// Line was cleared
/// </summary>
public record ClearLineEvent : TerminalEvent
{
    /// <summary>
    /// The line number that was cleared
    /// </summary>
    public required int LineNumber { get; init; }
    
    /// <summary>
    /// How the line was cleared
    /// </summary>
    public ClearMode Mode { get; init; } = ClearMode.Entire;
}

/// <summary>
/// Screen scrolled
/// </summary>
public record ScrollEvent : TerminalEvent
{
    /// <summary>
    /// Number of lines scrolled
    /// </summary>
    public required int Lines { get; init; }
    
    /// <summary>
    /// Direction of scroll
    /// </summary>
    public ScrollDirection Direction { get; init; } = ScrollDirection.Up;
    
    /// <summary>
    /// Line that scrolled out of view (if applicable)
    /// </summary>
    public string? ScrolledOutLine { get; init; }
}

/// <summary>
/// Cursor position changed
/// </summary>
public record CursorMovedEvent : TerminalEvent
{
    /// <summary>
    /// New X position
    /// </summary>
    public required int X { get; init; }
    
    /// <summary>
    /// New Y position
    /// </summary>
    public required int Y { get; init; }
    
    /// <summary>
    /// Previous X position
    /// </summary>
    public int OldX { get; init; }
    
    /// <summary>
    /// Previous Y position
    /// </summary>
    public int OldY { get; init; }
}

/// <summary>
/// Cursor visibility changed
/// </summary>
public record CursorVisibilityEvent : TerminalEvent
{
    /// <summary>
    /// Whether cursor is now visible
    /// </summary>
    public required bool Visible { get; init; }
}

/// <summary>
/// How a line clear operation works
/// </summary>
public enum ClearMode
{
    ToEnd,      // Clear from cursor to end
    ToStart,    // Clear from start to cursor
    Entire      // Clear entire line
}

/// <summary>
/// Direction of scrolling
/// </summary>
public enum ScrollDirection
{
    Up,
    Down
}