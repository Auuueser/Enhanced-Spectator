using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Preview only: the game's own end-of-round sounds, so the report preview's timing can be heard against them.</summary>
internal static class LethalCompanyRoundSounds
{
    internal static void Play(RoundReportSound sound)
    {
        var hud = HUDManager.Instance;
        var clip = sound == RoundReportSound.Music ? hud.endStatsMusic[Random.Range(0, hud.endStatsMusic.Length)]
            : sound == RoundReportSound.Experience ? hud.increaseXPSFX : sound == RoundReportSound.LevelUp ? hud.levelIncreaseSFX
            : sound == RoundReportSound.ExperienceDown ? hud.decreaseXPSFX : sound == RoundReportSound.LevelDown ? hud.levelDecreaseSFX
            : hud.profitQuotaDaysLeftCalmSFX;
        hud.UIAudio.PlayOneShot(clip);
    }
}
