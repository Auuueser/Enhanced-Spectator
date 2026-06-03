# Usage

Enhanced Spectator currently implements client-local spectator freecam, modded-peer spectator presence, runtime floating-head/name visuals, and configurable routed spectator voice. Vanilla still owns death state, target switching, connection approval, and round lifecycle.

## Controls

Default controls:

| Action | Default |
| --- | --- |
| Toggle enhanced freecam | `F6` |
| Recenter near current target | `R` |
| Return to vanilla spectator view | `F7` |
| Move | `W` / `A` / `S` / `D` |
| Move up / down | `Space` / `LeftControl` |
| Look | Mouse |
| Fast movement | Hold `LeftShift` |
| Slow movement | Hold `LeftAlt` |

Forward and backward movement follows the camera look direction, so looking down while moving forward descends and looking up while moving forward ascends. The toggle, recenter, reset, fast, slow, ascend, and descend keys are configurable. WASD horizontal movement keys are fixed for the MVP.

While enhanced freecam is active, holding the configured ascend or descend key suppresses vanilla spectator target switching for that input frame. This keeps the default `Space` ascend key from being stolen by vanilla spectator controls.

When the player currently being watched disconnects and another living player is available, Enhanced Spectator invokes vanilla target switching once after disconnect cleanup so the spectator view does not remain attached to the disconnected player's off-map model.

## Config

BepInEx writes the config file after the plugin has run once.

| Entry | Default | Purpose |
| --- | --- | --- |
| `Spectator.Freecam.EnableEnhancedSpectator` | `true` | Master switch for enhanced spectator behavior. |
| `Spectator.Freecam.EnableFreecam` | `true` | Enables local freecam behavior. |
| `Spectator.Freecam.FreecamDefaultOn` | `true` | Enables freecam automatically after entering spectator state. |
| `Spectator.Freecam.FreecamRadius` | `8.0` | Maximum offset from the current target anchor. |
| `Spectator.Freecam.FreecamMoveSpeed` | `4.0` | Base movement speed. |
| `Spectator.Freecam.FreecamFastMoveMultiplier` | `2.5` | Fast movement multiplier. |
| `Spectator.Freecam.FreecamSlowMoveMultiplier` | `0.35` | Slow movement multiplier. |
| `Spectator.Freecam.FreecamLookSensitivity` | `1.0` | Mouse look sensitivity. |
| `Spectator.Freecam.FreecamSmoothTime` | `0.04` | Position smoothing time. Set to `0` to disable smoothing. |
| `Spectator.Freecam.ClampCameraToRadius` | `true` | Clamps camera offset to `FreecamRadius`. |
| `Spectator.Freecam.RecenterOnTargetSwitch` | `true` | Recenters when vanilla switches target. |
| `Spectator.Freecam.DisableDuringGameOverOverride` | `true` | Stops writing the camera during vanilla game-over override. |
| `Spectator.Freecam.Keys.ToggleFreecamKey` | `F6` | Toggle key. |
| `Spectator.Freecam.Keys.RecenterKey` | `R` | Recenter key. |
| `Spectator.Freecam.Keys.ResetToVanillaViewKey` | `F7` | Reset-to-vanilla key. |
| `Spectator.Freecam.Keys.FastMoveKey` | `LeftShift` | Fast movement key. |
| `Spectator.Freecam.Keys.SlowMoveKey` | `LeftAlt` | Slow movement key. |
| `Spectator.Freecam.Keys.AscendKey` | `Space` | Upward movement key. |
| `Spectator.Freecam.Keys.DescendKey` | `LeftControl` | Downward movement key. |
| `Logging.EnableDebugLogging` | `false` | Enables verbose Enhanced Spectator debug logs. |
| `Networking.EnableNetworking` | `true` | Enables mod-owned networking modules. |
| `Networking.EnableCapabilityHandshake` | `true` | Enables Unity Netcode custom messaging capability handshake. |
| `Networking.EnableSpectatorTargetSync` | `true` | Enables handshake-gated spectator target state sync. |
| `Networking.EnableSpectatorPoseSync` | `true` | Enables handshake-gated spectator camera pose sync for placeholder visuals. |
| `Networking.EnableHostRelay` | `true` | Enables host-mediated relay of compatible client spectator target, pose, identity, and voice-activity state to other modded clients. Required for Client A -> Client B visibility. |
| `Networking.SpectatorPoseSyncInterval` | `0.1` | Minimum seconds between spectator pose messages. Raise this to reduce traffic; lower it for tighter tracking. |
| `Networking.EnableVoiceActivitySync` | `true` | Enables visual-only voice activity sync so floating heads scale from the speaker's local microphone amplitude. This does not forward voice audio. |
| `Networking.VoiceActivitySyncInterval` | `0.066` | Minimum seconds between voice activity visual messages. Lower values react faster but send more metadata. |
| `Networking.VoiceActivityStaleSeconds` | `0.5` | Time before a received voice activity state expires, preventing dropped silence packets from leaving a head enlarged. |
| `Networking.DebugVoiceActivitySync` | `false` | Logs rate-limited voice activity send/receive/relay diagnostics. Requires `Networking.DebugNetworkMessages=true`; keep disabled outside targeted voice debugging. |
| `Networking.RepairVanillaConnectedPlayerState` | `true` | Repairs first-join/rejoin/revive cases where a connected peer is in vanilla `ClientPlayerList` but local player-script flags or live model renderer state are stale. This also runs in local-only sessions with an unmodded host. |
| `Networking.RepairVanillaPlayerNames` | `true` | Applies synced mod peer identity names, or a vanilla Steam lobby fallback when no mod peer identity is available, to repaired vanilla player scripts and the ESC player list when vanilla still exposes generic `Player #n` names. |
| `Networking.DebugPlayerStateRepair` | `false` | Logs each vanilla connected-player state repair for targeted multiplayer debugging. |
| `Networking.DebugNetworkMessages` | `false` | Enables verbose network message diagnostics. |
| `Networking.DebugPoseMessages` | `false` | Enables high-frequency pose observe/send/receive logs. Keep disabled outside targeted debugging. |
| `VoiceRouting.EnableSpectatorVoiceToTarget` | `true` | Enables routed dead-spectator voice between compatible Enhanced Spectator peers. Both peers must advertise support through capability handshake. |
| `VoiceRouting.SpectatorVoiceAudienceMode` | `AllModdedPlayers` | Controls who can hear routed dead spectator voice: `WatchedTargetOnly`, `AllModdedPlayers`, `AliveModdedPlayersOnly`, or `DeadModdedPlayersOnly`. |
| `VoiceRouting.SpectatorVoiceToTargetVolume` | `1.0` | Local playback volume used for eligible routed spectator voice. |
| `VoiceRouting.SpectatorVoiceUseRemotePosePosition` | `true` | Positions routed spectator voice at the synced remote spectator camera pose. Disable to force 2D local playback. |
| `VoiceRouting.SpectatorVoiceEnableDistanceAttenuation` | `true` | Lowers routed spectator voice volume by listener distance from the synced spectator pose. |
| `VoiceRouting.SpectatorVoiceMinDistance` | `2.0` | Distance in meters that keeps routed spectator voice at full configured volume. |
| `VoiceRouting.SpectatorVoiceMaxDistance` | `18.0` | Distance in meters where routed spectator voice reaches `SpectatorVoiceMinimumVolume`. |
| `VoiceRouting.SpectatorVoiceRolloffPower` | `1.25` | Distance attenuation curve. `1` is linear; higher values keep near voices louder and fade more near max distance. |
| `VoiceRouting.SpectatorVoiceMinimumVolume` | `0.0` | Minimum volume multiplier at or beyond `SpectatorVoiceMaxDistance`. |
| `VoiceRouting.SpectatorVoiceFallbackTo2DWhenPoseMissing` | `false` | Falls back to 2D routed voice if spectator pose data is temporarily unavailable. Disabled by default so relayed listeners do not hear stale global voice when pose sync is missing. |
| `VoiceRouting.DebugSpectatorVoiceRouting` | `false` | Logs spectator voice route enable/clear/skip diagnostics and rate-limited route-apply diagnostics including mode, playback source, remote source, listener, desired listener, remap status, distance, final volume, `spatialBlend`, and `set2D`. Requires `Logging.EnableDebugLogging=true`; keep disabled after positional voice is validated. |
| `Presence.EnableSpectatorPresenceDebug` | `true` | Enables remote spectator visibility inference for floating-head visuals. Despite the legacy name, this is now part of the visual feature path. |
| `Presence.DebugLogPresenceChanges` | `false` | Logs remote spectator visibility and watching-local-player changes. Requires `Logging.EnableDebugLogging`. |
| `ModelInspection.EnableModelInspection` | `false` | Enables key-triggered player model hierarchy inspection. |
| `ModelInspection.LogLocalPlayerModelOnKey` | `true` | Includes the local player in model inspection logs. |
| `ModelInspection.LogRemotePlayerModelsOnKey` | `true` | Includes remote players in model inspection logs. |
| `ModelInspection.InspectionKey` | `F8` | Runs one model inspection pass. |
| `ModelInspection.IncludeRendererBounds` | `true` | Includes renderer bounds center and size. |
| `ModelInspection.IncludeMaterials` | `false` | Includes material names when explicitly enabled. |
| `ModelInspection.MaxTransformDepth` | `8` | Limits head-related transform path traversal depth. |
| `HeadSourceInspection.EnableRuntimeHeadSourceInspection` | `false` | Enables key-triggered runtime inspection of `DeadBodyInfo.detachedHeadObject` candidates. |
| `HeadSourceInspection.InspectionKey` | `F10` | Runs one detached-head source inspection pass. |
| `HeadSourceInspection.IncludeRendererBounds` | `true` | Includes detached-head renderer bounds center and size. |
| `HeadSourceInspection.IncludeMaterials` | `false` | Includes detached-head material names when explicitly enabled. |
| `HeadSourceInspection.MaxTransformDepth` | `6` | Limits transform traversal below the detached-head object. |
| `VoiceDiagnostics.EnableVoiceDiagnostics` | `false` | Enables key-triggered read-only voice diagnostics. This does not route, forward, or modify voice. |
| `VoiceDiagnostics.InspectionKey` | `F11` | Runs one voice diagnostics log pass. |
| `VoiceDiagnostics.LogLocalVoiceStateOnKey` | `true` | Includes the local player in voice diagnostics logs. |
| `VoiceDiagnostics.LogRemoteVoiceStatesOnKey` | `true` | Includes remote players in voice diagnostics logs. |
| `VoiceDiagnostics.IncludeAudioSourceDetails` | `true` | Includes mapped `AudioSource` playback details in voice diagnostics logs. |
| `VoiceDiagnostics.IncludeWalkieDiagnostics` | `true` | Includes walkie-talkie voice flags in voice diagnostics logs. |
| `FloatingHead.EnableFloatingHeadVisuals` | `true` | Enables local placeholder visuals for remote modded spectators. |
| `FloatingHead.EnablePlaceholderVisuals` | `true` | Uses runtime-created placeholder markers instead of real head meshes. |
| `FloatingHead.UseRuntimeDetachedHeadVisuals` | `true` | Uses the loaded ghost-girl ragdoll detached-head template as a runtime-only marker source when available. Placeholder visuals remain the fallback when the runtime source is unavailable. |
| `FloatingHead.RuntimeDetachedHeadScale` | `0.35` | Scale applied to runtime detached-head visuals. |
| `FloatingHead.RuntimeDetachedHeadPitchOffset` | `-90` | Pitch correction applied after the remote spectator camera rotation. Calibrated from live testing. |
| `FloatingHead.RuntimeDetachedHeadYawOffset` | `360` | Yaw correction applied after the remote spectator camera rotation. Calibrated from live testing. |
| `FloatingHead.RuntimeDetachedHeadRollOffset` | `0` | Roll correction applied after the remote spectator camera rotation. Calibrated from live testing. |
| `FloatingHead.FallbackToPlaceholderWhenDetachedHeadUnavailable` | `true` | Uses the placeholder marker when the runtime detached-head template is unavailable. |
| `FloatingHead.ShowRemoteSpectators` | `true` | Shows remote modded players while they are in spectator state and pose sync is available. |
| `FloatingHead.ShowOnlySpectatorsWatchingMe` | `false` | Restricts placeholders to remote spectators whose current target is the local player. Leave disabled to see all remote spectators. |
| `FloatingHead.ShowDeadSpectatorsToAlivePlayers` | `true` | Allows living local players to see remote spectator placeholders. |
| `FloatingHead.ShowDeadSpectatorsToDeadPlayers` | `true` | Allows dead or spectating local players to see remote spectator placeholders. |
| `FloatingHead.MaxFloatingHeadsVisible` | `8` | Maximum remote spectator placeholders shown at once. Set to `0` to hide all placeholders. |
| `FloatingHead.VisualStyle` | `Sphere` | Runtime-only marker style: `Sphere`, `Billboard`, or `Ring`. Sphere is the default world-space marker style. |
| `FloatingHead.PlaceholderScale` | `0.18` | World scale for each placeholder sphere. |
| `FloatingHead.BillboardSize` | `0.22` | World size for billboard and ring marker styles. |
| `FloatingHead.BaseAlpha` | `1.0` | Marker material alpha where the runtime shader supports it. |
| `FloatingHead.UseUnlitMaterial` | `true` | Prefers an unlit runtime material for placeholder visibility. |
| `FloatingHead.EnableDepthTest` | `true` | Keeps normal depth testing when the runtime shader supports it. Disable only when diagnosing scene occlusion. |
| `FloatingHead.RingRadius` | `0.45` | Horizontal ring radius around the local head anchor. |
| `FloatingHead.HeightOffset` | `0.25` | Vertical offset above the local head anchor. |
| `FloatingHead.UseCameraVisiblePlacement` | `false` | Diagnostic fallback for no-pose markers. When enabled, fallback placeholders are biased into the local active camera view. |
| `FloatingHead.CameraForwardOffset` | `1.15` | Places placeholders this far in front of the active camera when camera-visible placement is enabled. |
| `FloatingHead.RemotePoseSmoothTime` | `0.08` | Smooth damp time for remote spectator pose movement. Set to `0` to snap to received poses. |
| `FloatingHead.KeepRemotePoseInView` | `false` | Diagnostic mode that projects remote freecam markers to the local camera view edge when the true remote pose is behind or outside the watched player's current view. Keep disabled for strict world-space placement. |
| `FloatingHead.RemotePoseVisibleProxyDistance` | `1.35` | Camera-forward distance used for the edge proxy when a remote freecam pose is outside the local view. |
| `FloatingHead.EnableScreenFallbackVisual` | `false` | Diagnostic runtime IMGUI fallback dot at the placeholder screen position when the 3D marker is hard to see in the render pipeline. It can clamp off-screen poses to a screen edge, so it is disabled by default. |
| `FloatingHead.ScreenFallbackSize` | `48` | Screen fallback marker diameter in pixels. |
| `FloatingHead.PresenceLostGraceSeconds` | `0.3` | Keeps existing placeholders alive through brief empty presence frames. Disconnect and shutdown still clear immediately. |
| `FloatingHead.FaceCamera` | `true` | Rotates placeholder markers toward the local camera when possible. Runtime detached-head visuals ignore this and follow the remote spectator pose. |
| `FloatingHead.PulseWhenSpeaking` | `true` | Scales/pulses the marker when local voice activity data reports that spectator speaking. |
| `FloatingHead.SpeakingScaleMultiplier` | `1.65` | Maximum speaking scale multiplier at high observed amplitude. |
| `FloatingHead.SpeakingPulseSpeed` | `8.0` | Speed of the speaking pulse. |
| `FloatingHead.MinimumSpeakingVoiceLevel` | `0.35` | Fallback pulse strength only when `IsSpeaking=true` but vanilla/Dissonance reports `Amplitude=0`. Positive amplitude values drive scaling only while `IsSpeaking=true`; silent remote players do not scale from stale playback amplitude. |
| `FloatingHead.SpeakingPulseAmount` | `0.32` | Extra scale pulse amount while the represented spectator is speaking. |
| `FloatingHead.VoiceAttackSmoothTime` | `0.005` | How quickly the head grows when speech starts. Lower values feel snappier. |
| `FloatingHead.VoiceReleaseSmoothTime` | `0.008` | How quickly the head returns to normal when speech stops. Lower values snap back faster. |
| `FloatingHead.SilenceScaleMultiplier` | `1.0` | Scale multiplier when silent or voice data is unavailable. |
| `FloatingHead.AmplitudeSmoothing` | `0.08` | Legacy compatibility setting. Current speaking response uses `VoiceAttackSmoothTime` and `VoiceReleaseSmoothTime`. |
| `FloatingHead.DestroyOnPresenceLost` | `true` | Destroys placeholders when remote spectator presence disappears. |
| `FloatingHead.DebugVisualLifecycle` | `false` | Logs placeholder create/destroy/anchor-lost lifecycle events, including the runtime material shader. |
| `NameTag.ShowNameTags` | `true` | Shows runtime-only identity labels above floating-head placeholders. |
| `NameTag.NameTagScale` | `0.035` | World-space text character size for name tags. |
| `NameTag.NameTagHeightOffset` | `0.78` | Vertical offset above each placeholder marker. Raise this if runtime detached-head labels overlap the model. |
| `NameTag.NameTagMaxDistance` | `35` | Hides name tags beyond this camera distance. Set to `0` to disable distance culling. |
| `NameTag.NameTagUseGamePlayerNames` | `true` | Uses synced mod peer identity first, then confirmed `PlayerControllerB.playerUsername` when available. Exact actual-client matches are preferred, and generic `Player #n` placeholders are ignored. |
| `NameTag.NameTagUseFallbackIds` | `true` | Uses `Client <id>` / `Slot <id>` when the game player name is unavailable. |
| `NameTag.DebugNameTagLifecycle` | `false` | Reserved for verbose name tag diagnostics. |

When the in-game ESC quick menu is open, Enhanced Spectator pauses freecam movement/look input and suppresses local gameplay interaction/activate input so menu clicks do not also move spectator view or interact with the world. Vanilla spectator target selection is not suppressed while the menu is open because the right-side player list uses vanilla target switching.

## Behavior

- Vanilla still decides when the local player is dead.
- Vanilla still chooses and switches the spectated target.
- Vanilla spectator UI remains authoritative.
- Enhanced Spectator only writes `StartOfRound.spectateCamera.transform` while freecam is active and safe.
- If the local player revives, target state is lost, or vanilla game-over override activates, freecam stops writing the camera.
- If the controller throws an exception, it logs the failure and disables enhanced freecam so vanilla spectator camera can continue.
- When `Logging.EnableDebugLogging=true`, startup logs include the loaded plugin DLL path and SHA256 hash for host/client build verification.
- During disconnect, host shutdown, application quit, or scene transition teardown, Enhanced Spectator stops mod-owned network sends and unregisters handlers when safe.
- When target sync is available, debug presence logs can report when a remote modded player starts or stops spectating, and separately whether that spectator is watching the local player.
- When remote pose sync is available, placeholder markers can represent any remote modded spectator, not only spectators watching the local player. The marker follows the last remote spectator freecam world position and smooths toward newer poses. `FloatingHead.ShowOnlySpectatorsWatchingMe=true` restores the older watched-player-only filtering. Strict world-space placement remains the default; diagnostic view-edge and screen fallback modes are still opt-in.
- When `FloatingHead.UseRuntimeDetachedHeadVisuals=true`, the visual service tries to read the already loaded ghost-girl ragdoll template from `StartOfRound.playerRagdolls[1]` and copy its `DeadBodyInfo.detachedHeadObject` renderer data into a clean mod-owned object. It does not export assets, instantiate the full ragdoll prefab, or require the represented spectator to have been killed by the ghost girl. If the template source is missing and fallback is enabled, the existing placeholder marker is used.
- When a modded peer identity is known but vanilla first-join/rejoin ownership timing left that peer out of the local ESC player list, or left stale dead/disconnected/model-hidden flags locally, the connected-player repair pass uses vanilla `ClientPlayerList` plus mod spectator target state to restore the peer's controlled/dead/disconnected/live-model state and display name locally. This is a targeted compatibility repair for modded peers only.
- Phase 3F adds runtime-only placeholder styles and optional visual pulse. Voice activity sync now sends visual-only local amplitude metadata between modded peers, so a dead spectator's head can scale from the spectator's own microphone amplitude even when the watched player's client sees remote playback amplitude as `0`. Positive synced amplitude drives scale directly; `VoicePlayerState.IsSpeaking` still drives a fallback pulse when amplitude is unavailable or zero. It does not forward voice or change Dissonance routing.
- Phase 4F adds configurable dead spectator voice routing. By default, `VoiceRouting.SpectatorVoiceAudienceMode=AllModdedPlayers`, so compatible modded players can hear dead spectators regardless of whether the listener is alive or dead. More restrictive modes can limit this to the watched target, alive players only, or dead players only. When pose sync is available, routed voice is positioned at the remote spectator's synced freecam pose and can be attenuated by listener distance. It does not open new Dissonance channels, create audio objects, patch voice methods, or bypass local deafen/local mute. It restores local playback volume for an eligible dead remote spectator already represented by vanilla/Dissonance playback state and only when both peers advertise the voice-routing capability. If vanilla fails to attach that playback state to the right `PlayerControllerB`, the mod uses synced peer identity's Dissonance player id as a fallback binding key for the existing playback object.
- For normal multiplayer testing after voice routing has been validated, keep `VoiceRouting.EnableSpectatorVoiceToTarget=true`, `VoiceRouting.SpectatorVoiceAudienceMode=AllModdedPlayers`, `VoiceRouting.SpectatorVoiceUseRemotePosePosition=true`, `VoiceRouting.SpectatorVoiceEnableDistanceAttenuation=true`, and `VoiceRouting.SpectatorVoiceFallbackTo2DWhenPoseMissing=false`, but keep `Logging.EnableDebugLogging=false`, `Networking.DebugNetworkMessages=false`, `Networking.DebugVoiceActivitySync=false`, `FloatingHead.DebugVisualLifecycle=false`, and `VoiceRouting.DebugSpectatorVoiceRouting=false` unless you are diagnosing a specific route issue.
- Positional spectator voice maps the synced remote spectator pose into Unity's actual active `AudioListener` frame. This preserves the direction and distance heard from the rendered spectate/freecam camera even when the game keeps a different internal audio listener enabled during dead spectator mode.
- Phase 4B adds runtime-only name tags above placeholder markers. Tags prefer synced mod peer identity, then confirmed in-game `playerUsername`, and finally client/slot ids if unavailable.
- When model inspection is enabled before launch, pressing the configured inspection key logs one player model hierarchy snapshot for runtime research.
- When head source inspection is enabled before launch, pressing the configured inspection key logs one dead-body detached-head source snapshot for each current player corpse. It reads `PlayerControllerB.deadBody`, `DeadBodyInfo.detachedHead`, and `DeadBodyInfo.detachedHeadObject` metadata only; it does not clone, export, instantiate, or mutate model objects.
- When voice diagnostics are enabled before launch, pressing the configured inspection key logs an Info-level key-pressed line plus one local read-only snapshot of Dissonance and vanilla player voice state. The snapshot includes `timestampTicks` for host/client log correlation and reports when no player voice rows were captured. It records local observation only, does not require mod custom messaging to be available, and does not prove that routing is safe by itself.

## Known Limits

- Networking is a Phase 3B transport spike: capability, spectator target, and spectator pose messages are sent only between modded peers that complete handshake.
- Capability sends are delayed briefly after transport registration to avoid sending during unstable Netcode connection setup.
- Direct client-to-client sends are not implemented. With `Networking.EnableHostRelay=true`, the host can relay compatible client spectator target and pose state to other modded clients.
- Floating-head visuals are local-only runtime objects, not network objects and not formal UI. By default they are placeholder markers. The optional detached-head visual mode clones a confirmed runtime corpse head object only when it exists and never exports or saves assets.
- Name tags do not patch HUD and do not read unconfirmed player-name fields. The only game name source used is confirmed `PlayerControllerB.playerUsername`; fallback ids remain available.
- Voice activity feedback prefers synced local speaker amplitude from modded peers. If voice activity sync is disabled or unavailable, it falls back to locally readable Dissonance playback state for the represented spectator. If neither source provides data, the marker remains at the silent style.
- Floating-head visuals require compatible modded peers, capability handshake, spectator target sync, and a matching active spectator pose. This prevents stale target-only state from creating local fallback visuals during disconnect, rejoin, or target-switch windows. Cross-client visibility requires a modded host with `Networking.EnableHostRelay=true`; it does not work with an unmodded host.
- Model inspection is log-only. It does not clone heads, create visuals, export assets, or save raw inspection output into the repository.
- Head source inspection is log-only. It is meant for ghost-girl decapitation research and does not create a real head visual.
- Voice diagnostics are log-only. They do not forward voice, open Dissonance channels, change `VoicePlayerState.Volume`, create audio objects, or patch voice methods.
- Voice routing is configurable and defaults on only for compatible Enhanced Spectator peers. Positional playback and distance attenuation depend on spectator pose sync; when pose data is missing, the route drops by default instead of falling back to stale 2D/global playback. It does not rewrite Dissonance routing or create independent positional audio channels.
- Freecam writes the spectator camera during Unity camera pre-cull. Another mod writing the same camera from a later camera callback can still win.
- WASD horizontal movement keys are fixed in this MVP; vertical and action keys are configurable.
