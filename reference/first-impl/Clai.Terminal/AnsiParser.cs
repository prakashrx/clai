using System.Text;
using System.Diagnostics;

namespace Clai.Terminal;

/// <summary>
/// Parses ANSI/VT100 escape sequences and updates the virtual terminal
/// </summary>
internal class AnsiParser
{
    private enum State
    {
        Normal,      // Regular text
        Escape,      // Got ESC
        Command,     // Got ESC[
        OsCommand    // Got ESC]
    }
    
    private readonly Screen screen;
    private State state = State.Normal;
    private readonly StringBuilder commandBuffer = new();
    private TerminalCell currentAttributes = TerminalCell.Empty;
    
    public AnsiParser(Screen screen)
    {
        this.screen = screen;
    }
    
    public void Process(byte[] data)
    {
        foreach (byte b in data)
        {
            switch (state)
            {
                case State.Normal:
                    ProcessNormal(b);
                    break;
                    
                case State.Escape:
                    ProcessEscape(b);
                    break;
                    
                case State.Command:
                    ProcessCommand(b);
                    break;
                    
                case State.OsCommand:
                    ProcessOsCommand(b);
                    break;
            }
        }
    }
    
    private void ProcessNormal(byte b)
    {
        if (b == 0x1B)  // ESC
        {
            state = State.Escape;
        }
        else
        {
            screen.PutChar((char)b);
        }
    }
    
    private void ProcessEscape(byte b)
    {
        switch ((char)b)
        {
            case '[':  // CSI - Control Sequence Introducer
                state = State.Command;
                commandBuffer.Clear();
                break;
                
            case ']':  // OSC - Operating System Command
                state = State.OsCommand;
                commandBuffer.Clear();
                break;
                
            case '7':  // Save cursor
                screen.SaveCursor();
                state = State.Normal;
                break;
                
            case '8':  // Restore cursor
                screen.RestoreCursor();
                state = State.Normal;
                break;
                
            default:
                // Unknown escape sequence, ignore
                state = State.Normal;
                break;
        }
    }
    
    private void ProcessCommand(byte b)
    {
        char c = (char)b;
        
        // Check if this ends the command
        if (IsCommandEnd(c))
        {
            ExecuteCommand(commandBuffer.ToString(), c);
            commandBuffer.Clear();
            state = State.Normal;
        }
        else
        {
            commandBuffer.Append(c);
        }
    }
    
    private void ProcessOsCommand(byte b)
    {
        // OS commands end with BEL (0x07) or ST (ESC \)
        if (b == 0x07)  // BEL
        {
            // Process OS command (like setting window title)
            // For now, we ignore these
            commandBuffer.Clear();
            state = State.Normal;
        }
        else if (b == 0x1B)  // Might be start of ST
        {
            // We'll handle this later if needed
            state = State.Normal;
        }
        else
        {
            commandBuffer.Append((char)b);
        }
    }
    
    private bool IsCommandEnd(char c)
    {
        // Commands end with a letter or certain symbols
        return (c >= 'A' && c <= 'Z') || 
               (c >= 'a' && c <= 'z') ||
               c == '@' || c == '`' || c == '{' || c == '|' || c == '}' || c == '~';
    }
    
    private void ExecuteCommand(string parameters, char command)
    {
        var parts = string.IsNullOrEmpty(parameters) ? 
            Array.Empty<int>() : 
            ParseParameters(parameters);
        
        var paramsStr = parts.Length > 0 ? string.Join(";", parts) : "";
        Debug.WriteLine($"[ANSI] ESC[{paramsStr}{command}");
        
        switch (command)
        {
            // Cursor movement
            case 'A':  // Cursor up
                screen.MoveCursor(0, -(parts.Length > 0 ? parts[0] : 1));
                break;
                
            case 'B':  // Cursor down
                screen.MoveCursor(0, parts.Length > 0 ? parts[0] : 1);
                break;
                
            case 'C':  // Cursor forward
                screen.MoveCursor(parts.Length > 0 ? parts[0] : 1, 0);
                break;
                
            case 'D':  // Cursor backward
                screen.MoveCursor(-(parts.Length > 0 ? parts[0] : 1), 0);
                break;
                
            case 'H':  // Cursor position
            case 'f':  // Force cursor position
                int row = parts.Length > 0 ? parts[0] - 1 : 0;
                int col = parts.Length > 1 ? parts[1] - 1 : 0;
                screen.SetCursorPosition(col, row);
                break;
                
            case 'G':  // Cursor horizontal absolute
                screen.SetCursorPosition(parts.Length > 0 ? parts[0] - 1 : 0, screen.CursorY);
                break;
                
            // Clearing
            case 'J':  // Clear screen
                if (parts.Length == 0 || parts[0] == 0)
                    screen.ClearFromCursor();
                else if (parts[0] == 1)
                    screen.ClearToCursor();
                else if (parts[0] == 2)
                    screen.Clear();
                break;
                
            case 'K':  // Clear line
                if (parts.Length == 0 || parts[0] == 0)
                    screen.ClearLine();  // Clear from cursor to end of line
                else if (parts[0] == 1)
                    screen.ClearToCursor();  // Clear from start to cursor
                else if (parts[0] == 2)
                    screen.ClearLine();  // Clear entire line
                break;
                
            // Graphics
            case 'm':  // Set graphics rendition (colors, bold, etc.)
                ProcessGraphicsCommand(parts);
                break;
                
            // Save/Restore cursor
            case 's':  // Save cursor position
                screen.SaveCursor();
                break;
                
            case 'u':  // Restore cursor position
                screen.RestoreCursor();
                break;
                
            // Set/Reset Mode
            case 'h':  // Set Mode (SM)
                if (parameters.StartsWith("?25"))
                {
                    // ESC[?25h - Show cursor (DECTCEM)
                    Debug.WriteLine("[ANSI] Show cursor");
                    // TODO: Track cursor visibility if needed
                }
                else if (parameters.StartsWith("?1049"))
                {
                    // ESC[?1049h - Enable alternate screen buffer (used by vim, htop, etc)
                    Debug.WriteLine("[ANSI] Enter alternate screen");
                    // TODO: Implement alternate screen in Screen class
                }
                // Add other modes as needed
                break;
                
            case 'l':  // Reset Mode (RM)
                if (parameters.StartsWith("?25"))
                {
                    // ESC[?25l - Hide cursor (DECTCEM) 
                    Debug.WriteLine("[ANSI] Hide cursor");
                    // TODO: Track cursor visibility if needed
                }
                else if (parameters.StartsWith("?1049"))
                {
                    // ESC[?1049l - Disable alternate screen buffer
                    Debug.WriteLine("[ANSI] Exit alternate screen");
                    // TODO: Implement alternate screen in Screen class
                }
                // Add other modes as needed
                break;
                
            // For now, ignore unknown commands
            default:
                break;
        }
    }
    
    private int[] ParseParameters(string parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
            return Array.Empty<int>();
            
        var parts = parameters.Split(';');
        var result = new List<int>();
        
        foreach (var part in parts)
        {
            if (int.TryParse(part, out int value))
                result.Add(value);
            else
                result.Add(0);  // Default value for empty parameters
        }
        
        return result.ToArray();
    }
    
    private void ProcessGraphicsCommand(int[] parameters)
    {
        if (parameters.Length == 0)
        {
            screen.ResetAttributes();
            return;
        }
        
        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            
            switch (param)
            {
                case 0:  // Reset all
                    currentAttributes = TerminalCell.Empty;
                    break;
                    
                case 1:  // Bold
                    currentAttributes.Bold = true;
                    break;
                    
                case 4:  // Underline
                    currentAttributes.Underline = true;
                    break;
                    
                case 7:  // Inverse
                    currentAttributes.Inverse = true;
                    break;
                    
                case 22:  // Not bold
                    currentAttributes.Bold = false;
                    break;
                    
                case 24:  // Not underlined
                    currentAttributes.Underline = false;
                    break;
                    
                case 27:  // Not inverse
                    currentAttributes.Inverse = false;
                    break;
                    
                // Foreground colors
                case >= 30 and <= 37:
                    currentAttributes.ForegroundColor = (byte)(param - 30);
                    break;
                    
                case 39:  // Default foreground
                    currentAttributes.ForegroundColor = 7;
                    break;
                    
                // Background colors
                case >= 40 and <= 47:
                    currentAttributes.BackgroundColor = (byte)(param - 40);
                    break;
                    
                case 49:  // Default background
                    currentAttributes.BackgroundColor = 0;
                    break;
                    
                // Bright foreground colors
                case >= 90 and <= 97:
                    currentAttributes.ForegroundColor = (byte)(param - 90 + 8);
                    break;
                    
                // Bright background colors
                case >= 100 and <= 107:
                    currentAttributes.BackgroundColor = (byte)(param - 100 + 8);
                    break;
                    
                // Extended colors (256 color mode)
                case 38:  // Foreground extended color
                    if (i + 2 < parameters.Length && parameters[i + 1] == 5)
                    {
                        // ESC[38;5;n - where n is 0-255
                        var colorIndex = parameters[i + 2];
                        if (colorIndex < 16)
                        {
                            // Use the standard 16 colors
                            currentAttributes.ForegroundColor = (byte)colorIndex;
                        }
                        else
                        {
                            // Map 256 colors to 16 (simplified)
                            currentAttributes.ForegroundColor = (byte)(colorIndex < 128 ? 7 : 15);
                        }
                        i += 2; // Skip the next two parameters
                    }
                    break;
                    
                case 48:  // Background extended color
                    if (i + 2 < parameters.Length && parameters[i + 1] == 5)
                    {
                        // ESC[48;5;n - where n is 0-255
                        var colorIndex = parameters[i + 2];
                        if (colorIndex < 16)
                        {
                            // Use the standard 16 colors
                            currentAttributes.BackgroundColor = (byte)colorIndex;
                        }
                        else
                        {
                            // Map 256 colors to 16 (simplified)
                            currentAttributes.BackgroundColor = (byte)(colorIndex < 128 ? 0 : 8);
                        }
                        i += 2; // Skip the next two parameters
                    }
                    break;
            }
        }
        
        screen.SetAttributes(currentAttributes);
    }
}