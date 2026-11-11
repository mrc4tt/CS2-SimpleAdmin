# Module Development Guide - CS2-SimpleAdmin

> **📘 Docs:** the module guide and the full API reference are also at
> [simpleadmin.miksen.me/extending/modules](https://simpleadmin.miksen.me/extending/modules/).

> **🎓 New to module development?** Start with the [Fun Commands Module](./CS2-SimpleAdmin_FunCommands/) - it's a fully documented reference implementation showing all best practices!

This guide explains how to create modules for CS2-SimpleAdmin with custom commands, menus, and translations.

## 📖 Table of Contents

1. [Quick Start](#-quick-start)
2. [Learning Resources](#-learning-resources)
3. [MenuContext API (New!)](#-menucontext-api-new)
4. [API Methods Reference](#-api-methods-reference)
5. [Menu Patterns](#-menu-patterns)
6. [Best Practices](#best-practices)
7. [Troubleshooting](#troubleshooting)

## 🚀 Quick Start

### Step 1: Study the Example Module

The **[CS2-SimpleAdmin_FunCommands](./CS2-SimpleAdmin_FunCommands/)** module is your best learning resource. It demonstrates:

✅ Command registration from config
✅ Dynamic menu creation
✅ Per-player translation support
✅ Proper cleanup on unload
✅ Code organization with partial classes
✅ All menu patterns you'll need

**Start here:** Read [`CS2-SimpleAdmin_FunCommands/README.md`](./CS2-SimpleAdmin_FunCommands/README.md)

### Step 2: Create Your Module Structure

```
YourModule/
├── YourModule.csproj                 # Project file
├── YourModule.cs                     # Main plugin class
├── Commands.cs                       # Command handlers (optional)
├── Menus.cs                          # Menu creation (optional)
├── Config.cs                         # Configuration
└── lang/                             # Translations
    ├── en.json
    ├── pl.json
    └── ru.json
```

### Step 3: Minimal Working Example (NEW MenuContext API!)

```csharp
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin; // AdminManager.CanPlayerTarget
using CS2_SimpleAdminApi;

public class MyModule : BasePlugin
{
    private ICS2_SimpleAdminApi? _api;
    private readonly PluginCapability<ICS2_SimpleAdminApi> _pluginCapability = new("simpleadmin:api");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = _pluginCapability.Get();
        if (_api == null)
        {
            Logger.LogError("CS2-SimpleAdmin API not found");
            return;
        }

        // Register menus after API is ready
        _api.OnSimpleAdminReady += RegisterMenus;
        RegisterMenus(); // Fallback for hot reload
    }

    private void RegisterMenus()
    {
        if (_api == null) return;

        // 1. Register a new category
        // IMPORTANT: If your module has lang/ folder with translations, use YOUR module's Localizer!
        // Example: _api.RegisterMenuCategory("mymodule", Localizer?["mymodule_category_name"] ?? "My Module", "@css/generic");
        //
        // This will use YOUR module's translations in server language.
        // For modules without translations, you can use hard-coded text:
        _api.RegisterMenuCategory("mymodule", "My Module", "@css/generic");

        // 2. Register menu items in the category
        // 🆕 NEW: Use MenuContext-aware overload (no duplication!)
        // NOTE: Use YOUR module's Localizer if you have lang/ folder with translations:
        //   _api.RegisterMenu("mymodule", "action1", Localizer?["mymodule_menu_action1"] ?? "Action 1", CreateAction1Menu, "@css/generic");
        _api.RegisterMenu("mymodule", "action1", "Action 1", CreateAction1Menu, "@css/generic");
        _api.RegisterMenu("mymodule", "action2", "Action 2", CreateAction2Menu, "@css/kick");
    }

    // 🆕 NEW: Factory now receives MenuContext parameter
    private object CreateAction1Menu(CCSPlayerController admin, MenuContext context)
    {
        // Create a menu with automatic back button
        // Use context instead of repeating title and category!
        var menu = _api!.CreateMenuWithBack(context, admin);

        // Add menu options
        _api.AddMenuOption(menu, "Option 1", player =>
        {
            player.PrintToChat("You selected Option 1");
        });

        _api.AddMenuOption(menu, "Option 2", player =>
        {
            player.PrintToChat("You selected Option 2");
        });

        return menu;
    }

    // 🆕 NEW: MenuContext eliminates duplication here too!
    private object CreateAction2Menu(CCSPlayerController admin, MenuContext context)
    {
        // Use the built-in player selection menu with context
        return _api!.CreateMenuWithPlayers(
            context,  // ← Contains title & category automatically!
            admin,
            player => player.IsValid && AdminManager.CanPlayerTarget(admin, player),
            (adminPlayer, targetPlayer) =>
            {
                adminPlayer.PrintToChat($"You selected {targetPlayer.PlayerName}");
            });
    }

    public override void Unload(bool hotReload)
    {
        if (_api == null) return;

        // Clean up registered menus
        _api.UnregisterMenu("mymodule", "action1");
        _api.UnregisterMenu("mymodule", "action2");
        _api.OnSimpleAdminReady -= RegisterMenus;
    }
}
```

## 📚 Learning Resources

### For Beginners

1. **Start Here:** [`CS2-SimpleAdmin_FunCommands/README.md`](./CS2-SimpleAdmin_FunCommands/README.md)
   - Explains every file and pattern
   - Shows code organization
   - Demonstrates all menu types

2. **Read the Code:** Study these files in order:
   - `Config.cs` - Simple configuration
   - `CS2-SimpleAdmin_FunCommands.cs` - Plugin initialization
   - `Commands.cs` - Command registration
   - `Menus.cs` - Menu creation patterns

3. **Translations:** [`TRANSLATION_EXAMPLE.md`](./TRANSLATION_EXAMPLE.md)
   - How to use module translations
   - Per-player language support
   - Best practices

### Key Concepts

| Concept | What It Does | Example File |
|---------|-------------|--------------|
| **API Capability** | Get access to SimpleAdmin API | `CS2-SimpleAdmin_FunCommands.cs:37` |
| **Command Registration** | Register console commands | `Commands.cs:15-34` |
| **Menu Registration** | Add menus to admin menu | `CS2-SimpleAdmin_FunCommands.cs:130-141` |
| **Translations** | Per-player language support | `Actions.cs:20-31` |
| **Cleanup** | Unregister on plugin unload | `CS2-SimpleAdmin_FunCommands.cs:63-97` |

## 🎯 Menu Patterns

The FunCommands module demonstrates **3 essential menu patterns** you'll use in every module:

### Pattern 1: Simple Player Selection (NEW MenuContext API!)
**When to use:** Select a player and immediately execute an action

```csharp
// 🆕 NEW: Factory receives MenuContext - eliminates duplication!
private object CreateGodModeMenu(CCSPlayerController admin, MenuContext context)
{
    return _api.CreateMenuWithPlayers(
        context,                            // ← Contains title & category automatically!
        admin,                              // Admin
        player => player.IsValid && AdminManager.CanPlayerTarget(admin, player),  // Filter
        (adminPlayer, target) =>            // Action
        {
            // Execute action immediately
            ToggleGodMode(target);
        });
}
```

**Why MenuContext is better:**
- ❌ **Old way:** `"God Mode"` and `"yourmodule"` had to be typed twice (in RegisterMenu and CreateMenuWithPlayers)
- ✅ **New way:** Context contains both automatically - no duplication, no mistakes!

**See:** `CS2-SimpleAdmin_FunCommands/Menus.cs:44-51`

### Pattern 2: Nested Menu (Player → Value) - NEW MenuContext API!
**When to use:** Select a player, then select a value/option for that player

```csharp
// 🆕 NEW: Uses MenuContext to eliminate duplication
private object CreateSetHpMenu(CCSPlayerController admin, MenuContext context)
{
    var menu = _api.CreateMenuWithBack(context, admin);  // ← No more repeating title & category!
    var players = _api.GetValidPlayers().Where(p => AdminManager.CanPlayerTarget(admin, p));

    foreach (var player in players)
    {
        // AddSubMenu automatically adds back button to submenu
        _api.AddSubMenu(menu, player.PlayerName,
            p => CreateHpValueMenu(admin, player));
    }

    return menu;
}

// Example: Set HP menu (value selection)
// Note: Submenus don't receive context - they create dynamic titles
private object CreateHpValueMenu(CCSPlayerController admin, CCSPlayerController target)
{
    var menu = _api.CreateMenuWithBack($"HP for {target.PlayerName}", "yourmodule", admin);
    var values = new[] { 50, 100, 200, 500 };

    foreach (var hp in values)
    {
        _api.AddMenuOption(menu, $"{hp} HP", _ =>
        {
            if (target.IsValid)  // Always validate!
            {
                target.PlayerPawn.Value.Health = hp;
            }
        });
    }

    return menu;
}
```

**Benefits of MenuContext:**
- ✅ Change menu title in one place (RegisterMenu) and it updates everywhere
- ✅ Can't accidentally mistype category ID
- ✅ Access to permission and command name from context if needed

**See:** `CS2-SimpleAdmin_FunCommands/Menus.cs:163-178`

### Pattern 3: Nested Menu with Complex Data
**When to use:** Need to display more complex options (like weapons with icons, items with descriptions)

```csharp
// Example: Give Weapon menu
private object CreateGiveWeaponMenu(CCSPlayerController admin)
{
    var menu = _api.CreateMenuWithBack("Give Weapon", "yourmodule", admin);
    var players = _api.GetValidPlayers().Where(p => AdminManager.CanPlayerTarget(admin, p));

    foreach (var player in players)
    {
        _api.AddSubMenu(menu, player.PlayerName,
            p => CreateWeaponSelectionMenu(admin, player));
    }

    return menu;
}

private object CreateWeaponSelectionMenu(CCSPlayerController admin, CCSPlayerController target)
{
    var menu = _api.CreateMenuWithBack($"Weapons for {target.PlayerName}", "yourmodule", admin);

    // Use cached data for performance
    foreach (var weapon in GetWeaponsCache())
    {
        _api.AddMenuOption(menu, weapon.Value.ToString(), _ =>
        {
            if (target.IsValid)
            {
                target.GiveNamedItem(weapon.Value);
            }
        });
    }

    return menu;
}
```

**See:** `CS2-SimpleAdmin_FunCommands/Menus.cs:67-109`

## 🆕 MenuContext API (New!)

### What is MenuContext?

`MenuContext` is a new feature that eliminates code duplication when creating menus. When you register a menu, you provide information like title, category, and permissions. Previously, you had to repeat this information in your menu factory method. Now, this information is automatically passed to your factory via `MenuContext`.

### MenuContext Properties

```csharp
public class MenuContext
{
    public string CategoryId { get; }      // e.g., "fun"
    public string MenuId { get; }          // e.g., "god"
    public string MenuTitle { get; }       // e.g., "God Mode"
    public string? Permission { get; }     // e.g., "@css/cheats"
    public string? CommandName { get; }    // e.g., "css_god"
}
```

### Before vs After Comparison

```csharp
// ❌ OLD API - Duplication everywhere
_api.RegisterMenu("fun", "god", "God Mode", CreateGodModeMenu, "@css/cheats");

private object CreateGodModeMenu(CCSPlayerController admin)
{
    return _api.CreateMenuWithPlayers(
        "God Mode",    // ← Duplicated from RegisterMenu
        "fun",         // ← Duplicated from RegisterMenu
        admin, filter, action);
}

// ✅ NEW API - No duplication!
_api.RegisterMenu("fun", "god", "God Mode", CreateGodModeMenu, "@css/cheats");

private object CreateGodModeMenu(CCSPlayerController admin, MenuContext context)
{
    return _api.CreateMenuWithPlayers(
        context,       // ← Contains all info automatically!
        admin, filter, action);
}
```

### When to Use MenuContext

| Menu Creation Method | Old Signature | New Signature (Recommended) |
|---------------------|---------------|----------------------------|
| `CreateMenuWithBack` | `(string title, string categoryId, ...)` | `(MenuContext context, ...)` |
| `CreateMenuWithPlayers` | `(string title, string categoryId, ...)` | `(MenuContext context, ...)` |

**Rule of thumb:** If you're creating a menu directly from a registered menu factory, use `MenuContext`. For dynamic submenus (e.g., player-specific menus), use the old API.

### Backward Compatibility

The old API still works! Both signatures are supported:

```csharp
// ✅ Old API - still works
_api.RegisterMenu("cat", "id", "Title",
    (CCSPlayerController admin) => CreateOldStyleMenu(admin));

// ✅ New API - recommended
_api.RegisterMenu("cat", "id", "Title",
    (CCSPlayerController admin, MenuContext ctx) => CreateNewStyleMenu(admin, ctx));
```

## 📋 API Methods Reference

### 1. Category Management

#### `RegisterMenuCategory(string categoryId, string categoryName, string permission = "@css/generic")`

Registers a new menu category that appears in the main admin menu.

**Parameters:**
- `categoryId` - Unique identifier for the category (e.g., "fun", "vip", "economy")
- `categoryName` - **TRANSLATION KEY** for the display name (e.g., "vip_category_name", NOT "VIP Features")
- `permission` - Required permission to see the category (default: "@css/generic")

**IMPORTANT FOR MODULES WITH TRANSLATIONS:**
- If your module has a `lang/` folder with translation files, use **YOUR module's Localizer**
- This will display menu names in the **server's language** (not per-player)
- For per-player localization, only **CS2-SimpleAdmin's built-in menus** support this currently

**Example:**
```csharp
// ✅ CORRECT: Module with translations uses its own Localizer
_api.RegisterMenuCategory("vip", Localizer?["vip_category_name"] ?? "VIP Features", "@css/vip");
// In YOUR module's lang/en.json: "vip_category_name": "VIP Features"
// In YOUR module's lang/pl.json: "vip_category_name": "Funkcje VIP"

// ✅ ALSO CORRECT: Module without translations uses hard-coded text
_api.RegisterMenuCategory("vip", "VIP Features", "@css/vip");

// ❌ INCORRECT: Using translation key without Localizer
_api.RegisterMenuCategory("vip", "vip_category_name", "@css/vip");
// This would display "vip_category_name" literally!
```

### 2. Menu Registration

#### `RegisterMenu(string categoryId, string menuId, string menuName, Func<CCSPlayerController, object> menuFactory, string? permission = null)`

Registers a menu item within a category.

**Parameters:**
- `categoryId` - The category to add this menu to
- `menuId` - Unique identifier for the menu
- `menuName` - Display name for the menu (use **your module's Localizer** if you have translations)
- `menuFactory` - Function that creates the menu when selected (receives admin player and MenuContext)
- `permission` - Optional permission required to see this menu item

**IMPORTANT FOR MODULES WITH TRANSLATIONS:**
- Use **your module's Localizer** if you have a `lang/` folder: `Localizer?["key"] ?? "Fallback"`
- This shows menu in **server language**, not per-player
- Per-player localization is only available for CS2-SimpleAdmin's built-in menus

**Example:**
```csharp
// ✅ CORRECT: Module with translations uses its own Localizer
_api.RegisterMenu("fun", "godmode", Localizer?["fun_menu_god"] ?? "God Mode", CreateGodModeMenu, "@css/cheats");
// In YOUR module's lang/en.json: "fun_menu_god": "God Mode"
// In YOUR module's lang/pl.json: "fun_menu_god": "Tryb Boga"

// ✅ ALSO CORRECT: Module without translations uses hard-coded text
_api.RegisterMenu("fun", "godmode", "God Mode", CreateGodModeMenu, "@css/cheats");

// ❌ INCORRECT: Using translation key without Localizer
_api.RegisterMenu("fun", "godmode", "fun_menu_god", CreateGodModeMenu, "@css/cheats");
// This would display "fun_menu_god" literally!
```

#### `UnregisterMenu(string categoryId, string menuId)`

Removes a menu item from a category.

**Example:**
```csharp
_api.UnregisterMenu("fun", "godmode");
```

### 3. Menu Creation

#### `CreateMenuWithBack(string title, string categoryId, CCSPlayerController player)`
#### `CreateMenuWithBack(MenuContext context, CCSPlayerController player)` 🆕 NEW!

Creates a menu with an automatic "Back" button that returns to the category menu.

**Parameters (Old API):**
- `title` - Menu title
- `categoryId` - Category this menu belongs to (for back navigation)
- `player` - The admin player viewing the menu

**Parameters (New API - Recommended):**
- `context` - MenuContext containing title, category, and other metadata
- `player` - The admin player viewing the menu

**Returns:** `object` (MenuBuilder instance)

**Example (Old API):**
```csharp
var menu = _api.CreateMenuWithBack("Weapon Selection", "fun", admin);
```

**Example (New API - Recommended):**
```csharp
private object CreateWeaponMenu(CCSPlayerController admin, MenuContext context)
{
    var menu = _api.CreateMenuWithBack(context, admin);  // ← Uses context!
    // ... add options
    return menu;
}
```

#### `CreateMenuWithPlayers(string title, string categoryId, CCSPlayerController admin, Func<CCSPlayerController, bool> filter, Action<CCSPlayerController, CCSPlayerController> onSelect)`
#### `CreateMenuWithPlayers(MenuContext context, CCSPlayerController admin, Func<CCSPlayerController, bool> filter, Action<CCSPlayerController, CCSPlayerController> onSelect)` 🆕 NEW!

Creates a menu with a list of players, filtered and with automatic back button.

**Parameters (Old API):**
- `title` - Menu title
- `categoryId` - Category for back navigation
- `admin` - The admin player viewing the menu
- `filter` - Function to filter which players appear in the menu
- `onSelect` - Action to execute when a player is selected (receives admin and target)

**Parameters (New API - Recommended):**
- `context` - MenuContext containing title and category
- `admin` - The admin player viewing the menu
- `filter` - Function to filter which players appear in the menu
- `onSelect` - Action to execute when a player is selected (receives admin and target)

**Returns:** `object` (MenuBuilder instance)

**Example (Old API):**
```csharp
return _api.CreateMenuWithPlayers("Select Player to Kick", "admin", admin,
    player => player.IsValid && AdminManager.CanPlayerTarget(admin, player),
    (adminPlayer, targetPlayer) =>
    {
        // Kick the selected player
        Server.ExecuteCommand($"css_kick {targetPlayer.UserId}");
    });
```

**Example (New API - Recommended):**
```csharp
private object CreateKickMenu(CCSPlayerController admin, MenuContext context)
{
    return _api.CreateMenuWithPlayers(context, admin,
        player => player.IsValid && AdminManager.CanPlayerTarget(admin, player),
        (adminPlayer, targetPlayer) =>
        {
            Server.ExecuteCommand($"css_kick {targetPlayer.UserId}");
        });
}
```

### 4. Menu Manipulation

#### `AddMenuOption(object menu, string name, Action<CCSPlayerController> action, bool disabled = false, string? permission = null)`

Adds a clickable option to a menu.

**Parameters:**
- `menu` - The menu object (from CreateMenuWithBack)
- `name` - Display name of the option
- `action` - Action to execute when clicked (receives the player who clicked)
- `disabled` - Whether the option is disabled (grayed out)
- `permission` - Optional permission required to see this option

**Example:**
```csharp
_api.AddMenuOption(menu, "Give AK-47", player =>
{
    player.GiveNamedItem("weapon_ak47");
}, permission: "@css/cheats");
```

#### `AddSubMenu(object menu, string name, Func<CCSPlayerController, object> subMenuFactory, bool disabled = false, string? permission = null)`

Adds a submenu option that opens another menu. **Automatically adds a back button to the submenu.**

**Parameters:**
- `menu` - The parent menu
- `name` - Display name of the submenu option
- `subMenuFactory` - Function that creates the submenu (receives the player)
- `disabled` - Whether the option is disabled
- `permission` - Optional permission required

**Example:**
```csharp
_api.AddSubMenu(menu, "Weapon Category", player =>
{
    var weaponMenu = _api.CreateMenuWithBack("Weapons", "fun", player);
    _api.AddMenuOption(weaponMenu, "AK-47", p => p.GiveNamedItem("weapon_ak47"));
    _api.AddMenuOption(weaponMenu, "AWP", p => p.GiveNamedItem("weapon_awp"));
    return weaponMenu;
});
```

#### `OpenMenu(object menu, CCSPlayerController player)`

Opens a menu for a specific player.

**Example:**
```csharp
var menu = _api.CreateMenuWithBack("Custom Menu", "fun", player);
_api.AddMenuOption(menu, "Test", p => p.PrintToChat("Test!"));
_api.OpenMenu(menu, player);
```

## Advanced Examples

### Nested Menus with Player Selection

```csharp
private object CreateGiveWeaponMenu(CCSPlayerController admin)
{
    var menu = _api.CreateMenuWithBack("Give Weapon", "fun", admin);
    var players = _api.GetValidPlayers()
        .Where(p => p.PlayerPawn?.Value?.LifeState == (int)LifeState_t.LIFE_ALIVE && AdminManager.CanPlayerTarget(admin, p));

    foreach (var player in players)
    {
        // Add submenu for each player - automatic back button will be added
        _api.AddSubMenu(menu, player.PlayerName, p => CreateWeaponSelectionMenu(admin, player));
    }

    return menu;
}

private object CreateWeaponSelectionMenu(CCSPlayerController admin, CCSPlayerController target)
{
    var weaponMenu = _api.CreateMenuWithBack($"Weapons for {target.PlayerName}", "fun", admin);

    var weapons = new[] { "weapon_ak47", "weapon_m4a1", "weapon_awp", "weapon_deagle" };
    foreach (var weapon in weapons)
    {
        _api.AddMenuOption(weaponMenu, weapon, _ =>
        {
            if (target.IsValid)
            {
                target.GiveNamedItem(weapon);
                admin.PrintToChat($"Gave {weapon} to {target.PlayerName}");
            }
        });
    }

    return weaponMenu;
}
```

### Dynamic Menu with Value Selection

```csharp
private object CreateSetHpMenu(CCSPlayerController admin)
{
    var menu = _api.CreateMenuWithBack("Set HP", "admin", admin);
    var players = _api.GetValidPlayers().Where(p => AdminManager.CanPlayerTarget(admin, p));

    foreach (var player in players)
    {
        _api.AddSubMenu(menu, player.PlayerName, p => CreateHpValueMenu(admin, player));
    }

    return menu;
}

private object CreateHpValueMenu(CCSPlayerController admin, CCSPlayerController target)
{
    var hpMenu = _api.CreateMenuWithBack($"HP for {target.PlayerName}", "admin", admin);
    var hpValues = new[] { 1, 50, 100, 200, 500, 1000 };

    foreach (var hp in hpValues)
    {
        _api.AddMenuOption(hpMenu, $"{hp} HP", _ =>
        {
            if (target.IsValid && target.PlayerPawn?.Value != null)
            {
                target.PlayerPawn.Value.Health = hp;
                admin.PrintToChat($"Set {target.PlayerName} HP to {hp}");
            }
        });
    }

    return hpMenu;
}
```

### Permission-Based Menu Options

```csharp
private object CreateAdminToolsMenu(CCSPlayerController admin)
{
    var menu = _api.CreateMenuWithBack("Admin Tools", "admin", admin);

    // Only root admins can see this
    _api.AddMenuOption(menu, "Dangerous Action", player =>
    {
        player.PrintToChat("Executing dangerous action...");
    }, permission: "@css/root");

    // All admins can see this
    _api.AddMenuOption(menu, "Safe Action", player =>
    {
        player.PrintToChat("Executing safe action...");
    }, permission: "@css/generic");

    return menu;
}
```

## Best Practices

1. **Use your module's Localizer for translations** 🌍
   ```csharp
   // ✅ CORRECT: Module with translations uses its own Localizer
   _api.RegisterMenuCategory("mymodule", Localizer?["mymodule_category"] ?? "My Module", "@css/generic");
   _api.RegisterMenu("mymodule", "action", Localizer?["mymodule_menu_action"] ?? "My Action", CreateMenu, "@css/generic");

   // ✅ ALSO CORRECT: Module without translations uses hard-coded text
   _api.RegisterMenuCategory("mymodule", "My Module", "@css/generic");
   _api.RegisterMenu("mymodule", "action", "My Action", CreateMenu, "@css/generic");

   // ❌ WRONG: Using translation key without Localizer
   _api.RegisterMenuCategory("mymodule", "mymodule_category", "@css/generic");
   // This would display "mymodule_category" literally!
   ```

   **✅ Per-player localization now available!**
   - Both CS2-SimpleAdmin built-in menus AND module menus support per-player localization
   - Each player sees menus in their own language based on their `css_lang` setting
   - See "Advanced: Per-Player Localization for Modules" section below for implementation details

2. **Always check for API availability**
   ```csharp
   if (_api == null) return;
   ```

3. **Validate player state before actions**
   ```csharp
   if (target.IsValid && target.PlayerPawn?.Value != null)
   {
       // Safe to perform action
   }
   ```

4. **Use descriptive category and menu IDs**
   - Good: `"economy"`, `"vip_features"`, `"fun_commands"`
   - Bad: `"cat1"`, `"menu"`, `"test"`

5. **Clean up on unload**
   ```csharp
   public override void Unload(bool hotReload)
   {
       _api?.UnregisterMenu("mymodule", "mymenu");
       _api.OnSimpleAdminReady -= RegisterMenus;
   }
   ```

6. **Use appropriate permissions**
   - `@css/generic` - All admins
   - `@css/ban` - Admins who can ban
   - `@css/kick` - Admins who can kick
   - `@css/root` - Root admins only
   - Custom permissions from your module

7. **Handle hot reload**
   ```csharp
   _api.OnSimpleAdminReady += RegisterMenus;
   RegisterMenus(); // Fallback for hot reload case
   ```

## Automatic Back Button

The menu system **automatically adds a "Back" button** to all submenus created with:
- `CreateMenuWithBack()` - Returns to the category menu
- `AddSubMenu()` - Returns to the parent menu

You don't need to manually add back buttons - the system handles navigation automatically!

## Getting Valid Players

Use the API helper method to get valid, connected players:

```csharp
var players = _api.GetValidPlayers();

// With filtering
var alivePlayers = _api.GetValidPlayers()
    .Where(p => p.PlayerPawn?.Value?.LifeState == (int)LifeState_t.LIFE_ALIVE);

var targetablePlayers = _api.GetValidPlayers()
    .Where(p => AdminManager.CanPlayerTarget(admin, p));
```

## Complete Module Example

See the `CS2-SimpleAdmin_FunCommands` module in the `Modules/` directory for a complete, production-ready example of:
- Category registration
- Multiple menu types
- Nested menus with automatic back buttons
- Player selection menus
- Permission-based access
- Proper cleanup on unload

## Troubleshooting

**Q: My category doesn't appear in the admin menu**
- Ensure you're calling `RegisterMenuCategory()` after the API is ready
- Check that the player has the required permission
- Verify the category has at least one menu registered with `RegisterMenu()`

**Q: Back button doesn't work**
- Make sure you're using `CreateMenuWithBack()` instead of creating menus manually
- The `categoryId` parameter must match the category you registered
- Use `AddSubMenu()` for nested menus - it handles back navigation automatically

**Q: Menu appears but is empty**
- Check that you're adding options with `AddMenuOption()` or `AddSubMenu()`
- Verify permission checks aren't filtering out all options
- Ensure player validation in filters isn't too restrictive

**Q: API is null in OnAllPluginsLoaded**
- Wait for the `OnSimpleAdminReady` event instead of immediate registration
- Make sure CS2-SimpleAdmin is loaded before your module

## Advanced: Per-Player Localization for Modules (✅ NOW AVAILABLE!)

**🆕 NEW:** Module menus now support **per-player localization** based on `css_lang`!

Both CS2-SimpleAdmin's built-in menus AND module menus can show in each player's configured language.

### How to Use Per-Player Localization in Your Module

**1. Register Category with Localizer:**

```csharp
// Pass translation KEY (not translated text) and module's Localizer
_api.RegisterMenuCategory(
    "mymodule",
    "mymodule_category_name",  // Translation key from your lang/ folder
    "@css/generic",
    Localizer!                  // Your module's localizer
);
```

**2. Register Menus with Localizer:**

```csharp
_api.RegisterMenu(
    "mymodule",
    "action",
    "mymodule_menu_action",     // Translation key from your lang/ folder
    CreateMenu,
    "@css/generic",             // Permission
    "css_mycommand",            // Command name for override (optional)
    Localizer!                  // Your module's localizer
);
```

**How it works:**
1. Your module passes its `IStringLocalizer` and translation **key** (not translated text)
2. SimpleAdmin's `MenuManager` stores both the key and the localizer
3. When displaying menu to a player, MenuManager translates using:
   ```csharp
   using (new WithTemporaryCulture(player.GetLanguage()))
   {
       localizedName = moduleLocalizer[translationKey];
   }
   ```
4. Each player sees menus in their own language based on their `css_lang` setting!

**Complete Example:**

See `Modules/CS2-SimpleAdmin_FunCommands/` for a real implementation:

```csharp
// Register category with per-player localization
_api.RegisterMenuCategory("fun", "fun_category_name", "@css/generic", Localizer!);

// Register menu with per-player localization
_api.RegisterMenu("fun", "god", "fun_menu_god",
    CreateGodModeMenu, "@css/cheats", "css_god", Localizer!);
```

**Without Per-Player Localization (backwards compatible):**

If you don't need per-player localization, the old API still works:

```csharp
// Hard-coded text (same for all players)
_api.RegisterMenuCategory("mymodule", "My Module", "@css/generic");
_api.RegisterMenu("mymodule", "action", "Do Action", CreateMenu, "@css/generic");
```
