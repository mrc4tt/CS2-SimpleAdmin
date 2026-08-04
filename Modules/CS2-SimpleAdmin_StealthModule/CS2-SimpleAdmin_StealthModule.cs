using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;
using CS2_SimpleAdminApi;
using Microsoft.Extensions.Logging;

namespace CS2_SimpleAdmin_StealthModule;

public class CS2_SimpleAdmin_StealthModule: BasePlugin, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "[CS2-SimpleAdmin] Stealth Module";
    public override string ModuleVersion => "v1.0.2";
    public override string ModuleAuthor => "daffyy";

    private static ICS2_SimpleAdminApi? _sharedApi;
    private readonly PluginCapability<ICS2_SimpleAdminApi> _pluginCapability  = new("simpleadmin:api");
    
    internal static readonly HashSet<CCSPlayerController> Players = [];
    // private readonly HashSet<int> _admins = [];
    // private readonly HashSet<CCSPlayerController> _spectatedPlayers = [];
    
    public PluginConfig Config { get; set; }

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.CheckTransmit>(OnCheckTransmit);
        
        // Old method
        // if (Config.BlockStatusCommand)
            // RegisterListener<Listeners.OnServerPostEntityThink>(OnServerPostEntityThink);

        if (hotReload)
        {
            Players.Clear();
            var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot).ToList();

            foreach (var player in players)
            {
                var steamId = new SteamID(player.SteamID);
                // if (Config.Permissions.Any(permission => AdminManager.PlayerHasPermissions(steamId, permission)))
                    // _admins.Add(player.Slot);

                Players.Add(player);
            }
        }
        
        // Old method
        // AddTimer(3, RefreshSpectatedPlayers, TimerFlags.REPEAT);
    }

    // Old method
    // private void RefreshSpectatedPlayers()
    // {
    //     _spectatedPlayers.Clear();
    //
    //     foreach (var admin in _admins)
    //     {
    //         var observer = admin.GetSpectatingPlayer();
    //         if (observer != null)
    //             _spectatedPlayers.Add(observer);
    //     }
    // }
    
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
        // Drop CCSPlayerController refs and tear down listeners so plugin
        // state doesn't survive hot reloads. CheckTransmit listener +
        // OnAdminToggleSilent event handler would otherwise keep firing
        // against the unloaded instance and pin the whole plugin graph.
        RemoveListener<Listeners.CheckTransmit>(OnCheckTransmit);

        if (_sharedApi != null && Config is { BlockStatusCommand: true })
            _sharedApi.OnAdminToggleSilent -= OnAdminToggleSilent;

        Players.Clear();
        _sharedApi = null;
    }

    private void OnAdminToggleSilent(int slot, bool status)
    {
        Server.ExecuteCommand(status ? $"mm_excludeslot {slot}" : $"mm_removeexcludeslot {slot}");
    }

    // Old method
    // private void OnServerPostEntityThink()
    // {
    //     if (Players.Count <= 2 || _sharedApi is not null && _sharedApi.ListSilentAdminsSlots().Count == 0) return;
    //     foreach (var player in _spectatedPlayers)
    //     {
    //         if (player?.IsValid != true || player.IsBot)  continue;
    //         player.PrintToConsole(" ");
    //     }
    // }

    // Reused across ticks. The old LINQ chain allocated an anonymous-type instance per player
    // plus a backing array on EVERY CheckTransmit dispatch — that is once per tick, for the whole
    // time a silent admin is connected. Only the slot and the pawn index are needed downstream,
    // so both are cached as value tuples and the list is refilled in place.
    private readonly List<(int Slot, int PawnIndex)> _hiddenObservers = [];

    private void OnCheckTransmit(CCheckTransmitInfoList infolist)
    {
        if (Players.Count <= 2 || _sharedApi is not null && _sharedApi.ListSilentAdminsSlots().Count == 0) return;

        _hiddenObservers.Clear();
        foreach (var p in Players)
        {
            if (p is not { IsValid: true }) continue;

            var observerPawn = p.ObserverPawn.Value;
            if (observerPawn?.IsValid != true) continue; // safe check

            _hiddenObservers.Add((p.Slot, (int)observerPawn.Index));
        }

        if (_hiddenObservers.Count == 0) return;

        foreach (var (info, player) in infolist)
        {
            if (player == null || player.IsHLTV)
                continue;

            var entities = info.TransmitEntities;
            int viewerSlot = player.Slot;

            foreach (var (slot, pawnIndex) in _hiddenObservers)
            {
                if (slot == viewerSlot)
                    continue;

                // Remove is a bit clear and is idempotent, so the Contains() guard that used to
                // sit in front of it doubled the work for nothing. This loop is
                // clients x hidden observers, every tick.
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
        
        Players.Add(player);
        
        var steamId = new SteamID(player.SteamID);
        if (!Config.Permissions.Any(permission => AdminManager.PlayerHasPermissions(steamId, permission)))
            return HookResult.Continue;
        
        // _admins.Add(player.Slot);

        if (!Config.HideAdminsOnJoin) return HookResult.Continue;
        
        AddTimer(0.75f, () =>
        {
            player.ChangeTeam(CsTeam.Spectator);
        });
        
        AddTimer(1.25f, () =>
        {
            player.ExecuteClientCommandFromServer("css_hide");
        });
        
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult EventPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player?.IsValid != true || player.IsBot) return HookResult.Continue;

        if (Config.BlockStatusCommand && _sharedApi != null && _sharedApi.IsAdminSilent(player))
            Server.ExecuteCommand($"mm_removeexcludeslot {player.Slot}");

        Players.Remove(player);
        // _admins.Remove(player.Slot);
        
        if (ShouldSuppressBroadcast(@event.Userid))
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
    
    // private IEnumerable<CCSPlayerController> GetNonAdmins()
    //     => Players.Where(p => !_admins.Contains(p));
} 