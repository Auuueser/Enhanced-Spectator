namespace EnhancedSpectator.Networking;

/// <summary>
/// A spectator's split-screen state as others see it. Enlarging a view means choosing to watch that player, so only
/// then does the spectator stand by them (model and voice); with every view tiled they sit in the audience.
/// </summary>
public enum SpectatorSplitView : byte
{
    /// <summary>Not split-screen (single view, or an older version that does not say).</summary>
    None,

    /// <summary>Split-screen with every view tiled: in the audience, model hidden, heard only by the audience.</summary>
    Audience,

    /// <summary>Split-screen with a view enlarged: watching that player.</summary>
    Watching,
}
