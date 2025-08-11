namespace Clai.Terminal;

/// <summary>
/// Represents a single cell in the terminal display
/// </summary>
public struct TerminalCell : IEquatable<TerminalCell>
{
    /// <summary>
    /// The character in this cell
    /// </summary>
    public char Character { get; set; }
    
    /// <summary>
    /// Foreground color
    /// </summary>
    public TerminalColor Foreground { get; set; }
    
    /// <summary>
    /// Background color
    /// </summary>
    public TerminalColor Background { get; set; }
    
    /// <summary>
    /// Text attributes (bold, underline, etc.)
    /// </summary>
    public TextAttributes Attributes { get; set; }
    
    /// <summary>
    /// Empty cell with default colors
    /// </summary>
    public static TerminalCell Empty => new()
    {
        Character = ' ',
        Foreground = TerminalColor.Default,
        Background = TerminalColor.Default,
        Attributes = TextAttributes.None
    };
    
    public bool Equals(TerminalCell other)
    {
        return Character == other.Character &&
               Foreground == other.Foreground &&
               Background == other.Background &&
               Attributes == other.Attributes;
    }
    
    public override bool Equals(object? obj) => obj is TerminalCell cell && Equals(cell);
    
    public override int GetHashCode() => HashCode.Combine(Character, Foreground, Background, Attributes);
    
    public static bool operator ==(TerminalCell left, TerminalCell right) => left.Equals(right);
    
    public static bool operator !=(TerminalCell left, TerminalCell right) => !left.Equals(right);
}

/// <summary>
/// Terminal color representation
/// </summary>
public struct TerminalColor : IEquatable<TerminalColor>
{
    private readonly byte _type;
    private readonly byte _r, _g, _b;
    private readonly byte _index;
    
    private TerminalColor(byte type, byte index = 0, byte r = 0, byte g = 0, byte b = 0)
    {
        _type = type;
        _index = index;
        _r = r;
        _g = g;
        _b = b;
    }
    
    /// <summary>
    /// Default terminal color
    /// </summary>
    public static TerminalColor Default => new(0);
    
    /// <summary>
    /// Create from standard 16-color palette
    /// </summary>
    public static TerminalColor FromStandard(ConsoleColor color) => new(1, (byte)color);
    
    /// <summary>
    /// Create from 256-color palette
    /// </summary>
    public static TerminalColor From256(byte index) => new(2, index);
    
    /// <summary>
    /// Create from RGB values
    /// </summary>
    public static TerminalColor FromRgb(byte r, byte g, byte b) => new(3, 0, r, g, b);
    
    public bool IsDefault => _type == 0;
    public bool IsStandard => _type == 1;
    public bool Is256Color => _type == 2;
    public bool IsRgb => _type == 3;
    
    public ConsoleColor? AsConsoleColor => IsStandard ? (ConsoleColor)_index : null;
    public byte? As256Color => Is256Color ? _index : null;
    public (byte r, byte g, byte b)? AsRgb => IsRgb ? (_r, _g, _b) : null;
    
    public bool Equals(TerminalColor other)
    {
        if (_type != other._type) return false;
        return _type switch
        {
            1 or 2 => _index == other._index,
            3 => _r == other._r && _g == other._g && _b == other._b,
            _ => true
        };
    }
    
    public override bool Equals(object? obj) => obj is TerminalColor color && Equals(color);
    public override int GetHashCode() => HashCode.Combine(_type, _index, _r, _g, _b);
    public static bool operator ==(TerminalColor left, TerminalColor right) => left.Equals(right);
    public static bool operator !=(TerminalColor left, TerminalColor right) => !left.Equals(right);
}

/// <summary>
/// Text attributes for terminal cells
/// </summary>
[Flags]
public enum TextAttributes
{
    None = 0,
    Bold = 1,
    Dim = 2,
    Italic = 4,
    Underline = 8,
    Blink = 16,
    Reverse = 32,
    Hidden = 64,
    Strikethrough = 128
}