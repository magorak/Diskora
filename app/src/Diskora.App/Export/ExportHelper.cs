using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Windows;

namespace Diskora.App.Export;

/// <summary>
/// Sdílené uložení CSV/JSON exportu přes <see cref="Microsoft.Win32.SaveFileDialog"/> -
/// stejná logika (dialog, zápis, ošetření chyby) se jinak opakovala v každém okně
/// s exportem zvlášť. JSON encoder je omezený na Basic Latin + Latin-1 Supplement +
/// Latin Extended-A, aby čeština v souboru zůstala čitelná (přímo diakritika, ne
/// `\uXXXX` escapy) - stejné nastavení jako u exportu v okně Analýza zaplněnosti.
/// </summary>
public static class ExportHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Latin1Supplement, UnicodeRanges.LatinExtendedA),
    };

    public static void SaveCsv(Window owner, string csv, string suggestedName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportovat do CSV",
            Filter = "CSV soubor (*.csv)|*.csv|Všechny soubory (*.*)|*.*",
            FileName = suggestedName,
        };

        if (dialog.ShowDialog(owner) != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, csv, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(owner, $"Export se nepodařilo uložit: {ex.Message}", "Export CSV",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Uloží zprávu pro člověka a rovnou ji otevře v prohlížeči - jinak by
    /// uživatel musel soubor hledat na disku, aby zjistil, co vlastně uložil.
    /// </summary>
    public static void SaveHtmlReport(Window owner, string html, string suggestedName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Uložit zprávu o stavu disku",
            Filter = "Webová stránka (*.html)|*.html|Všechny soubory (*.*)|*.*",
            FileName = suggestedName,
        };

        if (dialog.ShowDialog(owner) != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, html, Encoding.UTF8);
            Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(owner, $"Zprávu se nepodařilo uložit nebo otevřít: {ex.Message}", "Uložení zprávy",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public static void SaveJson(Window owner, object payload, string suggestedName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportovat do JSON",
            Filter = "JSON soubor (*.json)|*.json|Všechny soubory (*.*)|*.*",
            FileName = suggestedName,
        };

        if (dialog.ShowDialog(owner) != true)
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            File.WriteAllText(dialog.FileName, json, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(owner, $"Export se nepodařilo uložit: {ex.Message}", "Export JSON",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
