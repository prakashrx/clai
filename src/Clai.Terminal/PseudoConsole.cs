using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using Clai.Terminal.Native;
using System.Runtime.InteropServices;

namespace Clai.Terminal;

/// <summary>
/// Wrapper for Windows Pseudo Console (ConPTY) functionality.
/// Combines native ConPTY API with .NET Process management.
/// </summary>
public sealed class PseudoConsole : IDisposable
{
    private readonly IntPtr handle;
    private readonly SafeFileHandle inputWriteHandle;
    private readonly SafeFileHandle outputReadHandle;
    private Process? attachedProcess;
    private bool disposed;

    public IntPtr Handle => handle;
    public SafeFileHandle InputWriter => inputWriteHandle;
    public SafeFileHandle OutputReader => outputReadHandle;
    public Process? AttachedProcess => attachedProcess;

    private PseudoConsole(IntPtr handle, SafeFileHandle inputWrite, SafeFileHandle outputRead)
    {
        this.handle = handle;
        this.inputWriteHandle = inputWrite;
        this.outputReadHandle = outputRead;
    }

    /// <summary>
    /// Creates a new pseudo console with specified dimensions.
    /// </summary>
    public static PseudoConsole Create(short columns, short rows)
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
    /// Resizes the pseudo console.
    /// </summary>
    public void Resize(short columns, short rows)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        
        var size = new ConPtyApi.COORD(columns, rows);
        var result = ConPtyApi.ResizePseudoConsole(handle, size);
        
        if (result != 0)
        {
            throw new InvalidOperationException($"Failed to resize pseudo console. Error: {result}");
        }
    }

    /// <summary>
    /// Starts a process attached to this pseudo console.
    /// </summary>
    public Process StartProcess(string command, string? workingDirectory = null, IDictionary<string, string>? environment = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        
        if (attachedProcess != null && !attachedProcess.HasExited)
        {
            throw new InvalidOperationException("A process is already attached to this pseudo console");
        }

        // Prepare the command line
        var commandLine = string.IsNullOrWhiteSpace(command) ? "cmd.exe" : $"cmd.exe /c {command}";
        
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
                handle,  // Pass the ConPTY handle directly, not a pointer to it
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
            attachedProcess = Process.GetProcessById(processInfo.dwProcessId);
            return attachedProcess;
        }
        finally
        {
            ConPtyApi.DeleteProcThreadAttributeList(attributeList);
            Marshal.FreeHGlobal(attributeList);
        }
    }

    public void Dispose()
    {
        if (disposed) return;

        // Kill attached process if still running
        if (attachedProcess != null && !attachedProcess.HasExited)
        {
            try
            {
                attachedProcess.Kill();
                attachedProcess.WaitForExit(1000);
            }
            catch { }
            finally
            {
                attachedProcess.Dispose();
            }
        }

        // Close ConPTY
        ConPtyApi.ClosePseudoConsole(handle);

        // Close handles
        inputWriteHandle?.Dispose();
        outputReadHandle?.Dispose();

        disposed = true;
    }
}