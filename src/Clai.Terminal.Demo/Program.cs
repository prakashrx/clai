using Clai.Terminal;
using Clai.Terminal.Demo;

Console.WriteLine("=== Clai.Terminal Demo ===\n");
Console.WriteLine("1. Run a simple command");
Console.WriteLine("2. Interactive terminal");
Console.WriteLine("3. PowerShell example");
Console.WriteLine("4. Exit\n");

Console.Write("Choose an option: ");
var choice = Console.ReadLine();

try
{
    switch (choice)
    {
        case "1":
            await RunSimpleCommand();
            break;
        case "2":
            await RunInteractiveTerminal();
            break;
        case "3":
            await RunPowerShellExample();
            break;
        default:
            Console.WriteLine("Exiting...");
            break;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\nError: {ex.Message}");
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();

// Demo 1: Run a simple command
async Task RunSimpleCommand()
{
    Console.WriteLine("\n=== Running Simple Command ===\n");
    
    using var terminal = new Terminal();
    
    // Simple renderer - just display new content
    terminal.ScreenUpdated += update =>
    {
        // For simple demo, just render the entire screen
        RenderScreen(terminal.Screen);
    };
    
    terminal.ProcessExited += code => Console.WriteLine($"\nProcess exited with code: {code}");
    
    terminal.Start();
    
    Console.WriteLine("Executing 'dir' command...\n");
    await terminal.WriteLineAsync("dir");
    await Task.Delay(1000);
    
    await terminal.WriteLineAsync("exit");
    await Task.Delay(500);
}

// Demo 2: Interactive terminal
async Task RunInteractiveTerminal()
{
    Console.Clear();
    
    using var terminal = new Terminal();
    var renderer = new TerminalRenderer(terminal.Screen);
    
    // Use optimized renderer
    terminal.ScreenUpdated += update => renderer.RequestUpdate();
    
    terminal.ProcessExited += code => 
    {
        Console.SetCursorPosition(0, terminal.Screen.Height + 1);
        Console.WriteLine($"Process exited with code: {code}");
    };
    
    terminal.Start();
    
    // Initial render
    renderer.RenderImmediate();
    
    // Read keys until ESC
    while (terminal.IsRunning)
    {
        var key = Console.ReadKey(intercept: true);
        
        if (key.Key == ConsoleKey.Escape)
        {
            Console.SetCursorPosition(0, terminal.Screen.Height + 1);
            Console.WriteLine("ESC pressed. Exiting...");
            break;
        }
        
        await terminal.SendKeyAsync(key);
    }
    
    terminal.Stop();
}

// Demo 3: PowerShell example
async Task RunPowerShellExample()
{
    Console.Clear();
    
    using var terminal = new Terminal();
    var renderer = new TerminalRenderer(terminal.Screen);
    
    terminal.ScreenUpdated += update => renderer.RequestUpdate();
    
    terminal.Start("powershell.exe -NoLogo");
    
    // Initial render
    renderer.RenderImmediate();
    
    await Task.Delay(1000); // Wait for PowerShell to initialize
    
    await terminal.WriteLineAsync("Get-Process | Sort-Object CPU -Descending | Select-Object -First 5 Name, CPU");
    await Task.Delay(2000);
    
    await terminal.WriteLineAsync("Get-Date");
    await Task.Delay(1000);
    
    await terminal.WriteLineAsync("exit");
    await Task.Delay(500);
}

// Simple screen renderer
void RenderScreen(Screen screen)
{
    Console.SetCursorPosition(0, 0);
    var buffer = screen.GetScreen();
    
    for (int y = 0; y < screen.Height; y++)
    {
        for (int x = 0; x < screen.Width; x++)
        {
            var cell = buffer[y, x];
            
            // Apply colors if different from current
            var fg = TerminalCell.AnsiToConsoleColor(cell.ForegroundColor);
            var bg = TerminalCell.AnsiToConsoleColor(cell.BackgroundColor);
            
            if (cell.Inverse)
            {
                (fg, bg) = (bg, fg);
            }
            
            Console.ForegroundColor = fg;
            Console.BackgroundColor = bg;
            
            // Apply bold (make it bright)
            if (cell.Bold && fg <= ConsoleColor.Gray)
            {
                Console.ForegroundColor = fg + 8;
            }
            
            Console.Write(cell.Char);
        }
        
        // Clear rest of line if terminal is smaller than console
        if (screen.Width < Console.WindowWidth)
        {
            Console.Write(new string(' ', Console.WindowWidth - screen.Width));
        }
    }
    
    // Reset colors
    Console.ResetColor();
    
    // Set cursor position
    Console.SetCursorPosition(screen.CursorX, screen.CursorY);
}
