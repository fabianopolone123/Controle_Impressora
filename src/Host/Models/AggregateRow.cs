namespace PrintControl.Host.Models;

public sealed record AggregateRow
{
    public string Key { get; init; } = "";
    public long TotalPages { get; init; }
    public long Jobs { get; init; }
}
