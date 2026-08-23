namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Describes one host-authorized monster sound event.
/// </summary>
public sealed class FearSoundEventState
{
    /// <summary>Creates a sound event.</summary>
    public FearSoundEventState(
        ulong clientId,
        ulong playerSlotId,
        string modelKey,
        FearSoundAction action,
        int clipIndex,
        long sequence)
    {
        ClientId = clientId;
        PlayerSlotId = playerSlotId;
        ModelKey = modelKey ?? string.Empty;
        Action = action;
        ClipIndex = clipIndex;
        Sequence = sequence;
    }

    /// <summary>Gets the originating Netcode client id.</summary>
    public ulong ClientId { get; }

    /// <summary>Gets the originating player slot id.</summary>
    public ulong PlayerSlotId { get; }

    /// <summary>Gets the host-validated selected model key.</summary>
    public string ModelKey { get; }

    /// <summary>Gets whether this event plays, advances, or stops audio.</summary>
    public FearSoundAction Action { get; }

    /// <summary>Gets the index into the model's bounded original clip catalog.</summary>
    public int ClipIndex { get; }

    /// <summary>Gets the host-authoritative event sequence.</summary>
    public long Sequence { get; }
}
