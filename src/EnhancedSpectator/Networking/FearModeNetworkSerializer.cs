using System;
using EnhancedSpectator.Features.FearMode;
using Unity.Collections;
using Unity.Netcode;

namespace EnhancedSpectator.Networking;

/// <summary>
/// Serializes the isolated optional fear-mode protocol.
/// </summary>
public static class FearModeNetworkSerializer
{
    /// <summary>Gets the capability packet capacity.</summary>
    public static int CapabilityMessageSize => FastBufferWriter.GetWriteSize<int>();

    /// <summary>Gets the session packet capacity.</summary>
    public static int SessionMessageSize =>
        FastBufferWriter.GetWriteSize<int>() + FastBufferWriter.GetWriteSize<bool>();

    /// <summary>Gets the selection packet capacity.</summary>
    public static int SelectionMessageSize =>
        FastBufferWriter.GetWriteSize<int>()
        + FastBufferWriter.GetWriteSize<ulong>()
        + FastBufferWriter.GetWriteSize<ulong>()
        + FastBufferWriter.GetWriteSize<FixedString64Bytes>()
        + FastBufferWriter.GetWriteSize<long>();

    /// <summary>Writes a capability packet.</summary>
    public static void WriteCapability(ref FastBufferWriter writer)
    {
        writer.WriteValueSafe(FearModeNetworkConstants.ProtocolVersion);
    }

    /// <summary>Reads and validates a capability packet.</summary>
    public static bool TryReadCapability(ref FastBufferReader reader, out string reason)
    {
        reason = string.Empty;
        try
        {
            reader.ReadValueSafe(out int protocolVersion);
            if (protocolVersion != FearModeNetworkConstants.ProtocolVersion)
            {
                reason = $"unsupported fear protocol version {protocolVersion}";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            reason = $"fear capability read failed: {ex.GetType().Name}";
            return false;
        }
    }

    /// <summary>Writes the host session gate.</summary>
    public static void WriteSession(ref FastBufferWriter writer, bool enabled)
    {
        writer.WriteValueSafe(FearModeNetworkConstants.ProtocolVersion);
        writer.WriteValueSafe(enabled);
    }

    /// <summary>Reads the host session gate.</summary>
    public static bool TryReadSession(ref FastBufferReader reader, out bool enabled, out string reason)
    {
        enabled = false;
        reason = string.Empty;
        try
        {
            reader.ReadValueSafe(out int protocolVersion);
            if (protocolVersion != FearModeNetworkConstants.ProtocolVersion)
            {
                reason = $"unsupported fear protocol version {protocolVersion}";
                return false;
            }

            reader.ReadValueSafe(out enabled);
            return true;
        }
        catch (Exception ex)
        {
            reason = $"fear session read failed: {ex.GetType().Name}";
            return false;
        }
    }

    /// <summary>Writes one player-owned selection.</summary>
    public static void WriteSelection(ref FastBufferWriter writer, FearModeSelectionState state)
    {
        FixedString64Bytes modelKey = state.ModelKey;
        writer.WriteValueSafe(FearModeNetworkConstants.ProtocolVersion);
        writer.WriteValueSafe(state.ClientId);
        writer.WriteValueSafe(state.PlayerSlotId);
        writer.WriteValueSafe(modelKey);
        writer.WriteValueSafe(state.Revision);
    }

    /// <summary>Reads one player-owned selection.</summary>
    public static bool TryReadSelection(
        ref FastBufferReader reader,
        out FearModeSelectionState state,
        out string reason)
    {
        state = null!;
        reason = string.Empty;
        try
        {
            reader.ReadValueSafe(out int protocolVersion);
            if (protocolVersion != FearModeNetworkConstants.ProtocolVersion)
            {
                reason = $"unsupported fear protocol version {protocolVersion}";
                return false;
            }

            reader.ReadValueSafe(out ulong clientId);
            reader.ReadValueSafe(out ulong slotId);
            reader.ReadValueSafe(out FixedString64Bytes modelKey);
            reader.ReadValueSafe(out long revision);
            string key = modelKey.ToString();
            if (!FearModeRules.IsValidModelKey(key))
            {
                reason = "invalid fear model key";
                return false;
            }

            state = new FearModeSelectionState(clientId, slotId, key, revision);
            return true;
        }
        catch (Exception ex)
        {
            reason = $"fear selection read failed: {ex.GetType().Name}";
            return false;
        }
    }
}
