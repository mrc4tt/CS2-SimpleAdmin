using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CS2MenuManager.API.Enum;
using System.Web;

namespace CS2_SimpleAdmin.Menus;

public static class PlayersMenu
{
    public static void OpenRealPlayersMenu(CCSPlayerController admin, string menuName, Action<CCSPlayerController, CCSPlayerController> onSelectAction, Func<CCSPlayerController, bool>? enableFilter = null)
    {
        OpenMenu(admin, menuName, onSelectAction, p => !p.IsBot);
    }

    public static void OpenAdminPlayersMenu(CCSPlayerController admin, string menuName, Action<CCSPlayerController, CCSPlayerController> onSelectAction, Func<CCSPlayerController?, bool>? enableFilter = null)
    {
        OpenMenu(admin, menuName, onSelectAction, p => AdminManager.GetPlayerAdminData(p)?.Flags.Count > 0);
    }

    public static void OpenAliveMenu(CCSPlayerController admin, string menuName, Action<CCSPlayerController, CCSPlayerController> onSelectAction, Func<CCSPlayerController, bool>? enableFilter = null)
    {
        OpenMenu(admin, menuName, onSelectAction, p => p.PlayerPawn?.Value?.LifeState == (int)LifeState_t.LIFE_ALIVE);
    }

    public static void OpenDeadMenu(CCSPlayerController admin, string menuName, Action<CCSPlayerController?, CCSPlayerController> onSelectAction, Func<CCSPlayerController, bool>? enableFilter = null)
    {
        OpenMenu(admin, menuName, onSelectAction, p => p.PlayerPawn?.Value?.LifeState != (int)LifeState_t.LIFE_ALIVE);
    }

    public static void OpenMenu(CCSPlayerController admin, string menuName, Action<CCSPlayerController, CCSPlayerController> onSelectAction, Func<CCSPlayerController, bool>? enableFilter = null)
    {
        var menu = AdminMenu.CreateMenu(menuName);

        var players = Helper.GetValidPlayersWithBots();

        foreach (var player in players)
        {
            // GetValidPlayersWithBots already filters invalid/disconnected players
            var playerName = player.PlayerName.Length > 26 ? player.PlayerName[..26] : player.PlayerName;

            var optionName = HttpUtility.HtmlEncode(playerName);
            if (enableFilter != null && !enableFilter(player))
                continue;

            // For ForceTeam and similar commands, allow targeting self
            // CanTarget will be false for self, but we still want to show the option
            var canTarget = admin.CanTarget(player);
            var isSelf = player.Slot == admin.Slot;
            
            // Enable the option if admin can target the player OR if it's the admin themselves
            var enabled = canTarget || isSelf;
            
            var capturedPlayer = player; // Capture in local variable to avoid closure issues

            if (optionName != null)
                menu?.AddItem(optionName, (controller, option) =>
                    {
                        if (capturedPlayer != null) onSelectAction.Invoke(admin, capturedPlayer);
                    },
                    !enabled ? DisableOption.DisableShowNumber : DisableOption.None);
        }

        if (menu != null) AdminMenu.OpenMenu(admin, menu);
    }
}