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
        + FastBufferWriter.GetWriteSize<long>();

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
    public static void WriteEvent(ref FastBufferWriter writer, FearSoundEventState state)
    {
        FixedString64Bytes modelKey = state.ModelKey;
        writer.WriteValueSafe(FearModeNetworkConstants.SoundProtocolVersion);
        writer.WriteValueSafe(state.ClientId);
        writer.WriteValueSafe(state.PlayerSlotId);
        writer.WriteValueSafe(modelKey);
        writer.WriteValueSafe((int)state.Action);
        writer.WriteValueSafe(state.ClipIndex);
        writer.WriteValueSafe(state.Sequence);
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

            state = new FearSoundEventState(clientId, slotId, key, action, clipIndex, sequence);
            return true;
        }
        catch (Exception ex)
        {
            reason = $"fear sound event read failed: {ex.GetType().Name}";
            return false;
        }
    }
}
