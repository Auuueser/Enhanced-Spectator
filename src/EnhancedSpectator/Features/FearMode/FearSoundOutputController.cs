using EnhancedSpectator.GameInterop;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>Restores native output in a defined order; independent of a source's previous mute cache.</summary>
internal static class FearSoundOutputController
{
    internal static void Apply(IGameFearAudioOutput output, float volume, bool own, bool receive, int closer, int maximum)
    {
        if (output.Muted) output.Muted = false;
        output.Volume = FearSoundAudibilityRules.OutputVolume(volume, own, receive, closer, maximum);
    }
}
