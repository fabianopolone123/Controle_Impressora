namespace PrintControl.Host.Models;

public sealed class PrintJobFilter
{
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? User { get; init; }
    public string? Machine { get; init; }
    public string? Printer { get; init; }
    public int? MinPages { get; init; }
    public int? MaxPages { get; init; }
    public int Limit { get; init; } = 500;
    public int Offset { get; init; }
}
