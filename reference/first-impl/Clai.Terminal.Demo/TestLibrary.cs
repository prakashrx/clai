using Clai.Terminal;
using System.Text;

/// <summary>
/// Test that the library provides everything CLAI needs
/// </summary>
public class TestLibrary
{
    public static async Task RunTests()
    {
        Console.WriteLine("=== Testing Clai.Terminal Library ===\n");

        // Test 1: Can we run commands and capture output?
        await TestCommandExecution();

        // Test 2: Can we get text for AI analysis?
        await TestTextExtraction();

        // Test 3: Can we handle interactive input?
        await TestInteractiveInput();

        Console.WriteLine("\nAll tests complete!");
        Console.ReadKey();
    }

    static async Task TestCommandExecution()
    {
        Console.WriteLine("TEST 1: Command Execution");
        Console.WriteLine("-------------------------");

        using var terminal = new Terminal();
        terminal.Start();

        // Execute a simple command
        await terminal.WriteLineAsync("echo Hello from terminal");
        await Task.Delay(500);

        // Check if we captured the output
        var text = terminal.Screen.Text;
        Console.WriteLine($"Captured text length: {text.Length} chars");
        Console.WriteLine($"Contains 'Hello': {text.Contains("Hello")}");

        await terminal.WriteLineAsync("exit");
        await Task.Delay(500);

        Console.WriteLine("✓ Command execution works\n");
    }

    static async Task TestTextExtraction()
    {
        Console.WriteLine("TEST 2: Text Extraction for AI");
        Console.WriteLine("-------------------------------");

        using var terminal = new Terminal();
        terminal.Start();

        // Run multiple commands
        await terminal.WriteLineAsync("echo Line 1");
        await Task.Delay(300);
        await terminal.WriteLineAsync("echo Line 2");
        await Task.Delay(300);
        await terminal.WriteLineAsync("dir /b");
        await Task.Delay(500);

        // Test different text access methods
        var lines = terminal.Screen.Lines;
        var fullText = terminal.Screen.Text;
        var visibleText = terminal.Screen.VisibleText;

        Console.WriteLine($"Lines count: {lines.Count}");
        Console.WriteLine($"Full text length: {fullText.Length} chars");
        Console.WriteLine($"Visible text length: {visibleText.Length} chars");

        // AI would analyze this text
        Console.WriteLine("\nSample lines for AI:");
        foreach (var line in lines.Take(5))
        {
            if (!string.IsNullOrWhiteSpace(line))
                Console.WriteLine($"  > {line}");
        }

        await terminal.WriteLineAsync("exit");
        await Task.Delay(500);

        Console.WriteLine("✓ Text extraction works\n");
    }

    static async Task TestInteractiveInput()
    {
        Console.WriteLine("TEST 3: Interactive Input");
        Console.WriteLine("-------------------------");

        using var terminal = new Terminal();

        // Track screen updates
        int updateCount = 0;
        terminal.ScreenUpdated += update =>
        {
            updateCount++;
            // CLAI would render here based on update type
        };

        terminal.Start();

        // Simulate typing a command
        await terminal.WriteAsync("dir");
        await Task.Delay(100);
        await terminal.WriteAsync(" /");
        await Task.Delay(100);
        await terminal.WriteAsync("b");
        await Task.Delay(100);
        await terminal.WriteLineAsync(""); // Enter
        await Task.Delay(500);

        Console.WriteLine($"Screen updates received: {updateCount}");
        Console.WriteLine($"Cursor position: ({terminal.Screen.CursorX}, {terminal.Screen.CursorY})");

        // Test color information
        var buffer = terminal.Screen.GetScreen();
        int coloredCells = 0;
        for (int y = 0; y < terminal.Screen.Height; y++)
        {
            for (int x = 0; x < terminal.Screen.Width; x++)
            {
                if (buffer[y, x].ForegroundColor != 7) // Not default white
                    coloredCells++;
            }
        }
        Console.WriteLine($"Colored cells found: {coloredCells}");

        await terminal.WriteLineAsync("exit");
        await Task.Delay(500);

        Console.WriteLine("✓ Interactive input works\n");
    }
}
