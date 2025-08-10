namespace Clai.Terminal;

/// <summary>
/// Represents a single cell in the terminal display
/// </summary>
public struct TerminalCell
{
    public char Char { get; set; }
    public byte ForegroundColor { get; set; }  // 0-255 for full color support
    public byte BackgroundColor { get; set; }
    public bool Bold { get; set; }
    public bool Underline { get; set; }
    public bool Inverse { get; set; }
    
    public static readonly TerminalCell Empty = new() 
    { 
        Char = ' ', 
        ForegroundColor = 7,  // Default gray
        BackgroundColor = 0   // Default black
    };
    
    /// <summary>
    /// Creates a simple character cell with default styling
    /// </summary>
    public TerminalCell(char c)
    {
        Char = c;
        ForegroundColor = 7;
        BackgroundColor = 0;
        Bold = false;
        Underline = false;
        Inverse = false;
    }
    
    /// <summary>
    /// Converts ANSI colors (0-15) to ConsoleColor
    /// </summary>
    public static ConsoleColor AnsiToConsoleColor(byte ansiColor)
    {
        return ansiColor switch
        {
            0 => ConsoleColor.Black,
            1 => ConsoleColor.DarkRed,
            2 => ConsoleColor.DarkGreen,
            3 => ConsoleColor.DarkYellow,
            4 => ConsoleColor.DarkBlue,
            5 => ConsoleColor.DarkMagenta,
            6 => ConsoleColor.DarkCyan,
            7 => ConsoleColor.Gray,
            8 => ConsoleColor.DarkGray,
            9 => ConsoleColor.Red,
            10 => ConsoleColor.Green,
            11 => ConsoleColor.Yellow,
            12 => ConsoleColor.Blue,
            13 => ConsoleColor.Magenta,
            14 => ConsoleColor.Cyan,
            15 => ConsoleColor.White,
            _ => ConsoleColor.Gray
        };
    }
}