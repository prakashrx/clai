using System.Text;
using System.Threading.Channels;
using Clai.Terminal.Events;

namespace Clai.Terminal;

/// <summary>
/// Parses ANSI escape sequences and emits terminal events
/// </summary>
public class AnsiEventParser
{
    private enum State
    {
        Normal,      // Regular text
        Escape,      // Got ESC
        CsiSequence, // Got ESC[ (Control Sequence Introducer)
        OscSequence  // Got ESC] (Operating System Command)
    }
    
    private readonly ChannelWriter<TerminalEvent> _eventWriter;
    private readonly StringBuilder _textBuffer = new();
    private readonly StringBuilder _sequenceBuffer = new();
    private State _state = State.Normal;
    private int _cursorX;
    private int _cursorY;
    
    public AnsiEventParser(ChannelWriter<TerminalEvent> eventWriter)
    {
        _eventWriter = eventWriter;
    }
    
    /// <summary>
    /// Process raw bytes from terminal output
    /// </summary>
    public async Task ProcessBytesAsync(byte[] data)
    {
        var text = Encoding.UTF8.GetString(data);
        
        foreach (var ch in text)
        {
            switch (_state)
            {
                case State.Normal:
                    await ProcessNormalAsync(ch);
                    break;
                    
                case State.Escape:
                    await ProcessEscapeAsync(ch);
                    break;
                    
                case State.CsiSequence:
                    await ProcessCsiSequenceAsync(ch);
                    break;
                    
                case State.OscSequence:
                    await ProcessOscSequenceAsync(ch);
                    break;
            }
        }
        
        // Flush any remaining text
        if (_textBuffer.Length > 0)
        {
            await EmitTextAsync(_textBuffer.ToString());
            _textBuffer.Clear();
        }
    }
    
    private async Task ProcessNormalAsync(char ch)
    {
        if (ch == '\x1B') // ESC character
        {
            // Flush any pending text
            if (_textBuffer.Length > 0)
            {
                await EmitTextAsync(_textBuffer.ToString());
                _textBuffer.Clear();
            }
            _state = State.Escape;
        }
        else
        {
            // Process regular character
            await ProcessCharacterAsync(ch);
        }
    }
    
    private async Task ProcessEscapeAsync(char ch)
    {
        switch (ch)
        {
            case '[':  // CSI - Control Sequence Introducer
                _state = State.CsiSequence;
                _sequenceBuffer.Clear();
                break;
                
            case ']':  // OSC - Operating System Command
                _state = State.OscSequence;
                _sequenceBuffer.Clear();
                break;
                
            case '7':  // Save cursor (DEC)
                // TODO: Add CursorSaveEvent to events
                _state = State.Normal;
                break;
                
            case '8':  // Restore cursor (DEC)
                // TODO: Add CursorRestoreEvent to events
                _state = State.Normal;
                break;
                
            default:
                // Unknown escape sequence, return to normal
                _state = State.Normal;
                break;
        }
    }
    
    private async Task ProcessCsiSequenceAsync(char ch)
    {
        // CSI sequences end with a letter or certain symbols
        if (IsCsiTerminator(ch))
        {
            await ExecuteCsiCommandAsync(_sequenceBuffer.ToString(), ch);
            _sequenceBuffer.Clear();
            _state = State.Normal;
        }
        else
        {
            _sequenceBuffer.Append(ch);
        }
    }
    
    private async Task ProcessOscSequenceAsync(char ch)
    {
        // OSC sequences end with BEL (0x07) or ST (ESC\)
        if (ch == '\a')  // BEL
        {
            await ExecuteOscCommandAsync(_sequenceBuffer.ToString());
            _sequenceBuffer.Clear();
            _state = State.Normal;
        }
        else if (ch == '\x1B')  // Might be start of ST
        {
            // For now, treat any ESC as terminating OSC
            // TODO: Properly handle ESC\ (String Terminator)
            await ExecuteOscCommandAsync(_sequenceBuffer.ToString());
            _sequenceBuffer.Clear();
            _state = State.Escape;
        }
        else
        {
            _sequenceBuffer.Append(ch);
        }
    }
    
    private static bool IsCsiTerminator(char ch)
    {
        // CSI sequences end with a letter or certain symbols
        return char.IsLetter(ch) || ch == '~' || ch == '@' || ch == '`' || 
               ch == '{' || ch == '|' || ch == '}';
    }
    
    private async Task ProcessCharacterAsync(char ch)
    {
        switch (ch)
        {
            case '\r': // Carriage return
                if (_textBuffer.Length > 0)
                {
                    await EmitTextAsync(_textBuffer.ToString());
                    _textBuffer.Clear();
                }
                _cursorX = 0;
                await _eventWriter.WriteAsync(new CursorMovedEvent
                {
                    X = 0,
                    Y = _cursorY,
                    OldX = _cursorX,
                    OldY = _cursorY
                });
                break;
                
            case '\n': // Line feed
                if (_textBuffer.Length > 0)
                {
                    await EmitTextAsync(_textBuffer.ToString());
                    _textBuffer.Clear();
                }
                _cursorY++;
                await _eventWriter.WriteAsync(new NewLineEvent());
                break;
                
            case '\b': // Backspace
                if (_cursorX > 0)
                {
                    _cursorX--;
                    await _eventWriter.WriteAsync(new CursorMovedEvent
                    {
                        X = _cursorX,
                        Y = _cursorY,
                        OldX = _cursorX + 1,
                        OldY = _cursorY
                    });
                }
                break;
                
            case '\t': // Tab
                _textBuffer.Append("    "); // Simple 4-space tab
                _cursorX += 4;
                break;
                
            case '\a': // Bell
                await _eventWriter.WriteAsync(new BellEvent());
                break;
                
            default:
                if (ch >= 32) // Printable character
                {
                    _textBuffer.Append(ch);
                    _cursorX++;
                }
                break;
        }
    }
    
    private async Task ExecuteCsiCommandAsync(string parameters, char command)
    {
        var parsedParams = ParseParameters(parameters);
        
        switch (command)
        {
            case 'H': // Cursor position
            case 'f':
                var row = parsedParams.Length > 0 ? parsedParams[0] - 1 : 0;
                var col = parsedParams.Length > 1 ? parsedParams[1] - 1 : 0;
                await _eventWriter.WriteAsync(new CursorMovedEvent
                {
                    X = col,
                    Y = row,
                    OldX = _cursorX,
                    OldY = _cursorY
                });
                _cursorX = col;
                _cursorY = row;
                break;
                
            case 'A': // Cursor up
                var upLines = parsedParams.Length > 0 ? parsedParams[0] : 1;
                _cursorY = Math.Max(0, _cursorY - upLines);
                await _eventWriter.WriteAsync(new CursorMovedEvent
                {
                    X = _cursorX,
                    Y = _cursorY,
                    OldX = _cursorX,
                    OldY = _cursorY + upLines
                });
                break;
                
            case 'B': // Cursor down
                var downLines = parsedParams.Length > 0 ? parsedParams[0] : 1;
                _cursorY += downLines;
                await _eventWriter.WriteAsync(new CursorMovedEvent
                {
                    X = _cursorX,
                    Y = _cursorY,
                    OldX = _cursorX,
                    OldY = _cursorY - downLines
                });
                break;
                
            case 'C': // Cursor forward
                var forwardCols = parsedParams.Length > 0 ? parsedParams[0] : 1;
                _cursorX += forwardCols;
                await _eventWriter.WriteAsync(new CursorMovedEvent
                {
                    X = _cursorX,
                    Y = _cursorY,
                    OldX = _cursorX - forwardCols,
                    OldY = _cursorY
                });
                break;
                
            case 'D': // Cursor backward
                var backCols = parsedParams.Length > 0 ? parsedParams[0] : 1;
                _cursorX = Math.Max(0, _cursorX - backCols);
                await _eventWriter.WriteAsync(new CursorMovedEvent
                {
                    X = _cursorX,
                    Y = _cursorY,
                    OldX = _cursorX + backCols,
                    OldY = _cursorY
                });
                break;
                
            case 'J': // Clear screen
                var clearMode = parsedParams.Length > 0 ? parsedParams[0] : 0;
                if (clearMode == 2) // Clear entire screen
                {
                    await _eventWriter.WriteAsync(new ClearScreenEvent());
                    _cursorX = 0;
                    _cursorY = 0;
                }
                break;
                
            case 'K': // Clear line
                var lineClearMode = parsedParams.Length > 0 ? parsedParams[0] : 0;
                await _eventWriter.WriteAsync(new ClearLineEvent
                {
                    LineNumber = _cursorY,
                    Mode = lineClearMode switch
                    {
                        0 => ClearMode.ToEnd,
                        1 => ClearMode.ToStart,
                        2 => ClearMode.Entire,
                        _ => ClearMode.Entire
                    }
                });
                break;
                
            case 'h': // Set mode
                if (parameters == "?25") // Show cursor
                {
                    await _eventWriter.WriteAsync(new CursorVisibilityEvent { Visible = true });
                }
                break;
                
            case 'l': // Reset mode
                if (parameters == "?25") // Hide cursor
                {
                    await _eventWriter.WriteAsync(new CursorVisibilityEvent { Visible = false });
                }
                break;
        }
    }
    
    private async Task ExecuteOscCommandAsync(string sequence)
    {
        // Parse OSC number and text
        var semicolon = sequence.IndexOf(';');
        if (semicolon > 0)
        {
            if (int.TryParse(sequence[..semicolon], out var oscNumber))
            {
                var text = sequence[(semicolon + 1)..];
                
                switch (oscNumber)
                {
                    case 0: // Set window title
                    case 2:
                        await _eventWriter.WriteAsync(new TitleChangedEvent { Title = text });
                        break;
                }
            }
        }
    }
    
    private async Task EmitTextAsync(string text)
    {
        await _eventWriter.WriteAsync(new TextWrittenEvent
        {
            Text = text,
            CursorX = _cursorX,
            CursorY = _cursorY
        });
        
        // Check for prompt patterns
        if (text.TrimEnd().EndsWith(">") || text.TrimEnd().EndsWith("$"))
        {
            await _eventWriter.WriteAsync(new PromptDetectedEvent
            {
                PromptText = text.TrimEnd()
            });
        }
    }
    
    private static int[] ParseParameters(string paramString)
    {
        if (string.IsNullOrEmpty(paramString))
            return [];
        
        var parts = paramString.Split(';');
        var parameters = new List<int>();
        
        foreach (var part in parts)
        {
            if (int.TryParse(part, out var value))
                parameters.Add(value);
            else if (string.IsNullOrEmpty(part))
                parameters.Add(0);
        }
        
        return parameters.ToArray();
    }
}