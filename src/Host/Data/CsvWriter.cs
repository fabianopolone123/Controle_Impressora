using System.Text;
using PrintControl.Host.Models;

namespace PrintControl.Host.Data;

public static class CsvWriter
{
    public static async Task WriteAsync(HttpResponse response, IEnumerable<PrintJobRecord> rows, CancellationToken ct)
    {
        response.ContentType = "text/csv; charset=utf-8";
        await using var writer = new StreamWriter(response.Body, new UTF8Encoding(false));
        await writer.WriteLineAsync("PrintedAt,UserName,MachineName,PrinterName,Pages,Bytes,JobId,DocumentName");

        foreach (var row in rows)
        {
            var line = string.Join(",",
                Escape(row.PrintedAt.ToUniversalTime().ToString("O")),
                Escape(row.UserName),
                Escape(row.MachineName),
                Escape(row.PrinterName),
                Escape(row.Pages.ToString()),
                Escape(row.Bytes.ToString()),
                Escape(row.JobId),
                Escape(row.DocumentName));

            await writer.WriteLineAsync(line);
        }

        await writer.FlushAsync();
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        if (!needsQuotes)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
