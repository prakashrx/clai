namespace Clai.Core;

public class ReplEngine(
    IInputHandler inputHandler,
    ICommandProcessor commandProcessor,
    IOutputRenderer outputRenderer)
{
    private bool isRunning;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        isRunning = true;
        outputRenderer.ShowWelcome();

        while (isRunning && !cancellationToken.IsCancellationRequested)
        {
            var input = await inputHandler.GetInputAsync(cancellationToken);
            
            if (string.IsNullOrWhiteSpace(input))
                continue;

            var result = await commandProcessor.ProcessAsync(input, cancellationToken);
            
            if (result.ShouldExit)
                isRunning = false;
            else
                outputRenderer.RenderResult(result);
        }

        outputRenderer.ShowGoodbye();
    }
}