namespace EnhancedSpectator.Runtime;

/// <summary>A feature evaluated after all game scripts' LateUpdate, before cameras render.</summary>
public interface IRuntimeAfterLateTickable
{
    /// <summary>Finishes cameras and then publishes their completed poses, in registration order.</summary>
    void AfterLateTick();
}
