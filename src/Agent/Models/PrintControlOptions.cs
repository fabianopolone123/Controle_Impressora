namespace PrintControl.Agent.Models;

public sealed class PrintControlOptions
{
    public string HostUrl { get; set; } = "http://localhost:5080";
    public string ApiKey { get; set; } = "";
    public string QueuePath { get; set; } = @"%LOCALAPPDATA%\\PrintControl\\Agent\\queue.json";
    public int FlushIntervalSeconds { get; set; } = 30;
    public bool IncludeDocumentName { get; set; } = false;
}
