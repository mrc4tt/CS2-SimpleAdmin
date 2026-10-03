using System.Data.Common;

namespace CS2_SimpleAdmin.Database;

public interface IDatabaseProvider
{
    bool IsSqlite { get; }

    Task<DbConnection> CreateConnectionAsync();
    Task<(bool Success, string? Exception)> CheckConnectionAsync();
    Task DatabaseMigrationAsync();
    Task<(bool Success, string? Error)> BackupAsync(string destinationPath);

    /// <summary>One-time setup at plugin start (e.g. SQLite persistent pragmas).</summary>
    Task InitializeAsync();

    /// <summary>Final-cleanup hook on plugin unload (e.g. SQLite WAL checkpoint).</summary>
    Task ShutdownAsync();
    
    // CacheManager
    string GetBanSelectQuery(bool multiServer);
    string GetIpHistoryQuery();
    string GetBanUpdateQuery(bool multiServer);

    // PlayerManager
    string GetUpsertPlayerIpQuery();
    
    // PermissionManager
    string GetAdminsQuery();
    string GetDeleteAdminQuery(bool globalDelete);
    string GetAddAdminQuery();
    string GetAddAdminFlagsQuery();
    string GetUpdateAdminGroupQuery();
    string GetGroupsQuery();
    string GetGroupIdByNameQuery();
    string GetAddGroupQuery();
    string GetAddGroupFlagsQuery();
    string GetAddGroupServerQuery();
    string GetDeleteGroupQuery();
    string GetDeleteOldAdminsQuery();
    
    // BanManager
    string GetAddBanQuery();
    string GetAddBanBySteamIdQuery();
    string GetAddBanByIpQuery();
    string GetUnbanRetrieveBansQuery(bool multiServer);
    string GetUnbanAdminIdQuery();
    string GetInsertUnbanQuery(bool includeReason);
    string GetUpdateBanStatusQuery();
    string GetExpireBansQuery(bool multiServer);
    string GetExpireIpBansQuery(bool multiServer);
    string GetExpireOldPlayerIpsQuery();

    // Renames (css_prename)
    string GetRenamesQuery();
    string GetUpsertRenameQuery();
    string GetDeleteRenameQuery();

    // MuteManager
    string GetAddMuteQuery(bool includePlayerName);
    string GetIsMutedQuery(bool multiServer, int timeMode);
    string GetMuteStatsQuery(bool multiServer);
    string GetUpdateMutePassedQuery(bool multiServer);
    string GetCheckExpiredMutesQuery(bool multiServer);
    string GetRetrieveMutesQuery(bool multiServer);
    string GetUnmuteAdminIdQuery();
    string GetInsertUnmuteQuery(bool includeReason);
    string GetUpdateMuteStatusQuery();
    string GetExpireMutesQuery(bool multiServer, int timeMode);
    
    // WarnManager
    string GetAddWarnQuery(bool includePlayerName);
    string GetPlayerWarnsQuery(bool multiServer, bool active);
    string GetPlayerWarnsCountQuery(bool multiServer, bool active);
    string GetUnwarnByIdQuery(bool multiServer);
    string GetUnwarnLastQuery(bool multiServer);
    string GetUnwarnAllQuery(bool multiServer);
    string GetExpireWarnsQuery(bool multiServer);
}