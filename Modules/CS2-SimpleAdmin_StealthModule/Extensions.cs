using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace CS2_SimpleAdmin_StealthModule;

public static class Extensions
{
    public static CCSPlayerController? GetSpectatingPlayer(this CCSPlayerController player)
    {
        if (player.Pawn.Value is not { IsValid: true } pawn)
            return null;

        if (player.ControllingBot)
            return null;

        if (pawn.ObserverServices is not { } observerServices)
            return null;

        if (observerServices.ObserverTarget?.Value?.As<CCSPlayerPawn>() is not { IsValid: true } observerPawn)
            return null;

        return observerPawn.OriginalController.Value is not { IsValid: true } observerController ? null : observerController;
    }

    public static List<CCSPlayerController> GetSpectators(this CCSPlayerController player)
    {
        // Resolve each tracked slot fresh — the module stores slots, not controller
        // wrappers, precisely so nothing here can touch a freed entity.
        var spectators = new List<CCSPlayerController>();

        foreach (var slot in CS2_SimpleAdmin_StealthModule.PlayerSlots)
        {
            var candidate = Utilities.GetPlayerFromSlot(slot);
            if (candidate is not { IsValid: true, IsBot: false }) continue;

            if (candidate.GetSpectatingPlayer()?.Slot == player.Slot)
                spectators.Add(candidate);
        }

        return spectators;
    }
}
