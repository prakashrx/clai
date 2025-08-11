using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Clai.Terminal.Events;
using Clai.Terminal.Native;
using Microsoft.Win32.SafeHandles;

namespace Clai.Terminal;

/// <summary>
/// Wrapper for Windows Pseudo Console (ConPTY) with event streaming
/// </summary>
public sealed class PseudoConsole : IDisposable
{
    private readonly IntPtr _handle;
    private readonly SafeFileHandle _inputWriteHandle;
    private readonly SafeFileHandle _outputReadHandle;
    private readonly Channel<TerminalEvent> _eventChannel;
    private readonly AnsiEventParser _parser;
    private readonly FileStream _inputStream;
    private readonly StreamWriter _inputWriter;
    private readonly FileStream _outputStream;
    private Process? _attachedProcess;
    private Task? _readTask;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _disposed;
    
    /// <summary>
    /// The attached process
    /// </summary>
    public Process? AttachedProcess => _attachedProcess;
    
    /// <summary>
    /// Whether a process is running
    /// </summary>
    public bool IsRunning => _attachedProcess != null && !_attachedProcess.HasExited;
    
    /// <summary>
    /// Event stream reader
    /// </summary>
    public ChannelReader<TerminalEvent> Events => _eventChannel.Reader;
    
    private PseudoConsole(IntPtr handle, SafeFileHandle inputWrite, SafeFileHandle outputRead)
    {
        _handle = handle;
        _inputWriteHandle = inputWrite;
        _outputReadHandle = outputRead;
        
        // Create streams for I/O
        _inputStream = new FileStream(_inputWriteHandle, FileAccess.Write);
        _outputStream = new FileStream(_outputReadHandle, FileAccess.Read);
        _inputWriter = new StreamWriter(_inputStream, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
        
        // Create unbounded channel for events
        _eventChannel = Channel.CreateUnbounded<TerminalEvent>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = true
        });
        
        // Create parser that will emit events
        _parser = new AnsiEventParser(_eventChannel.Writer);
    }
    
    /// <summary>
    /// Creates a new pseudo console
    /// </summary>
    public static PseudoConsole Create(short columns = 80, short rows = 24)
    {
        // Create pipes for ConPTY communication
        if (!ConPtyApi.CreatePipe(out var inputReadHandle, out var inputWriteHandle, IntPtr.Zero, 0))
        {
            throw new InvalidOperationException($"Failed to create input pipe. Error: {Marshal.GetLastWin32Error()}");
        }
        
        if (!ConPtyApi.CreatePipe(out var outputReadHandle, out var outputWriteHandle, IntPtr.Zero, 0))
        {
            inputReadHandle.Dispose();
            inputWriteHandle.Dispose();
            throw new InvalidOperationException($"Failed to create output pipe. Error: {Marshal.GetLastWin32Error()}");
        }
        
        var size = new ConPtyApi.COORD(columns, rows);
        var result = ConPtyApi.CreatePseudoConsole(
            size,
            inputReadHandle,
            outputWriteHandle,
            0,
            out var hPty);
        
        if (result != 0)
        {
            inputReadHandle.Dispose();
            inputWriteHandle.Dispose();
            outputReadHandle.Dispose();
            outputWriteHandle.Dispose();
            throw new InvalidOperationException($"Failed to create pseudo console. Error: {result}");
        }
        
        // Close the handles that ConPTY now owns
        inputReadHandle.Dispose();
        outputWriteHandle.Dispose();
        
        return new PseudoConsole(hPty, inputWriteHandle, outputReadHandle);
    }
    
    /// <summary>
    /// Starts a process attached to this pseudo console
    /// </summary>
    public void StartProcess(string command = "", string? workingDirectory = null)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PseudoConsole));
        
        if (_attachedProcess != null && !_attachedProcess.HasExited)
            throw new InvalidOperationException("A process is already attached to this pseudo console");
        
        // Prepare the command line - always start cmd.exe as persistent shell
        var commandLine = "cmd.exe";
        
        // Initialize process and thread attributes
        var lpSize = IntPtr.Zero;
        var success = ConPtyApi.InitializeProcThreadAttributeList(
            IntPtr.Zero, 1, 0, ref lpSize);
        
        if (lpSize == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to get attribute list size");
        }
        
        var attributeList = Marshal.AllocHGlobal(lpSize);
        try
        {
            success = ConPtyApi.InitializeProcThreadAttributeList(
                attributeList, 1, 0, ref lpSize);
            
            if (!success)
            {
                throw new InvalidOperationException($"Failed to initialize attribute list. Error: {Marshal.GetLastWin32Error()}");
            }
            
            // Update the attribute list with the ConPTY handle
            success = ConPtyApi.UpdateProcThreadAttribute(
                attributeList,
                0,
                (IntPtr)ConPtyApi.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                _handle,
                (IntPtr)IntPtr.Size,
                IntPtr.Zero,
                IntPtr.Zero);
            
            if (!success)
            {
                throw new InvalidOperationException($"Failed to update attribute list. Error: {Marshal.GetLastWin32Error()}");
            }
            
            // Create the process
            var startupInfo = new ProcessApi.STARTUPINFOEX
            {
                StartupInfo = new ProcessApi.STARTUPINFO
                {
                    cb = Marshal.SizeOf<ProcessApi.STARTUPINFOEX>()
                },
                lpAttributeList = attributeList
            };
            
            var processInfo = new ProcessApi.PROCESS_INFORMATION();
            var securityAttrs = new ProcessApi.SECURITY_ATTRIBUTES
            {
                nLength = Marshal.SizeOf<ProcessApi.SECURITY_ATTRIBUTES>()
            };
            
            success = ProcessApi.CreateProcess(
                null,
                commandLine,
                ref securityAttrs,
                ref securityAttrs,
                false,
                ProcessApi.EXTENDED_STARTUPINFO_PRESENT,
                IntPtr.Zero,
                workingDirectory,
                ref startupInfo,
                out processInfo);
            
            if (!success)
            {
                throw new InvalidOperationException($"Failed to create process. Error: {Marshal.GetLastWin32Error()}");
            }
            
            // Close thread handle immediately (we don't need it)
            ProcessApi.CloseHandle(processInfo.hThread);
            
            // Create a Process object from the handle
            _attachedProcess = Process.GetProcessById(processInfo.dwProcessId);
            
            // Start reading output
            _cancellationTokenSource = new CancellationTokenSource();
            _readTask = Task.Run(() => ReadOutputAsync(_cancellationTokenSource.Token));
            
            // Monitor process exit
            Task.Run(async () =>
            {
                await _attachedProcess.WaitForExitAsync();
                // Only emit CommandCompletedEvent if the channel is still open
                // This prevents immediate termination events from closing everything
                if (!_eventChannel.Reader.Completion.IsCompleted)
                {
                    _eventChannel.Writer.TryWrite(new CommandCompletedEvent
                    {
                        ExitCode = _attachedProcess.ExitCode
                    });
                }
            });
        }
        finally
        {
            ConPtyApi.DeleteProcThreadAttributeList(attributeList);
            Marshal.FreeHGlobal(attributeList);
        }
    }
    
    /// <summary>
    /// Writes text to the terminal
    /// </summary>
    public async Task WriteAsync(string text)
    {
        if (!IsRunning)
            throw new InvalidOperationException("No process is running");
        
        await _inputWriter.WriteAsync(text);
    }
    
    /// <summary>
    /// Writes a line to the terminal
    /// </summary>
    public async Task WriteLineAsync(string text)
    {
        await WriteAsync(text + "\r\n");
    }
    
    /// <summary>
    /// Resizes the pseudo console
    /// </summary>
    public void Resize(short columns, short rows)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PseudoConsole));
        
        var size = new ConPtyApi.COORD(columns, rows);
        var result = ConPtyApi.ResizePseudoConsole(_handle, size);
        
        if (result != 0)
        {
            throw new InvalidOperationException($"Failed to resize pseudo console. Error: {result}");
        }
    }
    
    private async Task ReadOutputAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        
        try
        {
            while (!cancellationToken.IsCancellationRequested && IsRunning)
            {
                var bytesRead = await _outputStream.ReadAsync(buffer, cancellationToken);
                
                if (bytesRead > 0)
                {
                    // Pass bytes to parser which will emit events
                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);
                    await _parser.ProcessBytesAsync(data);
                }
                else
                {
                    // No data available
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
        if (_disposed) return;
        
        _cancellationTokenSource?.Cancel();
        _readTask?.Wait(1000);
        
        // Kill attached process if still running
        if (_attachedProcess != null && !_attachedProcess.HasExited)
        {
            try
            {
                _attachedProcess.Kill();
                _attachedProcess.WaitForExit(1000);
            }
            catch { }
            finally
            {
                _attachedProcess.Dispose();
            }
        }
        
        // Close ConPTY
        ConPtyApi.ClosePseudoConsole(_handle);
        
        // Close handles
        _inputWriteHandle?.Dispose();
        _outputReadHandle?.Dispose();
        
        // Complete the event channel
        _eventChannel.Writer.TryComplete();
        
        _cancellationTokenSource?.Dispose();
        _disposed = true;
    }
}