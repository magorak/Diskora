using System.Text;

namespace Diskora.Core.Export;

/// <summary>
/// Minimalistický CSV writer (RFC 4180 escapování) - žádná externí závislost
/// pro tak jednoduchý formát (hlavička + řádky plochých textových hodnot).
/// </summary>
public static class CsvWriter
{
    public static string Write(IEnumerable<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(',', headers.Select(Escape)));
        sb.Append("\r\n");

        foreach (var row in rows)
        {
            sb.Append(string.Join(',', row.Select(Escape)));
            sb.Append("\r\n");
        }

        return sb.ToString();
    }

    private static string Escape(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
