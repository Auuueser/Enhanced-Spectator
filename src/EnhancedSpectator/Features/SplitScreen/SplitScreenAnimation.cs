using System;

namespace EnhancedSpectator.Features.SplitScreen;

/// <summary>Frame-rate independent critical damping, retaining momentum when layout targets change.</summary>
internal sealed class SplitScreenTileMotion
{
    internal const float SmoothTime = .15f;
    // Within a hundredth of a pixel and slower than half a pixel per second, the rect snaps and stays still.
    private const float RestDistance = .01f, RestSpeed = .5f;
    internal SplitScreenTileRect Current { get; private set; }
    internal SplitScreenTileRect Target { get; private set; }
    internal bool Settled { get; private set; } = true;
    private float _velocityX, _velocityY, _velocityWidth;
    internal SplitScreenTileMotion(SplitScreenTileRect initial)
    { Current = Target = initial; }
    internal void Retarget(SplitScreenTileRect target) { Target = target; Settled = false; }
    /// <summary>Returns whether the current rect changed.</summary>
    internal bool Tick(float deltaTime)
    {
        if (deltaTime <= 0 || Settled) return false;
        Current = new SplitScreenTileRect(Damp(Current.CenterX, Target.CenterX, ref _velocityX, deltaTime),
            Damp(Current.CenterY, Target.CenterY, ref _velocityY, deltaTime),
            Damp(Current.Width, Target.Width, ref _velocityWidth, deltaTime));
        if (Resting(Current.CenterX - Target.CenterX, _velocityX) && Resting(Current.CenterY - Target.CenterY, _velocityY)
            && Resting(Current.Width - Target.Width, _velocityWidth))
        { Current = Target; _velocityX = _velocityY = _velocityWidth = 0; Settled = true; }
        return true;
    }
    private static bool Resting(float distance, float velocity) => Math.Abs(distance) < RestDistance && Math.Abs(velocity) < RestSpeed;
    private static float Damp(float current, float target, ref float velocity, float dt)
    {
        const float omega = 2f / SmoothTime;
        float displacement = current - target;
        float decay = (float)Math.Exp(-omega * dt);
        float advance = (velocity + omega * displacement) * dt;
        velocity = (velocity - omega * advance) * decay;
        return target + (displacement + advance) * decay;
    }
}
