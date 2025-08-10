namespace Clai.Commands;

public class CommandProcessor(SessionContext context) : ICommandProcessor
{
    private readonly Dictionary<string, Func<Task<CommandResult>>> systemCommands = new()
    {
        ["exit"] = () => Task.FromResult(new CommandResult("", true, ShouldExit: true)),
        ["quit"] = () => Task.FromResult(new CommandResult("", true, ShouldExit: true)),
        ["q"] = () => Task.FromResult(new CommandResult("", true, ShouldExit: true)),
        ["clear"] = () => { AnsiConsole.Clear(); return Task.FromResult(new CommandResult("", true, Type: CommandType.System)); },
        ["cls"] = () => { AnsiConsole.Clear(); return Task.FromResult(new CommandResult("", true, Type: CommandType.System)); },
        ["help"] = ShowHelp,
        ["?"] = ShowHelp,
    };

    public async Task<CommandResult> ProcessAsync(string input, CancellationToken cancellationToken = default)
    {
        var trimmedInput = input.Trim().ToLowerInvariant();
        
        // Check for system commands
        if (systemCommands.TryGetValue(trimmedInput, out var systemCommand))
            return await systemCommand();
        
        // Special case for mode switching
        if (trimmedInput == "mode")
        {
            context.Mode = context.Mode == OperationMode.AI ? OperationMode.Manual : OperationMode.AI;
            return new CommandResult($"Switched to {context.Mode} mode", true, Type: CommandType.System);
        }
        
        // Process based on current mode
        return context.Mode == OperationMode.AI
            ? await ProcessAICommand(input, cancellationToken)
            : await ProcessManualCommand(input, cancellationToken);
    }

    private async Task<CommandResult> ProcessAICommand(string input, CancellationToken cancellationToken)
    {
        // Simulate AI processing
        await Task.Delay(300, cancellationToken);
        
        var response = input.ToLowerInvariant() switch
        {
            var s when s.Contains("hello") => "Hello! I'm CLAI, ready to help you with terminal commands.",
            var s when s.Contains("time") => $"The current time is {DateTime.Now:HH:mm:ss}",
            var s when s.Contains("date") => $"Today is {DateTime.Now:dddd, MMMM d, yyyy}",
            _ => $"I understand you want to: '{input}'. Terminal integration coming soon!"
        };
        
        return new CommandResult($"[cyan]AI:[/] {response}", true, Type: CommandType.AI);
    }

    private async Task<CommandResult> ProcessManualCommand(string input, CancellationToken cancellationToken)
    {
        // TODO: Integrate with ConPTY for actual command execution
        await Task.Delay(100, cancellationToken);
        return new CommandResult($"Would execute: {input}", true, Type: CommandType.Manual);
    }

    private static Task<CommandResult> ShowHelp()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Command")
            .AddColumn("Description");
            
        table.AddRow("help, ?", "Show this help message");
        table.AddRow("mode", "Toggle between AI and Manual mode");
        table.AddRow("clear, cls", "Clear the screen");
        table.AddRow("exit, quit, q", "Exit CLAI");
        table.AddRow("Ctrl+C", "Interrupt current operation");
        
        // Render the table directly to the console
        AnsiConsole.Write(table);
        
        // Return empty result since we've already rendered
        return Task.FromResult(new CommandResult("", true, Type: CommandType.System));
    }
}