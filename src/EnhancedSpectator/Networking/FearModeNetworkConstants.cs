namespace EnhancedSpectator.Networking;

/// <summary>
/// Constants for the optional fear-mode compatibility protocol.
/// </summary>
public static class FearModeNetworkConstants
{
    /// <summary>Current fear-mode protocol version.</summary>
    public const int ProtocolVersion = 1;

    /// <summary>Client-to-host capability probe.</summary>
    public const string CapabilityMessageName = "EnhancedSpectator.FearCapability.V1";

    /// <summary>Host-authoritative session gate.</summary>
    public const string SessionMessageName = "EnhancedSpectator.FearSession.V1";

    /// <summary>Player-owned, host-validated model selection.</summary>
    public const string SelectionMessageName = "EnhancedSpectator.FearSelection.V1";

    /// <summary>Current isolated fear-sound protocol version.</summary>
    public const int SoundProtocolVersion = 2;

    /// <summary>Client-to-host fear-sound support probe.</summary>
    public const string SoundCapabilityMessageName = "EnhancedSpectator.FearSoundCapability.V2";

    /// <summary>Player-to-host requested sound event.</summary>
    public const string SoundRequestMessageName = "EnhancedSpectator.FearSoundRequest.V2";

    /// <summary>Host-authorized sound event sent to sound-capable peers.</summary>
    public const string SoundEventMessageName = "EnhancedSpectator.FearSoundEvent.V2";
}
