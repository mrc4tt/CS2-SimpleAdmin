---
title: Module translations
description: Giving a module its own translations, so every player sees its menus and admin activity messages in their own language.
sidebar:
  order: 2
---

A module can ship its own `lang/` files and pass its localizer to the API. SimpleAdmin then sends
each player the module's texts in that player's language, and applies the server's
`ShowActivityType` rules to them like to its own messages. The
[Fun Commands module](https://github.com/mrc4tt/CS2-SimpleAdmin/tree/main/Modules/CS2-SimpleAdmin_FunCommands)
does this for every text it shows.

## 1. Add lang files

Put a `lang/` folder next to your module DLL, one JSON file per language, with keys prefixed by
your module's name so they do not clash with SimpleAdmin's:

```txt
plugins/MyModule/
  MyModule.dll
  lang/
    en.json
    de.json
```

```json
{
  "mymod_category": "My module",
  "mymod_menu_slap": "Slap a player",
  "mymod_admin_slap_message": "{lightred}{0}{default} slapped {lightred}{1}{default}!"
}
```

CounterStrikeSharp loads them into the plugin's `Localizer`. Copy the folder to the output in the
`.csproj`:

```xml
<ItemGroup>
  <None Update="lang\**\*.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Color tags such as `{lightred}` and `{default}` work as in SimpleAdmin's own files.

## 2. Admin activity messages

Use `ShowAdminActivityLocalized` with your `Localizer` and put the string `"CALLER"` where the
admin's name goes:

```csharp
private void ShowSlap(CCSPlayerController? admin, CCSPlayerController target)
{
    if (admin != null && _api!.IsAdminSilent(admin))
        return; // admin is hidden with css_hide: no announcement

    var callerName = admin?.PlayerName ?? "Console";
    _api!.ShowAdminActivityLocalized(Localizer, "mymod_admin_slap_message", callerName, false,
        "CALLER", target.PlayerName);
}
```

`"CALLER"` is replaced per player according to `ShowActivityType`:

| `ShowActivityType` | Who gets the message | `CALLER` becomes |
| --- | --- | --- |
| `0` | nobody | |
| `1` | everyone | "ADMIN" for players, the admin's name for admins (`@css/kick` or `@css/ban`) |
| `2` (default) | everyone | the admin's name |
| `3` | only admins (`@css/kick` or `@css/ban`) | the admin's name |

Without a caller (from the server console) the name is "CONSOLE". Log the action separately with
`_api.LogCommand(...)` so it reaches the admin log and Discord.

Also in the API: `ShowAdminActivity(messageKey, ...)` uses **SimpleAdmin's** translation keys, and
`ShowAdminActivityTranslated(text, ...)` sends one already-translated text to everyone.

## 3. Menus

`RegisterMenuCategory` and `RegisterMenu` have overloads that take translation keys and the
localizer; the names are then shown in each admin's language:

```csharp
_api!.RegisterMenuCategory("mymod", "mymod_category", "@css/generic", Localizer);
_api.RegisterMenu("mymod", "slap", "mymod_menu_slap", CreateSlapMenu, "@css/slay", "css_slap", Localizer);
```

The `commandName` argument (`"css_slap"` here) lets CounterStrikeSharp permission overrides for that
command (from `admin_overrides.json`) apply to the menu entry too. Texts inside your own menus come from
`Localizer["key"]` as usual.

## Common mistakes

- **Passing the admin's name instead of `"CALLER"`**: the name is then shown to everyone,
  ignoring `ShowActivityType`, so a server that hides admin names would show them.
- **Using SimpleAdmin's keys with your localizer** (or yours with `ShowAdminActivity`): the key is
  not found and the raw key is shown.
- **Not handling a missing `Localizer`**: fall back to `ShowAdminActivity` or a fixed text, as the
  Fun Commands module does.
