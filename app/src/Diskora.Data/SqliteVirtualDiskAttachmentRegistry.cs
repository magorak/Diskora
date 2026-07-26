using System.Globalization;
using Diskora.Core.Models;
using Diskora.Core.Services;
using Microsoft.Data.Sqlite;

namespace Diskora.Data;

/// <summary>
/// Perzistentní registr připojených virtuálních disků/obrazů - stejný SQLite
/// soubor jako <see cref="SqliteDiskHistoryStore"/> (%LocalAppData%\Diskora\diskora.db),
/// ale samostatná tabulka a rozhraní, protože jde koncepčně o jiný druh dat
/// (aktuální stav, ne historie). Cesty se ukládají přes <see cref="Path.GetFullPath"/>
/// a porovnávají case-insensitive, ať se stejný soubor otevřený s jiným zápisem
/// cesty (relativní/jiná velikost písmen) nezdvojí.
/// </summary>
public sealed class SqliteVirtualDiskAttachmentRegistry : IVirtualDiskAttachmentRegistry
{
    private readonly string _connectionString;

    public SqliteVirtualDiskAttachmentRegistry(string? databasePath = null)
    {
        var path = databasePath ?? SqliteDiskHistoryStore.GetDefaultDatabasePath();
        var directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        InitializeSchema();
    }

    public void RecordAttached(string path, VirtualDiskFormat format, bool readOnly)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AttachedVirtualDisks (Path, Format, ReadOnly, AttachedAtUtc)
            VALUES ($path, $format, $readOnly, $attachedAt)
            ON CONFLICT(Path) DO UPDATE SET
                Format = excluded.Format,
                ReadOnly = excluded.ReadOnly,
                AttachedAtUtc = excluded.AttachedAtUtc;
            """;
        command.Parameters.AddWithValue("$path", NormalizePath(path));
        command.Parameters.AddWithValue("$format", format.ToString());
        command.Parameters.AddWithValue("$readOnly", readOnly ? 1 : 0);
        command.Parameters.AddWithValue("$attachedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public void RecordDetached(string path)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM AttachedVirtualDisks WHERE Path = $path;";
        command.Parameters.AddWithValue("$path", NormalizePath(path));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<AttachedVirtualDiskEntry> GetTrackedAttachments()
    {
        using var connection = OpenConnection();

        var rows = new List<(string Path, string Format, bool ReadOnly, string AttachedAtUtc)>();
        using (var selectCommand = connection.CreateCommand())
        {
            selectCommand.CommandText = "SELECT Path, Format, ReadOnly, AttachedAtUtc FROM AttachedVirtualDisks;";
            using var reader = selectCommand.ExecuteReader();
            while (reader.Read())
            {
                rows.Add((reader.GetString(0), reader.GetString(1), reader.GetInt32(2) != 0, reader.GetString(3)));
            }
        }

        var results = new List<AttachedVirtualDiskEntry>();
        foreach (var row in rows)
        {
            if (!File.Exists(row.Path))
            {
                using var deleteCommand = connection.CreateCommand();
                deleteCommand.CommandText = "DELETE FROM AttachedVirtualDisks WHERE Path = $path;";
                deleteCommand.Parameters.AddWithValue("$path", row.Path);
                deleteCommand.ExecuteNonQuery();
                continue;
            }

            results.Add(new AttachedVirtualDiskEntry(
                row.Path,
                Enum.Parse<VirtualDiskFormat>(row.Format),
                row.ReadOnly,
                ParseTimestamp(row.AttachedAtUtc)));
        }

        return results.OrderBy(entry => entry.AttachedAtUtc).ToList();
    }

    private void InitializeSchema()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS AttachedVirtualDisks (
                Path TEXT PRIMARY KEY COLLATE NOCASE,
                Format TEXT NOT NULL,
                ReadOnly INTEGER NOT NULL,
                AttachedAtUtc TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static string NormalizePath(string path) => System.IO.Path.GetFullPath(path);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
