using Clai.Terminal;

namespace Clai.Core;

public class SessionContext : IDisposable
{
    private Terminal.Terminal? terminal;
    
    public OperationMode Mode { get; set; } = OperationMode.AI;
    public string CurrentDirectory { get; set; } = Environment.CurrentDirectory;
    public Dictionary<string, object> Variables { get; } = [];
    
    public Terminal.Terminal Terminal
    {
        get
        {
            if (terminal == null)
            {
                terminal = new Terminal.Terminal();
                terminal.Start();
            }
            return terminal;
        }
    }
    
    public void Dispose()
    {
        terminal?.Dispose();
    }
}

public enum OperationMode
{
    AI,
    Manual
}