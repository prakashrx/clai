using Clai.Terminal;
using Clai.Terminal.Events;
using Spectre.Console;

AnsiConsole.MarkupLine("[bold cyan]CLAI - Command Line AI[/]");
AnsiConsole.MarkupLine("[dim]Event Streaming Terminal - Proof of Concept[/]");
AnsiConsole.WriteLine();

// Create pseudo console
using var pty = PseudoConsole.Create(columns: 120, rows: 30);

// Start the shell
pty.StartProcess();

// Give shell time to initialize
await Task.Delay(500);

// Task to read and display events
var eventTask = Task.Run(async () =>
{
    await foreach (var evt in pty.Events.ReadAllAsync())
    {
        switch (evt)
        {
            case TextWrittenEvent text:
                // Show text with cursor position for debugging
                AnsiConsole.MarkupLine($"[dim green]TEXT[/] @({text.CursorX},{text.CursorY}): {Markup.Escape(text.Text)}");
                break;

            case NewLineEvent:
                AnsiConsole.MarkupLine("[dim yellow]NEWLINE[/]");
                break;

            case CursorMovedEvent cursor:
                AnsiConsole.MarkupLine($"[dim blue]CURSOR:[/] ({cursor.X}, {cursor.Y})");
                break;

            case ClearScreenEvent:
                AnsiConsole.MarkupLine("[dim red]CLEAR SCREEN[/]");
                break;

            case PromptDetectedEvent prompt:
                AnsiConsole.MarkupLine($"[bold yellow]PROMPT:[/] {Markup.Escape(prompt.PromptText)}");
                break;

            case CommandCompletedEvent completed:
                AnsiConsole.MarkupLine($"[bold green]COMMAND COMPLETED[/] Exit Code: {completed.ExitCode}");
                return; // Exit the event loop

            default:
                AnsiConsole.MarkupLine($"[dim]Event:[/] {evt.GetType().Name}");
                break;
        }
    }
});

// Send a test command
await Task.Delay(1000);
AnsiConsole.MarkupLine("[bold]Sending command: echo Hello World[/]");
await pty.WriteLineAsync("dir");

// Wait for events to process
await Task.WhenAny(eventTask, Task.Delay(3000));

// Send exit command to cleanly close (only if still running)
if (pty.IsRunning)
{
    await pty.WriteLineAsync("exit");
    await Task.Delay(500); // Give it time to exit cleanly
}

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[green]Test completed![/]");
