using System;
using EnhancedSpectator.Features.VoiceActivity;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace EnhancedSpectator.Networking;

/// <summary>
/// Encodes and decodes Enhanced Spectator binary custom messages.
/// </summary>
public static class ModNetworkSerializer
{
    /// <summary>
    /// Gets the byte capacity needed for one capability message.
    /// </summary>
    public static int CapabilityMessageSize =>
        FastBufferWriter.GetWriteSize<int>()
        + FastBufferWriter.GetWriteSize<ulong>()
        + FastBufferWriter.GetWriteSize<bool>()
        + FastBufferWriter.GetWriteSize<bool>()
        + FastBufferWriter.GetWriteSize<long>()
        + FastBufferWriter.GetWriteSize<bool>()
        + FastBufferWriter.GetWriteSize<bool>();

    /// <summary>
    /// Gets the byte capacity needed for one spectator target message.
    /// </summary>
    public static int SpectatorTargetMessageSize =>
        FastBufferWriter.GetWriteSize<int>()
        + FastBufferWriter.GetWriteSize<bool>()
        + FastBufferWriter.GetWriteSize<ulong>()
        + FastBufferWriter.GetWriteSize<ulong>()
        + FastBufferWriter.GetWriteSize<bool>()
        + FastBufferWriter.GetWriteSize<ulong>()
        + FastBufferWriter.GetWriteSize<bool>()
        + FastBufferWriter.GetWriteSize<ulong>()
        + FastBufferWriter.GetWriteSize<long>();

    /// <summary>
    /// Gets the byte capacity needed for one spectator pose message.
    /// </summary>
    public static int SpectatorPoseMessageSize =>
        FastBufferWriter.GetWriteSize<int>()
        + FastBufferWriter.GetWriteSize<bool>()
        + FastBufferWriter.GetWriteSize<ulong>()
        + FastBufferWriter.GetWriteSize<ulong>()
        + FastBufferWriter.GetWriteSize<bool>()
        + FastBufferWriter.GetWriteSize<ulong>()
        + FastBufferWriter.GetWriteSize<bool>()
        + FastBufferWriter.GetWriteSize<ulong>()
        + FastBufferWriter.GetWriteSize<float>() * 7
        + FastBufferWriter.GetWriteSize<long>()
        + FastBufferWriter.GetWriteSize<bool>()
        + FastBufferWriter.GetWriteSize<float>() * 7
        + FastBufferWriter.GetWriteSize<bool>()
        + FastBufferWriter.GetWriteSize<float>() * 7;

    /// <summary>
    /// Gets the byte capacity needed for one peer identity message.
    /// </summary>
    public static int PeerIdentityMessageSize =>
        FastBufferWriter.GetWriteSize<int>()
        + FastBufferWriter.GetWriteSize<ulong>()
        + FastBufferWriter.GetWriteSize<ulong>()
        + FastBufferWriter.GetWriteSize<FixedString64Bytes>()
        + FastBufferWriter.GetWriteSize<long>()
        + FastBufferWriter.GetWriteSize<FixedString64Bytes>();

    /// <summary>
    /// Gets the byte capacity needed for one voice activity message.
    /// </summary>
    public static int VoiceActivityMessageSize =>
        FastBufferWriter.GetWriteSize<int>()
        + FastBufferWriter.GetWriteSize<bool>()
        + FastBufferWriter.GetWriteSize<bool>()
        + FastBufferWriter.GetWriteSize<ulong>()
        + FastBufferWriter.GetWriteSize<ulong>()
        + FastBufferWriter.GetWriteSize<float>()
        + FastBufferWriter.GetWriteSize<float>()
        + FastBufferWriter.GetWriteSize<long>();

    /// <summary>
    /// Writes a capability message in protocol order.
    /// </summary>
    public static void WriteCapability(ref FastBufferWriter writer, ModPeerCapability capability)
    {
        writer.WriteValueSafe(capability.ProtocolVersion);
        writer.WriteValueSafe(capability.ClientId);
        writer.WriteValueSafe(capability.SupportsCapabilityHandshake);
        writer.WriteValueSafe(capability.SupportsSpectatorTargetSync);
        writer.WriteValueSafe(capability.LastSeenTicks);
        writer.WriteValueSafe(capability.SupportsVoiceActivitySync);
        writer.WriteValueSafe(capability.SupportsSpectatorVoiceToTarget);
    }

    /// <summary>
    /// Attempts to read a capability message.
    /// </summary>
    public static bool TryReadCapability(
        ref FastBufferReader reader,
        out ModPeerCapability capability,
        out string reason)
    {
        capability = null!;
        reason = string.Empty;

        try
        {
            reader.ReadValueSafe(out int protocolVersion);
            if (protocolVersion != ModNetworkConstants.ProtocolVersion)
            {
                reason = $"unsupported protocol version {protocolVersion}";
                return false;
            }

            reader.ReadValueSafe(out ulong clientId);
            reader.ReadValueSafe(out bool supportsCapabilityHandshake);
            reader.ReadValueSafe(out bool supportsSpectatorTargetSync);
            reader.ReadValueSafe(out long timestampTicks);
            bool supportsVoiceActivitySync = false;
            bool supportsSpectatorVoiceToTarget = false;
            try
            {
                reader.ReadValueSafe(out supportsVoiceActivitySync);
            }
            catch
            {
                supportsVoiceActivitySync = false;
            }

            try
            {
                reader.ReadValueSafe(out supportsSpectatorVoiceToTarget);
            }
            catch
            {
                supportsSpectatorVoiceToTarget = false;
            }

            capability = new ModPeerCapability(
                clientId,
                protocolVersion,
                supportsCapabilityHandshake,
                supportsSpectatorTargetSync,
                supportsCapabilityHandshake,
                timestampTicks,
                supportsVoiceActivitySync,
                supportsSpectatorVoiceToTarget);
            return true;
        }
        catch (Exception ex)
        {
            reason = $"capability read failed: {ex.GetType().Name}";
            return false;
        }
    }

    /// <summary>
    /// Writes a spectator target message in protocol order.
    /// </summary>
    public static void WriteSpectatorTarget(ref FastBufferWriter writer, SpectatorTargetSyncMessage message)
    {
        SpectatorTargetState state = message.State;
        bool hasTargetClientId = state.TargetClientId.HasValue;
        bool hasTargetPlayerSlotId = state.TargetPlayerSlotId.HasValue;
        ulong targetClientId = state.TargetClientId.GetValueOrDefault();
        ulong targetPlayerSlotId = state.TargetPlayerSlotId.GetValueOrDefault();

        writer.WriteValueSafe(message.ProtocolVersion);
        writer.WriteValueSafe(state.IsSpectating);
        writer.WriteValueSafe(state.LocalClientId);
        writer.WriteValueSafe(state.LocalPlayerSlotId);
        writer.WriteValueSafe(hasTargetClientId);
        writer.WriteValueSafe(targetClientId);
        writer.WriteValueSafe(hasTargetPlayerSlotId);
        writer.WriteValueSafe(targetPlayerSlotId);
        writer.WriteValueSafe(state.TimestampTicks);
    }

    /// <summary>
    /// Attempts to read a spectator target message.
    /// </summary>
    public static bool TryReadSpectatorTarget(
        ref FastBufferReader reader,
        out SpectatorTargetState state,
        out string reason)
    {
        state = null!;
        reason = string.Empty;

        try
        {
            reader.ReadValueSafe(out int protocolVersion);
            if (protocolVersion != ModNetworkConstants.ProtocolVersion)
            {
                reason = $"unsupported protocol version {protocolVersion}";
                return false;
            }

            reader.ReadValueSafe(out bool isSpectating);
            reader.ReadValueSafe(out ulong localClientId);
            reader.ReadValueSafe(out ulong localPlayerSlotId);
            reader.ReadValueSafe(out bool hasTargetClientId);
            reader.ReadValueSafe(out ulong targetClientId);
            reader.ReadValueSafe(out bool hasTargetPlayerSlotId);
            reader.ReadValueSafe(out ulong targetPlayerSlotId);
            reader.ReadValueSafe(out long timestampTicks);

            state = new SpectatorTargetState(
                isSpectating,
                localClientId,
                localPlayerSlotId,
                hasTargetClientId ? targetClientId : (ulong?)null,
                hasTargetPlayerSlotId ? targetPlayerSlotId : (ulong?)null,
                timestampTicks);
            return true;
        }
        catch (Exception ex)
        {
            reason = $"spectator target read failed: {ex.GetType().Name}";
            return false;
        }
    }

    /// <summary>
    /// Writes a spectator pose message in protocol order.
    /// </summary>
    public static void WriteSpectatorPose(ref FastBufferWriter writer, SpectatorPoseSyncMessage message)
    {
        SpectatorPoseState state = message.State;
        bool hasTargetClientId = state.TargetClientId.HasValue;
        bool hasTargetPlayerSlotId = state.TargetPlayerSlotId.HasValue;
        ulong targetClientId = state.TargetClientId.GetValueOrDefault();
        ulong targetPlayerSlotId = state.TargetPlayerSlotId.GetValueOrDefault();

        writer.WriteValueSafe(message.ProtocolVersion);
        writer.WriteValueSafe(state.IsSpectating);
        writer.WriteValueSafe(state.LocalClientId);
        writer.WriteValueSafe(state.LocalPlayerSlotId);
        writer.WriteValueSafe(hasTargetClientId);
        writer.WriteValueSafe(targetClientId);
        writer.WriteValueSafe(hasTargetPlayerSlotId);
        writer.WriteValueSafe(targetPlayerSlotId);
        writer.WriteValueSafe(state.Position.x);
        writer.WriteValueSafe(state.Position.y);
        writer.WriteValueSafe(state.Position.z);
        writer.WriteValueSafe(state.Rotation.x);
        writer.WriteValueSafe(state.Rotation.y);
        writer.WriteValueSafe(state.Rotation.z);
        writer.WriteValueSafe(state.Rotation.w);
        writer.WriteValueSafe(state.TimestampTicks);
        writer.WriteValueSafe(state.HasMotionReference);
        if (state.HasMotionReference)
        {
            writer.WriteValueSafe(state.MotionReferenceLocalPosition.x);
            writer.WriteValueSafe(state.MotionReferenceLocalPosition.y);
            writer.WriteValueSafe(state.MotionReferenceLocalPosition.z);
            writer.WriteValueSafe(state.MotionReferenceLocalRotation.x);
            writer.WriteValueSafe(state.MotionReferenceLocalRotation.y);
            writer.WriteValueSafe(state.MotionReferenceLocalRotation.z);
            writer.WriteValueSafe(state.MotionReferenceLocalRotation.w);
        }

        writer.WriteValueSafe(state.HasTargetMotionReference);
        if (state.HasTargetMotionReference)
        {
            writer.WriteValueSafe(state.TargetMotionReferenceLocalPosition.x);
            writer.WriteValueSafe(state.TargetMotionReferenceLocalPosition.y);
            writer.WriteValueSafe(state.TargetMotionReferenceLocalPosition.z);
            writer.WriteValueSafe(state.TargetMotionReferenceLocalRotation.x);
            writer.WriteValueSafe(state.TargetMotionReferenceLocalRotation.y);
            writer.WriteValueSafe(state.TargetMotionReferenceLocalRotation.z);
            writer.WriteValueSafe(state.TargetMotionReferenceLocalRotation.w);
        }
    }

    /// <summary>
    /// Attempts to read a spectator pose message.
    /// </summary>
    public static bool TryReadSpectatorPose(
        ref FastBufferReader reader,
        out SpectatorPoseState state,
        out string reason)
    {
        state = null!;
        reason = string.Empty;

        try
        {
            reader.ReadValueSafe(out int protocolVersion);
            if (protocolVersion != ModNetworkConstants.ProtocolVersion)
            {
                reason = $"unsupported protocol version {protocolVersion}";
                return false;
            }

            reader.ReadValueSafe(out bool isSpectating);
            reader.ReadValueSafe(out ulong localClientId);
            reader.ReadValueSafe(out ulong localPlayerSlotId);
            reader.ReadValueSafe(out bool hasTargetClientId);
            reader.ReadValueSafe(out ulong targetClientId);
            reader.ReadValueSafe(out bool hasTargetPlayerSlotId);
            reader.ReadValueSafe(out ulong targetPlayerSlotId);
            reader.ReadValueSafe(out float positionX);
            reader.ReadValueSafe(out float positionY);
            reader.ReadValueSafe(out float positionZ);
            reader.ReadValueSafe(out float rotationX);
            reader.ReadValueSafe(out float rotationY);
            reader.ReadValueSafe(out float rotationZ);
            reader.ReadValueSafe(out float rotationW);
            reader.ReadValueSafe(out long timestampTicks);
            bool hasMotionReference = false;
            Vector3 motionReferenceLocalPosition = Vector3.zero;
            Quaternion motionReferenceLocalRotation = Quaternion.identity;
            try
            {
                reader.ReadValueSafe(out hasMotionReference);
                if (hasMotionReference)
                {
                    reader.ReadValueSafe(out float localPositionX);
                    reader.ReadValueSafe(out float localPositionY);
                    reader.ReadValueSafe(out float localPositionZ);
                    reader.ReadValueSafe(out float localRotationX);
                    reader.ReadValueSafe(out float localRotationY);
                    reader.ReadValueSafe(out float localRotationZ);
                    reader.ReadValueSafe(out float localRotationW);
                    motionReferenceLocalPosition = new Vector3(localPositionX, localPositionY, localPositionZ);
                    motionReferenceLocalRotation = new Quaternion(localRotationX, localRotationY, localRotationZ, localRotationW);
                }
            }
            catch
            {
                hasMotionReference = false;
                motionReferenceLocalPosition = Vector3.zero;
                motionReferenceLocalRotation = Quaternion.identity;
            }

            bool hasTargetMotionReference = false;
            Vector3 targetMotionReferenceLocalPosition = Vector3.zero;
            Quaternion targetMotionReferenceLocalRotation = Quaternion.identity;
            try
            {
                reader.ReadValueSafe(out hasTargetMotionReference);
                if (hasTargetMotionReference)
                {
                    reader.ReadValueSafe(out float targetLocalPositionX);
                    reader.ReadValueSafe(out float targetLocalPositionY);
                    reader.ReadValueSafe(out float targetLocalPositionZ);
                    reader.ReadValueSafe(out float targetLocalRotationX);
                    reader.ReadValueSafe(out float targetLocalRotationY);
                    reader.ReadValueSafe(out float targetLocalRotationZ);
                    reader.ReadValueSafe(out float targetLocalRotationW);
                    targetMotionReferenceLocalPosition = new Vector3(
                        targetLocalPositionX,
                        targetLocalPositionY,
                        targetLocalPositionZ);
                    targetMotionReferenceLocalRotation = new Quaternion(
                        targetLocalRotationX,
                        targetLocalRotationY,
                        targetLocalRotationZ,
                        targetLocalRotationW);
                }
            }
            catch
            {
                hasTargetMotionReference = false;
                targetMotionReferenceLocalPosition = Vector3.zero;
                targetMotionReferenceLocalRotation = Quaternion.identity;
            }

            state = new SpectatorPoseState(
                isSpectating,
                localClientId,
                localPlayerSlotId,
                hasTargetClientId ? targetClientId : (ulong?)null,
                hasTargetPlayerSlotId ? targetPlayerSlotId : (ulong?)null,
                new Vector3(positionX, positionY, positionZ),
                new Quaternion(rotationX, rotationY, rotationZ, rotationW),
                timestampTicks,
                hasMotionReference,
                motionReferenceLocalPosition,
                motionReferenceLocalRotation,
                hasTargetMotionReference,
                targetMotionReferenceLocalPosition,
                targetMotionReferenceLocalRotation);
            return true;
        }
        catch (Exception ex)
        {
            reason = $"spectator pose read failed: {ex.GetType().Name}";
            return false;
        }
    }

    /// <summary>
    /// Writes a peer identity message in protocol order.
    /// </summary>
    public static void WritePeerIdentity(ref FastBufferWriter writer, PeerIdentityState state)
    {
        FixedString64Bytes displayName = state.DisplayName ?? string.Empty;
        FixedString64Bytes voicePlayerName = state.VoicePlayerName ?? string.Empty;
        writer.WriteValueSafe(ModNetworkConstants.ProtocolVersion);
        writer.WriteValueSafe(state.ClientId);
        writer.WriteValueSafe(state.PlayerSlotId);
        writer.WriteValueSafe(displayName);
        writer.WriteValueSafe(state.TimestampTicks);
        writer.WriteValueSafe(voicePlayerName);
    }

    /// <summary>
    /// Attempts to read a peer identity message.
    /// </summary>
    public static bool TryReadPeerIdentity(
        ref FastBufferReader reader,
        out PeerIdentityState state,
        out string reason)
    {
        state = null!;
        reason = string.Empty;

        try
        {
            reader.ReadValueSafe(out int protocolVersion);
            if (protocolVersion != ModNetworkConstants.ProtocolVersion)
            {
                reason = $"unsupported protocol version {protocolVersion}";
                return false;
            }

            reader.ReadValueSafe(out ulong clientId);
            reader.ReadValueSafe(out ulong playerSlotId);
            reader.ReadValueSafe(out FixedString64Bytes displayName);
            reader.ReadValueSafe(out long timestampTicks);
            FixedString64Bytes voicePlayerName = default;
            try
            {
                reader.ReadValueSafe(out voicePlayerName);
            }
            catch
            {
                voicePlayerName = default;
            }

            state = new PeerIdentityState(
                clientId,
                playerSlotId,
                displayName.ToString(),
                voicePlayerName.ToString(),
                timestampTicks);
            return true;
        }
        catch (Exception ex)
        {
            reason = $"peer identity read failed: {ex.GetType().Name}";
            return false;
        }
    }

    /// <summary>
    /// Writes a voice activity message in protocol order.
    /// </summary>
    public static void WriteVoiceActivity(ref FastBufferWriter writer, VoiceActivitySyncMessage message)
    {
        VoiceActivityState state = message.State;
        writer.WriteValueSafe(message.ProtocolVersion);
        writer.WriteValueSafe(state.HasData);
        writer.WriteValueSafe(state.IsSpeaking);
        writer.WriteValueSafe(state.ClientId);
        writer.WriteValueSafe(state.SlotId);
        writer.WriteValueSafe(state.Amplitude);
        writer.WriteValueSafe(state.Volume);
        writer.WriteValueSafe(state.TimestampTicks);
    }

    /// <summary>
    /// Attempts to read a voice activity message.
    /// </summary>
    public static bool TryReadVoiceActivity(
        ref FastBufferReader reader,
        out VoiceActivityState state,
        out string reason)
    {
        state = VoiceActivityState.NoData;
        reason = string.Empty;

        try
        {
            reader.ReadValueSafe(out int protocolVersion);
            if (protocolVersion != ModNetworkConstants.ProtocolVersion)
            {
                reason = $"unsupported protocol version {protocolVersion}";
                return false;
            }

            reader.ReadValueSafe(out bool hasData);
            reader.ReadValueSafe(out bool isSpeaking);
            reader.ReadValueSafe(out ulong clientId);
            reader.ReadValueSafe(out ulong slotId);
            reader.ReadValueSafe(out float amplitude);
            reader.ReadValueSafe(out float volume);
            reader.ReadValueSafe(out long timestampTicks);

            state = new VoiceActivityState(
                hasData,
                isSpeaking,
                amplitude,
                volume,
                clientId,
                slotId,
                timestampTicks);
            return true;
        }
        catch (Exception ex)
        {
            reason = $"voice activity read failed: {ex.GetType().Name}";
            return false;
        }
    }
}
