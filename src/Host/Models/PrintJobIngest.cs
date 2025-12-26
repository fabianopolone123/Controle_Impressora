namespace PrintControl.Host.Models;

public sealed record PrintJobIngest
{
    public DateTimeOffset PrintedAt { get; init; }
    public string UserName { get; init; } = "";
    public string MachineName { get; init; } = "";
    public string PrinterName { get; init; } = "";
    public int Pages { get; init; }
    public long Bytes { get; init; }
    public string? JobId { get; init; }
    public string? DocumentName { get; init; }

    public PrintJobRecord ToRecord()
    {
        return new PrintJobRecord
        {
            PrintedAt = PrintedAt,
            UserName = UserName,
            MachineName = MachineName,
            PrinterName = PrinterName,
            Pages = Pages,
            Bytes = Bytes,
            JobId = JobId,
            DocumentName = DocumentName
        };
    }
}
