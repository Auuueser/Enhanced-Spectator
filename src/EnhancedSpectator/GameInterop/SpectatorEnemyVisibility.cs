using System;
using System.Collections.Generic;
using EnhancedSpectator.Config;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

// The game hides every enemy outside the ship (moves it to a layer no camera draws) while the hangar doors are closed
// and the local player is in the ship. A dead local player stays "in the ship" where it died, so spectating from there
// showed no enemy outside at all, thermal included. While the local player is dead, enemies show as with the doors
// open (the game's own other branch); revived, the game's rule applies again.
internal static class SpectatorEnemyVisibility
{
    private static EnhancedSpectatorConfig? _config;
    internal static void Configure(EnhancedSpectatorConfig config)
    {
        if(_config!=null) _config.EnableEnhancedSpectator.SettingChanged-=EnabledChanged;
        _config=config;
        _config.EnableEnhancedSpectator.SettingChanged+=EnabledChanged;
    }
    private static void EnabledChanged(object? sender,EventArgs args) => LocalLifeChanged();
    internal static void Shutdown()
    {
        if(_config!=null) _config.EnableEnhancedSpectator.SettingChanged-=EnabledChanged;
        _config=null;
        LocalLifeChanged();
        Unlisted.Clear();
    }
    // Enemies on no game enemy list: the masked's Start skips EnemyAI's, which lists each enemy on clients.
    internal static readonly List<EnemyAI> Unlisted=new List<EnemyAI>();
    internal static readonly System.Predicate<EnemyAI> Gone=enemy => enemy==null;

    private static bool LocalDead => _config?.EnableEnhancedSpectator.Value==true
        && StartOfRound.Instance?.localPlayerController?.isPlayerDead==true;

    // Replaces StartOfRound.SetPlayerSafeInShip while the local player is dead; false lets the game's run.
    internal static bool ShowAllWhileDead()
    {
        if(!LocalDead) return false;
        foreach(var enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>()) enemy.EnableEnemyMesh(true);
        return true;
    }

    // Applies the rule again when the local player dies or is revived.
    internal static void LocalLifeChanged() => StartOfRound.Instance?.SetPlayerSafeInShip();

    // An outside enemy starting while the local player is dead: Start hid it by its own copy of the ship rule.
    internal static void Started(EnemyAI enemy)
    {
        if(enemy.isOutside && !enemy.isEnemyDead && LocalDead) enemy.EnableEnemyMesh(true);
    }

    internal static void MaskedStarted(MaskedPlayerEnemy masked)
    {
        Started(masked);
        Unlisted.RemoveAll(Gone);
        if(RoundManager.Instance!=null && !RoundManager.Instance.SpawnedEnemies.Contains(masked) && !Unlisted.Contains(masked)) Unlisted.Add(masked);
    }
}
