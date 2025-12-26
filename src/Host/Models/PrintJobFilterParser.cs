using System.Globalization;
using Microsoft.Extensions.Primitives;

namespace PrintControl.Host.Models;

public static class PrintJobFilterParser
{
    public static PrintJobFilter FromQuery(IQueryCollection query)
    {
        return new PrintJobFilter
        {
            From = TryParseDate(query["from"]),
            To = TryParseDate(query["to"]),
            User = Normalize(query["user"]),
            Machine = Normalize(query["machine"]),
            Printer = Normalize(query["printer"]),
            MinPages = TryParseInt(query["minPages"]),
            MaxPages = TryParseInt(query["maxPages"]),
            Limit = Clamp(TryParseInt(query["limit"]) ?? 500, 1, 2000),
            Offset = Math.Max(TryParseInt(query["offset"]) ?? 0, 0)
        };
    }

    private static string? Normalize(StringValues values)
    {
        var value = values.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static DateTimeOffset? TryParseDate(StringValues values)
    {
        var value = values.ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static int? TryParseInt(StringValues values)
    {
        var value = values.ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }
}
