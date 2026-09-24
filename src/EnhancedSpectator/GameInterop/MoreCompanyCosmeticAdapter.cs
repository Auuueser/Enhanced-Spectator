using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GameNetcodeStuff;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

// Keep optional assembly types out of the caller's signatures and JIT body.
internal static class MoreCompanyCosmeticAdapter
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void CopyRenderers(PlayerControllerB target, List<Renderer> destination)
    {
        foreach (var application in target.GetComponentsInChildren<MoreCompany.Cosmetics.CosmeticApplication>(true))
        {
            foreach (var cosmetic in application.spawnedCosmetics)
            {
                if (cosmetic == null) continue;
                foreach (var renderer in cosmetic.GetComponentsInChildren<Renderer>(true))
                    if (renderer != null && !destination.Contains(renderer)) destination.Add(renderer);
            }
        }
    }
}
