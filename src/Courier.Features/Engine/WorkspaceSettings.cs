namespace Courier.Features.Engine;

public class WorkspaceSettings
{
    public string BaseDirectory { get; set; } = Path.GetTempPath();
    public bool CleanupOnCompletion { get; set; } = true;
}
