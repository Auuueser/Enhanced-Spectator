using System;
using System.Collections.Generic;
using EnhancedSpectator.Features;
using EnhancedSpectator.Features.FloatingHead;
using EnhancedSpectator.Features.PlayerStateSync;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Features.SpectatorPresence;
using EnhancedSpectator.Features.VoiceActivity;
using EnhancedSpectator.Features.VoiceDiagnostics;
using EnhancedSpectator.Features.VoiceRouting;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Config;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Networking;
using EnhancedSpectator.Runtime;
using UnityEngine;
using InputKey = UnityEngine.InputSystem.Key;

namespace EnhancedSpectator.Tests;

internal static class Program
{
    private static int Main()
    {
        try
        {
            HostRelayRecipientsExcludeOriginLocalAndRelayedPeers();
            VoiceActivityRecipientsRequireVoiceCapability();
            RelayedPeersAreNotDirectSendRecipientsByDefault();
            ClientAcceptsOnlyServerRelayedForeignOrigins();
            ClientRejectsServerRelayWhenOriginWasNotMarkedRelayed();
            ClientRejectsDirectSenderForRelayedOrigin();
            HostRejectsForeignOriginFromDifferentSender();
            NetworkSamplingSkipsBeforeNextSampleTime();
            NetworkSamplingRunsAtNextSampleTime();
            NetworkSamplingRunsFirstSampleEvenWhenWindowFuture();
            NetworkSamplingNextTimeUsesMinimumInterval();
            NetworkVoiceActivityIntervalUsesProtocolMinimum();
            UnchangedActiveStateRefreshesAfterWindow();
            CapabilityProbeTimeoutUsesNoCompatiblePeerLocalOnlyState();
            CapabilityProbeRetriesAfterNoCompatibleWindow();
            CapabilityProbeDoesNotRetryWhenTargetSyncReady();
            CompatiblePeerKeepsTransportRegisteredState();
            TargetClientIdTakesPriorityOverSlotFallback();
            TargetSlotFallbackWorksOnlyWhenClientIdMissing();
            GlobalRemoteSpectatorVisibilityAllowsNonLocalTargets();
            RemoteSpectatorVisibilityRequiresMatchingPose();
            RemoteTargetRegistryRetainsInactiveStateForRepair();
            RemoteTargetRegistryRevisionChangesOnlyForRepairRelevantChanges();
            RemoteTargetRegistryRevisionChangesOnRemoveAndClear();
            ConnectedAliveVanillaSlotRepairRunsWithoutModIdentity();
            DeadVanillaSlotRepairDoesNotRestoreControl();
            PlayerStateRepairScheduleRunsFirstEligibleTick();
            PlayerStateRepairScheduleSkipsCleanIdleBeforeFallback();
            PlayerStateRepairScheduleRunsWhenRemoteStateRevisionChanges();
            PlayerStateRepairScheduleRunsIdleFallbackWithoutRevisionChange();
            PlayerStateRepairScheduleRepeatsAfterSuccessfulRepair();
            VanillaFallbackNameOnlyReplacesGenericLabels();
            GenericPlayerNumberDisplayNamesAreRejected();
            GenericPlayerNumberDisplayNamesIgnoreNonSpaceWhitespace();
            RealDisplayNamesAreAcceptedAndTrimmed();
            RemotePeerIdentityRegistryStoresAndRemovesNames();
            RemotePeerIdentityRegistryKeepsVoiceNameWhenNewerLegacyIdentityArrives();
            RemotePeerIdentityRegistryIgnoresOlderIdentity();
            RemotePeerIdentityRegistryCopiesIntoCallerOwnedList();
            RemotePeerIdentityRegistryCopyClearsDestination();
            RemoteIdentityRegistryRevisionIgnoresOlderIdentity();
            RemoteIdentityRegistryRevisionChangesOnlyForStoredIdentityChanges();
            RemoteIdentityRegistryRevisionChangesOnRemoveAndClear();
            DetachedHeadVisualSourceRequiresConfigAndSource();
            DetachedHeadVisualSourceFallsBackOnlyWhenAllowed();
            RuntimeDetachedHeadUsesRemotePoseRotation();
            FloatingHeadFrameUpdateRequiresFullUpdateWithoutCache();
            FloatingHeadFrameUpdateUsesCameraOnlyAfterSameFrameFullUpdate();
            FloatingHeadFrameUpdateRequiresFullUpdateOnNewFrame();
            SpeakingWithZeroAmplitudeUsesFallbackPulseLevel();
            SpeakingWithLowPositiveAmplitudeUsesAmplitudeInsteadOfFallback();
            PositiveAmplitudeWithoutSpeakingDoesNotDriveVoiceLevel();
            SilentVoiceStateUsesNoPulseLevel();
            VoiceActivitySyncDisablesLocalVisualFallback();
            VoiceLevelRiseUsesAttackSmoothing();
            VoiceLevelFallUsesReleaseSmoothing();
            RemoteVoiceActivityRegistryStoresAndClearsStates();
            RemoteVoiceActivityRegistryIgnoresOlderTimestamps();
            RemoteVoiceActivityFreshnessUsesReceiveTimeNotSenderClock();
            VoiceActivitySyncRulesDetectAmplitudeChanges();
            VoiceActivitySyncRulesExpiresStaleState();
            VoiceActivityDebugLimiterSuppressesNoisyRepeats();
            VoiceDiagnosticsReportIncludesLocalAndPlayerVoiceState();
            VoiceDiagnosticsReportOmitsAudioAndWalkieWhenDisabled();
            VoiceDiagnosticsReportIncludesTimestampAndEmptyPlayerNotice();
            SpectatorVoiceRoutingRequiresEnabledLivingLocalWatchedTarget();
            SpectatorVoiceRoutingAudienceModesSelectExpectedListeners();
            SpectatorVoiceRoutingRequiresRemoteCapabilityOptIn();
            SpectatorVoicePlayerLookupCachePrefersClientIdOverSlotFallback();
            SpectatorVoicePlayerLookupCacheKeepsFirstClientMatch();
            SpectatorVoicePlayerLookupCacheKeepsFirstSlotFallbackMatch();
            SpectatorVoicePlayerLookupCacheClearRemovesStaleEntries();
            ModLogDebugEnabledReflectsConfiguredGate();
            SpectatorInputKeyCacheReusesResolvedBinding();
            SpectatorInputKeyCacheRefreshesWhenConfiguredKeyChanges();
            SpectatorInputKeyCacheCachesUnmappedBindings();
            SpectatorVoiceDistanceAttenuationScalesVolumeByDistance();
            SpectatorVoiceSpatializationRemapsPoseIntoActualListenerFrame();
            SpectatorVoiceSpatializationPreservesWorldPoseWhenFramesMatch();
            SpectatorVoiceRouteDiagnosticsAreRateLimited();
            FreecamTargetSwitchInvalidTargetSoftPausesDuringGrace();
            FreecamTargetSwitchInvalidTargetClearsAfterGrace();
            FreecamInactiveSpectateCameraSoftPausesDuringGrace();
            FreecamInactiveSpectateCameraPreservesPoseAfterGrace();
            FreecamLifecycleUnsafeSoftPausesWhenPoseExists();
            DisconnectAutoSwitchRunsOnlyForInvalidCurrentTarget();
            DisconnectAutoSwitchIgnoresInvalidNonDisconnectedTarget();
            DisconnectAutoSwitchRequiresReplacementTarget();
            DisconnectAutoSwitchTreatsUncontrolledAliveTargetAsDisconnectLike();
            InternalTargetSwitchBypassesVanillaInputSuppression();
            ExternalVerticalInputSuppressesVanillaTargetSwitch();
            DisconnectSwitchResultRequiresChangedValidTarget();
            SpectatorPoseSyncUsesVanillaPoseWhenFreecamIsInactive();
            SpectatorPoseSyncDoesNotPublishWithoutFreecamOrActiveVanillaCamera();
            RemotePoseRegistryKeepsVisiblePoseThroughFreecamVanillaFreecamCycle();
            GameSpectatorSnapshotUsesStructStorageToAvoidHotPathAllocation();
            SpectatorSnapshotCacheReusesSuccessfulSnapshotWithinFrame();
            SpectatorSnapshotCacheCachesFailureWithinFrame();
            SpectatorSnapshotCacheRefreshesOnNewFrame();
            SpectatorSnapshotCacheClearForcesResampleWithinFrame();
            SpectatorSnapshotCacheDoesNotCommitFrameWhenAdapterThrows();
            SpectatorAnchorTargetIdentityMatchesStablePlayerIds();
            SpectatorAnchorTargetIdentityDetectsActualClientSwitch();
            SpectatorAnchorTargetIdentityFallsBackToAnchorInstance();
            SpectatorTargetAnchorSelectionPrefersStableRoot();
            SpectatorTargetAnchorSelectionFallsBackToAnimatedBones();
            NameTagTextCacheInvalidatesWhenIdentityRevisionChanges();
            NameTagTextCacheInvalidatesWhenDisplayConfigChanges();
            FeatureRuntimeDispatchListsKeepsPhaseOrder();
            FeatureRuntimeDispatchListsAddsFeatureToEveryRegisteredPhase();
            FeatureRuntimeDispatchListsDispatchesCallbacksInPhaseOrder();
            Console.WriteLine("All EnhancedSpectator tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void HostRelayRecipientsExcludeOriginLocalAndRelayedPeers()
    {
        RemotePeerRegistry registry = new RemotePeerRegistry();
        registry.RegisterLocal(Capability(0));
        registry.RegisterRemote(Capability(1), isRelayed: false);
        registry.RegisterRemote(Capability(2), isRelayed: false);
        registry.RegisterRemote(Capability(3), isRelayed: true);

        IReadOnlyList<ulong> recipients = HostRelayPlanner.GetRelayRecipients(registry, 0, 1);

        AssertSequence(new ulong[] { 2 }, recipients, "host relay recipients should include direct compatible peers except origin and local host");
    }

    private static void VoiceActivityRecipientsRequireVoiceCapability()
    {
        RemotePeerRegistry registry = new RemotePeerRegistry();
        registry.RegisterLocal(Capability(0));
        registry.RegisterRemote(Capability(1, supportsVoiceActivitySync: true), isRelayed: false);
        registry.RegisterRemote(Capability(2, supportsVoiceActivitySync: false), isRelayed: false);

        IReadOnlyList<ulong> recipients = registry.GetVoiceActivitySyncPeerIds(0);

        AssertSequence(new ulong[] { 1 }, recipients, "voice activity recipients should exclude peers without voice activity capability");
    }

    private static void RelayedPeersAreNotDirectSendRecipientsByDefault()
    {
        RemotePeerRegistry registry = new RemotePeerRegistry();
        registry.RegisterLocal(Capability(2));
        registry.RegisterRemote(Capability(0), isRelayed: false);
        registry.RegisterRemote(Capability(1), isRelayed: true);

        IReadOnlyList<ulong> directRecipients = registry.GetSpectatorTargetSyncPeerIds(2);
        IReadOnlyList<ulong> allRecipients = registry.GetSpectatorTargetSyncPeerIds(2, includeRelayed: true);

        AssertSequence(new ulong[] { 0 }, directRecipients, "direct recipients should exclude host-relayed peers");
        AssertSequence(new ulong[] { 0, 1 }, allRecipients, "explicit all recipients should include relayed peers");
    }

    private static void ClientAcceptsOnlyServerRelayedForeignOrigins()
    {
        RemotePeerRegistry registry = new RemotePeerRegistry();
        registry.RegisterLocal(Capability(2));
        registry.RegisterRemote(Capability(0), isRelayed: false);
        registry.RegisterRemote(Capability(1), isRelayed: true);

        bool acceptsHostRelay = HostRelayPlanner.CanAcceptSpectatorState(
            isHost: false,
            localClientId: 2,
            serverClientId: 0,
            senderClientId: 0,
            originClientId: 1,
            registry,
            out string hostRelayReason);

        bool acceptsClientSpoof = HostRelayPlanner.CanAcceptSpectatorState(
            isHost: false,
            localClientId: 2,
            serverClientId: 0,
            senderClientId: 3,
            originClientId: 1,
            registry,
            out string clientSpoofReason);

        AssertTrue(acceptsHostRelay, $"client should accept host-relayed foreign origin: {hostRelayReason}");
        AssertFalse(acceptsClientSpoof, $"client should reject non-host relayed foreign origin: {clientSpoofReason}");
    }

    private static void ClientRejectsServerRelayWhenOriginWasNotMarkedRelayed()
    {
        RemotePeerRegistry registry = new RemotePeerRegistry();
        registry.RegisterLocal(Capability(2));
        registry.RegisterRemote(Capability(0), isRelayed: false);
        registry.RegisterRemote(Capability(1), isRelayed: false);

        bool accepts = HostRelayPlanner.CanAcceptSpectatorState(
            isHost: false,
            localClientId: 2,
            serverClientId: 0,
            senderClientId: 0,
            originClientId: 1,
            registry,
            out string reason);

        AssertFalse(accepts, $"client should reject server relay for a direct peer: {reason}");
    }

    private static void ClientRejectsDirectSenderForRelayedOrigin()
    {
        RemotePeerRegistry registry = new RemotePeerRegistry();
        registry.RegisterLocal(Capability(2));
        registry.RegisterRemote(Capability(0), isRelayed: false);
        registry.RegisterRemote(Capability(1), isRelayed: true);

        bool accepts = HostRelayPlanner.CanAcceptSpectatorState(
            isHost: false,
            localClientId: 2,
            serverClientId: 0,
            senderClientId: 1,
            originClientId: 1,
            registry,
            out string reason);

        AssertFalse(accepts, $"client should reject direct state from a host-relayed origin: {reason}");
    }

    private static void HostRejectsForeignOriginFromDifferentSender()
    {
        RemotePeerRegistry registry = new RemotePeerRegistry();
        registry.RegisterLocal(Capability(0));
        registry.RegisterRemote(Capability(1), isRelayed: false);
        registry.RegisterRemote(Capability(2), isRelayed: false);

        bool accepts = HostRelayPlanner.CanAcceptSpectatorState(
            isHost: true,
            localClientId: 0,
            serverClientId: 0,
            senderClientId: 2,
            originClientId: 1,
            registry,
            out string reason);

        AssertFalse(accepts, $"host should reject client state where sender does not match origin: {reason}");
    }

    private static void NetworkSamplingSkipsBeforeNextSampleTime()
    {
        AssertFalse(
            NetworkSyncSamplingRules.ShouldSample(hasObservedState: true, now: 1.19f, nextSampleTime: 1.20f),
            "network sampling should skip provider reads before the next sample window");
    }

    private static void NetworkSamplingRunsAtNextSampleTime()
    {
        AssertTrue(
            NetworkSyncSamplingRules.ShouldSample(hasObservedState: true, now: 1.20f, nextSampleTime: 1.20f),
            "network sampling should run when the sample window opens");
    }

    private static void NetworkSamplingRunsFirstSampleEvenWhenWindowFuture()
    {
        AssertTrue(
            NetworkSyncSamplingRules.ShouldSample(hasObservedState: false, now: 0.10f, nextSampleTime: 1.20f),
            "network sampling should run first provider read even when a future sample time exists");
    }

    private static void NetworkSamplingNextTimeUsesMinimumInterval()
    {
        float nextSampleTime = NetworkSyncSamplingRules.ResolveNextSampleTime(now: 2.0f, intervalSeconds: -1.0f);

        AssertNear(2.0f, nextSampleTime, "negative sampling interval should clamp to current time");
    }

    private static void NetworkVoiceActivityIntervalUsesProtocolMinimum()
    {
        float interval = NetworkSyncSamplingRules.ResolveVoiceActivitySyncInterval(0.03f);

        AssertNear(
            (float)ModNetworkConstants.VoiceActivitySyncMinIntervalSeconds,
            interval,
            "voice activity sync interval should use the protocol minimum");
    }

    private static void UnchangedActiveStateRefreshesAfterWindow()
    {
        AssertFalse(
            NetworkSyncSamplingRules.ShouldRefreshUnchangedState(
                stateIsActive: true,
                hasSentState: true,
                now: 1.49f,
                nextRefreshTime: 1.50f),
            "unchanged active state should not refresh before the recovery window");
        AssertTrue(
            NetworkSyncSamplingRules.ShouldRefreshUnchangedState(
                stateIsActive: true,
                hasSentState: true,
                now: 1.50f,
                nextRefreshTime: 1.50f),
            "unchanged active state should refresh when the recovery window opens");
        AssertFalse(
            NetworkSyncSamplingRules.ShouldRefreshUnchangedState(
                stateIsActive: false,
                hasSentState: true,
                now: 2.0f,
                nextRefreshTime: 1.50f),
            "inactive state should not use lossy heartbeat refresh");
    }

    private static void CapabilityProbeTimeoutUsesNoCompatiblePeerLocalOnlyState()
    {
        NetworkLifecycleState lifecycleState = NetworkCompatibilityPolicy.ResolveLifecycleState(
            targetSyncReady: false,
            capabilitySent: true,
            capabilityProbeSentRealtime: 1f,
            currentRealtime: 4f,
            noCompatiblePeerTimeoutSeconds: 2.5f);

        AssertEqual(
            NetworkLifecycleState.NoCompatiblePeerLocalOnly,
            lifecycleState,
            "capability probe timeout without compatible peer should use no-compatible local-only state");
        AssertFalse(
            NetworkCompatibilityPolicy.ShouldRunBusinessSync(lifecycleState, targetSyncReady: false),
            "no-compatible local-only state should not run target/pose/voice business sync");
    }

    private static void CapabilityProbeRetriesAfterNoCompatibleWindow()
    {
        AssertFalse(
            NetworkCompatibilityPolicy.ShouldRetryCapabilityProbe(
                targetSyncReady: false,
                capabilitySent: true,
                capabilityProbeSentRealtime: 1f,
                currentRealtime: 3.49f,
                retryIntervalSeconds: 2.5f),
            "capability probe should not retry before the no-compatible window elapses");
        AssertTrue(
            NetworkCompatibilityPolicy.ShouldRetryCapabilityProbe(
                targetSyncReady: false,
                capabilitySent: true,
                capabilityProbeSentRealtime: 1f,
                currentRealtime: 3.50f,
                retryIntervalSeconds: 2.5f),
            "capability probe should retry after no compatible peer answered");
    }

    private static void CapabilityProbeDoesNotRetryWhenTargetSyncReady()
    {
        AssertFalse(
            NetworkCompatibilityPolicy.ShouldRetryCapabilityProbe(
                targetSyncReady: true,
                capabilitySent: true,
                capabilityProbeSentRealtime: 1f,
                currentRealtime: 10f,
                retryIntervalSeconds: 2.5f),
            "capability probe should not retry once a compatible peer is ready");
    }

    private static void CompatiblePeerKeepsTransportRegisteredState()
    {
        NetworkLifecycleState lifecycleState = NetworkCompatibilityPolicy.ResolveLifecycleState(
            targetSyncReady: true,
            capabilitySent: true,
            capabilityProbeSentRealtime: 1f,
            currentRealtime: 10f,
            noCompatiblePeerTimeoutSeconds: 2.5f);

        AssertEqual(
            NetworkLifecycleState.TransportRegistered,
            lifecycleState,
            "compatible peer should keep transport registered lifecycle state");
        AssertTrue(
            NetworkCompatibilityPolicy.ShouldRunBusinessSync(lifecycleState, targetSyncReady: true),
            "compatible peer should allow target/pose/voice business sync");
    }

    private static ModPeerCapability Capability(
        ulong clientId,
        bool supportsVoiceActivitySync = true,
        bool supportsSpectatorVoiceToTarget = false)
    {
        return new ModPeerCapability(
            clientId,
            ModNetworkConstants.ProtocolVersion,
            supportsCapabilityHandshake: true,
            supportsSpectatorTargetSync: true,
            handshakeComplete: true,
            lastSeenTicks: (long)clientId + 10L,
            supportsVoiceActivitySync: supportsVoiceActivitySync,
            supportsSpectatorVoiceToTarget: supportsSpectatorVoiceToTarget);
    }

    private static void FreecamTargetSwitchInvalidTargetSoftPausesDuringGrace()
    {
        SpectatorFreecamRecoveryAction action = SpectatorFreecamRecoveryPolicy.GetIneligibleAction(
            SpectatorFreecamIneligibleReason.MissingCameraAnchorOrTarget,
            currentFrame: 12,
            hasPose: true,
            targetSwitchGraceUntilFrame: 14,
            cameraInactiveGraceUntilFrame: -1);

        AssertEqual(
            SpectatorFreecamRecoveryAction.SoftPausePreservePose,
            action,
            "target switch transient invalid target should preserve freecam pose during grace");
    }

    private static void FreecamTargetSwitchInvalidTargetClearsAfterGrace()
    {
        SpectatorFreecamRecoveryAction action = SpectatorFreecamRecoveryPolicy.GetIneligibleAction(
            SpectatorFreecamIneligibleReason.MissingCameraAnchorOrTarget,
            currentFrame: 15,
            hasPose: true,
            targetSwitchGraceUntilFrame: 14,
            cameraInactiveGraceUntilFrame: -1);

        AssertEqual(
            SpectatorFreecamRecoveryAction.DeactivateClearPose,
            action,
            "target switch invalid target should clear freecam pose after grace expires");
    }

    private static void FreecamInactiveSpectateCameraSoftPausesDuringGrace()
    {
        SpectatorFreecamRecoveryAction action = SpectatorFreecamRecoveryPolicy.GetIneligibleAction(
            SpectatorFreecamIneligibleReason.SpectateCameraInactive,
            currentFrame: 20,
            hasPose: true,
            targetSwitchGraceUntilFrame: -1,
            cameraInactiveGraceUntilFrame: 21);

        AssertEqual(
            SpectatorFreecamRecoveryAction.SoftPausePreservePose,
            action,
            "inactive spectate camera should preserve freecam pose during camera grace");
    }

    private static void FreecamInactiveSpectateCameraPreservesPoseAfterGrace()
    {
        SpectatorFreecamRecoveryAction action = SpectatorFreecamRecoveryPolicy.GetIneligibleAction(
            SpectatorFreecamIneligibleReason.SpectateCameraInactive,
            currentFrame: 22,
            hasPose: true,
            targetSwitchGraceUntilFrame: -1,
            cameraInactiveGraceUntilFrame: 21);

        AssertEqual(
            SpectatorFreecamRecoveryAction.DeactivatePreservePose,
            action,
            "inactive spectate camera should preserve pose but stop writing after grace expires");
    }

    private static void FreecamLifecycleUnsafeSoftPausesWhenPoseExists()
    {
        SpectatorFreecamRecoveryAction action = SpectatorFreecamRecoveryPolicy.GetIneligibleAction(
            SpectatorFreecamIneligibleReason.LifecycleUnsafe,
            currentFrame: 30,
            hasPose: true,
            targetSwitchGraceUntilFrame: -1,
            cameraInactiveGraceUntilFrame: -1);

        AssertEqual(
            SpectatorFreecamRecoveryAction.SoftPausePreservePose,
            action,
            "lifecycle unsafe windows should preserve existing freecam pose without writing camera");
    }

    private static void DisconnectAutoSwitchRunsOnlyForInvalidCurrentTarget()
    {
        AssertTrue(
            SpectatorDisconnectTargetSwitchRules.ShouldAutoSwitch(
                isLocalPlayerDead: true,
                hasSpectateCamera: true,
                gameOverOverrideActive: false,
                hasCurrentTarget: true,
                currentTargetIsValid: false,
                currentTargetDisconnected: true,
                hasReplacementTarget: true),
            "disconnect auto-switch should run when the currently watched target is no longer valid");
        AssertFalse(
            SpectatorDisconnectTargetSwitchRules.ShouldAutoSwitch(
                isLocalPlayerDead: true,
                hasSpectateCamera: true,
                gameOverOverrideActive: false,
                hasCurrentTarget: true,
                currentTargetIsValid: true,
                currentTargetDisconnected: false,
                hasReplacementTarget: true),
            "disconnect auto-switch should not disturb a valid current target");
    }

    private static void DisconnectAutoSwitchIgnoresInvalidNonDisconnectedTarget()
    {
        AssertFalse(
            SpectatorDisconnectTargetSwitchRules.ShouldAutoSwitch(
                isLocalPlayerDead: true,
                hasSpectateCamera: true,
                gameOverOverrideActive: false,
                hasCurrentTarget: true,
                currentTargetIsValid: false,
                currentTargetDisconnected: false,
                hasReplacementTarget: true),
            "disconnect auto-switch should not handle invalid targets unless the target is disconnected");
    }

    private static void DisconnectAutoSwitchRequiresReplacementTarget()
    {
        AssertFalse(
            SpectatorDisconnectTargetSwitchRules.ShouldAutoSwitch(
                isLocalPlayerDead: true,
                hasSpectateCamera: true,
                gameOverOverrideActive: false,
                hasCurrentTarget: true,
                currentTargetIsValid: false,
                currentTargetDisconnected: true,
                hasReplacementTarget: false),
            "disconnect auto-switch should not call vanilla target switching when no living replacement exists");
    }

    private static void DisconnectAutoSwitchTreatsUncontrolledAliveTargetAsDisconnectLike()
    {
        AssertTrue(
            SpectatorDisconnectTargetSwitchRules.IsDisconnectLikeInvalidTarget(
                disconnectedMidGame: false,
                isPlayerControlled: false,
                isPlayerDead: false,
                removedFromClientMap: false),
            "an uncontrolled alive current target should be treated as disconnect-like for spectator recovery");
        AssertTrue(
            SpectatorDisconnectTargetSwitchRules.IsDisconnectLikeInvalidTarget(
                disconnectedMidGame: false,
                isPlayerControlled: true,
                isPlayerDead: false,
                removedFromClientMap: true),
            "a current target removed from the vanilla client map should remain disconnect-like");
        AssertFalse(
            SpectatorDisconnectTargetSwitchRules.IsDisconnectLikeInvalidTarget(
                disconnectedMidGame: false,
                isPlayerControlled: false,
                isPlayerDead: true,
                removedFromClientMap: false),
            "a dead target alone should not be treated as a disconnect by the disconnect recovery rule");
    }

    private static void InternalTargetSwitchBypassesVanillaInputSuppression()
    {
        bool suppress = SpectatorVanillaInputGuardRules.ShouldSuppressTargetSwitchInput(
            internalTargetSwitchInProgress: true,
            freecamWantsVerticalInput: true,
            ascendKeyHeld: true,
            descendKeyHeld: false,
            out string reason);

        AssertFalse(suppress, "internal disconnect target switch should bypass freecam vertical input suppression");
        AssertEqual(string.Empty, reason, "internal bypass should not expose an input suppression reason");
    }

    private static void ExternalVerticalInputSuppressesVanillaTargetSwitch()
    {
        bool suppress = SpectatorVanillaInputGuardRules.ShouldSuppressTargetSwitchInput(
            internalTargetSwitchInProgress: false,
            freecamWantsVerticalInput: true,
            ascendKeyHeld: false,
            descendKeyHeld: true,
            out string reason);

        AssertTrue(suppress, "external vanilla target switching should still be suppressed while vertical freecam input is held");
        AssertEqual("freecam vertical movement is held", reason, "vertical input suppression should keep its diagnostic reason");
    }

    private static void DisconnectSwitchResultRequiresChangedValidTarget()
    {
        AssertTrue(
            SpectatorDisconnectTargetSwitchResultRules.DidSwitchToValidTarget(
                targetSwitchInvoked: true,
                targetChanged: true,
                newTargetIsValid: true),
            "disconnect auto-switch should report success only when vanilla changed to a valid target");
        AssertFalse(
            SpectatorDisconnectTargetSwitchResultRules.DidSwitchToValidTarget(
                targetSwitchInvoked: true,
                targetChanged: false,
                newTargetIsValid: true),
            "disconnect auto-switch should not report success when vanilla left the same target selected");
        AssertFalse(
            SpectatorDisconnectTargetSwitchResultRules.DidSwitchToValidTarget(
                targetSwitchInvoked: true,
                targetChanged: true,
                newTargetIsValid: false),
            "disconnect auto-switch should not report success when the new target is still invalid");
    }

    private static void SpectatorPoseSyncUsesVanillaPoseWhenFreecamIsInactive()
    {
        bool useFreecamPose = SpectatorPoseSourceRules.ShouldUseFreecamPose(
            freecamActive: false,
            freecamHasWorldPose: true);
        bool useVanillaPose = !useFreecamPose
            && SpectatorPoseSourceRules.ShouldUseVanillaSpectatorPose(
                hasSpectateCamera: true,
                isSpectateCameraActive: true);
        bool shouldPublish = SpectatorPoseSourceRules.ShouldPublishSpectatorPose(
            isLocalPlayerDead: true,
            hasSpectatedTarget: true,
            useFreecamPose,
            useVanillaPose);

        AssertFalse(useFreecamPose, "disabled freecam should not publish the enhanced freecam pose");
        AssertTrue(useVanillaPose, "disabled freecam should fall back to the vanilla spectator camera pose");
        AssertTrue(shouldPublish, "vanilla spectating should continue publishing pose so remote spectator visuals remain visible");
    }

    private static void SpectatorPoseSyncDoesNotPublishWithoutFreecamOrActiveVanillaCamera()
    {
        bool useFreecamPose = SpectatorPoseSourceRules.ShouldUseFreecamPose(
            freecamActive: false,
            freecamHasWorldPose: false);
        bool useVanillaPose = !useFreecamPose
            && SpectatorPoseSourceRules.ShouldUseVanillaSpectatorPose(
                hasSpectateCamera: true,
                isSpectateCameraActive: false);
        bool shouldPublish = SpectatorPoseSourceRules.ShouldPublishSpectatorPose(
            isLocalPlayerDead: true,
            hasSpectatedTarget: true,
            useFreecamPose,
            useVanillaPose);

        AssertFalse(useFreecamPose, "inactive freecam without a pose should not be a pose source");
        AssertFalse(useVanillaPose, "inactive vanilla spectator camera should not be a pose source");
        AssertFalse(shouldPublish, "pose sync should publish inactive state when no safe pose source exists");
    }

    private static void RemotePoseRegistryKeepsVisiblePoseThroughFreecamVanillaFreecamCycle()
    {
        RemoteSpectatorPoseRegistry registry = new RemoteSpectatorPoseRegistry();
        SpectatorPoseState freecamPose = new SpectatorPoseState(
            isSpectating: true,
            localClientId: 2,
            localPlayerSlotId: 2,
            targetClientId: 0,
            targetPlayerSlotId: 0,
            position: new Vector3(1f, 2f, 3f),
            rotation: Quaternion.identity,
            timestampTicks: 100);
        SpectatorPoseState vanillaPose = new SpectatorPoseState(
            isSpectating: SpectatorPoseSourceRules.ShouldPublishSpectatorPose(
                isLocalPlayerDead: true,
                hasSpectatedTarget: true,
                useFreecamPose: false,
                useVanillaSpectatorPose: true),
            localClientId: 2,
            localPlayerSlotId: 2,
            targetClientId: 0,
            targetPlayerSlotId: 0,
            position: new Vector3(4f, 5f, 6f),
            rotation: new Quaternion(0f, 0.70710677f, 0f, 0.70710677f),
            timestampTicks: 101);
        SpectatorPoseState restoredFreecamPose = new SpectatorPoseState(
            isSpectating: true,
            localClientId: 2,
            localPlayerSlotId: 2,
            targetClientId: 0,
            targetPlayerSlotId: 0,
            position: new Vector3(7f, 8f, 9f),
            rotation: new Quaternion(0.08583165f, 0.1729874f, -0.01513444f, 0.9810603f),
            timestampTicks: 102);

        registry.Update(freecamPose);
        AssertTrue(registry.TryGet(2, out SpectatorPoseState storedFreecamPose), "freecam pose should be visible");
        AssertVectorNear(new Vector3(1f, 2f, 3f), storedFreecamPose.Position, "freecam pose should be stored");

        registry.Update(vanillaPose);
        AssertTrue(registry.TryGet(2, out SpectatorPoseState storedVanillaPose), "vanilla fallback pose should keep the spectator visible");
        AssertTrue(storedVanillaPose.IsSpectating, "vanilla fallback pose should not clear the remote pose registry");
        AssertVectorNear(new Vector3(4f, 5f, 6f), storedVanillaPose.Position, "vanilla fallback pose should replace the previous freecam pose");

        registry.Update(restoredFreecamPose);
        AssertTrue(registry.TryGet(2, out SpectatorPoseState storedRestoredFreecamPose), "restored freecam pose should remain visible");
        AssertVectorNear(new Vector3(7f, 8f, 9f), storedRestoredFreecamPose.Position, "restored freecam pose should replace the vanilla fallback pose");
    }

    private static void GameSpectatorSnapshotUsesStructStorageToAvoidHotPathAllocation()
    {
        AssertStruct<GameSpectatorSnapshot>(
            "local spectator snapshots should use struct storage to avoid hot-path heap allocation");
    }

    private static void SpectatorSnapshotCacheReusesSuccessfulSnapshotWithinFrame()
    {
        CountingGameSpectatorAdapter adapter = new CountingGameSpectatorAdapter
        {
            Snapshot = CreateSpectatorSnapshot(spectatedPlayerSlotId: 3, spectatedPlayerActualClientId: 7),
        };
        SpectatorSnapshotCache cache = new SpectatorSnapshotCache(adapter);

        AssertTrue(cache.TryGetSnapshotForFrame(42, out GameSpectatorSnapshot first), "first snapshot read should succeed");
        AssertTrue(cache.TryGetSnapshotForFrame(42, out GameSpectatorSnapshot second), "same-frame cached snapshot read should succeed");

        AssertEqual(1, adapter.SnapshotCalls, "snapshot cache should sample adapter once per frame");
        AssertTrue(first.SpectatedPlayerSlotId == 3UL, "first snapshot should preserve slot id");
        AssertTrue(second.SpectatedPlayerActualClientId == 7UL, "cached snapshot should preserve actual client id");
    }

    private static void SpectatorSnapshotCacheCachesFailureWithinFrame()
    {
        CountingGameSpectatorAdapter adapter = new CountingGameSpectatorAdapter
        {
            SnapshotResult = false,
        };
        SpectatorSnapshotCache cache = new SpectatorSnapshotCache(adapter);

        AssertFalse(cache.TryGetSnapshotForFrame(43, out _), "first failed snapshot read should return false");
        AssertFalse(cache.TryGetSnapshotForFrame(43, out _), "same-frame failed snapshot read should stay cached");

        AssertEqual(1, adapter.SnapshotCalls, "snapshot cache should cache adapter failures within a frame");
    }

    private static void SpectatorSnapshotCacheRefreshesOnNewFrame()
    {
        CountingGameSpectatorAdapter adapter = new CountingGameSpectatorAdapter
        {
            Snapshot = CreateSpectatorSnapshot(spectatedPlayerSlotId: 4, spectatedPlayerActualClientId: 8),
        };
        SpectatorSnapshotCache cache = new SpectatorSnapshotCache(adapter);

        AssertTrue(cache.TryGetSnapshotForFrame(44, out GameSpectatorSnapshot first), "first frame snapshot should succeed");
        adapter.Snapshot = CreateSpectatorSnapshot(spectatedPlayerSlotId: 5, spectatedPlayerActualClientId: 9);
        AssertTrue(cache.TryGetSnapshotForFrame(45, out GameSpectatorSnapshot second), "new frame snapshot should resample adapter");

        AssertEqual(2, adapter.SnapshotCalls, "snapshot cache should resample on new frame");
        AssertTrue(first.SpectatedPlayerSlotId == 4UL, "first frame should keep original slot id");
        AssertTrue(second.SpectatedPlayerSlotId == 5UL, "new frame should read updated slot id");
    }

    private static void SpectatorSnapshotCacheClearForcesResampleWithinFrame()
    {
        CountingGameSpectatorAdapter adapter = new CountingGameSpectatorAdapter
        {
            Snapshot = CreateSpectatorSnapshot(spectatedPlayerSlotId: 6, spectatedPlayerActualClientId: 10),
        };
        SpectatorSnapshotCache cache = new SpectatorSnapshotCache(adapter);

        AssertTrue(cache.TryGetSnapshotForFrame(46, out GameSpectatorSnapshot first), "first snapshot should succeed");
        cache.Clear();
        adapter.Snapshot = CreateSpectatorSnapshot(spectatedPlayerSlotId: 7, spectatedPlayerActualClientId: 11);
        AssertTrue(cache.TryGetSnapshotForFrame(46, out GameSpectatorSnapshot second), "cleared same-frame snapshot should resample adapter");

        AssertEqual(2, adapter.SnapshotCalls, "clearing cache should force same-frame resample");
        AssertTrue(first.SpectatedPlayerSlotId == 6UL, "first cached snapshot should keep original slot id");
        AssertTrue(second.SpectatedPlayerSlotId == 7UL, "cleared snapshot should read updated slot id");
    }

    private static void SpectatorSnapshotCacheDoesNotCommitFrameWhenAdapterThrows()
    {
        CountingGameSpectatorAdapter adapter = new CountingGameSpectatorAdapter
        {
            Snapshot = CreateSpectatorSnapshot(spectatedPlayerSlotId: 8, spectatedPlayerActualClientId: 12),
        };
        SpectatorSnapshotCache cache = new SpectatorSnapshotCache(adapter);

        AssertTrue(cache.TryGetSnapshotForFrame(47, out _), "initial snapshot should succeed");
        adapter.ThrowOnSnapshot = true;
        AssertThrows<InvalidOperationException>(
            () => cache.TryGetSnapshotForFrame(48, out _),
            "adapter exception should propagate");
        adapter.ThrowOnSnapshot = false;
        adapter.Snapshot = CreateSpectatorSnapshot(spectatedPlayerSlotId: 9, spectatedPlayerActualClientId: 13);
        AssertTrue(cache.TryGetSnapshotForFrame(48, out GameSpectatorSnapshot recovered), "same-frame retry after exception should resample adapter");

        AssertEqual(3, adapter.SnapshotCalls, "failed adapter read should not commit frame cache");
        AssertTrue(recovered.SpectatedPlayerSlotId == 9UL, "same-frame retry after exception should not return stale snapshot");
    }

    private static void SpectatorAnchorTargetIdentityMatchesStablePlayerIds()
    {
        GameSpectatorSnapshot firstSnapshot = CreateSpectatorSnapshot(spectatedPlayerSlotId: 2, spectatedPlayerActualClientId: 10);
        GameSpectatorSnapshot secondSnapshot = CreateSpectatorSnapshot(spectatedPlayerSlotId: 2, spectatedPlayerActualClientId: 10);

        AssertTrue(
            SpectatorAnchorTargetIdentity.TryCreate(firstSnapshot, anchorInstanceId: 100, out SpectatorAnchorTargetIdentity first),
            "identity should be created when spectator target exists");
        AssertTrue(
            SpectatorAnchorTargetIdentity.TryCreate(secondSnapshot, anchorInstanceId: 200, out SpectatorAnchorTargetIdentity second),
            "identity should prefer stable player ids over anchor instance");

        AssertTrue(first.Equals(second), "same stable player ids should match even when anchor instance changes");
    }

    private static void SpectatorAnchorTargetIdentityDetectsActualClientSwitch()
    {
        GameSpectatorSnapshot firstSnapshot = CreateSpectatorSnapshot(spectatedPlayerSlotId: 2, spectatedPlayerActualClientId: 10);
        GameSpectatorSnapshot secondSnapshot = CreateSpectatorSnapshot(spectatedPlayerSlotId: 2, spectatedPlayerActualClientId: 11);

        AssertTrue(
            SpectatorAnchorTargetIdentity.TryCreate(firstSnapshot, anchorInstanceId: 100, out SpectatorAnchorTargetIdentity first),
            "identity should be created for first target");
        AssertTrue(
            SpectatorAnchorTargetIdentity.TryCreate(secondSnapshot, anchorInstanceId: 100, out SpectatorAnchorTargetIdentity second),
            "identity should be created for switched target");

        AssertFalse(first.Equals(second), "changed actual client id should be treated as target switch");
    }

    private static void SpectatorAnchorTargetIdentityFallsBackToAnchorInstance()
    {
        GameSpectatorSnapshot firstSnapshot = CreateSpectatorSnapshot(spectatedPlayerSlotId: null, spectatedPlayerActualClientId: null);
        GameSpectatorSnapshot secondSnapshot = CreateSpectatorSnapshot(spectatedPlayerSlotId: null, spectatedPlayerActualClientId: null);

        AssertTrue(
            SpectatorAnchorTargetIdentity.TryCreate(firstSnapshot, anchorInstanceId: 30, out SpectatorAnchorTargetIdentity first),
            "identity should fall back to anchor instance when player ids are unavailable");
        AssertTrue(
            SpectatorAnchorTargetIdentity.TryCreate(secondSnapshot, anchorInstanceId: 30, out SpectatorAnchorTargetIdentity matching),
            "same anchor instance should create matching fallback identity");
        AssertTrue(
            SpectatorAnchorTargetIdentity.TryCreate(secondSnapshot, anchorInstanceId: 31, out SpectatorAnchorTargetIdentity changed),
            "different anchor instance should create different fallback identity");

        AssertTrue(first.Equals(matching), "same fallback anchor instance should match");
        AssertFalse(first.Equals(changed), "different fallback anchor instance should be treated as target switch");
    }

    private static void SpectatorTargetAnchorSelectionPrefersStableRoot()
    {
        SpectatorTargetAnchorSource source = SpectatorTargetAnchorSelectionRules.Resolve(
            hasPlayerRoot: true,
            hasLowerSpine: true,
            hasGlobalHead: true);

        AssertEqual(
            SpectatorTargetAnchorSource.PlayerRoot,
            source,
            "spectator camera anchor should prefer stable player root over animated bones");
    }

    private static void SpectatorTargetAnchorSelectionFallsBackToAnimatedBones()
    {
        SpectatorTargetAnchorSource lowerSpineSource = SpectatorTargetAnchorSelectionRules.Resolve(
            hasPlayerRoot: false,
            hasLowerSpine: true,
            hasGlobalHead: true);
        SpectatorTargetAnchorSource headSource = SpectatorTargetAnchorSelectionRules.Resolve(
            hasPlayerRoot: false,
            hasLowerSpine: false,
            hasGlobalHead: true);

        AssertEqual(
            SpectatorTargetAnchorSource.LowerSpine,
            lowerSpineSource,
            "spectator camera anchor should keep lower spine as fallback when root is unavailable");
        AssertEqual(
            SpectatorTargetAnchorSource.GlobalHead,
            headSource,
            "spectator camera anchor should keep global head as final animated fallback");
    }

    private static void NameTagTextCacheInvalidatesWhenIdentityRevisionChanges()
    {
        AssertTrue(
            NameTagTextCacheRules.ShouldClear(
                lastIdentityRevision: 3,
                currentIdentityRevision: 4,
                lastUseGamePlayerNames: true,
                currentUseGamePlayerNames: true,
                lastUseFallbackIds: true,
                currentUseFallbackIds: true),
            "name tag text cache should clear when synced identity revision changes");
    }

    private static void NameTagTextCacheInvalidatesWhenDisplayConfigChanges()
    {
        AssertTrue(
            NameTagTextCacheRules.ShouldClear(
                lastIdentityRevision: 4,
                currentIdentityRevision: 4,
                lastUseGamePlayerNames: true,
                currentUseGamePlayerNames: false,
                lastUseFallbackIds: true,
                currentUseFallbackIds: true),
            "name tag text cache should clear when display-name source config changes");
        AssertFalse(
            NameTagTextCacheRules.ShouldClear(
                lastIdentityRevision: 4,
                currentIdentityRevision: 4,
                lastUseGamePlayerNames: false,
                currentUseGamePlayerNames: false,
                lastUseFallbackIds: true,
                currentUseFallbackIds: true),
            "name tag text cache should remain valid when revision and display config are unchanged");
    }

    private static void FeatureRuntimeDispatchListsKeepsPhaseOrder()
    {
        FeatureRuntimeDispatchLists lists = new FeatureRuntimeDispatchLists();
        RuntimeTickFeature first = new RuntimeTickFeature();
        RuntimeTickFeature second = new RuntimeTickFeature();

        lists.AddTickable(first);
        lists.AddTickable(second);

        AssertEqual(2, lists.Tickables.Count, "dispatch list should keep only Update tick features");
        AssertSame(first, lists.Tickables[0], "dispatch list should keep original phase order");
        AssertSame(second, lists.Tickables[1], "dispatch list should keep second registered Update feature");
        AssertEqual(0, lists.LateTickables.Count, "dispatch list should leave unrelated phases empty");
    }

    private static void FeatureRuntimeDispatchListsAddsFeatureToEveryRegisteredPhase()
    {
        FeatureRuntimeDispatchLists lists = new FeatureRuntimeDispatchLists();
        FullRuntimeFeature feature = new FullRuntimeFeature();

        lists.AddTickable(feature);
        lists.AddLateTickable(feature);
        lists.AddCameraPreCullTickable(feature);
        lists.AddGuiTickable(feature);

        AssertSame(feature, lists.Tickables[0], "dispatch list should include Update phase");
        AssertSame(feature, lists.LateTickables[0], "dispatch list should include LateUpdate phase");
        AssertSame(feature, lists.CameraPreCullTickables[0], "dispatch list should include camera pre-cull phase");
        AssertSame(feature, lists.GuiTickables[0], "dispatch list should include GUI phase");
    }

    private static void FeatureRuntimeDispatchListsDispatchesCallbacksInPhaseOrder()
    {
        FeatureRuntimeDispatchLists lists = new FeatureRuntimeDispatchLists();
        List<string> calls = new List<string>();

        lists.AddTickable(new RuntimeTickFeature("first", calls));
        lists.AddTickable(new RuntimeTickFeature("second", calls));

        lists.TickAll();

        AssertEqual(2, calls.Count, "dispatch should call only registered Update phase features");
        AssertEqual("first", calls[0], "dispatch should keep first registered Update callback");
        AssertEqual("second", calls[1], "dispatch should keep second registered Update callback");
    }

    private static void TargetClientIdTakesPriorityOverSlotFallback()
    {
        SpectatorTargetState remoteTarget = new SpectatorTargetState(
            isSpectating: true,
            localClientId: 2,
            localPlayerSlotId: 2,
            targetClientId: 0,
            targetPlayerSlotId: 1,
            timestampTicks: 100);

        bool isWatchingLocal = RemoteSpectatorVisibilityRules.IsWatchingLocalPlayer(
            remoteTarget,
            localClientId: 1,
            localPlayerSlotId: 1);

        AssertFalse(
            isWatchingLocal,
            "target client id should take priority over a colliding target slot fallback");
    }

    private static void GlobalRemoteSpectatorVisibilityAllowsNonLocalTargets()
    {
        SpectatorTargetState remoteTarget = new SpectatorTargetState(
            isSpectating: true,
            localClientId: 2,
            localPlayerSlotId: 2,
            targetClientId: 0,
            targetPlayerSlotId: 0,
            timestampTicks: 100);

        bool isWatchingLocal = RemoteSpectatorVisibilityRules.IsWatchingLocalPlayer(
            remoteTarget,
            localClientId: 1,
            localPlayerSlotId: 1);
        bool shouldShow = RemoteSpectatorVisibilityRules.ShouldShowRemoteSpectator(
            remoteTarget,
            isWatchingLocal,
            hasMatchingPose: true,
            localPlayerIsDeadOrSpectating: false,
            showRemoteSpectators: true,
            showOnlySpectatorsWatchingMe: false,
            showDeadSpectatorsToAlivePlayers: true,
            showDeadSpectatorsToDeadPlayers: true);

        AssertFalse(isWatchingLocal, "remote spectator should not be treated as watching local player");
        AssertTrue(shouldShow, "global remote spectator visibility should show spectators watching another player");
    }

    private static void RemoteSpectatorVisibilityRequiresMatchingPose()
    {
        SpectatorTargetState remoteTarget = new SpectatorTargetState(
            isSpectating: true,
            localClientId: 2,
            localPlayerSlotId: 2,
            targetClientId: 1,
            targetPlayerSlotId: 1,
            timestampTicks: 100);

        bool isWatchingLocal = RemoteSpectatorVisibilityRules.IsWatchingLocalPlayer(
            remoteTarget,
            localClientId: 1,
            localPlayerSlotId: 1);
        bool shouldShow = RemoteSpectatorVisibilityRules.ShouldShowRemoteSpectator(
            remoteTarget,
            isWatchingLocal,
            hasMatchingPose: false,
            localPlayerIsDeadOrSpectating: false,
            showRemoteSpectators: true,
            showOnlySpectatorsWatchingMe: false,
            showDeadSpectatorsToAlivePlayers: true,
            showDeadSpectatorsToDeadPlayers: true);

        AssertTrue(isWatchingLocal, "test setup should represent a spectator watching the local player");
        AssertFalse(
            shouldShow,
            "floating-head visibility should require a matching active pose even when the target is the local player");
    }

    private static void TargetSlotFallbackWorksOnlyWhenClientIdMissing()
    {
        SpectatorTargetState remoteTarget = new SpectatorTargetState(
            isSpectating: true,
            localClientId: 2,
            localPlayerSlotId: 2,
            targetClientId: null,
            targetPlayerSlotId: 1,
            timestampTicks: 100);

        bool isWatchingLocal = RemoteSpectatorVisibilityRules.IsWatchingLocalPlayer(
            remoteTarget,
            localClientId: 1,
            localPlayerSlotId: 1);

        AssertTrue(
            isWatchingLocal,
            "target slot id should be a fallback when target client id is absent");
    }

    private static void RemoteTargetRegistryRetainsInactiveStateForRepair()
    {
        RemoteSpectatorTargetRegistry registry = new RemoteSpectatorTargetRegistry();
        registry.Update(new SpectatorTargetState(
            isSpectating: true,
            localClientId: 3,
            localPlayerSlotId: 1,
            targetClientId: 0,
            targetPlayerSlotId: 0,
            timestampTicks: 100));

        AssertTrue(registry.TryGet(3, out _), "active remote target state should be stored");

        registry.Update(new SpectatorTargetState(
            isSpectating: false,
            localClientId: 3,
            localPlayerSlotId: 1,
            targetClientId: null,
            targetPlayerSlotId: null,
            timestampTicks: 101));

        AssertTrue(
            registry.TryGet(3, out SpectatorTargetState inactiveState),
            "inactive remote target state should be retained so player-state repair can clear stale dead flags");
        AssertFalse(inactiveState.IsSpectating, "retained inactive state should preserve the alive/non-spectating signal");
    }

    private static void RemoteTargetRegistryRevisionChangesOnlyForRepairRelevantChanges()
    {
        RemoteSpectatorTargetRegistry registry = new RemoteSpectatorTargetRegistry();

        registry.Update(new SpectatorTargetState(false, 2, 2, null, null, 100));
        AssertEqual(1, registry.Revision, "target registry revision should change after first state");

        registry.Update(new SpectatorTargetState(false, 2, 2, null, null, 101));
        AssertEqual(1, registry.Revision, "target registry revision should ignore timestamp-only refresh");

        registry.Update(new SpectatorTargetState(true, 2, 2, 1, 1, 102));
        AssertEqual(2, registry.Revision, "target registry revision should change when repair-relevant target state changes");
    }

    private static void RemoteTargetRegistryRevisionChangesOnRemoveAndClear()
    {
        RemoteSpectatorTargetRegistry registry = new RemoteSpectatorTargetRegistry();
        registry.Update(new SpectatorTargetState(false, 2, 2, null, null, 100));
        registry.Update(new SpectatorTargetState(false, 3, 3, null, null, 101));

        registry.Remove(2);
        AssertEqual(3, registry.Revision, "target registry revision should change after removing stored state");

        registry.Remove(9);
        AssertEqual(3, registry.Revision, "target registry revision should ignore missing removals");

        registry.Clear();
        AssertEqual(4, registry.Revision, "target registry revision should change after non-empty clear");

        registry.Clear();
        AssertEqual(4, registry.Revision, "target registry revision should ignore empty clear");
    }

    private static void ConnectedAliveVanillaSlotRepairRunsWithoutModIdentity()
    {
        AssertTrue(
            ConnectedPlayerStateRepairRules.ShouldRestoreConnectedAliveControl(
                isLocalPlayer: false,
                isCurrentlyConnected: true,
                isPlayerControlled: false,
                isPlayerDead: false),
            "connected non-local alive vanilla slots should be restored to controlled even without mod identity");

        AssertTrue(
            ConnectedPlayerStateRepairRules.ShouldClearDisconnectedMidGame(
                isLocalPlayer: false,
                isCurrentlyConnected: true,
                disconnectedMidGame: true),
            "connected non-local vanilla slots should clear stale disconnectedMidGame");
    }

    private static void DeadVanillaSlotRepairDoesNotRestoreControl()
    {
        AssertFalse(
            ConnectedPlayerStateRepairRules.ShouldRestoreConnectedAliveControl(
                isLocalPlayer: false,
                isCurrentlyConnected: true,
                isPlayerControlled: false,
                isPlayerDead: true),
            "connected dead vanilla slots must not be restored to controlled without explicit revive evidence");

        AssertFalse(
            ConnectedPlayerStateRepairRules.ShouldRestoreConnectedAliveControl(
                isLocalPlayer: true,
                isCurrentlyConnected: true,
                isPlayerControlled: false,
                isPlayerDead: false),
            "local player state must not be repaired by remote-slot vanilla repair");
    }

    private static void PlayerStateRepairScheduleRunsFirstEligibleTick()
    {
        AssertTrue(
            ConnectedPlayerStateRepairScheduleRules.ShouldRunRepair(
                initialized: true,
                enabled: true,
                now: 1f,
                nextRepairTime: 0f,
                lastIdentityRevision: -1,
                currentIdentityRevision: 0,
                lastTargetRevision: -1,
                currentTargetRevision: 0,
                nextIdleFallbackRepairTime: 5f,
                repeatAfterRepair: false),
            "player state repair should run once after initialization");
    }

    private static void PlayerStateRepairScheduleSkipsCleanIdleBeforeFallback()
    {
        AssertFalse(
            ConnectedPlayerStateRepairScheduleRules.ShouldRunRepair(
                initialized: true,
                enabled: true,
                now: 2f,
                nextRepairTime: 1f,
                lastIdentityRevision: 3,
                currentIdentityRevision: 3,
                lastTargetRevision: 4,
                currentTargetRevision: 4,
                nextIdleFallbackRepairTime: 10f,
                repeatAfterRepair: false),
            "player state repair should skip clean idle ticks before fallback");
    }

    private static void PlayerStateRepairScheduleRunsWhenRemoteStateRevisionChanges()
    {
        AssertTrue(
            ConnectedPlayerStateRepairScheduleRules.ShouldRunRepair(
                initialized: true,
                enabled: true,
                now: 2f,
                nextRepairTime: 1f,
                lastIdentityRevision: 3,
                currentIdentityRevision: 4,
                lastTargetRevision: 4,
                currentTargetRevision: 4,
                nextIdleFallbackRepairTime: 10f,
                repeatAfterRepair: false),
            "player state repair should run when identity revision changes");
    }

    private static void PlayerStateRepairScheduleRunsIdleFallbackWithoutRevisionChange()
    {
        AssertTrue(
            ConnectedPlayerStateRepairScheduleRules.ShouldRunRepair(
                initialized: true,
                enabled: true,
                now: 10f,
                nextRepairTime: 1f,
                lastIdentityRevision: 3,
                currentIdentityRevision: 3,
                lastTargetRevision: 4,
                currentTargetRevision: 4,
                nextIdleFallbackRepairTime: 10f,
                repeatAfterRepair: false),
            "player state repair should keep a low-frequency vanilla fallback scan");
    }

    private static void PlayerStateRepairScheduleRepeatsAfterSuccessfulRepair()
    {
        AssertTrue(
            ConnectedPlayerStateRepairScheduleRules.ShouldRunRepair(
                initialized: true,
                enabled: true,
                now: 2f,
                nextRepairTime: 1f,
                lastIdentityRevision: 3,
                currentIdentityRevision: 3,
                lastTargetRevision: 4,
                currentTargetRevision: 4,
                nextIdleFallbackRepairTime: 10f,
                repeatAfterRepair: true),
            "player state repair should repeat after a pass repaired state");
    }

    private static void VanillaFallbackNameOnlyReplacesGenericLabels()
    {
        AssertTrue(
            ConnectedPlayerStateRepairRules.ShouldUseVanillaFallbackDisplayName(
                updatePlayerNames: true,
                hasModIdentityDisplayName: false,
                currentDisplayName: "Player #1"),
            "vanilla fallback names should be allowed for generic Player #n labels");

        AssertFalse(
            ConnectedPlayerStateRepairRules.ShouldUseVanillaFallbackDisplayName(
                updatePlayerNames: true,
                hasModIdentityDisplayName: false,
                currentDisplayName: "Ueser"),
            "vanilla fallback names should not overwrite already real names");

        AssertFalse(
            ConnectedPlayerStateRepairRules.ShouldUseVanillaFallbackDisplayName(
                updatePlayerNames: true,
                hasModIdentityDisplayName: true,
                currentDisplayName: "Player #1"),
            "mod identity names should take priority over vanilla fallback names");
    }

    private static void GenericPlayerNumberDisplayNamesAreRejected()
    {
        AssertFalse(
            PlayerDisplayNameRules.TryNormalize("Player #1", out _),
            "generic Player #n placeholders should not be treated as real display names");
        AssertFalse(
            PlayerDisplayNameRules.TryNormalize("player#22", out _),
            "compact player#n placeholders should not be treated as real display names");
    }

    private static void GenericPlayerNumberDisplayNamesIgnoreNonSpaceWhitespace()
    {
        AssertFalse(
            PlayerDisplayNameRules.TryNormalize("Player\t#1", out _),
            "tab-separated player placeholders should not be treated as real display names");
        AssertFalse(
            PlayerDisplayNameRules.TryNormalize("Player\u00A0#1", out _),
            "non-breaking-space player placeholders should not be treated as real display names");
    }

    private static void RealDisplayNamesAreAcceptedAndTrimmed()
    {
        bool result = PlayerDisplayNameRules.TryNormalize("  Ueser  ", out string displayName);

        AssertTrue(result, "real display names should be accepted");
        if (displayName != "Ueser")
        {
            throw new InvalidOperationException($"real display names should be trimmed. Got '{displayName}'.");
        }
    }

    private static void RemotePeerIdentityRegistryStoresAndRemovesNames()
    {
        RemotePeerIdentityRegistry registry = new RemotePeerIdentityRegistry();
        registry.Update(new PeerIdentityState(1, 1, "Auuueser", "voice-player-1", 100));

        AssertTrue(registry.TryGet(1, out PeerIdentityState identity), "remote peer identity should be stored");
        if (identity.DisplayName != "Auuueser")
        {
            throw new InvalidOperationException($"remote peer identity should preserve display name. Got '{identity.DisplayName}'.");
        }

        if (identity.VoicePlayerName != "voice-player-1")
        {
            throw new InvalidOperationException($"remote peer identity should preserve voice player name. Got '{identity.VoicePlayerName}'.");
        }

        registry.Remove(1);
        AssertFalse(registry.TryGet(1, out _), "remote peer identity should be removable on disconnect cleanup");
    }

    private static void RemotePeerIdentityRegistryKeepsVoiceNameWhenNewerLegacyIdentityArrives()
    {
        RemotePeerIdentityRegistry registry = new RemotePeerIdentityRegistry();
        registry.Update(new PeerIdentityState(1, 1, "Auuueser", "voice-player-1", 100));
        registry.Update(new PeerIdentityState(1, 1, "Auuueser", string.Empty, 101));

        AssertTrue(registry.TryGet(1, out PeerIdentityState identity), "remote peer identity should remain stored");
        if (identity.VoicePlayerName != "voice-player-1")
        {
            throw new InvalidOperationException("newer identity without voice id should not erase the last known voice player id");
        }
    }

    private static void RemotePeerIdentityRegistryIgnoresOlderIdentity()
    {
        RemotePeerIdentityRegistry registry = new RemotePeerIdentityRegistry();
        registry.Update(new PeerIdentityState(1, 1, "NewName", "voice-new", 200));
        registry.Update(new PeerIdentityState(1, 1, "OldName", "voice-old", 100));

        AssertTrue(registry.TryGet(1, out PeerIdentityState identity), "remote peer identity should remain stored");
        if (identity.DisplayName != "NewName" || identity.VoicePlayerName != "voice-new")
        {
            throw new InvalidOperationException("older identity should not replace newer identity data");
        }
    }

    private static void RemotePeerIdentityRegistryCopiesIntoCallerOwnedList()
    {
        RemotePeerIdentityRegistry registry = new RemotePeerIdentityRegistry();
        registry.Update(new PeerIdentityState(1, 1, "First", "voice-first", 100));
        List<PeerIdentityState> destination = new List<PeerIdentityState>();

        registry.CopySnapshotTo(destination);

        AssertEqual(1, destination.Count, "identity registry copy should populate caller-owned list");
        AssertEqual("First", destination[0].DisplayName, "identity registry copy should preserve stored display name");
        AssertEqual("voice-first", destination[0].VoicePlayerName, "identity registry copy should preserve stored voice name");
    }

    private static void RemotePeerIdentityRegistryCopyClearsDestination()
    {
        RemotePeerIdentityRegistry registry = new RemotePeerIdentityRegistry();
        registry.Update(new PeerIdentityState(2, 2, "Second", "voice-second", 200));
        List<PeerIdentityState> destination = new List<PeerIdentityState>
        {
            new PeerIdentityState(9, 9, "Stale", "voice-stale", 90),
        };

        registry.CopySnapshotTo(destination);

        AssertEqual(1, destination.Count, "identity registry copy should replace previous destination contents");
        AssertEqual(2UL, destination[0].ClientId, "identity registry copy should remove stale caller-owned entries");
    }

    private static void RemoteIdentityRegistryRevisionIgnoresOlderIdentity()
    {
        RemotePeerIdentityRegistry registry = new RemotePeerIdentityRegistry();

        registry.Update(new PeerIdentityState(2, 2, "First", "voice-first", 100));
        registry.Update(new PeerIdentityState(2, 2, "Older", "voice-old", 99));

        AssertEqual(1, registry.Revision, "identity registry revision should ignore older identity state");
        AssertTrue(registry.TryGet(2, out PeerIdentityState stored), "identity should remain stored");
        AssertEqual("First", stored.DisplayName, "older identity should not replace stored identity");
    }

    private static void RemoteIdentityRegistryRevisionChangesOnlyForStoredIdentityChanges()
    {
        RemotePeerIdentityRegistry registry = new RemotePeerIdentityRegistry();

        registry.Update(new PeerIdentityState(2, 2, "First", "voice-first", 100));
        registry.Update(new PeerIdentityState(2, 2, "First", "voice-first", 101));
        AssertEqual(1, registry.Revision, "identity registry revision should ignore timestamp-only refresh");

        registry.Update(new PeerIdentityState(2, 2, "Second", "voice-first", 102));
        AssertEqual(2, registry.Revision, "identity registry revision should change when display name changes");
    }

    private static void RemoteIdentityRegistryRevisionChangesOnRemoveAndClear()
    {
        RemotePeerIdentityRegistry registry = new RemotePeerIdentityRegistry();
        registry.Update(new PeerIdentityState(2, 2, "First", "voice-first", 100));
        registry.Update(new PeerIdentityState(3, 3, "Second", "voice-second", 101));

        registry.Remove(2);
        AssertEqual(3, registry.Revision, "identity registry revision should change after removing stored identity");

        registry.Remove(9);
        AssertEqual(3, registry.Revision, "identity registry revision should ignore missing removals");

        registry.Clear();
        AssertEqual(4, registry.Revision, "identity registry revision should change after non-empty clear");

        registry.Clear();
        AssertEqual(4, registry.Revision, "identity registry revision should ignore empty clear");
    }

    private static void DetachedHeadVisualSourceRequiresConfigAndSource()
    {
        AssertFalse(
            DetachedHeadVisualSourceRules.ShouldUseRuntimeDetachedHead(
                useRuntimeDetachedHeadVisuals: false,
                hasRuntimeDetachedHeadTemplate: true),
            "runtime detached head visual must be disabled when config is off");

        AssertFalse(
            DetachedHeadVisualSourceRules.ShouldUseRuntimeDetachedHead(
                useRuntimeDetachedHeadVisuals: true,
                hasRuntimeDetachedHeadTemplate: false),
            "runtime detached head visual must fall back when no source exists");

        AssertTrue(
            DetachedHeadVisualSourceRules.ShouldUseRuntimeDetachedHead(
                useRuntimeDetachedHeadVisuals: true,
                hasRuntimeDetachedHeadTemplate: true),
            "runtime detached head visual should be used only when config and source are both available");
    }

    private static void DetachedHeadVisualSourceFallsBackOnlyWhenAllowed()
    {
        AssertTrue(
            DetachedHeadVisualSourceRules.TryResolveVisualSourceKind(
                enablePlaceholderVisuals: true,
                useRuntimeDetachedHeadVisuals: true,
                hasRuntimeDetachedHeadTemplate: false,
                fallbackToPlaceholderWhenDetachedHeadUnavailable: true,
                out FloatingHeadVisualSourceKind fallbackKind),
            "runtime detached head visuals should use placeholder fallback when configured");
        if (fallbackKind != FloatingHeadVisualSourceKind.Placeholder)
        {
            throw new InvalidOperationException($"expected placeholder fallback, got {fallbackKind}.");
        }

        AssertFalse(
            DetachedHeadVisualSourceRules.TryResolveVisualSourceKind(
                enablePlaceholderVisuals: true,
                useRuntimeDetachedHeadVisuals: true,
                hasRuntimeDetachedHeadTemplate: false,
                fallbackToPlaceholderWhenDetachedHeadUnavailable: false,
                out _),
            "runtime detached head visuals should skip visual creation when source is missing and fallback is disabled");

        AssertFalse(
            DetachedHeadVisualSourceRules.TryResolveVisualSourceKind(
                enablePlaceholderVisuals: false,
                useRuntimeDetachedHeadVisuals: false,
                hasRuntimeDetachedHeadTemplate: false,
                fallbackToPlaceholderWhenDetachedHeadUnavailable: true,
                out _),
            "visual creation should be skipped when both detached-head and placeholder visuals are disabled");
    }

    private static void RuntimeDetachedHeadUsesRemotePoseRotation()
    {
        AssertTrue(
            FloatingHeadRotationRules.ShouldFaceLocalCamera(
                FloatingHeadVisualSourceKind.Placeholder,
                faceCameraConfig: true),
            "placeholder visuals may keep local camera facing when configured");

        AssertFalse(
            FloatingHeadRotationRules.ShouldFaceLocalCamera(
                FloatingHeadVisualSourceKind.RuntimeDetachedHead,
                faceCameraConfig: true),
            "runtime detached head visuals must follow remote spectator rotation instead of local camera facing");

        AssertTrue(
            FloatingHeadRotationRules.DefaultRuntimeDetachedHeadPitchOffsetDegrees == -90f,
            "default detached-head pitch correction should match the calibrated runtime template orientation");

        AssertTrue(
            FloatingHeadRotationRules.DefaultRuntimeDetachedHeadYawOffsetDegrees == 360f,
            "default detached-head yaw correction should match the calibrated runtime template orientation");

        AssertTrue(
            FloatingHeadRotationRules.DefaultRuntimeDetachedHeadRollOffsetDegrees == 0f,
            "default detached-head roll correction should match the calibrated runtime template orientation");
    }

    private static void FloatingHeadFrameUpdateRequiresFullUpdateWithoutCache()
    {
        AssertTrue(
            FloatingHeadFrameUpdateRules.ShouldRunFullVisualUpdate(
                currentFrame: 10,
                lastFullUpdateFrame: 10,
                hasCachedVisualState: false),
            "pre-cull should run a full visual update when no cached visual state exists");
    }

    private static void FloatingHeadFrameUpdateUsesCameraOnlyAfterSameFrameFullUpdate()
    {
        AssertFalse(
            FloatingHeadFrameUpdateRules.ShouldRunFullVisualUpdate(
                currentFrame: 10,
                lastFullUpdateFrame: 10,
                hasCachedVisualState: true),
            "pre-cull should use camera-only pose refresh after same-frame full update");
    }

    private static void FloatingHeadFrameUpdateRequiresFullUpdateOnNewFrame()
    {
        AssertTrue(
            FloatingHeadFrameUpdateRules.ShouldRunFullVisualUpdate(
                currentFrame: 11,
                lastFullUpdateFrame: 10,
                hasCachedVisualState: true),
            "pre-cull should run a full visual update when no full update happened this frame");
    }

    private static void SpeakingWithZeroAmplitudeUsesFallbackPulseLevel()
    {
        float voiceLevel = FloatingHeadVoiceScaleRules.ResolveTargetVoiceLevel(
            hasVoiceData: true,
            isSpeaking: true,
            amplitude: 0f,
            minimumSpeakingVoiceLevel: 0.75f);

        AssertTrue(
            Math.Abs(voiceLevel - 0.75f) < 0.0001f,
            $"speaking with zero amplitude should use the configured fallback pulse level. Got {voiceLevel}.");

        float scaleMultiplier = FloatingHeadVoiceScaleRules.ResolveScaleMultiplier(
            silenceScaleMultiplier: 1f,
            speakingScaleMultiplier: 1.35f,
            voiceLevel,
            pulseWhenSpeaking: true,
            speakingPulseAmount: 0.16f,
            pulse01: 1f);

        AssertTrue(
            scaleMultiplier > 1.30f,
            $"speaking with zero amplitude should visibly scale the marker. Got {scaleMultiplier}.");
    }

    private static void SpeakingWithLowPositiveAmplitudeUsesAmplitudeInsteadOfFallback()
    {
        float voiceLevel = FloatingHeadVoiceScaleRules.ResolveTargetVoiceLevel(
            hasVoiceData: true,
            isSpeaking: true,
            amplitude: 0.12f,
            minimumSpeakingVoiceLevel: 0.55f);

        AssertTrue(
            Math.Abs(voiceLevel - 0.12f) < 0.0001f,
            $"positive amplitude should drive voice level instead of being forced to fallback. Got {voiceLevel}.");
    }

    private static void PositiveAmplitudeWithoutSpeakingDoesNotDriveVoiceLevel()
    {
        float voiceLevel = FloatingHeadVoiceScaleRules.ResolveTargetVoiceLevel(
            hasVoiceData: true,
            isSpeaking: false,
            amplitude: 0.31f,
            minimumSpeakingVoiceLevel: 0.75f);

        AssertTrue(
            Math.Abs(voiceLevel) < 0.0001f,
            $"positive amplitude should not scale a silent spectator head unless IsSpeaking is true. Got {voiceLevel}.");
    }

    private static void SilentVoiceStateUsesNoPulseLevel()
    {
        float voiceLevel = FloatingHeadVoiceScaleRules.ResolveTargetVoiceLevel(
            hasVoiceData: true,
            isSpeaking: false,
            amplitude: 0f,
            minimumSpeakingVoiceLevel: 0.75f);

        AssertTrue(
            Math.Abs(voiceLevel) < 0.0001f,
            $"silent voice state should not pulse when both speaking and amplitude are zero. Got {voiceLevel}.");
    }

    private static void VoiceActivitySyncDisablesLocalVisualFallback()
    {
        AssertFalse(
            FloatingHeadVoiceActivityRules.ShouldUseLocalFallback(
                voiceActivitySyncEnabled: true,
                hasNetworkService: true),
            "remote visual voice scaling should not fall back to listener-side playback state when network voice sync is enabled");
        AssertTrue(
            FloatingHeadVoiceActivityRules.ShouldUseLocalFallback(
                voiceActivitySyncEnabled: false,
                hasNetworkService: true),
            "local fallback should remain available when voice activity sync is disabled");
        AssertTrue(
            FloatingHeadVoiceActivityRules.ShouldUseLocalFallback(
                voiceActivitySyncEnabled: true,
                hasNetworkService: false),
            "local fallback should remain available for non-networked/test visual contexts");
    }

    private static void VoiceLevelRiseUsesAttackSmoothing()
    {
        float smoothTime = FloatingHeadVoiceScaleRules.ResolveVoiceSmoothTime(
            currentLevel: 0.1f,
            targetLevel: 0.8f,
            attackSmoothTime: 0.01f,
            releaseSmoothTime: 0.05f);

        AssertTrue(
            Math.Abs(smoothTime - 0.01f) < 0.0001f,
            $"rising voice level should use attack smoothing. Got {smoothTime}.");
    }

    private static void VoiceLevelFallUsesReleaseSmoothing()
    {
        float smoothTime = FloatingHeadVoiceScaleRules.ResolveVoiceSmoothTime(
            currentLevel: 0.8f,
            targetLevel: 0f,
            attackSmoothTime: 0.01f,
            releaseSmoothTime: 0.02f);

        AssertTrue(
            Math.Abs(smoothTime - 0.02f) < 0.0001f,
            $"falling voice level should use release smoothing. Got {smoothTime}.");
    }

    private static void RemoteVoiceActivityRegistryStoresAndClearsStates()
    {
        RemoteVoiceActivityRegistry registry = new RemoteVoiceActivityRegistry();
        registry.Update(new VoiceActivityState(
            hasData: true,
            isSpeaking: true,
            amplitude: 0.33f,
            volume: 1f,
            clientId: 3,
            slotId: 2,
            timestampTicks: 100));

        AssertTrue(registry.TryGet(3, out VoiceActivityState stored), "remote voice activity should be stored");
        AssertTrue(stored.IsSpeaking, "stored voice activity should preserve speaking state");
        AssertTrue(Math.Abs(stored.Amplitude - 0.33f) < 0.0001f, $"stored amplitude should be preserved. Got {stored.Amplitude}.");

        registry.Update(new VoiceActivityState(
            hasData: false,
            isSpeaking: false,
            amplitude: 0f,
            volume: 0f,
            clientId: 3,
            slotId: 2,
            timestampTicks: 101));

        AssertFalse(registry.TryGet(3, out _), "no-data voice activity should clear remote voice state");
    }

    private static void RemoteVoiceActivityRegistryIgnoresOlderTimestamps()
    {
        RemoteVoiceActivityRegistry registry = new RemoteVoiceActivityRegistry();
        registry.Update(new VoiceActivityState(
            hasData: true,
            isSpeaking: true,
            amplitude: 0.80f,
            volume: 1f,
            clientId: 3,
            slotId: 2,
            timestampTicks: 200));
        registry.Update(new VoiceActivityState(
            hasData: true,
            isSpeaking: true,
            amplitude: 0.10f,
            volume: 1f,
            clientId: 3,
            slotId: 2,
            timestampTicks: 100));

        AssertTrue(registry.TryGet(3, out VoiceActivityState stored), "newer remote voice activity should remain stored");
        AssertTrue(
            Math.Abs(stored.Amplitude - 0.80f) < 0.0001f,
            $"older packets should not overwrite newer voice activity. Got {stored.Amplitude}.");
    }

    private static void RemoteVoiceActivityFreshnessUsesReceiveTimeNotSenderClock()
    {
        RemoteVoiceActivityRegistry registry = new RemoteVoiceActivityRegistry();
        registry.Update(
            new VoiceActivityState(
                hasData: true,
                isSpeaking: true,
                amplitude: 0.45f,
                volume: 1f,
                clientId: 3,
                slotId: 2,
                timestampTicks: 100),
            receivedAtTicks: 10_000);

        AssertTrue(
            registry.TryGet(3, out VoiceActivityState stored, out long receivedAtTicks),
            "remote voice activity should expose local receive time");
        AssertTrue(stored.IsSpeaking, "stored voice activity should preserve speaking state");
        AssertTrue(
            VoiceActivitySyncRules.IsFresh(receivedAtTicks, nowTicks: 10_050, staleTicks: 100),
            "freshness should use local receive time instead of the sender clock");
    }

    private static void VoiceActivitySyncRulesDetectAmplitudeChanges()
    {
        VoiceActivityState previous = new VoiceActivityState(
            hasData: true,
            isSpeaking: true,
            amplitude: 0.10f,
            volume: 1f,
            clientId: 2,
            slotId: 1,
            timestampTicks: 100);
        VoiceActivityState changed = new VoiceActivityState(
            hasData: true,
            isSpeaking: true,
            amplitude: 0.20f,
            volume: 1f,
            clientId: 2,
            slotId: 1,
            timestampTicks: 101);
        VoiceActivityState nearlySame = new VoiceActivityState(
            hasData: true,
            isSpeaking: true,
            amplitude: 0.105f,
            volume: 1f,
            clientId: 2,
            slotId: 1,
            timestampTicks: 102);

        AssertFalse(
            VoiceActivitySyncRules.ApproximatelyEquals(previous, changed),
            "meaningful amplitude changes should be sent for syllable-level visual scaling");
        AssertTrue(
            VoiceActivitySyncRules.ApproximatelyEquals(previous, nearlySame),
            "tiny amplitude jitter should be suppressed");
    }

    private static void VoiceActivitySyncRulesExpiresStaleState()
    {
        VoiceActivityState state = new VoiceActivityState(
            hasData: true,
            isSpeaking: true,
            amplitude: 0.8f,
            volume: 1f,
            clientId: 2,
            slotId: 1,
            timestampTicks: 100);

        AssertTrue(
            VoiceActivitySyncRules.IsFresh(state.TimestampTicks, nowTicks: 150, staleTicks: 100),
            "voice activity should remain fresh inside the stale window");
        AssertFalse(
            VoiceActivitySyncRules.IsFresh(state.TimestampTicks, nowTicks: 250, staleTicks: 100),
            "voice activity should expire after the stale window");
    }

    private static void VoiceActivityDebugLimiterSuppressesNoisyRepeats()
    {
        VoiceActivityDebugLimiter limiter = new VoiceActivityDebugLimiter(120);
        VoiceActivityState quiet = new VoiceActivityState(
            hasData: true,
            isSpeaking: false,
            amplitude: 0.02f,
            volume: 1f,
            clientId: 1,
            slotId: 1,
            timestampTicks: 100);
        VoiceActivityState smallJitter = new VoiceActivityState(
            hasData: true,
            isSpeaking: false,
            amplitude: 0.06f,
            volume: 1f,
            clientId: 1,
            slotId: 1,
            timestampTicks: 101);
        VoiceActivityState speaking = new VoiceActivityState(
            hasData: true,
            isSpeaking: true,
            amplitude: 0.07f,
            volume: 1f,
            clientId: 1,
            slotId: 1,
            timestampTicks: 102);
        VoiceActivityState louder = new VoiceActivityState(
            hasData: true,
            isSpeaking: true,
            amplitude: 0.40f,
            volume: 1f,
            clientId: 1,
            slotId: 1,
            timestampTicks: 103);

        AssertTrue(limiter.ShouldLog("received", 1, 10, quiet, isRelayed: false), "voice debug should log first state");
        AssertFalse(limiter.ShouldLog("received", 1, 20, smallJitter, isRelayed: false), "voice debug should suppress small repeated amplitude jitter");
        AssertTrue(limiter.ShouldLog("received", 1, 25, speaking, isRelayed: false), "voice debug should log speaking state changes");
        AssertTrue(limiter.ShouldLog("received", 1, 30, louder, isRelayed: false), "voice debug should log meaningful amplitude changes");
        AssertTrue(limiter.ShouldLog("received", 1, 160, louder, isRelayed: false), "voice debug should log again after the interval");
    }

    private static void VoiceDiagnosticsReportIncludesLocalAndPlayerVoiceState()
    {
        VoiceDiagnosticsSnapshot snapshot = new VoiceDiagnosticsSnapshot(
            hasRound: true,
            hasLocalPlayer: true,
            hasVoiceChatModule: true,
            localDissonancePlayerName: "local-voice",
            voiceChatMuted: false,
            voiceChatDeafened: false,
            localClientId: 0,
            localPlayerSlotId: 0,
            isLocalPlayerDead: true,
            isLocalPlayerSpectating: true,
            spectatedTargetClientId: 2,
            spectatedTargetPlayerSlotId: 2,
            includeAudioSourceDiagnostics: true,
            includeWalkieDiagnostics: true,
            players: new[]
            {
                new PlayerVoiceDiagnosticsSnapshot(
                    playerClientId: 2,
                    actualClientId: 2,
                    playerName: "Auuueser",
                    voicePlayerName: "voice-2",
                    isLocalPlayer: false,
                    isSpectatedTarget: true,
                    isPlayerControlled: true,
                    isPlayerDead: false,
                    hasVoicePlayerState: true,
                    voicePlayerIsConnected: true,
                    voicePlayerIsSpeaking: true,
                    voicePlayerIsLocallyMuted: false,
                    voiceAmplitude: 0.42f,
                    voiceVolume: 0.75f,
                    hasCurrentVoiceAudioSource: true,
                    voiceAudioIsPlaying: true,
                    voiceAudioMuted: false,
                    voiceAudioVolume: 0.60f,
                    voiceAudioSpatialBlend: 1.0f,
                    voiceAudioMixerName: "Voice2",
                    hasCurrentVoiceIngameSettings: true,
                    voiceIngameSettingsSet2D: false,
                    voicePlaybackPlayerName: "voice-2",
                    holdingWalkieTalkie: true,
                    speakingToWalkieTalkie: false,
                    voiceMuffledByEnemy: true),
            },
            timestampTicks: 123);

        string report = VoiceDiagnosticsReportFormatter.Build(snapshot);

        AssertContains(report, "Enhanced Spectator voice diagnostics", "voice diagnostics header should be present");
        AssertContains(report, "localClientId=0, localSlot=0, localDead=True, localSpectating=True", "local spectator state should be logged");
        AssertContains(report, "spectatedTargetClient=2, spectatedTargetSlot=2", "spectated target ids should be logged");
        AssertContains(report, "player slot=2 client=2 name=Auuueser local=False target=True controlled=True dead=False", "player identity should be logged");
        AssertContains(report, "voice present=True connected=True speaking=True muted=False amplitude=0.42 volume=0.75 voiceName=voice-2", "voice state should be logged");
        AssertContains(report, "audio present=True playing=True muted=False volume=0.60 spatialBlend=1.00 mixer=Voice2", "audio source diagnostics should be logged when enabled");
        AssertContains(report, "walkie holding=True speaking=False muffled=True", "walkie diagnostics should be logged when enabled");
    }

    private static void VoiceDiagnosticsReportOmitsAudioAndWalkieWhenDisabled()
    {
        VoiceDiagnosticsSnapshot snapshot = new VoiceDiagnosticsSnapshot(
            hasRound: true,
            hasLocalPlayer: true,
            hasVoiceChatModule: false,
            localDissonancePlayerName: string.Empty,
            voiceChatMuted: false,
            voiceChatDeafened: false,
            localClientId: 1,
            localPlayerSlotId: 1,
            isLocalPlayerDead: false,
            isLocalPlayerSpectating: false,
            spectatedTargetClientId: null,
            spectatedTargetPlayerSlotId: null,
            includeAudioSourceDiagnostics: false,
            includeWalkieDiagnostics: false,
            players: new[]
            {
                new PlayerVoiceDiagnosticsSnapshot(
                    playerClientId: 1,
                    actualClientId: 1,
                    playerName: "Ueser",
                    voicePlayerName: "voice-1",
                    isLocalPlayer: true,
                    isSpectatedTarget: false,
                    isPlayerControlled: true,
                    isPlayerDead: false,
                    hasVoicePlayerState: true,
                    voicePlayerIsConnected: true,
                    voicePlayerIsSpeaking: false,
                    voicePlayerIsLocallyMuted: false,
                    voiceAmplitude: 0f,
                    voiceVolume: 1f,
                    hasCurrentVoiceAudioSource: true,
                    voiceAudioIsPlaying: false,
                    voiceAudioMuted: false,
                    voiceAudioVolume: 1f,
                    voiceAudioSpatialBlend: 1f,
                    voiceAudioMixerName: "Voice1",
                    hasCurrentVoiceIngameSettings: true,
                    voiceIngameSettingsSet2D: false,
                    voicePlaybackPlayerName: "voice-1",
                    holdingWalkieTalkie: true,
                    speakingToWalkieTalkie: true,
                    voiceMuffledByEnemy: false),
            },
            timestampTicks: 124);

        string report = VoiceDiagnosticsReportFormatter.Build(snapshot);

        AssertDoesNotContain(report, "audio present=", "audio diagnostics should be omitted when disabled");
        AssertDoesNotContain(report, "walkie holding=", "walkie diagnostics should be omitted when disabled");
    }

    private static void VoiceDiagnosticsReportIncludesTimestampAndEmptyPlayerNotice()
    {
        VoiceDiagnosticsSnapshot snapshot = new VoiceDiagnosticsSnapshot(
            hasRound: true,
            hasLocalPlayer: true,
            hasVoiceChatModule: true,
            localDissonancePlayerName: "local-voice",
            voiceChatMuted: false,
            voiceChatDeafened: false,
            localClientId: 1,
            localPlayerSlotId: 1,
            isLocalPlayerDead: false,
            isLocalPlayerSpectating: false,
            spectatedTargetClientId: null,
            spectatedTargetPlayerSlotId: null,
            includeAudioSourceDiagnostics: true,
            includeWalkieDiagnostics: true,
            players: Array.Empty<PlayerVoiceDiagnosticsSnapshot>(),
            timestampTicks: 456);

        string report = VoiceDiagnosticsReportFormatter.Build(snapshot);

        AssertContains(report, "timestampTicks=456", "voice diagnostics should include a timestamp for log correlation");
        AssertContains(report, "no player voice rows captured", "voice diagnostics should say when player rows are empty");
    }

    private static void SpectatorVoiceRoutingRequiresEnabledLivingLocalWatchedTarget()
    {
        RemoteSpectatorInfo watchingLocal = new RemoteSpectatorInfo(
            spectatorClientId: 1,
            spectatorSlotId: 1,
            isWatchingLocalPlayer: true,
            lastObservedTicks: 100,
            poseState: null);
        RemoteSpectatorInfo watchingOther = new RemoteSpectatorInfo(
            spectatorClientId: 2,
            spectatorSlotId: 2,
            isWatchingLocalPlayer: false,
            lastObservedTicks: 100,
            poseState: null);

        AssertTrue(
            SpectatorVoiceRoutingRules.ShouldRouteToLocalTarget(
                featureEnabled: true,
                hasLocalPlayer: true,
                isLocalPlayerDead: false,
                watchingLocal),
            "voice routing should allow a remote dead spectator watching the living local player");
        AssertFalse(
            SpectatorVoiceRoutingRules.ShouldRouteToLocalTarget(
                featureEnabled: false,
                hasLocalPlayer: true,
                isLocalPlayerDead: false,
                watchingLocal),
            "voice routing should remain disabled unless explicitly configured on");
        AssertFalse(
            SpectatorVoiceRoutingRules.ShouldRouteToLocalTarget(
                featureEnabled: true,
                hasLocalPlayer: true,
                isLocalPlayerDead: true,
                watchingLocal),
            "voice routing should not override vanilla dead-local voice rules");
        AssertFalse(
            SpectatorVoiceRoutingRules.ShouldRouteToLocalTarget(
                featureEnabled: true,
                hasLocalPlayer: true,
                isLocalPlayerDead: false,
                watchingOther),
            "voice routing should not make spectators audible to unrelated local players");
    }

    private static void SpectatorVoiceRoutingAudienceModesSelectExpectedListeners()
    {
        AssertTrue(
            SpectatorVoiceRoutingRules.ShouldRouteToLocalPlayer(
                featureEnabled: true,
                hasLocalPlayer: true,
                isLocalPlayerDead: false,
                isRemoteSpectating: true,
                isWatchingLocalPlayer: false,
                SpectatorVoiceAudienceMode.AllModdedPlayers),
            "all-modded voice mode should allow alive unrelated modded listeners");
        AssertTrue(
            SpectatorVoiceRoutingRules.ShouldRouteToLocalPlayer(
                featureEnabled: true,
                hasLocalPlayer: true,
                isLocalPlayerDead: true,
                isRemoteSpectating: true,
                isWatchingLocalPlayer: false,
                SpectatorVoiceAudienceMode.AllModdedPlayers),
            "all-modded voice mode should allow dead unrelated modded listeners");
        AssertFalse(
            SpectatorVoiceRoutingRules.ShouldRouteToLocalPlayer(
                featureEnabled: true,
                hasLocalPlayer: true,
                isLocalPlayerDead: false,
                isRemoteSpectating: true,
                isWatchingLocalPlayer: false,
                SpectatorVoiceAudienceMode.WatchedTargetOnly),
            "watched-target-only mode should still require the spectator to watch the local player");
        AssertTrue(
            SpectatorVoiceRoutingRules.ShouldRouteToLocalPlayer(
                featureEnabled: true,
                hasLocalPlayer: true,
                isLocalPlayerDead: false,
                isRemoteSpectating: true,
                isWatchingLocalPlayer: false,
                SpectatorVoiceAudienceMode.AliveModdedPlayersOnly),
            "alive-only voice mode should allow alive modded listeners");
        AssertFalse(
            SpectatorVoiceRoutingRules.ShouldRouteToLocalPlayer(
                featureEnabled: true,
                hasLocalPlayer: true,
                isLocalPlayerDead: true,
                isRemoteSpectating: true,
                isWatchingLocalPlayer: false,
                SpectatorVoiceAudienceMode.AliveModdedPlayersOnly),
            "alive-only voice mode should reject dead listeners");
        AssertTrue(
            SpectatorVoiceRoutingRules.ShouldRouteToLocalPlayer(
                featureEnabled: true,
                hasLocalPlayer: true,
                isLocalPlayerDead: true,
                isRemoteSpectating: true,
                isWatchingLocalPlayer: false,
                SpectatorVoiceAudienceMode.DeadModdedPlayersOnly),
            "dead-only voice mode should allow dead modded listeners");
        AssertFalse(
            SpectatorVoiceRoutingRules.ShouldRouteToLocalPlayer(
                featureEnabled: true,
                hasLocalPlayer: true,
                isLocalPlayerDead: false,
                isRemoteSpectating: true,
                isWatchingLocalPlayer: false,
                SpectatorVoiceAudienceMode.DeadModdedPlayersOnly),
            "dead-only voice mode should reject alive listeners");
    }

    private static void SpectatorVoiceRoutingRequiresRemoteCapabilityOptIn()
    {
        ModPeerCapability optIn = Capability(1, supportsSpectatorVoiceToTarget: true);
        ModPeerCapability noOptIn = Capability(2, supportsSpectatorVoiceToTarget: false);

        AssertTrue(
            ModPeerCapabilityRules.SupportsCurrentSpectatorVoiceToTarget(optIn),
            "voice routing should require a compatible remote peer with explicit voice routing capability");
        AssertFalse(
            ModPeerCapabilityRules.SupportsCurrentSpectatorVoiceToTarget(noOptIn),
            "voice routing should not treat target/voice-activity capability as voice routing opt-in");
    }

    private static void SpectatorVoicePlayerLookupCachePrefersClientIdOverSlotFallback()
    {
        SpectatorVoicePlayerLookupCache<string> cache = new SpectatorVoicePlayerLookupCache<string>();
        cache.Store("slot-only", clientId: 5, slotId: 7);
        cache.Store("client-match", clientId: 2, slotId: 9);

        AssertTrue(cache.TryGet(clientId: 2, slotId: 7, out string player), "voice player lookup should find a cached player");
        AssertEqual("client-match", player, "voice player lookup should prefer actual client id over slot fallback");
    }

    private static void SpectatorVoicePlayerLookupCacheKeepsFirstClientMatch()
    {
        SpectatorVoicePlayerLookupCache<string> cache = new SpectatorVoicePlayerLookupCache<string>();
        cache.Store("first", clientId: 2, slotId: 2);
        cache.Store("second", clientId: 2, slotId: 3);

        AssertTrue(cache.TryGet(clientId: 2, slotId: 3, out string player), "voice player lookup should find duplicate client id");
        AssertEqual("first", player, "voice player lookup should keep first client match to preserve scan order");
    }

    private static void SpectatorVoicePlayerLookupCacheKeepsFirstSlotFallbackMatch()
    {
        SpectatorVoicePlayerLookupCache<string> cache = new SpectatorVoicePlayerLookupCache<string>();
        cache.Store("first", clientId: 2, slotId: 4);
        cache.Store("second", clientId: 3, slotId: 4);

        AssertTrue(cache.TryGet(clientId: 9, slotId: 4, out string player), "voice player lookup should find duplicate slot id fallback");
        AssertEqual("first", player, "voice player lookup should keep first slot fallback match to preserve scan order");
    }

    private static void SpectatorVoicePlayerLookupCacheClearRemovesStaleEntries()
    {
        SpectatorVoicePlayerLookupCache<string> cache = new SpectatorVoicePlayerLookupCache<string>();
        cache.Store("player", clientId: 2, slotId: 2);

        cache.Clear();

        AssertFalse(cache.TryGet(clientId: 2, slotId: 2, out _), "voice player lookup clear should remove cached entries");
    }

    private static void ModLogDebugEnabledReflectsConfiguredGate()
    {
        ModLog.SetDebugEnabled(false);
        AssertFalse(ModLog.IsDebugEnabled, "debug log gate should report disabled state");

        ModLog.SetDebugEnabled(true);
        AssertTrue(ModLog.IsDebugEnabled, "debug log gate should report enabled state");

        ModLog.SetDebugEnabled(false);
    }

    private static void SpectatorInputKeyCacheReusesResolvedBinding()
    {
        int resolveCalls = 0;
        SpectatorInputKeyCache cache = new SpectatorInputKeyCache(
            key =>
            {
                resolveCalls++;
                return new SpectatorInputKeyResolution(true, InputKey.F);
            });

        AssertTrue(cache.TryResolve(KeyCode.F, out InputKey first), "input key cache should resolve mapped key");
        AssertTrue(cache.TryResolve(KeyCode.F, out InputKey second), "input key cache should reuse mapped key");

        AssertEqual(InputKey.F, first, "input key cache should return resolved input key");
        AssertEqual(InputKey.F, second, "input key cache should keep cached input key");
        AssertEqual(1, resolveCalls, "input key cache should not remap unchanged configured key");
    }

    private static void SpectatorInputKeyCacheRefreshesWhenConfiguredKeyChanges()
    {
        int resolveCalls = 0;
        SpectatorInputKeyCache cache = new SpectatorInputKeyCache(
            key =>
            {
                resolveCalls++;
                return key == KeyCode.G
                    ? new SpectatorInputKeyResolution(true, InputKey.G)
                    : new SpectatorInputKeyResolution(true, InputKey.F);
            });

        AssertTrue(cache.TryResolve(KeyCode.F, out InputKey first), "input key cache should resolve first configured key");
        AssertTrue(cache.TryResolve(KeyCode.G, out InputKey second), "input key cache should resolve changed configured key");

        AssertEqual(InputKey.F, first, "input key cache should return first mapping");
        AssertEqual(InputKey.G, second, "input key cache should update mapping after config change");
        AssertEqual(2, resolveCalls, "input key cache should remap only when configured key changes");
    }

    private static void SpectatorInputKeyCacheCachesUnmappedBindings()
    {
        int resolveCalls = 0;
        SpectatorInputKeyCache cache = new SpectatorInputKeyCache(
            key =>
            {
                resolveCalls++;
                return new SpectatorInputKeyResolution(false, InputKey.None);
            });

        AssertFalse(cache.TryResolve(KeyCode.None, out InputKey first), "input key cache should reject unmapped key");
        AssertFalse(cache.TryResolve(KeyCode.None, out InputKey second), "input key cache should reuse unmapped key");

        AssertEqual(InputKey.None, first, "input key cache should return no input key for unmapped binding");
        AssertEqual(InputKey.None, second, "input key cache should keep unmapped input key");
        AssertEqual(1, resolveCalls, "input key cache should cache unmapped configured key");
    }

    private static void SpectatorVoiceDistanceAttenuationScalesVolumeByDistance()
    {
        AssertNear(
            1f,
            SpectatorVoiceDistanceAttenuation.CalculateVolume(
                baseVolume: 1f,
                attenuationEnabled: true,
                distance: 1f,
                minDistance: 2f,
                maxDistance: 12f,
                rolloffPower: 1f,
                minimumVolume: 0f),
            "distance attenuation should keep full volume inside the minimum distance");
        AssertNear(
            0f,
            SpectatorVoiceDistanceAttenuation.CalculateVolume(
                baseVolume: 1f,
                attenuationEnabled: true,
                distance: 12f,
                minDistance: 2f,
                maxDistance: 12f,
                rolloffPower: 1f,
                minimumVolume: 0f),
            "distance attenuation should reach minimum volume at max distance");
        AssertNear(
            0.5f,
            SpectatorVoiceDistanceAttenuation.CalculateVolume(
                baseVolume: 1f,
                attenuationEnabled: true,
                distance: 7f,
                minDistance: 2f,
                maxDistance: 12f,
                rolloffPower: 1f,
                minimumVolume: 0f),
            "distance attenuation should linearly attenuate with rolloff power 1");
        AssertNear(
            0.75f,
            SpectatorVoiceDistanceAttenuation.CalculateVolume(
                baseVolume: 1f,
                attenuationEnabled: true,
                distance: 7f,
                minDistance: 2f,
                maxDistance: 12f,
                rolloffPower: 2f,
                minimumVolume: 0f),
            "distance attenuation should use rolloff power for a softer near-field falloff");
        AssertNear(
            0.8f,
            SpectatorVoiceDistanceAttenuation.CalculateVolume(
                baseVolume: 0.8f,
                attenuationEnabled: false,
                distance: 100f,
                minDistance: 2f,
                maxDistance: 12f,
                rolloffPower: 1f,
                minimumVolume: 0f),
            "disabled distance attenuation should keep the configured route volume");
    }

    private static void SpectatorVoiceSpatializationRemapsPoseIntoActualListenerFrame()
    {
        const float halfSqrt = 0.70710677f;
        Quaternion desiredListenerRotation = new Quaternion(0f, halfSqrt, 0f, halfSqrt);
        Vector3 desiredListenerPosition = new Vector3(10f, 2f, -3f);
        Vector3 remotePosePosition = new Vector3(15f, 3f, -3f);
        Quaternion actualListenerRotation = Quaternion.identity;
        Vector3 actualListenerPosition = new Vector3(-2f, 4f, 8f);

        Vector3 playbackPosition = SpectatorVoiceSpatializationRules.ResolvePlaybackSourcePosition(
            remotePosePosition,
            desiredListenerPosition,
            desiredListenerRotation,
            actualListenerPosition,
            actualListenerRotation);

        AssertVectorNear(
            new Vector3(-2f, 5f, 13f),
            playbackPosition,
            "voice source should preserve the remote pose direction and distance relative to the rendered spectator camera when Unity uses a different AudioListener");
    }

    private static void SpectatorVoiceSpatializationPreservesWorldPoseWhenFramesMatch()
    {
        Quaternion listenerRotation = Quaternion.identity;
        Vector3 listenerPosition = new Vector3(3f, -2f, 9f);
        Vector3 remotePosePosition = new Vector3(6f, -1f, 12f);

        Vector3 playbackPosition = SpectatorVoiceSpatializationRules.ResolvePlaybackSourcePosition(
            remotePosePosition,
            listenerPosition,
            listenerRotation,
            listenerPosition,
            listenerRotation);

        AssertVectorNear(
            remotePosePosition,
            playbackPosition,
            "voice source should stay at the remote pose when the rendered listener and actual AudioListener are the same frame");
    }

    private static void SpectatorVoiceRouteDiagnosticsAreRateLimited()
    {
        SpectatorVoiceRouteDiagnosticLimiter limiter = new SpectatorVoiceRouteDiagnosticLimiter(60);

        AssertTrue(
            limiter.ShouldLog(
                spectatorClientId: 1,
                frame: 10,
                poseAvailable: true,
                fallbackTo2D: false,
                distance: 3f,
                finalVolume: 0.9f,
                spatialBlend: 1f,
                set2D: false),
            "voice route diagnostics should log the first apply for a spectator");
        AssertFalse(
            limiter.ShouldLog(
                spectatorClientId: 1,
                frame: 20,
                poseAvailable: true,
                fallbackTo2D: false,
                distance: 3.2f,
                finalVolume: 0.88f,
                spatialBlend: 1f,
                set2D: false),
            "voice route diagnostics should suppress near-identical apply logs before the interval");
        AssertTrue(
            limiter.ShouldLog(
                spectatorClientId: 1,
                frame: 25,
                poseAvailable: true,
                fallbackTo2D: false,
                distance: 9f,
                finalVolume: 0.35f,
                spatialBlend: 1f,
                set2D: false),
            "voice route diagnostics should log significant distance or volume changes");
        AssertTrue(
            limiter.ShouldLog(
                spectatorClientId: 1,
                frame: 30,
                poseAvailable: false,
                fallbackTo2D: true,
                distance: 0f,
                finalVolume: 1f,
                spatialBlend: 0f,
                set2D: true),
            "voice route diagnostics should log mode changes such as fallback to 2D");
        AssertTrue(
            limiter.ShouldLog(
                spectatorClientId: 1,
                frame: 90,
                poseAvailable: false,
                fallbackTo2D: true,
                distance: 0f,
                finalVolume: 1f,
                spatialBlend: 0f,
                set2D: true),
            "voice route diagnostics should log again after the interval");
    }

    private static GameSpectatorSnapshot CreateSpectatorSnapshot(
        ulong? spectatedPlayerSlotId,
        ulong? spectatedPlayerActualClientId)
    {
        return new GameSpectatorSnapshot(
            hasRound: true,
            hasLocalPlayer: true,
            isLocalPlayerDead: true,
            hasBegunSpectating: true,
            hasSpectatedTarget: true,
            isGameOverOverride: false,
            isSpectateCameraActive: true,
            spectateCamera: null,
            anchor: null,
            localPlayerSlotId: 0,
            localPlayerActualClientId: 0,
            spectatedPlayerSlotId,
            spectatedPlayerActualClientId);
    }

    private sealed class CountingGameSpectatorAdapter : IGameSpectatorAdapter
    {
        public int SnapshotCalls { get; private set; }

        public bool SnapshotResult { get; set; } = true;

        public bool ThrowOnSnapshot { get; set; }

        public GameSpectatorSnapshot Snapshot { get; set; } =
            CreateSpectatorSnapshot(spectatedPlayerSlotId: 1, spectatedPlayerActualClientId: 1);

        public SpectatorState ReadSpectatorState()
        {
            return SpectatorState.Unavailable;
        }

        public bool TryGetLocalSpectatorSnapshot(out GameSpectatorSnapshot snapshot)
        {
            SnapshotCalls++;
            if (ThrowOnSnapshot)
            {
                throw new InvalidOperationException("snapshot adapter failed");
            }

            snapshot = SnapshotResult ? Snapshot : GameSpectatorSnapshot.Unavailable;
            return SnapshotResult;
        }

        public bool IsLocalPlayerSpectating()
        {
            return false;
        }

        public bool TryGetSpectatedPlayerAnchor(out Transform? anchor)
        {
            anchor = null;
            return false;
        }

        public bool TryGetSpectateCamera(out Camera? camera)
        {
            camera = null;
            return false;
        }

        public bool TryGetActiveCamera(out Camera? camera)
        {
            camera = null;
            return false;
        }

        public bool IsGameOverSpectateOverrideActive()
        {
            return false;
        }

        public bool IsLocalQuickMenuOpen()
        {
            return false;
        }

        public bool IsValidSpectateTarget(object? target)
        {
            _ = target;
            return false;
        }

        public bool TryGetSpectatedPlayerId(out ulong slotId, out ulong actualClientId)
        {
            slotId = 0;
            actualClientId = 0;
            return false;
        }

        public bool TryGetLocalPlayerIdentity(out ulong clientId, out ulong slotId)
        {
            clientId = 0;
            slotId = 0;
            return false;
        }

        public bool TryGetPlayerDisplayName(ulong clientId, ulong slotId, out string displayName)
        {
            _ = clientId;
            _ = slotId;
            displayName = string.Empty;
            return false;
        }

        public bool TryGetLocalVoicePlayerName(out string voicePlayerName)
        {
            voicePlayerName = string.Empty;
            return false;
        }

        public bool TryGetLocalPlayerHeadPoint(out Transform? anchor)
        {
            anchor = null;
            return false;
        }

        public bool TryGetLocalPlayerHeadAnchor(out Transform? anchor)
        {
            anchor = null;
            return false;
        }

        public bool TryGetLocalPlayerHeadAnchorPosition(out Vector3 position)
        {
            position = Vector3.zero;
            return false;
        }
    }

    private sealed class RuntimeTickFeature : IFeatureModule, IRuntimeTickable
    {
        private readonly List<string>? _calls;
        private readonly string _name;

        public RuntimeTickFeature()
        {
            _name = string.Empty;
        }

        public RuntimeTickFeature(string name, List<string> calls)
        {
            _name = name;
            _calls = calls;
        }

        public void Initialize()
        {
        }

        public void Dispose()
        {
        }

        public void Tick()
        {
            _calls?.Add(_name);
        }
    }

    private sealed class FullRuntimeFeature :
        IFeatureModule,
        IRuntimeTickable,
        IRuntimeLateTickable,
        IRuntimeCameraPreCullTickable,
        IRuntimeGuiTickable
    {
        public void Initialize()
        {
        }

        public void Dispose()
        {
        }

        public void Tick()
        {
        }

        public void LateTick()
        {
        }

        public void CameraPreCullTick(Camera camera)
        {
            _ = camera;
        }

        public void GuiTick()
        {
        }
    }

    private static void AssertSequence(IReadOnlyList<ulong> expected, IReadOnlyList<ulong> actual, string message)
    {
        if (expected.Count != actual.Count)
        {
            throw new InvalidOperationException($"{message}. Expected count {expected.Count}, got {actual.Count}.");
        }

        for (int index = 0; index < expected.Count; index++)
        {
            if (expected[index] != actual[index])
            {
                throw new InvalidOperationException($"{message}. Expected {expected[index]} at index {index}, got {actual[index]}.");
            }
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertStruct<T>(string message)
        where T : struct
    {
        _ = message;
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void AssertSame<T>(T expected, T actual, string message)
        where T : class
    {
        if (!ReferenceEquals(expected, actual))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message}. Expected {expected}, got {actual}.");
        }
    }

    private static void AssertNear(float expected, float actual, string message)
    {
        if (MathF.Abs(expected - actual) > 0.0001f)
        {
            throw new InvalidOperationException($"{message}. Expected {expected}, got {actual}.");
        }
    }

    private static void AssertVectorNear(Vector3 expected, Vector3 actual, string message)
    {
        float dx = expected.x - actual.x;
        float dy = expected.y - actual.y;
        float dz = expected.z - actual.z;
        float distanceSquared = (dx * dx) + (dy * dy) + (dz * dz);
        if (distanceSquared > 0.00000001f)
        {
            throw new InvalidOperationException($"{message}. Expected ({expected.x}, {expected.y}, {expected.z}), got ({actual.x}, {actual.y}, {actual.z}).");
        }
    }

    private static void AssertContains(string text, string expected, string message)
    {
        if (text.IndexOf(expected, StringComparison.Ordinal) < 0)
        {
            throw new InvalidOperationException($"{message}. Expected to find '{expected}' in:{Environment.NewLine}{text}");
        }
    }

    private static void AssertDoesNotContain(string text, string unexpected, string message)
    {
        if (text.IndexOf(unexpected, StringComparison.Ordinal) >= 0)
        {
            throw new InvalidOperationException($"{message}. Did not expect to find '{unexpected}' in:{Environment.NewLine}{text}");
        }
    }
}
