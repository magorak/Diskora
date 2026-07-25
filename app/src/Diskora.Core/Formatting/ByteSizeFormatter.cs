using System.Globalization;

namespace Diskora.Core.Formatting;

/// <summary>
/// Formátuje velikosti v bajtech do čitelné podoby. Používá binární násobitel
/// (1024), ale jednotky značí zkráceně "KB/MB/GB/TB" — konvence shodná s
/// Průzkumníkem Windows, kterou uživatelé očekávají.
/// </summary>
public static class ByteSizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Format(long bytes, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;

        if (bytes < 0)
        {
            return "-" + Format(-bytes, culture);
        }

        double value = bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < Units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        var decimals = unitIndex == 0 ? 0 : 2;
        return string.Format(culture, "{0:N" + decimals + "} {1}", value, Units[unitIndex]);
    }
}
