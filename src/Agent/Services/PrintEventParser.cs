using System.Diagnostics.Eventing.Reader;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PrintControl.Agent.Models;

namespace PrintControl.Agent.Services;

public static class PrintEventParser
{
    private static readonly Regex IpRegex = new(@"\b\d{1,3}(?:\.\d{1,3}){3}\b", RegexOptions.Compiled);

    public static bool TryParse(EventRecord record, out PrintJobPayload job)
    {
        job = null!;
        if (record.Id != 307)
        {
            return false;
        }

        string? docName = null;
        string? user = null;
        string? machine = null;
        string? printer = null;
        string? printerAddress = null;
        string? bytesText = null;
        string? pagesText = null;
        string? jobId = null;

        TryReadFromXml(record, ref docName, ref user, ref machine, ref printer, ref printerAddress, ref bytesText, ref pagesText, ref jobId);

        docName ??= GetProperty(record, 0);
        user ??= GetProperty(record, 1);
        machine ??= GetProperty(record, 2);
        printer ??= GetProperty(record, 3);
        printerAddress ??= GetProperty(record, 4);
        bytesText ??= GetProperty(record, 5);
        pagesText ??= GetProperty(record, 6);
        jobId ??= GetProperty(record, 7);

        var pages = 0;
        _ = int.TryParse(pagesText, out pages);

        var bytes = 0L;
        _ = long.TryParse(bytesText, out bytes);

        var printedAt = record.TimeCreated.HasValue
            ? new DateTimeOffset(record.TimeCreated.Value)
            : DateTimeOffset.UtcNow;

        machine = NormalizeMachine(machine);
        printer = NormalizePrinter(printer, printerAddress);

        job = new PrintJobPayload
        {
            PrintedAt = printedAt.ToUniversalTime(),
            UserName = string.IsNullOrWhiteSpace(user) ? "UNKNOWN" : user,
            MachineName = string.IsNullOrWhiteSpace(machine) ? Environment.MachineName : machine,
            PrinterName = string.IsNullOrWhiteSpace(printer) ? "UNKNOWN" : printer,
            Pages = Math.Max(0, pages),
            Bytes = Math.Max(0, bytes),
            JobId = string.IsNullOrWhiteSpace(jobId) ? null : jobId,
            DocumentName = string.IsNullOrWhiteSpace(docName) ? null : docName
        };

        return true;
    }

    private static string? GetValue(IReadOnlyList<XElement> data, params string[] names)
    {
        foreach (var name in names)
        {
            var named = data.FirstOrDefault(element =>
                string.Equals(element.Attribute("Name")?.Value, name, StringComparison.OrdinalIgnoreCase));
            if (named is not null)
            {
                return named.Value;
            }
        }

        return null;
    }

    private static string? GetValueByIndex(IReadOnlyList<XElement> data, int index)
    {
        if (index >= 0 && index < data.Count)
        {
            return data[index].Value;
        }

        return null;
    }

    private static string? GetProperty(EventRecord record, int index)
    {
        try
        {
            if (record.Properties.Count <= index)
            {
                return null;
            }

            return record.Properties[index].Value?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static void TryReadFromXml(
        EventRecord record,
        ref string? docName,
        ref string? user,
        ref string? machine,
        ref string? printer,
        ref string? printerAddress,
        ref string? bytesText,
        ref string? pagesText,
        ref string? jobId)
    {
        string xml;
        try
        {
            xml = record.ToXml();
        }
        catch
        {
            return;
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch
        {
            return;
        }

        var documentPrinted = doc.Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, "DocumentPrinted", StringComparison.OrdinalIgnoreCase));

        if (documentPrinted is not null)
        {
            var map = documentPrinted.Elements()
                .ToDictionary(element => element.Name.LocalName, element => element.Value, StringComparer.OrdinalIgnoreCase);

            docName ??= GetFromMap(map, "Param2");
            user ??= GetFromMap(map, "Param3");
            machine ??= GetFromMap(map, "Param4");
            printer ??= GetFromMap(map, "Param5");
            printerAddress ??= GetFromMap(map, "Param6");
            bytesText ??= GetFromMap(map, "Param7");
            pagesText ??= GetFromMap(map, "Param8");
            jobId ??= GetFromMap(map, "Param1");
        }

        var eventData = doc.Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, "EventData", StringComparison.OrdinalIgnoreCase));

        var data = eventData?.Elements()
            .Where(element => string.Equals(element.Name.LocalName, "Data", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (data is null || data.Count == 0)
        {
            var userData = doc.Descendants()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, "UserData", StringComparison.OrdinalIgnoreCase));

            data = userData?.Descendants()
                .Where(element => string.Equals(element.Name.LocalName, "Data", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (data is null)
        {
            return;
        }

        if (data.Count == 0)
        {
            return;
        }

        docName ??= GetValue(data, "DocumentName", "Param2", "Param1");
        user ??= GetValue(data, "UserName", "Param3", "Param2");
        machine ??= GetValue(data, "ClientMachineName", "ClientComputerName", "MachineName", "ComputerName", "ClientName", "Workstation", "Param4", "Param3");
        printer ??= GetValue(data, "PrinterName", "Param5", "Param4");
        printerAddress ??= GetValue(data, "PrinterAddress", "IPAddress", "Port", "Param6");
        bytesText ??= GetValue(data, "Size", "Bytes", "Param7", "Param6");
        pagesText ??= GetValue(data, "Pages", "TotalPages", "PagesPrinted", "Param8", "Param7");
        jobId ??= GetValue(data, "JobId", "Param1", "Param8");

        if (!data.Any(element => element.Attribute("Name") is not null))
        {
            docName ??= GetValueByIndex(data, 1);
            user ??= GetValueByIndex(data, 2);
            machine ??= GetValueByIndex(data, 3);
            printer ??= GetValueByIndex(data, 4);
            printerAddress ??= GetValueByIndex(data, 5);
            bytesText ??= GetValueByIndex(data, 6);
            pagesText ??= GetValueByIndex(data, 7);
            jobId ??= GetValueByIndex(data, 0);
        }
    }

    private static string? NormalizeMachine(string? machine)
    {
        if (string.IsNullOrWhiteSpace(machine))
        {
            return machine;
        }

        return machine.StartsWith(@"\\", StringComparison.Ordinal)
            ? machine.TrimStart('\\')
            : machine;
    }

    private static string? NormalizePrinter(string? printer, string? address)
    {
        var ip = ExtractIp(address) ?? ExtractIp(printer);
        if (!string.IsNullOrWhiteSpace(ip))
        {
            return ip;
        }

        if (string.IsNullOrWhiteSpace(printer))
        {
            return NormalizeAddress(address);
        }

        return printer;
    }

    private static string? NormalizeAddress(string? address)
    {
        return string.IsNullOrWhiteSpace(address) ? address : address.Trim();
    }

    private static bool IsIpAddress(string value)
    {
        return IPAddress.TryParse(value, out _);
    }

    private static string? ExtractIp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (IsIpAddress(trimmed))
        {
            return trimmed;
        }

        var match = IpRegex.Match(trimmed);
        if (match.Success && IsIpAddress(match.Value))
        {
            return match.Value;
        }

        return null;
    }

    private static string? GetFromMap(IReadOnlyDictionary<string, string> map, string key)
    {
        return map.TryGetValue(key, out var value) ? value : null;
    }
}
