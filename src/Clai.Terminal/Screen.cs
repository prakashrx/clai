namespace Clai.Terminal;

/// <summary>
/// Screen buffer that maintains terminal display state and processes ANSI escape sequences
/// </summary>
public class Screen
{
    private readonly TerminalCell[,] buffer;
    private readonly List<TerminalCell[]> scrollback;
    private readonly AnsiParser parser;
    private readonly int width;
    private readonly int height;
    
    private int cursorX;
    private int cursorY;
    private int savedCursorX;
    private int savedCursorY;
    private TerminalCell currentAttributes;
    
    public int Width => width;
    public int Height => height;
    public int CursorX => cursorX;
    public int CursorY => cursorY;
    public IReadOnlyList<TerminalCell[]> Scrollback => scrollback.AsReadOnly();
    
    /// <summary>
    /// Fired when the terminal display is updated
    /// </summary>
    public event Action<TerminalUpdate>? Updated;
    
    public Screen(int width = 80, int height = 24, int scrollbackSize = 1000)
    {
        this.width = width;
        this.height = height;
        buffer = new TerminalCell[height, width];
        scrollback = new List<TerminalCell[]>(scrollbackSize);
        parser = new AnsiParser(this);
        currentAttributes = TerminalCell.Empty;
        Clear();
    }
    
    /// <summary>
    /// Process raw bytes from the terminal
    /// </summary>
    public void Write(byte[] data)
    {
        parser.Process(data);
    }
    
    /// <summary>
    /// Process a string (converts to bytes using UTF8)
    /// </summary>
    public void Write(string text)
    {
        Write(System.Text.Encoding.UTF8.GetBytes(text));
    }
    
    /// <summary>
    /// Get current screen buffer
    /// </summary>
    public TerminalCell[,] GetScreen()
    {
        return (TerminalCell[,])buffer.Clone();
    }
    
    /// <summary>
    /// Get a specific line from the buffer
    /// </summary>
    public TerminalCell[] GetLine(int line)
    {
        if (line < 0 || line >= height)
            throw new ArgumentOutOfRangeException(nameof(line));
            
        var result = new TerminalCell[width];
        for (int x = 0; x < width; x++)
            result[x] = buffer[line, x];
        return result;
    }
    
    /// <summary>
    /// Clear the entire screen
    /// </summary>
    public void Clear()
    {
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                buffer[y, x] = TerminalCell.Empty;
        
        cursorX = 0;
        cursorY = 0;
        OnUpdated(TerminalUpdateType.ScreenCleared);
    }
    
    /// <summary>
    /// Clear from cursor to end of screen
    /// </summary>
    public void ClearFromCursor()
    {
        // Clear rest of current line
        for (int x = cursorX; x < width; x++)
            buffer[cursorY, x] = TerminalCell.Empty;
            
        // Clear all lines below
        for (int y = cursorY + 1; y < height; y++)
            for (int x = 0; x < width; x++)
                buffer[y, x] = TerminalCell.Empty;
                
        OnUpdated(TerminalUpdateType.ContentChanged);
    }
    
    /// <summary>
    /// Clear current line
    /// </summary>
    public void ClearLine()
    {
        for (int x = 0; x < width; x++)
            buffer[cursorY, x] = TerminalCell.Empty;
        OnUpdated(TerminalUpdateType.ContentChanged);
    }
    
    /// <summary>
    /// Clear from start of line to cursor
    /// </summary>
    public void ClearToCursor()
    {
        for (int x = 0; x <= cursorX && x < width; x++)
            buffer[cursorY, x] = TerminalCell.Empty;
        OnUpdated(TerminalUpdateType.ContentChanged);
    }
    
    /// <summary>
    /// Put a character at the current cursor position
    /// </summary>
    internal void PutChar(char c)
    {
        switch (c)
        {
            case '\r':  // Carriage return
                cursorX = 0;
                break;
                
            case '\n':  // Line feed
                LineFeed();
                break;
                
            case '\b':  // Backspace
                if (cursorX > 0)
                    cursorX--;
                break;
                
            case '\t':  // Tab
                cursorX = Math.Min(((cursorX / 8) + 1) * 8, width - 1);
                break;
                
            case '\x7F':  // DEL
                // Handle delete
                if (cursorX > 0)
                {
                    cursorX--;
                    buffer[cursorY, cursorX] = TerminalCell.Empty;
                }
                break;
                
            default:
                if (c >= 32)  // Printable character
                {
                    if (cursorX >= width)
                    {
                        cursorX = 0;
                        LineFeed();
                    }
                    
                    var cell = currentAttributes;
                    cell.Char = c;
                    buffer[cursorY, cursorX] = cell;
                    cursorX++;
                }
                break;
        }
        
        OnUpdated(TerminalUpdateType.ContentChanged);
    }
    
    /// <summary>
    /// Move to next line, scrolling if necessary
    /// </summary>
    internal void LineFeed()
    {
        cursorY++;
        if (cursorY >= height)
        {
            ScrollUp();
            cursorY = height - 1;
        }
    }
    
    /// <summary>
    /// Scroll the screen up by one line
    /// </summary>
    internal void ScrollUp()
    {
        // Save the top line to scrollback
        var topLine = new TerminalCell[width];
        for (int x = 0; x < width; x++)
            topLine[x] = buffer[0, x];
        scrollback.Add(topLine);
        
        // Move all lines up
        for (int y = 0; y < height - 1; y++)
            for (int x = 0; x < width; x++)
                buffer[y, x] = buffer[y + 1, x];
                
        // Clear the bottom line
        for (int x = 0; x < width; x++)
            buffer[height - 1, x] = TerminalCell.Empty;
            
        OnUpdated(TerminalUpdateType.Scrolled);
    }
    
    /// <summary>
    /// Move cursor to specified position
    /// </summary>
    internal void SetCursorPosition(int x, int y)
    {
        cursorX = Math.Max(0, Math.Min(x, width - 1));
        cursorY = Math.Max(0, Math.Min(y, height - 1));
        OnUpdated(TerminalUpdateType.CursorMoved);
    }
    
    /// <summary>
    /// Move cursor relative to current position
    /// </summary>
    internal void MoveCursor(int deltaX, int deltaY)
    {
        SetCursorPosition(cursorX + deltaX, cursorY + deltaY);
    }
    
    /// <summary>
    /// Save current cursor position
    /// </summary>
    internal void SaveCursor()
    {
        savedCursorX = cursorX;
        savedCursorY = cursorY;
    }
    
    /// <summary>
    /// Restore saved cursor position
    /// </summary>
    internal void RestoreCursor()
    {
        cursorX = savedCursorX;
        cursorY = savedCursorY;
        OnUpdated(TerminalUpdateType.CursorMoved);
    }
    
    /// <summary>
    /// Set current text attributes
    /// </summary>
    internal void SetAttributes(TerminalCell attributes)
    {
        currentAttributes = attributes;
    }
    
    /// <summary>
    /// Reset all attributes to default
    /// </summary>
    internal void ResetAttributes()
    {
        currentAttributes = TerminalCell.Empty;
    }
    
    private void OnUpdated(TerminalUpdateType type)
    {
        Updated?.Invoke(new TerminalUpdate(type, cursorX, cursorY));
    }
}

/// <summary>
/// Information about a terminal update
/// </summary>
public class TerminalUpdate
{
    public TerminalUpdateType Type { get; }
    public int CursorX { get; }
    public int CursorY { get; }
    
    public TerminalUpdate(TerminalUpdateType type, int cursorX, int cursorY)
    {
        Type = type;
        CursorX = cursorX;
        CursorY = cursorY;
    }
}

public enum TerminalUpdateType
{
    ContentChanged,
    CursorMoved,
    ScreenCleared,
    Scrolled
}