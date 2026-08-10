using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using CS2_SimpleAdminApi;
using Microsoft.Extensions.Logging;

namespace CS2_SimpleAdmin_StealthModule;

public class CS2_SimpleAdmin_StealthModule: BasePlugin, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "[CS2-SimpleAdmin] Stealth Module";
    public override string ModuleVersion => "v1.0.3";
    public override string ModuleAuthor => "daffyy";

    private static ICS2_SimpleAdminApi? _sharedApi;
    private readonly PluginCapability<ICS2_SimpleAdminApi> _pluginCapability  = new("simpleadmin:api");

    // Slots, NOT CCSPlayerController references. A controller wrapper holds a raw
    // native pointer; the entity behind it is freed on disconnect and on every map
    // change. Keeping wrappers in a long-lived set meant CheckTransmit dereferenced
    // freed CCSPlayerController memory every tick after the first map change (nothing
    // ever cleared the set), which is the "Couldn't resolve offset" spam + SIGSEGV in
    // libserver.so from issue #291. Slots are plain ints and are re-resolved through
    // Utilities.GetPlayerFromSlot on use, so a stale entry can only ever yield null.
    internal static readonly HashSet<int> PlayerSlots = [];

    public PluginConfig Config { get; set; } = new();

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.CheckTransmit>(OnCheckTransmit);
        RegisterListener<Listeners.OnMapStart>(OnMapStart);

        if (hotReload)
            RebuildPlayerSlots();
    }

    private void OnMapStart(string mapName)
    {
        // Every controller from the previous map is gone by now.
        PlayerSlots.Clear();
    }

    private static void RebuildPlayerSlots()
    {
        PlayerSlots.Clear();

        foreach (var player in Utilities.GetPlayers())
        {
            if (player is { IsValid: true, IsBot: false, IsHLTV: false })
                PlayerSlots.Add(player.Slot);
        }
    }

    public void OnConfigParsed(PluginConfig config)
    {
        Config = config;
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        try
        {
            _sharedApi = _pluginCapability.Get();
            if (_sharedApi == null) throw new NullReferenceException("_sharedApi is null");

            if (Config.BlockStatusCommand)
                _sharedApi.OnAdminToggleSilent += OnAdminToggleSilent;
        }
        catch (Exception)
        {
            Logger.LogError("CS2-SimpleAdmin SharedApi not found");
            Unload(false);
        }
    }

    public override void Unload(bool hotReload)
    {
        // Tear down listeners so plugin state doesn't survive hot reloads. The
        // CheckTransmit listener + OnAdminToggleSilent handler would otherwise keep
        // firing against the unloaded instance and pin the whole plugin graph.
        RemoveListener<Listeners.CheckTransmit>(OnCheckTransmit);
        RemoveListener<Listeners.OnMapStart>(OnMapStart);

        if (_sharedApi != null && Config is { BlockStatusCommand: true })
            _sharedApi.OnAdminToggleSilent -= OnAdminToggleSilent;

        PlayerSlots.Clear();
        _sharedApi = null;
    }

    private void OnAdminToggleSilent(int slot, bool status)
    {
        Server.ExecuteCommand(status ? $"mm_excludeslot {slot}" : $"mm_removeexcludeslot {slot}");
    }

    // Reused across ticks so the per-tick dispatch doesn't allocate.
    private readonly List<(int Slot, int PawnIndex)> _hiddenObservers = [];

    private void OnCheckTransmit(CCheckTransmitInfoList infolist)
    {
        if (_sharedApi is null || PlayerSlots.Count <= 2) return;

        var silentSlots = _sharedApi.ListSilentAdminsSlots();
        if (silentSlots.Count == 0) return;

        _hiddenObservers.Clear();

        // Only silent admins get hidden. The old loop walked every tracked player and
        // stripped their observer pawn from everyone else's transmit set as soon as a
        // single admin was hidden, which also blanked ordinary spectators.
        foreach (var slot in silentSlots)
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player is not { IsValid: true, IsBot: false }) continue;
            if (player.Connected != PlayerConnectedState.Connected) continue;

            var observerPawn = player.ObserverPawn.Value;
            if (observerPawn is not { IsValid: true }) continue;

            _hiddenObservers.Add((slot, (int)observerPawn.Index));
        }

        if (_hiddenObservers.Count == 0) return;

        foreach (var (info, player) in infolist)
        {
            if (player == null || player.IsHLTV)
                continue;

            var entities = info.TransmitEntities;
            var viewerSlot = player.Slot;

            foreach (var (slot, pawnIndex) in _hiddenObservers)
            {
                if (slot == viewerSlot)
                    continue;

                // Remove is idempotent, so the Contains() guard that used to sit in
                // front of it doubled the work for nothing. This loop is
                // clients x hidden admins, every tick.
                entities.Remove(pawnIndex);
            }
        }
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult EventPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        if (ShouldSuppressBroadcast(@event.Userid))
            info.DontBroadcast = true;

        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult EventPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player?.IsValid != true || player.IsBot) return HookResult.Continue;

        PlayerSlots.Add(player.Slot);

        var steamId = new SteamID(player.SteamID);
        if (!Config.Permissions.Any(permission => AdminManager.PlayerHasPermissions(steamId, permission)))
            return HookResult.Continue;

        if (!Config.HideAdminsOnJoin) return HookResult.Continue;

        // Capture slot + SteamID, never the controller: the admin can drop before the
        // timer fires, and by then the wrapper points at freed memory.
        var slot = player.Slot;
        var steamId64 = player.SteamID;

        AddTimer(1.0f, () =>
        {
            var target = Utilities.GetPlayerFromSlot(slot);
            if (target is not { IsValid: true, IsBot: false }) return;
            if (target.SteamID != steamId64) return;
            if (target.Connected != PlayerConnectedState.Connected) return;
            if (_sharedApi?.IsAdminSilent(target) == true) return;

            // css_hide owns the whole hide sequence (suicide if alive -> spectator ->
            // team none -> re-enable the team select menu). The module used to call
            // ChangeTeam(Spectator) itself 0.5s beforehand, which raced that sequence
            // and left the admin stuck in a half-applied team state on join.
            target.ExecuteClientCommandFromServer("css_hide");
        }, TimerFlags.STOP_ON_MAPCHANGE);

        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult EventPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player?.IsValid != true || player.IsBot) return HookResult.Continue;

        var suppress = ShouldSuppressBroadcast(player);

        if (Config.BlockStatusCommand && _sharedApi != null && _sharedApi.IsAdminSilent(player))
            Server.ExecuteCommand($"mm_removeexcludeslot {player.Slot}");

        PlayerSlots.Remove(player.Slot);

        if (suppress)
            info.DontBroadcast = true;

        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult EventPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (ShouldSuppressBroadcast(@event.Userid, true))
            info.DontBroadcast = true;

        return HookResult.Continue;
    }

    private bool ShouldSuppressBroadcast(CCSPlayerController? player, bool checkTeam = false)
    {
        if (player?.IsValid != true || player.IsBot)
            return false;

        if (_sharedApi is not null && _sharedApi.IsAdminSilent(player))
        {
            return true;
        }

        if (checkTeam && player.TeamNum > 1)
            return false;

        var steamId = new SteamID(player.SteamID);
        return Config.Permissions.Any(permission => AdminManager.PlayerHasPermissions(steamId, permission));
    }
}
