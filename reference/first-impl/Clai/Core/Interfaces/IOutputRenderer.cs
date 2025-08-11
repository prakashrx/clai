namespace Clai.Core;

public interface IOutputRenderer
{
    void ShowWelcome();
    void ShowGoodbye();
    void RenderResult(CommandResult result);
    void ShowStatus(string message);
    void ShowError(string message);
}