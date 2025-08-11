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
        // For now, in AI mode we'll just execute the command too
        // Later this will interpret natural language and create commands
        
        try
        {
            var terminal = context.Terminal;
            
            // Show what we're executing
            var result = $"[dim yellow][[AI: Executing '{input}']][/]\n";
            
            // Execute the command and get just its output
            var output = await terminal.ExecuteCommandAsync(input, cancellationToken: cancellationToken);
            result += output;
            
            return new CommandResult(result, true, Type: CommandType.AI);
        }
        catch (Exception ex)
        {
            return new CommandResult($"[red]Error: {ex.Message}[/]", false, Type: CommandType.AI);
        }
    }

    private async Task<CommandResult> ProcessManualCommand(string input, CancellationToken cancellationToken)
    {
        try
        {
            // Get the terminal from context
            var terminal = context.Terminal;
            
            // Execute the command and get just its output
            var output = await terminal.ExecuteCommandAsync(input, cancellationToken: cancellationToken);
            
            return new CommandResult(output, true, Type: CommandType.Manual);
        }
        catch (Exception ex)
        {
            return new CommandResult($"Error executing command: {ex.Message}", false, Type: CommandType.Manual);
        }
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