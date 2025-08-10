using System.Diagnostics;
using System.Text;
using Debug = System.Diagnostics.Debug;

namespace Clai.Terminal;

/// <summary>
/// High-level terminal emulator that wraps ConPTY complexity
/// </summary>
public class Terminal : IDisposable
{
    private readonly PseudoConsole pseudoConsole;
    private readonly FileStream inputStream;
    private readonly FileStream outputStream;
    private readonly StreamWriter inputWriter;
    private readonly CancellationTokenSource cancellationTokenSource;
    private readonly Screen screen;
    private Task? readTask;
    private bool disposed;

    public Process? Process { get; private set; }
    public bool IsRunning => Process != null && !Process.HasExited;
    public Screen Screen => screen;
    
    // Event fired when the screen is updated
    public event Action<TerminalUpdate>? ScreenUpdated;
    
    // Event fired when the process exits
    public event Action<int>? ProcessExited;

    public Terminal(short columns = 80, short rows = 24)
    {
        pseudoConsole = PseudoConsole.Create(columns, rows);
        inputStream = new FileStream(pseudoConsole.InputWriter, FileAccess.Write);
        outputStream = new FileStream(pseudoConsole.OutputReader, FileAccess.Read);
        // Use UTF8 without BOM to avoid extra characters
        inputWriter = new StreamWriter(inputStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
        cancellationTokenSource = new CancellationTokenSource();
        
        // Create screen buffer
        screen = new Screen(columns, rows);
        screen.Updated += update => ScreenUpdated?.Invoke(update);
    }

    /// <summary>
    /// Starts a command in the terminal
    /// </summary>
    public void Start(string command = "", string? workingDirectory = null)
    {
        if (Process != null)
            throw new InvalidOperationException("Terminal already has a running process");

        Process = pseudoConsole.StartProcess(command, workingDirectory);
        
        // Start reading output
        readTask = Task.Run(() => ReadOutputAsync(cancellationTokenSource.Token));
        
        // Monitor process exit
        Task.Run(async () =>
        {
            await Process.WaitForExitAsync();
            ProcessExited?.Invoke(Process.ExitCode);
        });
    }

    /// <summary>
    /// Writes text to the terminal
    /// </summary>
    public async Task WriteAsync(string text)
    {
        if (!IsRunning)
            throw new InvalidOperationException("No process is running");
            
        await inputWriter.WriteAsync(text);
    }

    /// <summary>
    /// Writes a line to the terminal (adds \r\n)
    /// </summary>
    public async Task WriteLineAsync(string text)
    {
        await WriteAsync(text + "\r\n");
    }

    /// <summary>
    /// Sends a key press to the terminal
    /// </summary>
    public async Task SendKeyAsync(ConsoleKeyInfo key)
    {
        if (!IsRunning)
            throw new InvalidOperationException("No process is running");

        Debug.WriteLine($"[INPUT] Key={key.Key}, Char='{key.KeyChar}' (0x{(int)key.KeyChar:X2}), Modifiers={key.Modifiers}");
        
        string text;
        
        // Handle special keys
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                text = "\r";  // Just CR, terminals handle line ending
                break;
                
            case ConsoleKey.Backspace:
                text = "\x7F";  // DEL character (127) for backspace
                break;
                
            case ConsoleKey.Tab:
                text = "\t";
                break;
                
            case ConsoleKey.Escape:
                text = "\x1b";
                break;
                
            // Arrow keys - send VT sequences
            case ConsoleKey.UpArrow:
                text = "\x1b[A";
                break;
            case ConsoleKey.DownArrow:
                text = "\x1b[B";
                break;
            case ConsoleKey.RightArrow:
                text = "\x1b[C";
                break;
            case ConsoleKey.LeftArrow:
                text = "\x1b[D";
                break;
                
            // Other navigation keys
            case ConsoleKey.Home:
                text = "\x1b[H";
                break;
            case ConsoleKey.End:
                text = "\x1b[F";
                break;
            case ConsoleKey.PageUp:
                text = "\x1b[5~";
                break;
            case ConsoleKey.PageDown:
                text = "\x1b[6~";
                break;
            case ConsoleKey.Insert:
                text = "\x1b[2~";
                break;
            case ConsoleKey.Delete:
                text = "\x1b[3~";
                break;
                
            default:
                // Handle Ctrl combinations
                if ((key.Modifiers & ConsoleModifiers.Control) != 0)
                {
                    if (key.Key == ConsoleKey.C)
                        text = "\x03";
                    else if (key.Key == ConsoleKey.Z)
                        text = "\x1a";
                    else if (key.Key == ConsoleKey.D)
                        text = "\x04";
                    else if (key.Key == ConsoleKey.A)
                        text = "\x01";
                    else if (key.Key == ConsoleKey.E)
                        text = "\x05";
                    else if (key.KeyChar != '\0')
                        text = key.KeyChar.ToString();
                    else
                        return; // Unknown control combination
                }
                // Regular printable character
                else if (key.KeyChar != '\0')
                {
                    text = key.KeyChar.ToString();
                }
                else
                {
                    // Non-printable key without special handling
                    return;
                }
                break;
        }

        await WriteAsync(text);
    }

    /// <summary>
    /// Resizes the terminal
    /// </summary>
    public void Resize(short columns, short rows)
    {
        pseudoConsole.Resize(columns, rows);
    }

    /// <summary>
    /// Stops the running process
    /// </summary>
    public void Stop()
    {
        if (Process != null && !Process.HasExited)
        {
            Process.Kill();
            Process.WaitForExit(1000);
        }
    }

    private async Task ReadOutputAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        try
        {
            while (!cancellationToken.IsCancellationRequested && IsRunning)
            {
                var bytesRead = await outputStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                
                if (bytesRead > 0)
                {
                    // Feed raw bytes to screen for processing
                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);
                    
                    // Debug output
                    var str = Encoding.UTF8.GetString(data);
                    var escaped = str.Replace("\x1B", "\\x1B").Replace("\r", "\\r").Replace("\n", "\\n");
                    Debug.WriteLine($"[OUTPUT] {bytesRead} bytes: \"{escaped}\"");
                    
                    screen.Write(data);
                }
                else
                {
                    // No data available, small delay to prevent busy loop
                    await Task.Delay(10, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when disposing
        }
    }

    public void Dispose()
    {
        if (disposed) return;

        cancellationTokenSource.Cancel();
        readTask?.Wait(1000);
        
        inputWriter?.Dispose();
        inputStream?.Dispose();
        outputStream?.Dispose();
        pseudoConsole?.Dispose();
        cancellationTokenSource?.Dispose();
        
        disposed = true;
    }
}