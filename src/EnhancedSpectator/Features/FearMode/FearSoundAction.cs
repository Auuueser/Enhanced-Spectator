namespace EnhancedSpectator.Features.FearMode;

/// <summary>Identifies the host-authorized fear-sound operation.</summary>
public enum FearSoundAction
{
    /// <summary>Starts the selected clip using the normal cooldown.</summary>
    Play = 0,

    /// <summary>Stops the origin player's current clip.</summary>
    Stop = 1,

    /// <summary>Replaces the current clip with the next selected clip.</summary>
    PlayNext = 2,
}
