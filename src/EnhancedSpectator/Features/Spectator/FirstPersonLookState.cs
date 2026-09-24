using System;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>Separates received eye pitch from the synthetic neutral rotation sent by melee animations.</summary>
internal sealed class FirstPersonLookState
{
    internal float Pitch { get; private set; }
    internal float Target { get; private set; }
    private bool _ignoreNextNeutral;
    private float _ignoreUntil;
    internal void Seed(float pitch, float target) { Pitch = Signed(pitch); Target = Signed(target); _ignoreNextNeutral = false; }
    internal void BeginTool(float now, float duration)
    { _ignoreNextNeutral = true; _ignoreUntil = now + Math.Max(.1f, Math.Min(1f, duration)) + .15f; }
    internal void Receive(float pitch, float now)
    {
        bool synthetic = _ignoreNextNeutral && now <= _ignoreUntil && Math.Abs(pitch) < .001f;
        _ignoreNextNeutral = false;
        if (!synthetic) Target = Signed(pitch);
    }
    internal float Advance(float deltaTime)
    {
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) deltaTime = 0;
        Pitch += Signed(Target - Pitch) * (1f - (float)Math.Exp(-14f * Math.Max(0, Math.Min(.1f, deltaTime))));
        return Pitch;
    }
    private static float Signed(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : (value % 360 + 540) % 360 - 180;
}
