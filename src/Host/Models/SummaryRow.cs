namespace PrintControl.Host.Models;

public sealed record SummaryRow
{
    public long TotalPages { get; init; }
    public long TotalJobs { get; init; }
}
