using Clai.Commands;
using Clai.Core;
using Clai.IO;

// Set up cancellation for graceful shutdown
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// Set up dependencies
var context = new SessionContext();
var inputHandler = new SpectreInputHandler(context);
var outputRenderer = new SpectreOutputRenderer();
var commandProcessor = new CommandProcessor(context);

// Create and run the REPL
var repl = new ReplEngine(inputHandler, commandProcessor, outputRenderer);

try
{
    await repl.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
    // Expected when user cancels
    outputRenderer.ShowGoodbye();
}
catch (Exception ex)
{
    outputRenderer.ShowError($"Unexpected error: {ex.Message}");
}