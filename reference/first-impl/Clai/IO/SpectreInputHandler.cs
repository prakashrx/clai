namespace Clai.IO;

public class SpectreInputHandler : IInputHandler
{
    private readonly List<string> history = [];
    private readonly SessionContext context;

    public SpectreInputHandler(SessionContext context)
    {
        this.context = context;
    }

    public async Task<string> GetInputAsync(CancellationToken cancellationToken = default)
    {
        var prompt = context.Mode == OperationMode.AI 
            ? "[cyan]ai[/]" 
            : "[green]manual[/]";
            
        var input = await Task.Run(() => 
            AnsiConsole.Prompt(
                new TextPrompt<string>($"{prompt} [dim]>[/] ")
                    .AllowEmpty()), 
            cancellationToken);
        
        if (!string.IsNullOrWhiteSpace(input))
            AddToHistory(input);
            
        return input;
    }

    public void AddToHistory(string input) => history.Add(input);
    
    public IReadOnlyList<string> GetHistory() => history.AsReadOnly();
}