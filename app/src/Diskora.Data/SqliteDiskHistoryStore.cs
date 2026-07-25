using System.Globalization;
using Diskora.Core.Models;
using Diskora.Core.Services;
using Microsoft.Data.Sqlite;

namespace Diskora.Data;

/// <summary>
/// Lokální SQLite historie zdraví disků a kontrol integrity. Žádný cloud,
/// žádný účet - databáze žije v %LocalAppData%\Diskora\diskora.db (nebo
/// vlastní cestě pro testy). Schéma se při každém otevření vytvoří, pokud
/// ještě neexistuje (CREATE TABLE IF NOT EXISTS) - žádná migrace zatím
/// není potřeba, protože se jedná o první verzi schématu.
/// </summary>
public sealed class SqliteDiskHistoryStore : IDiskHistoryStore
{
    private readonly string _connectionString;

    public SqliteDiskHistoryStore(string? databasePath = null)
    {
        var path = databasePath ?? GetDefaultDatabasePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        InitializeSchema();
    }

    public static string GetDefaultDatabasePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Diskora", "diskora.db");

    public void RecordSmartReading(int diskIndex, DiskHealthStatus overallHealth)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO SmartHistory (DiskIndex, RecordedAtUtc, OverallHealth)
            VALUES ($diskIndex, $recordedAt, $health);
            """;
        command.Parameters.AddWithValue("$diskIndex", diskIndex);
        command.Parameters.AddWithValue("$recordedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$health", overallHealth.ToString());
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<SmartHistoryEntry> GetRecentSmartHistory(int diskIndex, int maxCount = 20)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, DiskIndex, RecordedAtUtc, OverallHealth
            FROM SmartHistory
            WHERE DiskIndex = $diskIndex
            ORDER BY RecordedAtUtc DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$diskIndex", diskIndex);
        command.Parameters.AddWithValue("$limit", maxCount);

        var results = new List<SmartHistoryEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new SmartHistoryEntry(
                reader.GetInt64(0),
                reader.GetInt32(1),
                ParseTimestamp(reader.GetString(2)),
                Enum.Parse<DiskHealthStatus>(reader.GetString(3))));
        }

        return results;
    }

    public void RecordIntegrityCheck(string driveLetter, VolumeDirtyState dirtyState, int? scanExitCode, bool? scanAppearsClean)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO IntegrityHistory (DriveLetter, RecordedAtUtc, DirtyState, ScanExitCode, ScanAppearsClean)
            VALUES ($driveLetter, $recordedAt, $dirtyState, $exitCode, $appearsClean);
            """;
        command.Parameters.AddWithValue("$driveLetter", NormalizeDriveLetter(driveLetter));
        command.Parameters.AddWithValue("$recordedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$dirtyState", dirtyState.ToString());
        command.Parameters.AddWithValue("$exitCode", (object?)scanExitCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$appearsClean", scanAppearsClean is null ? DBNull.Value : (scanAppearsClean.Value ? 1 : 0));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<IntegrityHistoryEntry> GetRecentIntegrityHistory(string driveLetter, int maxCount = 20)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, DriveLetter, RecordedAtUtc, DirtyState, ScanExitCode, ScanAppearsClean
            FROM IntegrityHistory
            WHERE DriveLetter = $driveLetter
            ORDER BY RecordedAtUtc DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$driveLetter", NormalizeDriveLetter(driveLetter));
        command.Parameters.AddWithValue("$limit", maxCount);

        var results = new List<IntegrityHistoryEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new IntegrityHistoryEntry(
                reader.GetInt64(0),
                reader.GetString(1),
                ParseTimestamp(reader.GetString(2)),
                Enum.Parse<VolumeDirtyState>(reader.GetString(3)),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5) != 0));
        }

        return results;
    }

    private void InitializeSchema()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS SmartHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DiskIndex INTEGER NOT NULL,
                RecordedAtUtc TEXT NOT NULL,
                OverallHealth TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_SmartHistory_DiskIndex ON SmartHistory (DiskIndex, RecordedAtUtc);

            CREATE TABLE IF NOT EXISTS IntegrityHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DriveLetter TEXT NOT NULL,
                RecordedAtUtc TEXT NOT NULL,
                DirtyState TEXT NOT NULL,
                ScanExitCode INTEGER NULL,
                ScanAppearsClean INTEGER NULL
            );
            CREATE INDEX IF NOT EXISTS IX_IntegrityHistory_DriveLetter ON IntegrityHistory (DriveLetter, RecordedAtUtc);
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static string NormalizeDriveLetter(string driveLetter) =>
        driveLetter.TrimEnd('\\', ':').ToUpperInvariant();

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
