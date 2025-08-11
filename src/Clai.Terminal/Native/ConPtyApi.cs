using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Clai.Terminal.Native;

/// <summary>
/// P/Invoke declarations for Windows ConPTY API
/// </summary>
internal static class ConPtyApi
{
    private const string Kernel32 = "kernel32.dll";
    
    [DllImport(Kernel32, SetLastError = true)]
    internal static extern int CreatePseudoConsole(
        COORD size,
        SafeFileHandle hInput,
        SafeFileHandle hOutput,
        uint dwFlags,
        out IntPtr phPC);
    
    [DllImport(Kernel32, SetLastError = true)]
    internal static extern int ResizePseudoConsole(IntPtr hPC, COORD size);
    
    [DllImport(Kernel32, SetLastError = true)]
    internal static extern void ClosePseudoConsole(IntPtr hPC);
    
    [DllImport(Kernel32, SetLastError = true)]
    internal static extern bool CreatePipe(
        out SafeFileHandle hReadPipe,
        out SafeFileHandle hWritePipe,
        IntPtr lpPipeAttributes,
        uint nSize);
    
    [DllImport(Kernel32, SetLastError = true)]
    internal static extern bool InitializeProcThreadAttributeList(
        IntPtr lpAttributeList,
        int dwAttributeCount,
        int dwFlags,
        ref IntPtr lpSize);
    
    [DllImport(Kernel32, SetLastError = true)]
    internal static extern bool UpdateProcThreadAttribute(
        IntPtr lpAttributeList,
        uint dwFlags,
        IntPtr attribute,
        IntPtr lpValue,
        IntPtr cbSize,
        IntPtr lpPreviousValue,
        IntPtr lpReturnSize);
    
    [DllImport(Kernel32, SetLastError = true)]
    internal static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);
    
    internal const uint PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;
    
    [StructLayout(LayoutKind.Sequential)]
    internal struct COORD
    {
        public short X;
        public short Y;
        
        public COORD(short x, short y)
        {
            X = x;
            Y = y;
        }
    }
}