using System.Text;

namespace Clai.Terminal;

/// <summary>
/// Screen buffer that maintains terminal display state and processes ANSI escape sequences
/// </summary>
public class Screen
{
    private readonly TerminalCell[,] buffer;
    private readonly List<string> historyLines;
    private readonly AnsiParser parser;
    private readonly int width;
    private readonly int height;
    
    private int cursorX;
    private int cursorY;
    private int savedCursorX;
    private int savedCursorY;
    private TerminalCell currentAttributes;
    private bool isAlternateScreen;
    private TerminalCell[,]? alternateBuffer;
    
    public int Width => width;
    public int Height => height;
    public int CursorX => cursorX;
    public int CursorY => cursorY;
    public bool IsAlternateScreen => isAlternateScreen;
    
    /// <summary>
    /// Get all lines as plain text (for AI consumption)
    /// </summary>
    public IReadOnlyList<string> Lines
    {
        get
        {
            var lines = new List<string>();
            
            // Add history lines
            lines.AddRange(historyLines);
            
            // Add current buffer lines
            for (int y = 0; y < height; y++)
            {
                var line = GetLineText(y);
                if (!string.IsNullOrWhiteSpace(line) || y < cursorY)
                    lines.Add(line);
            }
            
            return lines;
        }
    }
    
    /// <summary>
    /// Get all text as a single string
    /// </summary>
    public string Text => string.Join(Environment.NewLine, Lines);
    
    /// <summary>
    /// Get just the visible screen text (current buffer)
    /// </summary>
    public string VisibleText
    {
        get
        {
            var lines = new List<string>();
            for (int y = 0; y < height; y++)
            {
                lines.Add(GetLineText(y));
            }
            
            // Trim trailing empty lines
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                lines.RemoveAt(lines.Count - 1);
                
            return string.Join(Environment.NewLine, lines);
        }
    }
    
    /// <summary>
    /// Fired when the terminal display is updated
    /// </summary>
    public event Action<TerminalUpdate>? Updated;
    
    public Screen(int width = 80, int height = 24, int maxHistory = 1000)
    {
        this.width = width;
        this.height = height;
        buffer = new TerminalCell[height, width];
        historyLines = new List<string>(maxHistory);
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
        Write(Encoding.UTF8.GetBytes(text));
    }
    
    /// <summary>
    /// Get current screen buffer
    /// </summary>
    public TerminalCell[,] GetScreen()
    {
        if (isAlternateScreen && alternateBuffer != null)
            return (TerminalCell[,])alternateBuffer.Clone();
        return (TerminalCell[,])buffer.Clone();
    }
    
    /// <summary>
    /// Get text for a specific line
    /// </summary>
    private string GetLineText(int y)
    {
        var sb = new StringBuilder();
        var currentBuffer = isAlternateScreen && alternateBuffer != null ? alternateBuffer : buffer;
        
        for (int x = 0; x < width; x++)
        {
            var c = currentBuffer[y, x].Char;
            sb.Append(c == '\0' ? ' ' : c);
        }
        
        return sb.ToString().TrimEnd();
    }
    
    /// <summary>
    /// Clear the entire screen
    /// </summary>
    public void Clear()
    {
        var currentBuffer = isAlternateScreen && alternateBuffer != null ? alternateBuffer : buffer;
        
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                currentBuffer[y, x] = TerminalCell.Empty;
        
        cursorX = 0;
        cursorY = 0;
        OnUpdated(TerminalUpdateType.ScreenCleared);
    }
    
    /// <summary>
    /// Clear from cursor to end of screen
    /// </summary>
    public void ClearFromCursor()
    {
        var currentBuffer = isAlternateScreen && alternateBuffer != null ? alternateBuffer : buffer;
        
        // Clear rest of current line
        for (int x = cursorX; x < width; x++)
            currentBuffer[cursorY, x] = TerminalCell.Empty;
            
        // Clear all lines below
        for (int y = cursorY + 1; y < height; y++)
            for (int x = 0; x < width; x++)
                currentBuffer[y, x] = TerminalCell.Empty;
                
        OnUpdated(TerminalUpdateType.ContentChanged);
    }
    
    /// <summary>
    /// Clear current line
    /// </summary>
    public void ClearLine()
    {
        var currentBuffer = isAlternateScreen && alternateBuffer != null ? alternateBuffer : buffer;
        
        for (int x = 0; x < width; x++)
            currentBuffer[cursorY, x] = TerminalCell.Empty;
            
        OnUpdated(TerminalUpdateType.ContentChanged);
    }
    
    /// <summary>
    /// Clear from start of line to cursor
    /// </summary>
    public void ClearToCursor()
    {
        var currentBuffer = isAlternateScreen && alternateBuffer != null ? alternateBuffer : buffer;
        
        for (int x = 0; x <= cursorX && x < width; x++)
            currentBuffer[cursorY, x] = TerminalCell.Empty;
            
        OnUpdated(TerminalUpdateType.ContentChanged);
    }
    
    /// <summary>
    /// Put a character at the current cursor position
    /// </summary>
    internal void PutChar(char c)
    {
        var currentBuffer = isAlternateScreen && alternateBuffer != null ? alternateBuffer : buffer;
        
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
                    currentBuffer[cursorY, cursorX] = cell;
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
        if (isAlternateScreen && alternateBuffer != null)
        {
            // In alternate screen, just shift content up
            for (int y = 0; y < height - 1; y++)
                for (int x = 0; x < width; x++)
                    alternateBuffer[y, x] = alternateBuffer[y + 1, x];
                    
            // Clear bottom line
            for (int x = 0; x < width; x++)
                alternateBuffer[height - 1, x] = TerminalCell.Empty;
        }
        else
        {
            // Save top line to history
            historyLines.Add(GetLineText(0));
            
            // Move all lines up
            for (int y = 0; y < height - 1; y++)
                for (int x = 0; x < width; x++)
                    buffer[y, x] = buffer[y + 1, x];
                    
            // Clear the bottom line
            for (int x = 0; x < width; x++)
                buffer[height - 1, x] = TerminalCell.Empty;
        }
        
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
    
    /// <summary>
    /// Enter alternate screen buffer (for vim, htop, etc)
    /// </summary>
    internal void EnterAlternateScreen()
    {
        if (!isAlternateScreen)
        {
            alternateBuffer = new TerminalCell[height, width];
            isAlternateScreen = true;
            Clear();
        }
    }
    
    /// <summary>
    /// Exit alternate screen buffer
    /// </summary>
    internal void ExitAlternateScreen()
    {
        if (isAlternateScreen)
        {
            isAlternateScreen = false;
            alternateBuffer = null;
            OnUpdated(TerminalUpdateType.ContentChanged);
        }
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