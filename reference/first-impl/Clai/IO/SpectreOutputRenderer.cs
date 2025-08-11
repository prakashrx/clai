namespace Clai.IO;

public class SpectreOutputRenderer : IOutputRenderer
{
    public void ShowWelcome()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(
            new FigletText("CLAI")
                .Centered()
                .Color(Color.Cyan1));
        
        AnsiConsole.MarkupLine("[dim]Command Line AI - Your intelligent terminal companion[/]");
        AnsiConsole.MarkupLine("[dim]Type 'help' for commands, 'exit' to quit[/]");
        AnsiConsole.WriteLine();
    }

    public void ShowGoodbye()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]Goodbye![/]");
    }

    public void RenderResult(CommandResult result)
    {
        var color = result.Type switch
        {
            CommandType.AI => "cyan",
            CommandType.Manual => "green",
            CommandType.System => "yellow",
            _ => "white"
        };

        if (result.IsSuccess)
            AnsiConsole.MarkupLine($"[{color}]{result.Output}[/]");
        else
            ShowError(result.Output);
    }

    public void ShowStatus(string message)
    {
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .Start(message, ctx =>
            {
                Thread.Sleep(500); // Will be replaced with actual work
            });
    }

    public void ShowError(string message)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
    }
}