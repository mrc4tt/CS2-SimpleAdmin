using System.Collections;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;
using CS2_SimpleAdmin.Managers;
using CS2_SimpleAdmin.Menus;
using CS2_SimpleAdminApi;
using Dapper;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using CS2_SimpleAdmin.Models;
using CS2MenuManager.API.Class;
using CS2MenuManager.API.Enum;
using CS2MenuManager.API.Interface;
using CS2MenuManager.API.Menu;
using System.Diagnostics.CodeAnalysis;

namespace CS2_SimpleAdmin;

public partial class CS2_SimpleAdmin
{
    /// <summary>
    /// Handles the command that shows active penalties and warns for the caller or specified player.
    /// Queries warnings and mute status, formats them locally, and sends the result to caller's chat.
    /// </summary>
    /// <param name="caller">The player issuing this command.</param>
    /// <param name="command">Command input parameters.</param>
    [CommandHelper(usage: "[#userid or name]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnPenaltiesCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller == null || !caller.IsValid || !caller.UserId.HasValue || DatabaseProvider == null)
            return;
        
        var steamId = caller.SteamID;

        if (!string.IsNullOrEmpty(command.GetArg(1)) && AdminManager.PlayerHasPermissions(new SteamID(caller.SteamID), "@css/kick"))
        {
            var targets = GetTarget(command);

            if (targets == null)
                return;

            var playersToTarget = targets.Players.Where(player => player is { IsValid: true, IsHLTV: false }).ToList();
            if (playersToTarget.Count != 1)
            {
                command.ReplyToCommand("Only one target is allowed.");
                return;
            }

            var target = playersToTarget[0];
            if (!target.UserId.HasValue || !caller.CanTarget(target)) return;

            // Only the user id was reassigned before, so every lookup below used the caller's own penalties
            steamId = target.SteamID;
        }

        // PlayersInfo is filled by the async connect load; a target who joined a moment ago may not be in it yet
        if (!PlayersInfo.ContainsKey(steamId))
        {
            command.ReplyToCommand("Player data is still loading, try again in a moment.");
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                var warns = await WarnManager.GetPlayerWarns(PlayersInfo[steamId], false);

                // Check if the player is muted
                var activeMutes = await MuteManager.IsPlayerMuted(PlayersInfo[steamId].SteamId.SteamId64.ToString());

                Dictionary<PenaltyType, List<string>> mutesList = new()
                {
                    { PenaltyType.Gag, [] },
                    { PenaltyType.Mute, [] },
                    { PenaltyType.Silence, [] }
                };

                List<string> warnsList = [];

                bool found = false;
                foreach (var warn in warns.TakeWhile(warn => (string)warn.status == "ACTIVE"))
                {
                    DateTime ends = warn.ends;
                    if (_localizer == null) continue;
                    using (new WithTemporaryCulture(caller.GetLanguage()))
                        warnsList.Add(_localizer["sa_player_penalty_info_active_warn", ends.ToLocalTime().ToString(CultureInfo.CurrentCulture), (string)warn.reason]);
                    found = true;
                }

                if (!found)
                {
                    if (_localizer != null)
                        warnsList.Add(_localizer["sa_player_penalty_info_no_active_warn"]);
                }

                if (activeMutes.Count > 0)
                {
                    foreach (var mute in activeMutes)
                    {
                        string muteType = mute.type;
                        DateTime ends = mute.ends;
                        using (new WithTemporaryCulture(caller.GetLanguage()))
                        {
                            switch (muteType)
                            {
                                // Apply mute penalty based on mute type
                                case "GAG":
                                    if (_localizer != null)
                                        mutesList[PenaltyType.Gag].Add(_localizer["sa_player_penalty_info_active_gag", ends.ToLocalTime().ToString(CultureInfo.CurrentCulture)]);
                                    break;
                                case "MUTE":
                                    if (_localizer != null)
                                        mutesList[PenaltyType.Mute].Add(_localizer["sa_player_penalty_info_active_mute", ends.ToLocalTime().ToString(CultureInfo.CurrentCulture)]);
                                    break;
                                default:
                                    if (_localizer != null)
                                        mutesList[PenaltyType.Silence].Add(_localizer["sa_player_penalty_info_active_silence", ends.ToLocalTime().ToString(CultureInfo.CurrentCulture)]);
                                    break;
                            }
                        }
                    }
                }

                if (_localizer != null)
                {
                    if (mutesList[PenaltyType.Gag].Count == 0)
                        mutesList[PenaltyType.Gag].Add(_localizer["sa_player_penalty_info_no_active_gag"]);
                    if (mutesList[PenaltyType.Mute].Count == 0)
                        mutesList[PenaltyType.Mute].Add(_localizer["sa_player_penalty_info_no_active_mute"]);
                    if (mutesList[PenaltyType.Silence].Count == 0)
                        mutesList[PenaltyType.Silence].Add(_localizer["sa_player_penalty_info_no_active_silence"]);
                }

                await Server.NextWorldUpdateAsync(() =>
                {
                    caller.SendLocalizedMessage(_localizer, "sa_player_penalty_info",
                    [
                        PlayersInfo[steamId].Name,
                        PlayersInfo[steamId].TotalBans,
                        PlayersInfo[steamId].TotalGags,
                        PlayersInfo[steamId].TotalMutes,
                        PlayersInfo[steamId].TotalSilences,
                        PlayersInfo[steamId].TotalWarns,
                        string.Join("\n", mutesList.SelectMany(kvp => kvp.Value)),
                        string.Join("\n", warnsList)
                    ]);
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error processing player information: {ex}");
            }
        });
    }

    /// <summary>
    /// Toggles the admin voice listening mode or mutes/unmutes all players' voice.
    /// Sends confirmation messages accordingly.
    /// </summary>
    /// <param name="caller">The player issuing this command.</param>
    /// <param name="command">Command input parameters.</param>
    [RequiresPermissions("@css/chat")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnAdminVoiceCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller == null || !caller.IsValid)
            return;

        if (command.ArgCount > 1)
        {
            if (command.GetArg(2).ToLower().Equals("muteAll"))
            {
                caller.SendLocalizedMessage(_localizer, "sa_admin_voice_mute_all");
                foreach (var player in Helper.GetValidPlayers().Where(p => p != caller && !AdminManager.PlayerHasPermissions(new SteamID(p.SteamID), "@css/chat")))
                {
                    player.VoiceFlags = VoiceFlags.Muted;
                }
            }
            
            if (command.GetArg(2).ToLower().Equals("unmuteAll"))
            {
                caller.SendLocalizedMessage(_localizer, "sa_admin_voice_unmute_all");
                foreach (var player in Helper.GetValidPlayers().Where(p => p != caller))
                {
                    if (PlayerPenaltyManager.GetPlayerPenalties(player.Slot, [PenaltyType.Silence, PenaltyType.Mute]).Count == 0)
                        player.VoiceFlags = VoiceFlags.Normal;
                }
            }

            return;
        }
        
        var enabled = caller.VoiceFlags.HasFlag(VoiceFlags.ListenAll);
        var messageKey = enabled 
            ? "sa_admin_voice_unlisten_all" 
            : "sa_admin_voice_listen_all";

        caller.SendLocalizedMessage(_localizer, messageKey);
        caller.VoiceFlags ^= VoiceFlags.ListenAll;
    }
    
    /// <summary>
    /// Opens the admin menu for the caller.
    /// </summary>
    /// <param name="caller">The player issuing the command.</param>
    /// <param name="command">Command input parameters.</param>
    [RequiresPermissions("@css/generic")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnAdminCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller == null || !caller.IsValid)
            return;

        AdminMenu.OpenMenu(caller);
    }

    /// <summary>
    /// Displays admin help text read from a file.
    /// Outputs lines one at a time as replies to the command.
    /// </summary>
    /// <param name="caller">The player issuing the command.</param>
    /// <param name="command">Command input parameters.</param>
    [RequiresPermissions("@css/generic")]
    public void OnAdminHelpCommand(CCSPlayerController? caller, CommandInfo command)
    {
        var lines = File.ReadAllLines(ModuleDirectory + "/admin_help.txt");

        foreach (var line in lines)
        {
            command.ReplyToCommand(string.IsNullOrWhiteSpace(line) ? " " : line.ReplaceColorTags());
        }
    }

    /// <summary>
    /// Handles adding a new admin with specified SteamID, name, flags, immunity, and duration.
    /// </summary>
    /// <param name="caller">The player issuing the command.</param>
    /// <param name="command">Command input parameters.</param>
    [CommandHelper(minArgs: 4, usage: "<steamid> <name> <flags/groups> <immunity> <duration>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/root")]
    public void OnAddAdminCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (DatabaseProvider == null) return;

        if (!Helper.ValidateSteamId(command.GetArg(1), out var steamId) || steamId == null)
        {
            command.ReplyToCommand($"Invalid SteamID64.");
            return;
        }

        var steamid = steamId.SteamId64.ToString();

        if (command.GetArg(2).Length <= 0)
        {
            command.ReplyToCommand($"Invalid player name.");
            return;
        }
        if (!command.GetArg(3).Contains('@') && !command.GetArg(3).Contains('#'))
        {
            command.ReplyToCommand($"Invalid flag or group.");
            return;
        }

        var name = command.GetArg(2);
        var flags = command.GetArg(3);
        var globalAdmin = command.GetArg(4).ToLower().Equals("-g") || command.GetArg(5).ToLower().Equals("-g") ||
                          command.GetArg(6).ToLower().Equals("-g");
        int.TryParse(command.GetArg(4), out var immunity);
        var time = Math.Max(0, Helper.ParsePenaltyTime(command.GetArg(5)));

        AddAdmin(caller, steamid, name, flags, immunity, time, globalAdmin, command);
    }

    /// <summary>
    /// Adds admin permissions and groups for a player.
    /// </summary>
    /// <param name="caller">The player issuing the command.</param>
    /// <param name="steamid">SteamID as string identifying the player.</param>
    /// <param name="name">Player's name.</param>
    /// <param name="flags">Comma-separated admin flags/groups.</param>
    /// <param name="immunity">Admin immunity level.</param>
    /// <param name="time">Duration of permission (default 0 = permanent).</param>
    /// <param name="globalAdmin">Whether admin is global.</param>
    /// <param name="command">Optional command info for confirmation messages.</param>
    public static void AddAdmin(CCSPlayerController? caller, string steamid, string name, string flags, int immunity, int time = 0, bool globalAdmin = false, CommandInfo? command = null)
    {
        if (DatabaseProvider == null) return;
        
        var flagsList = flags.Split(',').Select(flag => flag.Trim()).ToList();
        _ = Instance.PermissionManager.AddAdminBySteamId(steamid, name, flagsList, immunity, time, globalAdmin);

        Helper.LogCommand(caller, $"css_addadmin {steamid} {name} {flags} {immunity} {time}");

        var msg = $"Added '{flags}' flags to '{name}' ({steamid})";
        if (command != null)
            command.ReplyToCommand(msg);
        else if (caller != null && caller.IsValid)
            caller.PrintToChat(msg);
        else
            Server.PrintToConsole(msg);
    }

    /// <summary>
    /// Handles removing an admin's flags and groups by SteamID.
    /// </summary>
    /// <param name="caller">The player issuing the command.</param>
    /// <param name="command">Command input parameters.</param>
    [CommandHelper(minArgs: 1, usage: "<steamid>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/root")]
    public void OnDelAdminCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (DatabaseProvider == null) return;

        if (!Helper.ValidateSteamId(command.GetArg(1), out var steamId) || steamId == null)
        {
            command.ReplyToCommand($"Invalid SteamID64.");
            return;
        }

        var globalDelete = command.GetArg(2).ToLower().Equals("-g");

        RemoveAdmin(caller, steamId.SteamId64.ToString(), globalDelete, command);
    }

    /// <summary>
    /// Removes admin permissions and groups for a player.
    /// </summary>
    /// <param name="caller">The player issuing the command.</param>
    /// <param name="steamid">SteamID as string identifying the player.</param>
    /// <param name="globalDelete">Whether to delete globally.</param>
    /// <param name="command">Optional command info.</param>
    public void RemoveAdmin(CCSPlayerController? caller, string steamid, bool globalDelete = false, CommandInfo? command = null)
    {
        if (DatabaseProvider == null) return;
        _ = PermissionManager.DeleteAdminBySteamId(steamid, globalDelete);

        AddTimer(2, () =>
        {
            if (string.IsNullOrEmpty(steamid) || !SteamID.TryParse(steamid, out var steamId) ||
                steamId == null) return;
            if (PermissionManager.AdminCache.ContainsKey(steamId))
            {
                PermissionManager.AdminCache.TryRemove(steamId, out _);
            }

            AdminManager.ClearPlayerPermissions(steamId);
            AdminManager.RemovePlayerAdminData(steamId);
        }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.STOP_ON_MAPCHANGE);

        Helper.LogCommand(caller, $"css_deladmin {steamid}");

        var msg = $"Removed flags from '{steamid}'";
        if (command != null)
            command.ReplyToCommand(msg);
        else if (caller != null && caller.IsValid)
            caller.PrintToChat(msg);
        else
            Server.PrintToConsole(msg);
    }

    /// <summary>
    /// Adds a new admin group with specified flags and immunity settings.
    /// </summary>
    /// <param name="caller">The player issuing the command.</param>
    /// <param name="command">Command input parameters.</param>
    [CommandHelper(minArgs: 3, usage: "<group_name> <flags> <immunity>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/root")]
    public void OnAddGroup(CCSPlayerController? caller, CommandInfo command)
    {
        if (DatabaseProvider == null) return;

        if (!command.GetArg(1).StartsWith("#"))
        {
            command.ReplyToCommand($"Group name must start with #.");
            return;
        }

        if (!command.GetArg(2).StartsWith($"@") && !command.GetArg(2).StartsWith($"#"))
        {
            command.ReplyToCommand($"Invalid flag or group.");
            return;
        }

        var groupName = command.GetArg(1);
        var flags = command.GetArg(2);
        int.TryParse(command.GetArg(3), out var immunity);
        var globalGroup = command.GetArg(4).ToLower().Equals("-g");

        AddGroup(caller, groupName, flags, immunity, globalGroup, command);
    }

    /// <summary>
    /// Adds a new admin group with specified flags and immunity level.
    /// </summary>
    /// <param name="caller">The player issuing the command.</param>
    /// <param name="name">Group name (prefix with #).</param>
    /// <param name="flags">Comma-separated flags/groups string.</param>
    /// <param name="immunity">Immunity level.</param>
    /// <param name="globalGroup">Whether group is global.</param>
    /// <param name="command">Optional command info.</param>
    private static void AddGroup(CCSPlayerController? caller, string name, string flags, int immunity, bool globalGroup, CommandInfo? command = null)
    {
        if (DatabaseProvider == null) return;

        var flagsList = flags.Split(',').Select(flag => flag.Trim()).ToList();
        _ = Instance.PermissionManager.AddGroup(name, flagsList, immunity, globalGroup);

        Helper.LogCommand(caller, $"css_addgroup {name} {flags} {immunity}");

        var msg = $"Created group '{name}' with flags '{flags}'";
        if (command != null)
            command.ReplyToCommand(msg);
        else if (caller != null && caller.IsValid)
            caller.PrintToChat(msg);
        else
            Server.PrintToConsole(msg);
    }

    /// <summary>
    /// Handles removing a group by name.
    /// </summary>
    /// <param name="caller">The player issuing the command.</param>
    /// <param name="command">Command input parameters.</param>
    [CommandHelper(minArgs: 1, usage: "<group_name>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/root")]
    public void OnDelGroupCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (DatabaseProvider == null) return;

        if (!command.GetArg(1).StartsWith($"#"))
        {
            command.ReplyToCommand($"Group name must start with #.");
            return;
        }

        var groupName = command.GetArg(1);

        RemoveGroup(caller, groupName, command);
    }

    /// <summary>
    /// Removes a group.
    /// </summary>
    /// <param name="caller">The player issuing the command.</param>
    /// <param name="name">The group name to remove.</param>
    /// <param name="command">Optional command info for confirmation.</param>
    private void RemoveGroup(CCSPlayerController? caller, string name, CommandInfo? command = null)
    {
        if (DatabaseProvider == null) return;
        _ = PermissionManager.DeleteGroup(name);

        AddTimer(2, () =>
        {
            ReloadAdmins(caller);
        }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.STOP_ON_MAPCHANGE);

        Helper.LogCommand(caller, $"css_delgroup {name}");

        var msg = $"Removed group '{name}'";
        if (command != null)
            command.ReplyToCommand(msg);
        else if (caller != null && caller.IsValid)
            caller.PrintToChat(msg);
        else
            Server.PrintToConsole(msg);
    }

    /// <summary>
    /// Reloads admin and group data from database and json files.
    /// </summary>
    /// <param name="caller">The player issuing the reload command.</param>
    /// <param name="command">Command input parameters.</param>
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/root")]
    public void OnRelAdminCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (DatabaseProvider == null) return;
        ReloadAdmins(caller);
        command.ReplyToCommand("Reloaded sql admins and groups");
    }
    
    /// <summary>
    /// Reloads bans cache.
    /// </summary>
    /// <param name="caller">The player issuing the reload command.</param>
    /// <param name="command">Command input parameters.</param>
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/root")]
    public void OnRelBans(CCSPlayerController? caller, CommandInfo command)
    {
        if (DatabaseProvider == null) return;

        _ = Instance.CacheManager?.ForceReInitializeCacheAsync();
        command.ReplyToCommand("Reloaded bans");
    }

    /// <summary>
    /// Lists active bans (steamid / name / IP) from the database — always fresh,
    /// not from the in-memory cache (which only refreshes on player connect or
    /// css_reloadbans). Optional pattern filters by substring match on any
    /// field. Output is capped at 50 to avoid flooding the console.
    /// </summary>
    [CommandHelper(usage: "[pattern]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/ban")]
    public void OnBanlistCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (DatabaseProvider == null) return;

        var pattern = command.ArgCount >= 2 ? command.GetArg(1) : null;
        var multiServer = Config.MultiServerMode;

        Task.Run(async () =>
        {
            var bans = new List<BanlistEntry>();
            string? error = null;
            try
            {
                await using var connection = await DatabaseProvider.CreateConnectionAsync();
                var sql = multiServer
                    ? "SELECT id, player_name, player_steamid, player_ip, created FROM sa_bans " +
                      "WHERE status = 'ACTIVE' AND (duration = 0 OR ends > @now) " +
                      "ORDER BY created DESC LIMIT 200"
                    : "SELECT id, player_name, player_steamid, player_ip, created FROM sa_bans " +
                      "WHERE status = 'ACTIVE' AND (duration = 0 OR ends > @now) AND server_id = @serverid " +
                      "ORDER BY created DESC LIMIT 200";

                var rows = await connection.QueryAsync(sql, new { now = Time.ActualDateTime(), serverid = ServerId });

                foreach (var rowObj in rows)
                {
                    var row = (IDictionary<string, object?>)rowObj!;
                    var idVal = row["id"];
                    var nameVal = row["player_name"];
                    var steamVal = row["player_steamid"];
                    var ipVal = row["player_ip"];
                    var createdVal = row["created"];

                    bans.Add(new BanlistEntry
                    {
                        Id = idVal is null ? 0L : Convert.ToInt64(idVal),
                        PlayerName = nameVal as string ?? "",
                        PlayerSteamId = steamVal is null ? "" : (Convert.ToString(steamVal) ?? ""),
                        PlayerIp = ipVal as string ?? "",
                        Created = ParseDbDateTime(createdVal)
                    });
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                _logger?.LogError(ex, "css_banlist query failed");
            }

            if (error != null)
            {
                await Server.NextWorldUpdateAsync(() => command.ReplyToCommand($"banlist failed: {error}"));
                return;
            }

            if (!string.IsNullOrEmpty(pattern))
            {
                var filtered = new List<BanlistEntry>();
                foreach (var b in bans)
                {
                    if (b.PlayerSteamId.Contains(pattern, StringComparison.OrdinalIgnoreCase)
                        || b.PlayerName.Contains(pattern, StringComparison.OrdinalIgnoreCase)
                        || b.PlayerIp.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        filtered.Add(b);
                    }
                }
                bans = filtered;
            }

            var total = bans.Count;
            const int limit = 50;
            var shown = Math.Min(total, limit);

            await Server.NextWorldUpdateAsync(() =>
            {
                if (total == 0)
                {
                    command.ReplyToCommand(string.IsNullOrEmpty(pattern)
                        ? "No active bans."
                        : $"No active bans matching \"{pattern}\".");
                    return;
                }

                command.ReplyToCommand(
                    string.IsNullOrEmpty(pattern)
                        ? $"Active bans: {total} total, showing {shown} most recent."
                        : $"Active bans matching \"{pattern}\": {total} total, showing {shown} most recent.");

                var max = Math.Min(bans.Count, limit);
                for (var i = 0; i < max; i++)
                {
                    var b = bans[i];
                    var steamId = string.IsNullOrEmpty(b.PlayerSteamId) ? "-" : b.PlayerSteamId;
                    var name = string.IsNullOrEmpty(b.PlayerName) ? "-" : b.PlayerName;
                    var ip = string.IsNullOrEmpty(b.PlayerIp) ? "-" : b.PlayerIp;
                    var when = b.Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                    command.ReplyToCommand($"  #{b.Id}  {when}  {steamId,-17}  {name,-26}  {ip}");
                }

                if (total > limit)
                    command.ReplyToCommand($"  ... ({total - limit} more not shown — narrow with `css_banlist <pattern>`)");
            });
        });
    }

    private sealed class BanlistEntry
    {
        public long Id;
        public string PlayerName = "";
        public string PlayerSteamId = "";
        public string PlayerIp = "";
        public DateTime Created;
    }

    /// <summary>
    /// Defensively converts a value coming back from Dapper into a DateTime.
    /// System.Data.SQLite's built-in parser stumbles on the round-trip ISO 8601
    /// format with 7-digit fractional seconds and a trailing "Z" that the
    /// driver itself writes when binding a UTC DateTime, so we handle the
    /// string fallback ourselves.
    /// </summary>
    private static DateTime ParseDbDateTime(object? value)
    {
        if (value is null) return DateTime.MinValue;
        if (value is DateTime dt) return dt;
        if (value is string s && DateTime.TryParse(
                s,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }
        return DateTime.MinValue;
    }

    /// <summary>
    /// Lifts every active ban, gag/mute/silence and warn for one player in a single sweep,
    /// inside one DB transaction. Reports the per-category count back to the caller and
    /// invalidates in-memory penalty state for any matching online player.
    /// Bans accept steamid / name / IP; mutes accept steamid / name; warns are SteamID-only.
    /// </summary>
    [CommandHelper(minArgs: 1, usage: "<steamid> [reason]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/root")]
    public void OnClearAllCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (DatabaseProvider == null) return;

        var pattern = command.GetArg(1);
        if (pattern.Length <= 1)
        {
            command.ReplyToCommand("Too short pattern to search.");
            return;
        }

        var callerSteamId = caller?.SteamID.ToString() ?? _localizer?["sa_console"] ?? "Console";
        var callerLabel = caller?.PlayerName ?? _localizer?["sa_console"] ?? "Console";
        var reason = command.ArgCount >= 2
            ? string.Join(" ", Enumerable.Range(2, command.ArgCount - 2).Select(command.GetArg)).Trim()
            : _localizer?["sa_unknown"] ?? "Unknown";
        reason = string.IsNullOrWhiteSpace(reason) ? _localizer?["sa_unknown"] ?? "Unknown" : reason;

        Helper.LogCommand(caller, command);

        Task.Run(async () =>
        {
            var bans = 0;
            var gags = 0;
            var mutes = 0;
            var silences = 0;
            var warns = 0;
            string? error = null;

            try
            {
                await using var connection = await DatabaseProvider.CreateConnectionAsync();
                await using var tx = await connection.BeginTransactionAsync();

                var multiServer = Config.MultiServerMode;

                // Bans
                var banIds = (await connection.QueryAsync<int>(
                    DatabaseProvider.GetUnbanRetrieveBansQuery(multiServer),
                    new { pattern, serverid = ServerId },
                    tx)).ToList();
                if (banIds.Count > 0)
                {
                    var banAdminId = await connection.ExecuteScalarAsync<int?>(
                        DatabaseProvider.GetUnbanAdminIdQuery(),
                        new { adminSteamId = callerSteamId },
                        tx) ?? 0;
                    var insertUnban = DatabaseProvider.GetInsertUnbanQuery(true);
                    var updateBan = DatabaseProvider.GetUpdateBanStatusQuery();
                    foreach (var banId in banIds)
                    {
                        var unbanId = await connection.ExecuteScalarAsync<int>(
                            insertUnban, new { banId, adminId = banAdminId, reason }, tx);
                        await connection.ExecuteAsync(updateBan, new { unbanId, banId }, tx);
                        bans++;
                    }
                }

                // Mutes (GAG / MUTE / SILENCE)
                var muteAdminId = await connection.ExecuteScalarAsync<int?>(
                    DatabaseProvider.GetUnmuteAdminIdQuery(),
                    new { adminSteamId = callerSteamId },
                    tx) ?? 0;
                var insertUnmute = DatabaseProvider.GetInsertUnmuteQuery(true);
                var updateMute = DatabaseProvider.GetUpdateMuteStatusQuery();
                var retrieveMutes = DatabaseProvider.GetRetrieveMutesQuery(multiServer);

                async Task<int> ClearMutesOfType(string muteType)
                {
                    var ids = (await connection.QueryAsync<int>(
                        retrieveMutes, new { pattern, muteType, serverid = ServerId }, tx)).ToList();
                    foreach (var muteId in ids)
                    {
                        var unmuteId = await connection.ExecuteScalarAsync<int>(
                            insertUnmute, new { muteId, adminId = muteAdminId, reason }, tx);
                        await connection.ExecuteAsync(updateMute, new { unmuteId, muteId }, tx);
                    }
                    return ids.Count;
                }

                gags = await ClearMutesOfType("GAG");
                mutes = await ClearMutesOfType("MUTE");
                silences = await ClearMutesOfType("SILENCE");

                // Warns (SteamID only — schema has no name/IP index on sa_warns)
                warns = await connection.ExecuteAsync(
                    DatabaseProvider.GetUnwarnAllQuery(multiServer),
                    new { steamid = pattern, serverid = ServerId },
                    tx);

                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                error = ex.Message;
                _logger?.LogError(ex, "css_clearall failed for pattern {pattern}", pattern);
            }

            if (error == null)
            {
                ulong.TryParse(pattern, out var steamIdNum);
                await Server.NextWorldUpdateAsync(() =>
                {
                    foreach (var p in Utilities.GetPlayers()
                                 .Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }))
                    {
                        var match = steamIdNum != 0
                            ? p.SteamID == steamIdNum
                            : p.PlayerName.Contains(pattern, StringComparison.OrdinalIgnoreCase);
                        if (match)
                            PlayerPenaltyManager.RemoveAllPenalties(p.Slot);
                    }
                });

                if (bans > 0)
                    _ = Instance.CacheManager?.ForceReInitializeCacheAsync();
            }

            await Server.NextWorldUpdateAsync(() =>
            {
                if (error != null)
                    command.ReplyToCommand($"clearall failed for {pattern}: {error}");
                else
                    command.ReplyToCommand(
                        $"Cleared for {pattern} — bans:{bans} gags:{gags} mutes:{mutes} silences:{silences} warns:{warns}.");
            });

            if (error == null && DiscordWebhookClientLog != null
                && (bans + gags + mutes + silences + warns) > 0)
            {
                _ = DiscordWebhookClientLog.SendMessageAsync(
                    $"**[clearall]** by `{callerLabel}` ({callerSteamId}) on `{pattern}` — " +
                    $"bans:{bans}, gags:{gags}, mutes:{mutes}, silences:{silences}, warns:{warns}. Reason: {reason}");
            }
        });
    }

    /// <summary>
    /// Backs up the SQLite database to a timestamped file under {ModuleDirectory}/backups
    /// using <c>VACUUM INTO</c>, then prunes older backups beyond
    /// <c>SqliteBackupRetainCount</c>. Refuses on non-SQLite providers.
    /// </summary>
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/root")]
    public void OnSqlBackupCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (DatabaseProvider == null) return;

        if (!DatabaseProvider.IsSqlite)
        {
            command.ReplyToCommand("Backup is only supported for SQLite databases. Use mysqldump for MySQL.");
            return;
        }

        var backupDir = Path.Combine(ModuleDirectory, "backups");
        var fileName = $"cs2-simpleadmin-{DateTime.UtcNow:yyyyMMdd-HHmmss}.sqlite";
        var destination = Path.Combine(backupDir, fileName);
        var retain = Math.Max(1, Config.DatabaseConfig.SqliteBackupRetainCount);

        Task.Run(async () =>
        {
            var (success, error) = await DatabaseProvider.BackupAsync(destination);
            var pruned = 0;

            if (success)
            {
                try
                {
                    var stale = Directory.EnumerateFiles(backupDir, "cs2-simpleadmin-*.sqlite")
                        .OrderByDescending(p => p, StringComparer.Ordinal)
                        .Skip(retain)
                        .ToList();
                    foreach (var path in stale)
                    {
                        File.Delete(path);
                        pruned++;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Backup retention prune failed");
                }
            }

            await Server.NextWorldUpdateAsync(() =>
            {
                if (success)
                    command.ReplyToCommand(
                        pruned > 0
                            ? $"Backup written to {destination} (pruned {pruned} old backups, keeping {retain})"
                            : $"Backup written to {destination}");
                else
                    command.ReplyToCommand($"Backup failed: {error}");
            });
        });
    }

    /// <summary>
    /// Reloads admin data asynchronously and updates admin caches.
    /// </summary>
    /// <param name="caller">The player issuing the reload command.</param>
    private static readonly SemaphoreSlim ReloadAdminsLock = new(1, 1);
    private static int _reloadGeneration;

    public void ReloadAdmins(CCSPlayerController? caller)
    {
        if (DatabaseProvider == null) return;

        Task.Run(async () =>
        {
            // Serialize reloads end to end: overlapping ones (css_admins_reload, add admin, map change) race on the JSON files,
            // and CSS reads them on timers up to 4s later. Capped so a hibernating server cannot hold the lock forever.
            await ReloadAdminsLock.WaitAsync();
            // If the cap expires (hibernation) and a newer reload runs, this one's pending timers must not apply a stale snapshot
            var generation = Interlocked.Increment(ref _reloadGeneration);
            try
            {
                await PermissionManager.CrateGroupsJsonFile();
                var admins = await PermissionManager.CreateAdminsJsonFile();

                var adminsFile = await File.ReadAllTextAsync(Instance.ModuleDirectory + "/data/admins.json");
                var groupsFile = await File.ReadAllTextAsync(Instance.ModuleDirectory + "/data/groups.json");

                var loaded = new TaskCompletionSource();
                var scheduled = Server.NextWorldUpdateAsync(() =>
                {
                    AddTimer(1, () =>
                    {
                        if (!string.IsNullOrEmpty(adminsFile))
                            AddTimer(2.0f, () =>
                            {
                                if (generation != _reloadGeneration) return;
                                // Strip old permissions right before loading the new ones, not seconds earlier
                                PermissionManager.ApplyAdminCache(admins);
                                AdminManager.LoadAdminData(ModuleDirectory + "/data/admins.json");
                            });
                        if (!string.IsNullOrEmpty(groupsFile))
                            AddTimer(3.0f, () =>
                            {
                                if (generation != _reloadGeneration) return;
                                AdminManager.LoadAdminGroups(ModuleDirectory + "/data/groups.json");
                            });
                        AddTimer(4.0f, () =>
                        {
                            if (generation == _reloadGeneration)
                            {
                                if (!string.IsNullOrEmpty(adminsFile))
                                    AdminManager.LoadAdminData(ModuleDirectory + "/data/admins.json");
                                _logger?.LogInformation("Loaded admins!");
                            }

                            // Always completes, so a superseded reload cannot hold the lock until the cap
                            loaded.TrySetResult();
                        });
                    });
                });

                await Task.WhenAny(Task.WhenAll(scheduled, loaded.Task), Task.Delay(TimeSpan.FromSeconds(15)));
            }
            catch (Exception ex)
            {
                // A failed DB read aborts here, leaving current permissions untouched
                _logger?.LogError("Unable to reload admins: {exception}", ex.Message);
            }
            finally
            {
                ReloadAdminsLock.Release();
            }
        });

        //_ = _adminManager.GiveAllGroupsFlags();
        //_ = _adminManager.GiveAllFlags();
    }

    /// <summary>
    /// Toggles player visibility on the server, hiding or revealing them.
    /// </summary>
    /// <param name="caller">The player issuing the hide command.</param>
    /// <param name="command">Command input parameters.</param>
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    [RequiresPermissions("@css/kick")]
    public void OnHideCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller == null) return;

        Helper.LogCommand(caller, command);

        if (!SilentPlayers.Add(caller.Slot))
        {
            SilentPlayers.Remove(caller.Slot);
            caller.PrintToChat($"You aren't hidden now!");
            if (caller.TeamNum <= 1)
                caller.ChangeTeam(CsTeam.Spectator);
            SimpleAdminApi?.OnAdminToggleSilentEvent(caller.Slot, false);
        }
        else
        {
            // sv_disable_teamselect_menu is a SERVER-WIDE cvar, so while it is on nobody
            // can pick a team. Two admins hiding at once used to have the first one's
            // restore timer clear the flag while the second was still mid-sequence, and
            // a map change in the 0.5s window killed the restore timer outright (it is
            // STOP_ON_MAPCHANGE) — leaving the whole server unable to join a team.
            // Refcount it and restore through AcquireTeamSelectLock/ReleaseTeamSelectLock,
            // which OnMapStart also resets.
            AcquireTeamSelectLock();

            if (caller.PlayerPawn?.Value?.LifeState == (int)LifeState_t.LIFE_ALIVE)
                caller.PlayerPawn.Value?.CommitSuicide(true, false);

            caller.PrintToChat($"You are hidden now!");

            // Resolve the controller again inside the timers: the admin can disconnect in
            // the 0.15-0.5s window and the captured wrapper would then point at a freed
            // entity (use-after-free -> SIGSEGV in libserver.so on Linux).
            var slot = caller.Slot;
            var steamId64 = caller.SteamID;
            var wasOnTeam = caller.TeamNum > 1;

            if (wasOnTeam)
                AddTimer(0.15f, () => { Server.NextWorldUpdate(() => ChangeTeamBySlot(slot, steamId64, CsTeam.Spectator)); }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.STOP_ON_MAPCHANGE);
            AddTimer(0.26f, () => { Server.NextWorldUpdate(() => ChangeTeamBySlot(slot, steamId64, CsTeam.None)); }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.STOP_ON_MAPCHANGE);
            AddTimer(0.50f, () => { Server.NextWorldUpdate(ReleaseTeamSelectLock); });
            SimpleAdminApi?.OnAdminToggleSilentEvent(caller.Slot, true);
        }
    }

    private static void ChangeTeamBySlot(int slot, ulong steamId64, CsTeam team)
    {
        var player = Utilities.GetPlayerFromSlot(slot);
        if (player is not { IsValid: true, IsBot: false }) return;
        if (player.SteamID != steamId64) return;
        if (player.Connected != PlayerConnectedState.Connected) return;

        player.ChangeTeam(team);
    }

    internal static void AcquireTeamSelectLock()
    {
        TeamSelectLockDepth++;
        Server.ExecuteCommand("sv_disable_teamselect_menu 1");
    }

    internal static void ReleaseTeamSelectLock()
    {
        if (TeamSelectLockDepth > 0)
            TeamSelectLockDepth--;

        if (TeamSelectLockDepth > 0) return;

        Server.ExecuteCommand("sv_disable_teamselect_menu 0");
    }

    internal static void ResetTeamSelectLock()
    {
        TeamSelectLockDepth = 0;
        Server.ExecuteCommand("sv_disable_teamselect_menu 0");
    }

    /// <summary>
    /// Toggles penalty notification visibility to admins.
    /// </summary>
    /// <param name="caller">The player toggling notification visibility.</param>
    /// <param name="command">Command input parameters.</param>
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    [RequiresPermissions("@css/kick")]
    public void OnHideCommsCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller == null)
            return;

        if (!AdminDisabledJoinComms.Add(caller.SteamID))
        {
            AdminDisabledJoinComms.Remove(caller.SteamID);
            command.ReplyToCommand("From now on, you'll see penalty notifications");
        }
        else
        {
            AdminDisabledJoinComms.Add(caller.SteamID);
            command.ReplyToCommand($"You don't see penalty notifications now");
        }
    }

    /// <summary>
    /// Displays detailed information about target players, including admin groups, permissions, and penalties.
    /// </summary>
    /// <param name="caller">The player issuing the command.</param>
    /// <param name="command">Command input parameters including targets.</param>
    [CommandHelper(minArgs: 1, usage: "<#userid or name>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/generic")]
    public void OnWhoCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (DatabaseProvider == null) return;

        var targets = GetTarget(command);
        if (targets == null) return;

        Helper.LogCommand(caller, command);

        var playersToTarget = targets.Players.Where(player => player is { IsValid: true, IsHLTV: false }).ToList();

        playersToTarget.ForEach(player =>
        {
            if (!player.UserId.HasValue) return;
            if (!caller!.CanTarget(player)) return;

            var playerInfo = PlayersInfo[player.SteamID];

            Task.Run(async () =>
            {
                await Server.NextWorldUpdateAsync(() =>
                {
                    Action<string> printMethod = caller == null ? Server.PrintToConsole : caller.PrintToConsole;
                    
                    var adminData = AdminManager.GetPlayerAdminData(new SteamID(player.SteamID));
                    
                    printMethod($"--------- INFO ABOUT \"{playerInfo.Name}\" ---------");
                    printMethod($"• Clan: \"{player.Clan}\" Name: \"{playerInfo.Name}\"");
                    printMethod($"• UserID: \"{playerInfo.UserId}\"");
                    printMethod($"• SteamID64: \"{playerInfo.SteamId.SteamId64}\"");
                    if (adminData != null)
                    {
                        var flags = string.Join(",", adminData._flags);
                        var groups = string.Join(",", adminData.Groups);

                        printMethod($"• Groups/Flags: \"{groups}{flags}\"");
                    }
                    printMethod($"• SteamID2: \"{playerInfo.SteamId.SteamId2}\"");
                    printMethod($"• Community link: \"{playerInfo.SteamId.ToCommunityUrl()}\"");
                    if ((playerInfo.IpAddress != null || IpHelper.IsSdrAddress(player.IpAddress)) && AdminManager.PlayerHasPermissions(new SteamID(caller!.SteamID), "@css/showip"))
                        printMethod($"• IP Address: \"{playerInfo.IpAddress ?? IpHelper.GetPlayerIpDisplay(player.IpAddress)}\"");
                    printMethod($"• Ping: \"{player.Ping}\"");
                    printMethod($"• Total Bans: \"{playerInfo.TotalBans}\"");
                    printMethod($"• Total Gags: \"{playerInfo.TotalGags}\"");
                    printMethod($"• Total Mutes: \"{playerInfo.TotalMutes}\"");
                    printMethod($"• Total Silences: \"{playerInfo.TotalSilences}\"");
                    printMethod($"• Total Warns: \"{playerInfo.TotalWarns}\"");

                    var chunkedAccounts = playerInfo.AccountsAssociated.ChunkBy(3).ToList();
                    foreach (var chunk in chunkedAccounts)
                        printMethod($"• Associated Accounts: \"{string.Join(", ", chunk.Select(a => $"{a.PlayerName} ({a.SteamId})"))}\"");

                    printMethod($"--------- END INFO ABOUT \"{player.PlayerName}\" ---------");
                });
            });
        });
    }

    /// <summary>
    /// Displays a menu with disconnected players, allowing the caller to apply penalties like ban, mute, gag, or silence.
    /// </summary>
    /// <param name="caller">The player issuing the command.</param>
    /// <param name="command">The command containing parameters.</param>
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    [RequiresPermissions("@css/kick")]
    public void OnDisconnectedCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (_localizer == null || caller == null) return;

        var disconnectedMenu = Helper.CreateMenu(_localizer["sa_menu_disconnected_title"]);

        DisconnectedPlayers.ForEach(player =>
        {
            disconnectedMenu?.AddItem(player.Name, (_, _) =>
            {
                var disconnectedMenuAction = Helper.CreateMenu(_localizer["sa_menu_disconnected_action_title"]);
                disconnectedMenuAction?.AddItem(_localizer["sa_ban"], (_, _) =>
                {
                    DurationMenu.OpenMenu(caller, _localizer["sa_ban"], player, (_, _, duration) =>
                        ReasonMenu.OpenMenu(caller, PenaltyType.Ban, _localizer["sa_reason"], player, (_, _, reason) =>
                        {
                            caller.ExecuteClientCommandFromServer($"css_addban {player.SteamId.SteamId64} {duration} \"{reason}\"");
                            
                            // Determine message keys and arguments based on ban time
                            var (_, activityMessageKey, _, adminActivityArgs) = duration == 0
                                ? ("sa_player_ban_message_perm", "sa_admin_ban_message_perm",
                                    [reason, "CALLER"],
                                    ["CALLER", player.Name, reason])
                                : ("sa_player_ban_message_time", "sa_admin_ban_message_time",
                                    new object[] { reason, duration, "CALLER" },
                                    new object[] { "CALLER", player.Name, reason, duration });

                            // Display admin activity message if necessary
                            if (!SilentPlayers.Contains(caller.Slot))
                            {
                                Helper.ShowAdminActivity(activityMessageKey, caller.PlayerName, false ,adminActivityArgs);
                            }
                            
                            MenuManager.CloseActiveMenu(caller);
                        }));
                });
                disconnectedMenuAction?.AddItem(_localizer["sa_mute"], (_, _) =>
                {
                    DurationMenu.OpenMenu(caller, _localizer["sa_mute"], player, (_, _, duration) =>
                        ReasonMenu.OpenMenu(caller, PenaltyType.Mute, _localizer["sa_reason"], player, (_, _, reason) =>
                        {
                            caller.ExecuteClientCommandFromServer($"css_addmute {player.SteamId.SteamId64} {duration} \"{reason}\"");
                            
                            // Determine message keys and arguments based on mute time (permanent or timed)
                            var (_, activityMessageKey, _, adminActivityArgs) = duration == 0
                                ? ("sa_player_mute_message_perm", "sa_admin_mute_message_perm",
                                    [reason, "CALLER"],
                                    ["CALLER", player.Name, reason])
                                : ("sa_player_mute_message_time", "sa_admin_mute_message_time",
                                    new object[] { reason, duration, "CALLER" },
                                    new object[] { "CALLER", player.Name, reason, duration });

                            // Display admin activity message to other players
                            if (!SilentPlayers.Contains(caller.Slot))
                            {
                                Helper.ShowAdminActivity(activityMessageKey, caller.PlayerName, false, adminActivityArgs);
                            }
                            
                            MenuManager.CloseActiveMenu(caller);
                        }));
                });
                disconnectedMenuAction?.AddItem(_localizer["sa_gag"], (_, _) =>
                {
                    DurationMenu.OpenMenu(caller, _localizer["sa_gag"], player, (_, _, duration) =>
                        ReasonMenu.OpenMenu(caller, PenaltyType.Mute, _localizer["sa_reason"], player, (_, _, reason) =>
                        {
                            caller.ExecuteClientCommandFromServer($"css_addgag {player.SteamId.SteamId64} {duration} \"{reason}\"");
                            
                            // Determine message keys and arguments based on gag time (permanent or timed)
                            var (_, activityMessageKey, _, adminActivityArgs) = duration == 0
                                ? ("sa_player_gag_message_perm", "sa_admin_gag_message_perm",
                                    [reason, "CALLER"],
                                    ["CALLER", player.Name, reason])
                                : ("sa_player_gag_message_time", "sa_admin_gag_message_time",
                                    new object[] { reason, duration, "CALLER" },
                                    new object[] { "CALLER", player.Name, reason, duration});

                            // Display admin activity message to other players
                            if (!SilentPlayers.Contains(caller.Slot))
                            {
                                Helper.ShowAdminActivity(activityMessageKey, caller.PlayerName, false, adminActivityArgs);
                            }
                            
                            MenuManager.CloseActiveMenu(caller);
                        }));
                });
                disconnectedMenuAction?.AddItem(_localizer["sa_silence"], (_, _) =>
                {
                    DurationMenu.OpenMenu(caller, _localizer["sa_silence"], player, (_, _, duration) =>
                        ReasonMenu.OpenMenu(caller, PenaltyType.Mute, _localizer["sa_reason"], player, (_, _, reason) =>
                        {
                            caller.ExecuteClientCommandFromServer($"css_addsilence {player.SteamId.SteamId64} {duration} \"{reason}\"");
                            
                            // Determine message keys and arguments based on silence time (permanent or timed)
                            var (_, activityMessageKey, _, adminActivityArgs) = duration == 0
                                ? ("sa_player_silence_message_perm", "sa_admin_silence_message_perm",
                                    [reason, "CALLER"],
                                    ["CALLER", player.Name, reason])
                                : ("sa_player_silence_message_time", "sa_admin_silence_message_time",
                                    new object[] { reason, duration, "CALLER" },
                                    new object[] { "CALLER", player.Name, reason, duration });

                            // Display admin activity message to other players
                            if (!SilentPlayers.Contains(caller.Slot))
                            {
                                Helper.ShowAdminActivity(activityMessageKey, caller.PlayerName, false, adminActivityArgs);
                            }
                            
                            MenuManager.CloseActiveMenu(caller);
                        }));
                });

                disconnectedMenuAction?.Display(caller, 0);
            });
        });

        disconnectedMenu?.Display(caller, 0);
    }

    /// <summary>
    /// Displays the warning menu for a player specified by a command argument,
    /// showing active and past warns with options to remove them.
    /// </summary>
    /// <param name="caller">The player issuing the command.</param>
    /// <param name="command">The command containing target player identifier.</param>
    [CommandHelper(minArgs: 1, usage: "<#userid or name>", whoCanExecute: CommandUsage.CLIENT_ONLY)]
    [RequiresPermissions("@css/kick")]
    public void OnWarnsCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (DatabaseProvider == null || _localizer == null || caller == null) return;

        var targets = GetTarget(command);
        if (targets == null) return;

        Helper.LogCommand(caller, command);

        var playersToTarget = targets.Players.Where(player => player is { IsValid: true, IsBot: false }).ToList();
        if (playersToTarget.Count > 1)
            return;

        playersToTarget.ForEach(player =>
        {
            if (!player.UserId.HasValue) return;
            if (!caller.CanTarget(player)) return;

            var userId = player.UserId.Value;
            var steamId = player.SteamID;

            IMenu? warnsMenu = Helper.CreateMenu(_localizer["sa_admin_warns_menu_title", player.PlayerName]);

            Task.Run(async () =>
            {
                var warnsList = await WarnManager.GetPlayerWarns(PlayersInfo[steamId], false);
                var sortedWarns = warnsList
                    .OrderBy(warn => (string)warn.status == "ACTIVE" ? 0 : 1)
                    .ThenByDescending(warn => (int)warn.id)
                    .ToList();

                sortedWarns.ForEach(w =>
                {
                    warnsMenu?.AddItem($"[{((string)w.status == "ACTIVE" ? $"{ChatColors.LightRed}X" : $"{ChatColors.Lime}✔️")}{ChatColors.Default}] {(string)w.reason}",
                        (controller, option) =>
                        {
                            _ = WarnManager.UnwarnPlayer(PlayersInfo[steamId], (int)w.id);
                            player.PrintToChat(_localizer["sa_admin_warns_unwarn", player.PlayerName, (string)w.reason]);
                        });
                });

                await Server.NextWorldUpdateAsync(() =>
                {
                    warnsMenu?.Display(caller, 0);
                });
            });
        });
    }

    /// <summary>
    /// Prints a player's full penalty history (bans, gags, mutes, silences, warns) to the caller's console.
    /// Accepts a SteamID64 so it also works for players who are not online.
    /// </summary>
    /// <param name="caller">The player issuing the command or null for console.</param>
    /// <param name="command">The command containing the target and an optional penalty type filter.</param>
    [RequiresPermissions("@css/kick")]
    [CommandHelper(minArgs: 1, usage: "<#userid or name or steamid64> [bans|gags|mutes|silences|warns]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnHistoryCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (DatabaseProvider == null || _localizer == null) return;

        var filter = command.ArgCount > 2 ? command.GetArg(2).ToLowerInvariant() : null;
        if (filter is not (null or "bans" or "gags" or "mutes" or "silences" or "warns"))
        {
            command.ReplyToCommand("Usage: css_history <#userid or name or steamid64> [bans|gags|mutes|silences|warns]");
            return;
        }

        ulong steamId;
        string? name = null;

        if (Helper.ValidateSteamId(command.GetArg(1), out var parsedSteamId) && parsedSteamId != null)
        {
            if (caller != null && !caller.CanTarget(parsedSteamId)) return;

            steamId = parsedSteamId.SteamId64;
            if (PlayersInfo.TryGetValue(steamId, out var info)) name = info.Name;
        }
        else
        {
            var targets = GetTarget(command);
            if (targets == null) return;

            var players = targets.Players.Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }).ToList();
            if (players.Count != 1)
            {
                command.ReplyToCommand("Only one target is allowed.");
                return;
            }

            var player = players[0];
            if (caller != null && !caller.CanTarget(player)) return;

            steamId = player.SteamID;
            name = player.PlayerName;
        }

        Helper.LogCommand(caller, command);
        Action<string> print = caller == null ? Server.PrintToConsole : caller.PrintToConsole;
        var callerSteamId = caller?.SteamID;

        Task.Run(async () =>
        {
            var rows = await PlayerManager.GetPenaltyHistory(steamId, filter);
            // Offline players are only known by SteamID; borrow the name stored on their latest record
            name ??= rows.Count > 0 ? (string?)rows[0].player_name : null;

            await Server.NextWorldUpdateAsync(() =>
            {
                // The slot may have been reused by another player while the query ran
                if (caller != null && (!caller.IsValid || caller.SteamID != callerSteamId)) return;
                PrintHistory(print, name ?? steamId.ToString(), steamId, rows);
            });
        });
    }

    /// <summary>
    /// Writes history rows from <see cref="PlayerManager.GetPenaltyHistory"/> as one console line each.
    /// </summary>
    internal void PrintHistory(Action<string> print, string name, ulong steamId, List<dynamic> rows)
    {
        if (_localizer == null) return;

        if (rows.Count == 0)
        {
            print(_localizer["sa_history_none", name]);
            return;
        }

        print(_localizer["sa_history_header", name, steamId, rows.Count]);
        foreach (var r in rows)
        {
            var duration = (int)r.duration == 0 ? "perm" : $"{(int)r.duration}m";
            var line = $"[{Date(r.created)}] {(string)r.type,-7} {(string)r.status,-8} {duration,-6} by {(string)r.admin_name}: {(string)r.reason}";
            if (r.lift_date != null)
                line += $" | lifted {Date(r.lift_date)} by {(string?)r.lift_admin ?? "?"}: {(string?)r.lift_reason}";
            print(line);
        }

        static string Date(object? value) => PlayerManager.ToDateTime(value)?.ToString("yyyy-MM-dd HH:mm") ?? "?";
    }

    /// <summary>
    /// Lists players currently connected to the server with options to output JSON or filter duplicate IPs.
    /// </summary>
    /// <param name="caller">The player issuing the command or null for console.</param>
    /// <param name="command">The command containing output options.</param>
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/generic")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
    public void OnPlayersCommand(CCSPlayerController? caller, CommandInfo command)
    {
        var isJson = command.GetArg(1).ToLower().Equals("-json");
        var isDuplicate = command.GetArg(1).ToLower().Equals("-duplicate") || command.GetArg(2).ToLower().Equals("-duplicate");

        var playersToTarget = isDuplicate
            ? Helper.GetValidPlayers().Where(player => IpHelper.GetPlayerIp(player.IpAddress) != null)
                .GroupBy(player => IpHelper.GetPlayerIp(player.IpAddress))
                .Where(group => group.Count() > 1)
                .SelectMany(group => group)
                .ToList()
            : Helper.GetValidPlayers();

        if (!isJson)
        {
            if (caller != null)
            {
                caller.PrintToConsole("--------- PLAYER LIST ---------");
                foreach (var player in playersToTarget)
                {
                    caller.PrintToConsole(
                        $"• [#{player.UserId}] \"{player.PlayerName}\" (IP Address: \"{(AdminManager.PlayerHasPermissions(new SteamID(caller.SteamID), "@css/showip") ? IpHelper.GetPlayerIpDisplay(player.IpAddress) : "Unknown")}\" SteamID64: \"{player.SteamID}\")");
                };
                caller.PrintToConsole("--------- END PLAYER LIST ---------");
            }
            else
            {
                Server.PrintToConsole("--------- PLAYER LIST ---------");
                foreach (var player in playersToTarget)
                {
                    Server.PrintToConsole($"• [#{player.UserId}] \"{player.PlayerName}\" (IP Address: \"{IpHelper.GetPlayerIpDisplay(player.IpAddress)}\" SteamID64: \"{player.SteamID}\")");
                };
                Server.PrintToConsole("--------- END PLAYER LIST ---------");
            }
        }
        else
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            var playerDtos = playersToTarget
                .Where(player => player.UserId.HasValue)
                .Select(player =>
                {
                    var matchStats = player.ActionTrackingServices?.MatchStats;

                    return new PlayerDto(
                        player.UserId.GetValueOrDefault(0),
                        player.PlayerName,
                        player.SteamID.ToString(),
                        AdminManager.PlayerHasPermissions(new SteamID(caller!.SteamID), "@css/showip")
                            ? IpHelper.GetPlayerIpDisplay(player.IpAddress)
                            : "Unknown",
                        player.Ping,
                        AdminManager.PlayerHasPermissions(new SteamID(player.SteamID), "@css/ban")
                        || AdminManager.PlayerHasPermissions(new SteamID(player.SteamID), "@css/generic"),
                        new PlayerStats(
                            player.Score,
                            matchStats?.Kills ?? 0,
                            matchStats?.Deaths ?? 0,
                            player.MVPs
                        )
                    );
                })
                .ToList();

            var playersJson = JsonSerializer.Serialize(playerDtos, options);

            if (caller != null)
                caller.PrintToConsole(playersJson);
            else
                Server.PrintToConsole(playersJson);
        }
    }

    /// <summary>
    /// Issues a kick to one or multiple players specified in the command arguments.
    /// </summary>
    /// <param name="caller">The player issuing the kick command.</param>
    /// <param name="command">The command with target player(s) and optional reason.</param>
    [RequiresPermissions("@css/kick")]
    [CommandHelper(minArgs: 1, usage: "<#userid or name> [reason]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnKickCommand(CCSPlayerController? caller, CommandInfo command)
    {
        var callerName = caller == null ? _localizer?["sa_console"] ?? "Console" : caller.PlayerName;

        var targets = GetTarget(command);

        if (targets == null) return;
        var playersToTarget = targets.Players
            .Where(player => player is { IsValid: true, IsHLTV: false }).ToList();

        if (playersToTarget.Count > 1 && Config.OtherSettings.DisableDangerousCommands || playersToTarget.Count == 0)
        {
            return;
        }

        var reason = command.ArgCount >= 2 
            ? string.Join(" ", Enumerable.Range(2, command.ArgCount - 2).Select(command.GetArg)).Trim() 
            : _localizer?["sa_unknown"] ?? "Unknown";

        reason = string.IsNullOrWhiteSpace(reason) ? _localizer?["sa_unknown"] ?? "Unknown" : reason;

        playersToTarget.ForEach(player =>
        {
            if (!player.IsValid)
                return;

            if (caller!.CanTarget(player))
            {
                Kick(caller, player, reason, callerName, command);
            }
        });
        
        Helper.LogCommand(caller, command);
    }

    
    /// <summary>
    /// Kicks a specified player immediately with reason, notifying the server and logging the action.
    /// </summary>
    /// <param name="caller">The player issuing the kick.</param>
    /// <param name="player">The player to be kicked.</param>
    /// <param name="reason">The reason for the kick.</param>
    /// <param name="callerName">Optional name of the kick issuer for notifications.</param>
    /// <param name="command">Optional command for logging.</param>
    public void Kick(CCSPlayerController? caller, CCSPlayerController player, string? reason = "Unknown", string? callerName = null, CommandInfo? command = null)
    {
        if (!player.IsValid) return;
        if (!caller.CanTarget(player)) return;
        if (!player.UserId.HasValue) return;
        
        // Set default caller name if not provided
        callerName ??= caller != null ? caller.PlayerName : _localizer?["sa_console"] ?? "Console";
        reason ??= _localizer?["sa_unknown"] ?? "Unknown";
        
        var playerInfo = PlayersInfo[player.SteamID];
        var adminInfo = caller != null && caller.UserId.HasValue ? PlayersInfo[caller.SteamID] : null;
        
        // Determine message keys and arguments for the kick notification
        var (messageKey, activityMessageKey, centerArgs, adminActivityArgs) =
            ("sa_player_kick_message", "sa_admin_kick_message",
                new object[] { reason, "CALLER" },
                new object[] { "CALLER", player.PlayerName, reason });

        // Display center message to the kicked player
        Helper.DisplayCenterMessage(player, messageKey, callerName, centerArgs);

        // Display admin activity message to other players
        if (caller == null || !SilentPlayers.Contains(caller.Slot))
        {
            Helper.ShowAdminActivity(activityMessageKey, callerName, false, adminActivityArgs);
        }

        // Schedule the kick for the player
        if (player.UserId.HasValue)
        {
            Helper.KickPlayer(player.UserId.Value, NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED, Config.OtherSettings.KickTime);
        }

        // Log the command and send Discord notification
        if (command == null)
            Helper.LogCommand(caller, $"css_kick {(string.IsNullOrEmpty(player.PlayerName) ? player.SteamID.ToString() : player.PlayerName)} {reason}");
        
        SimpleAdminApi?.OnPlayerPenaltiedEvent(playerInfo, adminInfo, PenaltyType.Kick, reason, -1, null);
    }

    /// <summary>
    /// Changes the current map to the specified map name or workshop map ID.
    /// </summary>
    /// <param name="caller">The player issuing the map change.</param>
    /// <param name="command">The command containing the map name or ID.</param>
    [RequiresPermissions("@css/changemap")]
    [CommandHelper(minArgs: 1, usage: "<mapname>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnMapCommand(CCSPlayerController? caller, CommandInfo command)
    {
        var map = command.GetCommandString.Split(" ")[1];
        ChangeMap(caller, map, command);
    }

    /// <summary>
    /// Changes to a specified map, validating it or handling workshop maps, and notifying the server and admins.
    /// </summary>
    /// <param name="caller">The player issuing the change.</param>
    /// <param name="map">The map name or identifier.</param>
    /// <param name="command">Optional command object for logging and replies.</param>
    public void ChangeMap(CCSPlayerController? caller, string map, CommandInfo? command = null)
    {
        var callerName = caller != null ? caller.PlayerName : _localizer?["sa_console"] ?? "Console";
        map = map.ToLower();

        if (map.StartsWith("ws:"))
        {
            var issuedCommand = long.TryParse(map.Replace("ws:", ""), out var mapId)
                ? $"host_workshop_map {mapId}"
                : $"ds_workshop_changelevel {map.Replace("ws:", "")}";

            AddTimer(3.0f, () =>
            {
                Server.ExecuteCommand(issuedCommand);
            }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.STOP_ON_MAPCHANGE);
        }
        else
        {
            if (!Server.IsMapValid(map))
            {
                var msg = $"Map {map} not found.";
                if (command != null)
                    command.ReplyToCommand(msg);
                else if (caller != null && caller.IsValid)
                    caller.PrintToChat(msg);
                else
                    Server.PrintToConsole(msg);
                return;
            }

            AddTimer(3.0f, () =>
            {
                Server.ExecuteCommand($"changelevel {map}");
            }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.STOP_ON_MAPCHANGE);
        }

        var (activityMessageKey, adminActivityArgs) =
            ("sa_admin_changemap_message",
                new object[] { "CALLER", map });

        // Display admin activity message to other players
        if (caller == null || !SilentPlayers.Contains(caller.Slot))
        {
            Helper.ShowAdminActivity(activityMessageKey, callerName, false, adminActivityArgs);
        }

        Helper.LogCommand(caller, command?.GetCommandString ?? $"css_map {map}");
    }

    /// <summary>
    /// Changes the current map to a workshop map specified by name or ID.
    /// </summary>
    /// <param name="caller">The player issuing the command.</param>
    /// <param name="command">The command containing the workshop map identifier.</param>
    [CommandHelper(1, "<name or id>")]
    [RequiresPermissions("@css/changemap")]
    public void OnWorkshopMapCommand(CCSPlayerController? caller, CommandInfo command)
    {
        var map = command.GetArg(1);
        ChangeWorkshopMap(caller, map, command);
    }

    /// <summary>
    /// Changes to a specified workshop map by name or ID and notifies admins.
    /// </summary>
    /// <param name="caller">The player issuing the command.</param>
    /// <param name="map">The workshop map identifier.</param>
    /// <param name="command">Optional command for logging.</param>
    public void ChangeWorkshopMap(CCSPlayerController? caller, string map, CommandInfo? command = null)
    {
        map = map.ToLower();
        var callerName = caller != null ? caller.PlayerName : _localizer?["sa_console"] ?? "Console";

        // Determine the workshop command
        var issuedCommand = long.TryParse(map, out var mapId)
            ? $"host_workshop_map {mapId}"
            : $"ds_workshop_changelevel {map}";

        // Define the admin activity message and arguments
        var activityMessageKey = "sa_admin_changemap_message";

        // Display admin activity message to other players
        if (caller == null || !SilentPlayers.Contains(caller.Slot))
        {
            Helper.ShowAdminActivity(activityMessageKey, callerName, false, ["CALLER", map]);
        }

        // Add timer to execute the map change command after a delay
        AddTimer(3.0f, () =>
        {
            Server.ExecuteCommand(issuedCommand);
        }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.STOP_ON_MAPCHANGE);

        // Log the command for the change
        Helper.LogCommand(caller, command?.GetCommandString ?? $"css_wsmap {map}");
    }

    /// <summary>
    /// Allows changing a console variable's value.
    /// </summary>
    /// <param name="caller">The player issuing the command.</param>
    /// <param name="command">The command with cvar name and value.</param>
    [CommandHelper(2, "<cvar> <value>")]
    [RequiresPermissions("@css/cvar")]
    public void OnCvarCommand(CCSPlayerController? caller, CommandInfo command)
    {
        var cvar = ConVar.Find(command.GetArg(1));
        var callerName = caller == null ? _localizer?["sa_console"] ?? "Console" : caller.PlayerName;

        if (cvar == null)
        {
            command.ReplyToCommand($"Cvar \"{command.GetArg(1)}\" not found.");
            return;
        }

        if (cvar.Name.Equals("sv_cheats") && !AdminManager.PlayerHasPermissions(new SteamID(caller!.SteamID), "@css/cheats"))
        {
            command.ReplyToCommand($"You don't have permissions to change \"{command.GetArg(1)}\".");
            return;
        }

        Helper.LogCommand(caller, command);
        var value = command.GetArg(2);
        Server.ExecuteCommand($"{cvar.Name} {value}");
        command.ReplyToCommand($"{callerName} changed cvar {cvar.Name} to {value}.");
        Logger.LogInformation($"{callerName} changed cvar {cvar.Name} to {value}.");
    }

    /// <summary>
    /// Executes an RCON command on the server.
    /// </summary>
    /// <param name="caller">The player issuing the command.</param>
    /// <param name="command">The command string to execute via RCON.</param>
    [CommandHelper(1, "<command>")]
    [RequiresPermissions("@css/rcon")]
    public void OnRconCommand(CCSPlayerController? caller, CommandInfo command)
    {
        var callerName = caller == null ? _localizer?["sa_console"] ?? "Console" : caller.PlayerName;
        Helper.LogCommand(caller, command);
        Server.ExecuteCommand(command.ArgString);
        command.ReplyToCommand($"{callerName} executed command {command.ArgString}.");
        Logger.LogInformation($"{callerName} executed command ({command.ArgString}).");
    }

    /// <summary>
    /// Restarts the game.
    /// </summary>
    /// <param name="caller">The player or console initiating the restart.</param>
    /// <param name="command">The restart command info.</param>
    [RequiresPermissions("@css/generic")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnRestartCommand(CCSPlayerController? caller, CommandInfo command)
    {
        RestartGame(caller);
    }

    /// <summary>
    /// Opens plugin manager menu for the caller with options to load or unload plugins.
    /// </summary>
    /// <param name="caller">The player opening the plugin manager.</param>
    /// <param name="commandInfo">The command parameters.</param>
    [RequiresPermissions("@css/root")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnPluginManagerCommand(CCSPlayerController? caller, CommandInfo commandInfo)
    {
        if (caller == null)
            return;
        
        var pluginManager = Helper.GetPluginManager();
        if (pluginManager == null)
        {
            Logger.LogError("Unable to access PluginManager.");
            return;
        }

        var getLoadedPluginsMethod = pluginManager.GetType().GetMethod("GetLoadedPlugins", BindingFlags.Public | BindingFlags.Instance);
        if (getLoadedPluginsMethod?.Invoke(pluginManager, null) is not IEnumerable plugins)
        {
            Logger.LogError("Unable to retrieve plugins.");
            return;
        }

        var pluginsMenu = Helper.CreateMenu(Localizer["sa_menu_pluginsmanager_title"], _ => ManageServerMenu.OpenMenu(caller));
        
        foreach (var plugin in plugins)
        {
            var pluginType = plugin.GetType();

            // Accessing each property with the Type of the plugin
            var pluginId = pluginType.GetProperty("PluginId")?.GetValue(plugin);
            var state = pluginType.GetProperty("State")?.GetValue(plugin)?.ToString();
            var path = pluginType.GetProperty("FilePath")?.GetValue(plugin)?.ToString();
            path = Path.GetFileName(Path.GetDirectoryName(path));

            // Access nested properties within "Plugin" (like ModuleName, ModuleVersion, etc.)
            var nestedPlugin = pluginType.GetProperty("Plugin")?.GetValue(plugin);
            if (nestedPlugin == null) continue;
            
            var status = state?.ToUpper() != "UNLOADED" ? "ON" : "OFF";
            var configMenuType = Config.MenuConfigs.MenuType.ToLower();
            var rendersHtml = configMenuType is "center" or "wasdmenu" or "wasd" or "dynamic"
                || (configMenuType == "selectable"
                    && MenuTypeManager.GetPlayerMenuType(caller) is { } t
                    && (t == typeof(CenterHtmlMenu) || t == typeof(WasdMenu)));

            if (rendersHtml)
                status = state?.ToUpper() != "UNLOADED" ? "<font color='lime'>ON</font>" : "<font color='red'>OFF</font>";
            var nestedType = nestedPlugin.GetType();
            var moduleName = nestedType.GetProperty("ModuleName")?.GetValue(nestedPlugin)?.ToString() ?? "Unknown";
            var moduleVersion = nestedType.GetProperty("ModuleVersion")?.GetValue(nestedPlugin)?.ToString();
            // var moduleAuthor = nestedType.GetProperty("ModuleAuthor")?.GetValue(nestedPlugin)?.ToString();
            // var moduleDescription = nestedType.GetProperty("ModuleDescription")?.GetValue(nestedPlugin)?.ToString();

            pluginsMenu?.AddItem($"({status}) [{moduleName} {moduleVersion}]", (_, _) =>
            {
                if (state?.ToUpper() != "UNLOADED")
                {
                    caller.SendLocalizedMessage(Localizer, "sa_menu_pluginsmanager_unloaded", moduleName);
                    Server.ExecuteCommand($"css_plugins unload {pluginId}");
                }
                else
                {
                    caller.SendLocalizedMessage(Localizer, "sa_menu_pluginsmanager_loaded", moduleName);
                    Server.ExecuteCommand($"css_plugins load {path}");
                }

                AddTimer(0.1f, () => OnPluginManagerCommand(caller, commandInfo));
            });
                
            // Console.WriteLine($"[#{pluginId}:{state?.ToUpper()}]: \"{moduleName ?? "Unknown"}\" ({moduleVersion ?? "Unknown"}) by {moduleAuthor}");
        }
        
        pluginsMenu?.Display(caller, 0);
    }

    /// <summary>
    /// Restarts the game process by issuing the restart game command to the server and logging the action.
    /// </summary>
    /// <param name="admin">The admin or console requesting the restart.</param>
    public static void RestartGame(CCSPlayerController? admin)
    {
        Helper.LogCommand(admin, "css_restartgame");

        // TODO: Localize
        var name = admin == null ? _localizer?["sa_console"] ?? "Console" : admin.PlayerName;
        Server.PrintToChatAll($"[SA] {name}: Restarting game...");
        Server.ExecuteCommand("mp_restartgame 2");
    }
}