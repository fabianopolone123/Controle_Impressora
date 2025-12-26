namespace PrintControl.Agent.Models;

public sealed record PrintJobPayload
{
    public DateTimeOffset PrintedAt { get; init; }
    public string UserName { get; init; } = "";
    public string MachineName { get; init; } = "";
    public string PrinterName { get; init; } = "";
    public int Pages { get; init; }
    public long Bytes { get; init; }
    public string? JobId { get; init; }
    public string? DocumentName { get; init; }
}
