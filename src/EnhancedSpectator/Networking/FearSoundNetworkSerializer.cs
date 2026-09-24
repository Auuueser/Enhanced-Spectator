using System;
using EnhancedSpectator.Features.FearMode;
using Unity.Collections;
using Unity.Netcode;

namespace EnhancedSpectator.Networking;

/// <summary>Serializes optional fear-sound capability, request, and host event packets.</summary>
public static class FearSoundNetworkSerializer
{
    /// <summary>Gets the capability packet capacity.</summary>
    public static int CapabilityMessageSize => FastBufferWriter.GetWriteSize<int>();

    /// <summary>Gets the request/event packet capacity.</summary>
    public static int EventMessageSize =>
        FastBufferWriter.GetWriteSize<int>()
        + FastBufferWriter.GetWriteSize<ulong>()
        + FastBufferWriter.GetWriteSize<ulong>()
        + FastBufferWriter.GetWriteSize<FixedString64Bytes>()
        + FastBufferWriter.GetWriteSize<int>()
        + FastBufferWriter.GetWriteSize<int>()
        + FastBufferWriter.GetWriteSize<long>()
        + FastBufferWriter.GetWriteSize<FixedString64Bytes>();

    /// <summary>Writes a sound capability packet.</summary>
    public static void WriteCapability(ref FastBufferWriter writer)
    {
        writer.WriteValueSafe(FearModeNetworkConstants.SoundProtocolVersion);
    }

    /// <summary>Reads and validates a sound capability packet.</summary>
    public static bool TryReadCapability(ref FastBufferReader reader, out string reason)
    {
        reason = string.Empty;
        try
        {
            reader.ReadValueSafe(out int version);
            if (version != FearModeNetworkConstants.SoundProtocolVersion)
            {
                reason = $"unsupported fear sound protocol version {version}";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            reason = $"fear sound capability read failed: {ex.GetType().Name}";
            return false;
        }
    }

    /// <summary>Writes a sound request or host-authorized event.</summary>
    public static void WriteEvent(ref FastBufferWriter writer, FearSoundEventState state, string catalogFingerprint = "")
    {
        FixedString64Bytes modelKey = state.ModelKey;
        writer.WriteValueSafe(FearModeNetworkConstants.SoundProtocolVersion);
        writer.WriteValueSafe(state.ClientId);
        writer.WriteValueSafe(state.PlayerSlotId);
        writer.WriteValueSafe(modelKey);
        writer.WriteValueSafe((int)state.Action);
        writer.WriteValueSafe(state.ClipIndex);
        writer.WriteValueSafe(state.Sequence);
        if (FearItemSoundCompatibility.UsesCatalogFingerprint(state.ModelKey) && state.Action != FearSoundAction.Stop)
        {
            FixedString64Bytes fingerprint = catalogFingerprint;
            writer.WriteValueSafe(fingerprint);
        }
    }

    /// <summary>Reads and validates a sound request or event.</summary>
    public static bool TryReadEvent(
        ref FastBufferReader reader,
        out FearSoundEventState state,
        out string reason)
    {
        state = null!;
        reason = string.Empty;
        try
        {
            reader.ReadValueSafe(out int version);
            if (version != FearModeNetworkConstants.SoundProtocolVersion)
            {
                reason = $"unsupported fear sound protocol version {version}";
                return false;
            }

            reader.ReadValueSafe(out ulong clientId);
            reader.ReadValueSafe(out ulong slotId);
            reader.ReadValueSafe(out FixedString64Bytes modelKey);
            reader.ReadValueSafe(out int actionValue);
            reader.ReadValueSafe(out int clipIndex);
            reader.ReadValueSafe(out long sequence);
            string key = modelKey.ToString();
            if (!Enum.IsDefined(typeof(FearSoundAction), actionValue)
                || !FearModeRules.IsValidModelKey(key))
            {
                reason = "invalid fear sound event";
                return false;
            }

            FearSoundAction action = (FearSoundAction)actionValue;
            if ((action == FearSoundAction.Stop && clipIndex != -1)
                || (action != FearSoundAction.Stop && clipIndex < 0))
            {
                reason = "invalid fear sound action payload";
                return false;
            }

            string fingerprint = string.Empty;
            // FixedString is length-prefixed on the wire, NOT its 64-byte in-memory struct.
            // A SHA256 Base64 digest occupies 4 + 44 bytes. Requiring 64 silently skipped
            // every valid digest, so local playback worked while peers rejected item/rocket sounds.
            if (FearItemSoundCompatibility.UsesCatalogFingerprint(key) && action != FearSoundAction.Stop
                && reader.TryBeginRead(sizeof(int)))
            {
                int remaining = reader.Length - reader.Position;
                if (remaining != FearSoundFingerprintWire.WireLength)
                { reason = "invalid fear sound digest length"; return false; }
                var tail = new byte[remaining];
                reader.ReadBytesSafe(ref tail, remaining);
                if (!FearSoundFingerprintWire.TryDecode(tail, out fingerprint))
                { reason = "invalid fear sound digest"; return false; }
            }
            state = new FearSoundEventState(clientId, slotId, key, action, clipIndex, sequence, fingerprint);
            return true;
        }
        catch (Exception ex)
        {
            reason = $"fear sound event read failed: {ex.GetType().Name}";
            return false;
        }
    }
}
