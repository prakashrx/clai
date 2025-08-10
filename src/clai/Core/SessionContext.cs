namespace Clai.Core;

public class SessionContext
{
    public OperationMode Mode { get; set; } = OperationMode.AI;
    public string CurrentDirectory { get; set; } = Environment.CurrentDirectory;
    public Dictionary<string, object> Variables { get; } = [];
}

public enum OperationMode
{
    AI,
    Manual
}