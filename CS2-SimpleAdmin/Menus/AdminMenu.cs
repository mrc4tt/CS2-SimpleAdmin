using CounterStrikeSharp.API.Core;
using CS2MenuManager.API.Interface;

namespace CS2_SimpleAdmin.Menus;

public static class AdminMenu
{
    public static void OpenMenu(CCSPlayerController admin)
    {
        AdminMenuRegistry.Instance.OpenMainMenu(admin);
    }

    public static IMenu? CreateMenu(string title, Action<CCSPlayerController>? backAction = null)
    {
        return Helper.CreateMenu(title, backAction);
    }

    public static void OpenMenu(CCSPlayerController player, IMenu menu)
    {
        menu.Display(player, 0);
    }

    // public static void OpenMenu(CCSPlayerController admin)
    // {
    //     if (admin.IsValid == false)
    //         return;
    //
    //     var localizer = CS2_SimpleAdmin._localizer;
    //     if (AdminManager.PlayerHasPermissions(new SteamID(admin.SteamID), "@css/generic") == false)
    //     {
    //         admin.PrintToChat(localizer?["sa_prefix"] ??
    //                           "[SimpleAdmin] " +
    //                           (localizer?["sa_no_permission"] ?? "You do not have permissions to use this command")
    //         );
    //         return;
    //     }
    //
    //     var menu = CreateMenu(localizer?["sa_title"] ?? "SimpleAdmin");
    //     List<ChatMenuOptionData> options =
    //     [
    //         new(localizer?["sa_menu_players_manage"] ?? "Players Manage", () => ManagePlayersMenu.OpenMenu(admin)),
    //         new(localizer?["sa_menu_server_manage"] ?? "Server Manage", () => ManageServerMenu.OpenMenu(admin)),
    //         new(localizer?["sa_menu_fun_commands"] ?? "Fun Commands", () => FunActionsMenu.OpenMenu(admin)),
    //     ];
    //
    //     var customCommands = CS2_SimpleAdmin.Instance.Config.CustomServerCommands;
    //     if (customCommands.Count > 0)
    //     {
    //         options.Add(new ChatMenuOptionData(localizer?["sa_menu_custom_commands"] ?? "Custom Commands", () => CustomCommandsMenu.OpenMenu(admin)));
    //     }
    //
    //     if (AdminManager.PlayerHasPermissions(new SteamID(admin.SteamID), "@css/root"))
    //         options.Add(new ChatMenuOptionData(localizer?["sa_menu_admins_manage"] ?? "Admins Manage", () => ManageAdminsMenu.OpenMenu(admin)));
    //
    //     foreach (var menuOptionData in options)
    //     {
    //         var menuName = menuOptionData.Name;
    //         menu?.AddMenuOption(menuName, (_, _) => { menuOptionData.Action.Invoke(); }, menuOptionData.Disabled);
    //     }
    //
    //     if (menu != null) OpenMenu(admin, menu);
    // }
}