using System.Data.Common;
using Microsoft.Extensions.Logging;

namespace CS2_SimpleAdmin.Database;

public class Migration(string migrationsPath, bool isSqlite)
{
    /// <summary>
    /// Executes all migration scripts found in the configured migrations path that have not been applied yet.
    /// Creates a migration tracking table if it does not exist.
    /// Applies migration scripts in filename order and logs successes or failures.
    /// </summary>
    // Tables that MUST exist after all migrations run. If any are missing,
    // the migration tracker is out of sync with the actual schema (a previous
    // run recorded a version but the CREATE TABLE silently didn't apply, or
    // the user manually mucked with the DB). On detection, the affected rows
    // in sa_migrations are wiped and the migrations re-run — safe because
    // every migration in this project is idempotent (IF NOT EXISTS / GLOB
    // sanitization).
    private static readonly string[] CriticalTables =
    {
        "sa_bans", "sa_mutes", "sa_warns", "sa_admins", "sa_admins_flags",
        "sa_groups", "sa_groups_flags", "sa_groups_servers",
        "sa_unbans", "sa_unmutes", "sa_players_ips", "sa_servers"
    };

    public async Task ExecuteMigrationsAsync()
    {
        if (CS2_SimpleAdmin.DatabaseProvider == null) return;
        var files = Directory.GetFiles(migrationsPath, "*.sql").OrderBy(f => f).ToList();
        if (files.Count == 0) return;

        await using var connection = await CS2_SimpleAdmin.DatabaseProvider.CreateConnectionAsync();
        await EnsureMigrationsTableAsync(connection);

        await RunPendingMigrationsAsync(connection, files);
        await ReconcileSchemaAsync(connection, files);
    }

    private async Task EnsureMigrationsTableAsync(DbConnection connection)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = isSqlite
            ? "CREATE TABLE IF NOT EXISTS sa_migrations (id INTEGER PRIMARY KEY AUTOINCREMENT, version TEXT NOT NULL);"
            : "CREATE TABLE IF NOT EXISTS sa_migrations (id INT PRIMARY KEY AUTO_INCREMENT, version VARCHAR(128) NOT NULL);";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task RunPendingMigrationsAsync(DbConnection connection, List<string> files)
    {
        var applied = await GetAppliedVersionsAsync(connection);

        foreach (var file in files)
        {
            var version = Path.GetFileNameWithoutExtension(file);
            if (applied.Contains(version)) continue;

            try
            {
                var sqlScript = await File.ReadAllTextAsync(file);
                await using (var cmdMigration = connection.CreateCommand())
                {
                    cmdMigration.CommandText = sqlScript;
                    await cmdMigration.ExecuteNonQueryAsync();
                }

                await UpdateLastAppliedVersionAsync(connection, version);
                CS2_SimpleAdmin._logger?.LogInformation($"Migration \"{version}\" successfully applied.");
            }
            catch (Exception ex)
            {
                CS2_SimpleAdmin._logger?.LogError(ex, $"Error applying migration \"{version}\".");
                break;
            }
        }
    }

    /// <summary>
    /// Verifies that all critical tables exist. If any are missing, the
    /// migration tracker is desynced from the schema — wipe sa_migrations
    /// and re-run all migrations from scratch. Idempotency of every migration
    /// makes this safe.
    /// </summary>
    private async Task ReconcileSchemaAsync(DbConnection connection, List<string> files)
    {
        var missing = new List<string>();
        foreach (var table in CriticalTables)
        {
            if (!await TableExistsAsync(connection, table))
                missing.Add(table);
        }

        if (missing.Count == 0) return;

        CS2_SimpleAdmin._logger?.LogWarning(
            "Migration tracker desynced — missing tables: {tables}. Wiping sa_migrations and re-running.",
            string.Join(", ", missing));

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM sa_migrations;";
            await cmd.ExecuteNonQueryAsync();
        }

        await RunPendingMigrationsAsync(connection, files);
    }

    private async Task<bool> TableExistsAsync(DbConnection connection, string tableName)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = isSqlite
            ? "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @name;"
            : "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = @name;";

        var p = cmd.CreateParameter();
        p.ParameterName = "@name";
        p.Value = tableName;
        cmd.Parameters.Add(p);

        var result = await cmd.ExecuteScalarAsync();
        return result != null && Convert.ToInt64(result) > 0;
    }

    private static async Task<HashSet<string>> GetAppliedVersionsAsync(DbConnection connection)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT version FROM sa_migrations;";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var v = reader.GetString(0);
            if (!string.IsNullOrEmpty(v)) set.Add(v);
        }
        return set;
    }

    /// <summary>
    /// Inserts a record tracking the successful application of a migration version.
    /// </summary>
    /// <param name="connection">The open database connection.</param>
    /// <param name="version">The version string of the migration applied.</param>
    private static async Task UpdateLastAppliedVersionAsync(DbConnection connection, string version)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO sa_migrations (version) VALUES (@Version);";

        var param = cmd.CreateParameter();
        param.ParameterName = "@Version";
        param.Value = version;
        cmd.Parameters.Add(param);

        await cmd.ExecuteNonQueryAsync();
    }
}
