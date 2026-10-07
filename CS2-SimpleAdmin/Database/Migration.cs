using System.Data.Common;
using System.Reflection;
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
        "sa_unbans", "sa_unmutes", "sa_players_ips", "sa_servers", "sa_renames"
    };

    public async Task ExecuteMigrationsAsync()
    {
        if (CS2_SimpleAdmin.DatabaseProvider == null) return;
        var files = await LoadMigrationsAsync();
        if (files.Count == 0) return;

        await using var connection = await CS2_SimpleAdmin.DatabaseProvider.CreateConnectionAsync();
        await EnsureMigrationsTableAsync(connection);

        await RunPendingMigrationsAsync(connection, files);
        await ReconcileSchemaAsync(connection, files);
    }

    /// <summary>
    /// Collects migrations from the embedded copies in the DLL and from the migrations folder on disk.
    /// A file on disk wins over the embedded copy of the same version, but a version missing on disk
    /// (e.g. only the DLL was updated) still runs from the embedded copy.
    /// </summary>
    private async Task<List<(string version, string sql)>> LoadMigrationsAsync()
    {
        var scripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // LogicalName is Migrations/<Dialect>/<file>.sql; RecursiveDir may use either slash
        var prefix = $"Migrations/{Path.GetFileName(migrationsPath.TrimEnd('/', '\\'))}/";
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            var name = resource.Replace('\\', '/');
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)) continue;

            await using var stream = assembly.GetManifestResourceStream(resource);
            if (stream == null) continue;
            using var reader = new StreamReader(stream);
            scripts[Path.GetFileNameWithoutExtension(name)] = await reader.ReadToEndAsync();
        }

        if (Directory.Exists(migrationsPath))
        {
            foreach (var file in Directory.GetFiles(migrationsPath, "*.sql"))
                scripts[Path.GetFileNameWithoutExtension(file)] = await File.ReadAllTextAsync(file);
        }

        return scripts
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();
    }

    private async Task EnsureMigrationsTableAsync(DbConnection connection)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = isSqlite
            ? "CREATE TABLE IF NOT EXISTS sa_migrations (id INTEGER PRIMARY KEY AUTOINCREMENT, version TEXT NOT NULL);"
            : "CREATE TABLE IF NOT EXISTS sa_migrations (id INT PRIMARY KEY AUTO_INCREMENT, version VARCHAR(128) NOT NULL);";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task RunPendingMigrationsAsync(DbConnection connection, List<(string version, string sql)> files)
    {
        var applied = await GetAppliedVersionsAsync(connection);

        foreach (var (version, sqlScript) in files)
        {
            if (applied.Contains(version)) continue;

            try
            {

                if (string.IsNullOrWhiteSpace(sqlScript))
                {
                    // Empty migration file (intentional placeholder, e.g. SQLite
                    // version of a MySQL-only schema change). Record as applied
                    // so it isn't retried forever, but skip execution.
                    await UpdateLastAppliedVersionAsync(connection, version);
                    CS2_SimpleAdmin._logger?.LogInformation($"Migration \"{version}\" is empty; recorded as applied (no-op).");
                    continue;
                }

                if (isSqlite)
                {
                    // SQLite has no `ALTER TABLE ADD COLUMN IF NOT EXISTS`, so
                    // re-running a migration that already added a column throws
                    // "duplicate column name". That blocks ReconcileSchemaAsync
                    // from making progress when a later migration (e.g. the
                    // CREATE TABLE for sa_warns in 010) is the actual missing
                    // piece. Run each statement individually and tolerate the
                    // duplicate-column case so the rest of the script continues.
                    foreach (var statement in SplitSqliteStatements(sqlScript))
                    {
                        await using var cmdStmt = connection.CreateCommand();
                        cmdStmt.CommandText = statement;
                        try
                        {
                            await cmdStmt.ExecuteNonQueryAsync();
                        }
                        catch (System.Data.SQLite.SQLiteException ex)
                            when (ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
                        {
                            // Column already added by a prior run of this migration.
                        }
                    }
                }
                else
                {
                    await using var cmdMigration = connection.CreateCommand();
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
    private async Task ReconcileSchemaAsync(DbConnection connection, List<(string version, string sql)> files)
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

    /// <summary>
    /// Splits a SQLite migration script into individual statements on `;`.
    /// Naive — assumes no semicolons inside string literals or triggers. That
    /// holds for every migration currently in /Migrations/Sqlite; if a future
    /// migration needs embedded semicolons (e.g. CREATE TRIGGER ... BEGIN ... END;),
    /// switch to a proper tokenizer.
    /// </summary>
    private static IEnumerable<string> SplitSqliteStatements(string script)
    {
        foreach (var part in script.Split(';'))
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0) continue;
            // Strip pure-comment-only chunks so the SQLite engine isn't asked
            // to parse a statement that's nothing but `-- ...`.
            var hasCode = trimmed
                .Split('\n')
                .Any(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("--"));
            if (!hasCode) continue;
            yield return trimmed;
        }
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
