using System;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Living viewers hide a spectator model while its owner's camera recentres after mouse inactivity, so idle
/// spectators never block the view. When that hidden spectator speaks, the model shows as a translucent ghost
/// instead, so a voice is never heard from nowhere. It stays a moment after speech ends and fades out; once the
/// owner moves the camera again the model returns to full opacity (the proximity fade circle still applies).
/// </summary>
internal sealed class SpectatorCenteringReveal
{
    internal const float SpeakingOpacity = .35f, HoldSeconds = 1.5f, RampSeconds = .25f;
    private float _hold;

    internal SpectatorCenteringReveal(bool hidden) { Opacity = hidden ? 0 : 1; }

    /// <summary>Ceiling on the model's opacity; 0 hides it.</summary>
    internal float Opacity { get; private set; }

    internal float Update(bool hidden, bool revealSpeaking, bool speaking, float deltaTime)
    {
        _hold = revealSpeaking && speaking ? HoldSeconds : Math.Max(0, _hold - deltaTime);
        float target = !hidden ? 1 : _hold > 0 ? SpeakingOpacity : 0;
        float step = deltaTime / RampSeconds;
        Opacity = Opacity < target ? Math.Min(target, Opacity + step) : Math.Max(target, Opacity - step);
        return Opacity;
    }
}
