# Architecture

Enhanced Spectator is organized around narrow layers so game API knowledge stays isolated until verified.

## Plugin Layer

`Plugin` is the BepInEx entry point. It initializes logging, binds config, starts the feature bootstrapper, starts the patch bootstrapper, forwards `Update` / `LateUpdate` to runtime feature modules, and shuts both bootstrappers down during plugin destruction.

`PluginMetadata` owns the GUID, name, and version shared by BepInEx and Harmony.

## Feature Layer

Feature modules implement `IFeatureModule` with `Initialize()` and `Dispose()` lifecycle methods. `FeatureBootstrapper` creates configured modules and disposes them in reverse order.

`SpectatorModule` owns the local spectator freecam feature lifecycle. It depends on `IGameSpectatorAdapter`, exposes `ISpectatorStateService`, and delegates runtime behavior to focused services.

`SpectatorFreecamController` decides when enhanced freecam should run, maintains camera offset and yaw/pitch state, clamps the offset to the configured radius, applies input, and writes the vanilla spectator camera transform only while all safety checks pass. Camera writes happen from a Unity camera pre-cull callback so vanilla LateUpdate camera work can finish first.

`SpectatorAnchorService` tracks the current vanilla spectated target anchor and detects target changes.

`SpectatorInputService` reads local Unity Input System keyboard and mouse state. It does not depend on Lethal Company input action names.

Runtime feature modules can implement `IRuntimeTickable`, `IRuntimeLateTickable`, `IRuntimeCameraPreCullTickable`, and `IRuntimeGuiTickable`. `FeatureBootstrapper` calls those from the runtime driver during Unity `Update`, `LateUpdate`, legacy `Camera.onPreCull`, SRP `RenderPipelineManager.beginCameraRendering`, and `OnGUI`.

`RuntimeConnectionState` tracks application quit, plugin shutdown, short scene transition windows, and Netcode shutdown/disconnect state. Networking and the spectator input compatibility patch use this guard to stop mod-owned work during teardown without blocking vanilla disconnect.

## Networking Layer

`NetworkingModule` owns the mod-owned networking lifetime. It runs after the spectator module is created and consumes `ISpectatorTargetStateProvider` plus `ISpectatorPoseStateProvider`; it does not control the freecam or write game state.

`EnhancedSpectatorNetworkService` tracks Netcode availability, local capability, remote peer capability, local spectator target identity changes, local spectator camera pose changes, and received remote spectator target/pose states. It separates observed, pending, and sent target and pose states so throttle windows do not drop the last observed change.

`UnityNetcodeMessagingTransport` is the only networking component that directly uses Unity Netcode custom messaging. It registers `EnhancedSpectator.Capability.V1`, `EnhancedSpectator.SpectatorTarget.V1`, and `EnhancedSpectator.SpectatorPose.V1`, serializes payloads with `FastBufferWriter`, decodes with `FastBufferReader`, and unregisters handlers during disposal or degradation.

Network sends and message handlers are gated by `RuntimeConnectionState`. Capability sends are delayed briefly after transport registration or local client id changes so host/client lifecycle state can settle before the first mod packet.

`RemotePeerRegistry` records compatible modded peers after capability handshake. `RemoteSpectatorTargetRegistry` records the last spectator target state received from remote peers, including explicit non-spectating states that confirm a revived peer has left spectator mode. `RemoteSpectatorPoseRegistry` records the latest active spectator camera pose received from remote peers until an explicit stop, mismatch, disconnect, or network cleanup removes it. These registries carry ids, primitives, and vectors only, never Unity objects or game component references.

Direct client-to-client sends are not implemented. `Networking.EnableHostRelay=true` by default, so a modded host validates and fans out compatible client-origin spectator target, pose, identity, and voice-activity state to other modded clients.

`ConnectedPlayerStateRepairModule` runs after networking and reconciles local vanilla player slots against `StartOfRound.ClientPlayerList`. When a compatible mod peer identity is available, the repair uses that identity and the peer's last spectator target state. When the host is unmodded and no mod identity can be relayed, the same GameInterop adapter still performs a conservative local-only repair for connected non-local slots that are alive but incorrectly uncontrolled, then refreshes the quick-menu player slot/name. This avoids showing generic `Player #n` labels and prevents vanilla spectator target cycling from skipping connected clients after revive or late join. It does not change connection approval and does not repair peers that are absent from `ClientPlayerList` or currently disconnected.

## Spectator Presence Layer

`SpectatorPresenceModule` runs after networking and drives floating-head visibility. It consumes the read-only remote target state exposed by `IEnhancedSpectatorNetworkService` and reads local player identity through `IGameSpectatorAdapter`. The legacy `Presence.EnableSpectatorPresenceDebug` config name now gates this visibility inference, not just debug logging.

`SpectatorPresenceService` now produces the local remote-spectator visibility set. It can include any remote peer whose latest target state is spectating and whose pose state matches that target, so floating-head visuals always represent a current world-space freecam pose instead of falling back to a local camera placement during disconnect or target-switch windows. `FloatingHead.ShowOnlySpectatorsWatchingMe=true` narrows that set back to spectators whose current target is the local player. Client id is authoritative for watched-player matching; player slot id is only a fallback when target client id is absent, which avoids three-player slot collisions. The service reuses caller-owned scratch collections for per-frame target snapshots and logs start/stop changes only when debug logging is explicitly enabled. It silently clears state during disconnect, scene transition, or network degradation. It does not patch HUD, instantiate visuals, or control freecam behavior.

## Floating Head Placeholder Layer

`FloatingHeadModule` is a local-only visual feature that runs after spectator presence. It consumes `ISpectatorPresenceProvider` and never reads networking registries or game fields directly.

`FloatingHeadVisualService` owns one runtime-created visual per remote spectator client id. It creates visuals when the visibility set includes a remote spectator, keeps lifecycle state during `LateUpdate`, smooths toward true remote spectator camera poses when available, can optionally project out-of-view remote poses to a diagnostic view-edge proxy, can draw a runtime IMGUI fallback dot for render-pipeline diagnostics, falls back to local placement only for watched-local-player entries without pose data, holds visuals briefly through transient empty visibility frames, and destroys or hides visuals when visibility is lost beyond the grace window, local player head anchor data is unavailable, networking degrades, or the feature is disposed. It reuses per-frame sort/stale-id scratch collections to avoid avoidable allocations in the visual hot path. When `FloatingHead.UseRuntimeDetachedHeadVisuals=true`, it asks `IGameDetachedHeadVisualSourceAdapter` for a loaded ghost-girl ragdoll detached-head visual template from `StartOfRound.playerRagdolls[1]`; if available, it creates a clean mod-owned renderer copy, otherwise it falls back to the placeholder marker when configured.

`NameTagVisual` is an optional runtime-only world-space text label created with the placeholder visual. It follows the rendered placeholder pose and faces the active camera. It uses confirmed `PlayerControllerB.playerUsername` through `IGameSpectatorAdapter` when available, falls back to client/slot identity, and does not read unconfirmed Lethal Company player-name fields or patch HUD.

`FloatingHeadPlacementService` asks `IGameSpectatorAdapter` for the local player head anchor position and active camera. `LethalCompanySpectatorAdapter` resolves the anchor through confirmed runtime fields and a safe transform traversal for `HeadPoint`: `HeadPoint`, `playerGlobalHead`, `headCostumeContainer`, then local player transform plus `Vector3.up`. Default placement keeps remote spectator poses in world space; camera-visible placement is a diagnostic fallback for missing pose data.

`PlaceholderHeadVisualFactory` creates simple runtime markers under an `Enhanced Spectator Visuals` root. Supported placeholder styles are sphere, billboard, and ring. Billboard and ring styles use runtime-created meshes only. The factory can also create an experimental runtime detached-head visual from a confirmed template object. Detached-head visuals are clean GameObjects that copy only `MeshFilter.sharedMesh` and `MeshRenderer.sharedMaterials`; they do not instantiate the full ragdoll prefab, do not move a vanilla corpse head, do not export assets, and are not parented under the vanilla player hierarchy.

## Voice Activity Layer

`IVoiceActivityProvider` is a read-only provider used by placeholder visuals and voice-activity metadata sync. `LethalCompanyVoiceActivityProvider` resolves represented remote spectators through confirmed player ids and reads `PlayerControllerB.voicePlayerState` / Dissonance public state as a fallback. For the local speaker it reads `StartOfRound.voiceChatModule.FindPlayer(LocalPlayerName).Amplitude`, which is the confirmed local Dissonance microphone amplitude source. It does not create audio objects, change voice routing, alter Dissonance settings, or patch voice methods. `NoopVoiceActivityProvider` exists for unavailable data or future fallback scenarios.

`EnhancedSpectator.VoiceActivity.V1` is a visual-only custom message. It carries `IsSpeaking`, `Amplitude`, `Volume`, local client id, slot id, and timestamp so remote floating-head visuals can scale from the speaker's own amplitude. It is throttled, unreliable sequenced, refreshed while active, and separate from any future voice forwarding work. Receivers use their local receive time for stale expiry, so host/client clock skew does not discard fresh activity packets.

## Spectator Voice Routing Layer

`SpectatorVoiceRoutingModule` is a configurable Phase 4F feature. It consumes remote spectator target snapshots plus peer capability state and asks `IGameSpectatorVoiceRoutingAdapter` to make eligible remote dead spectators audible to configured local modded listeners. `VoiceRouting.EnableSpectatorVoiceToTarget=true` by default for compatible Enhanced Spectator peers, and `VoiceRouting.SpectatorVoiceAudienceMode` controls whether routed dead spectator voice is heard by all modded players, only alive modded players, only dead modded players, or only the current watched target. When configured, it passes the matching synced spectator pose so the adapter can place playback at the remote spectator freecam position.

`LethalCompanySpectatorVoiceRoutingAdapter` is the only component that writes confirmed voice playback fields for this experiment. It uses existing vanilla/Dissonance playback objects, respects local deafen and per-player local mute state, snapshots the previous local playback settings before a route, and restores them when the route ends. Positional mode writes the existing voice source transform to the synced spectator pose, sets the playback to 3D, and applies local distance attenuation to `VoicePlayerState.Volume`. Missing pose data can fall back to the older 2D route behavior. It does not create audio objects, open Dissonance channels, patch voice methods, write `AudioSource.mute`, or modify voice filters.

`SpectatorVoiceDistanceAttenuation` is pure feature logic for calculating the local volume multiplier between configured minimum and maximum distances. It is tested without Unity audio objects so future tuning can change the curve safely.

## Voice Diagnostics Layer

`VoiceDiagnosticsModule` is a default-off Phase 4E research feature. It runs one read-only inspection pass when `VoiceDiagnostics.InspectionKey` is pressed and writes a summarized BepInEx log. It does not run every frame.

`VoiceDiagnosticsService` formats the local diagnostics snapshot. `LethalCompanyVoiceDiagnosticsAdapter` is the only component that reads confirmed vanilla and Dissonance voice members for this feature, including `StartOfRound.voiceChatModule`, `PlayerControllerB.voicePlayerState`, `currentVoiceChatAudioSource`, `currentVoiceChatIngameSettings`, and walkie voice flags. The layer does not open Dissonance channels, write `VoicePlayerState.Volume`, create audio sources, patch voice methods, or change routing.

## Planned Phase 4 Layers

The Phase 4 work order is documented under `docs/work-orders/full-spectator-presence-voice/`. It is planning-only and does not add runtime code yet.

Phase 4A host relay extends the networking layer so the host can validate and fan out mod-owned spectator target and pose state from Client A to Client B when `Networking.EnableHostRelay=true`. Relay preserves local-freecam fallback, avoids client-side forwarding loops, and keeps vanilla connection approval unchanged.

Phase 4C remote spectator visibility makes any remote modded spectating player visible when configured, rather than only showing spectators that target the local player. This is still driven by target and pose sync only; it does not add new gameplay patches, real head meshes, voice, or formal UI.

Future floating-head work should keep visuals runtime-only. Current research does not confirm a standalone head-only renderer, so generated head-like visuals and additional head/corpse inspection must precede any renderer clone attempt.

Future name-tag refinement can improve styling, clutter control, and late-join behavior. It should continue using runtime text and should not patch HUD.

Future deeper voice routing work must remain research-gated. Current routing does not forward audio through new channels; it only restores and positions already existing vanilla/Dissonance playback for compatible modded peers.

Phase 4H performance work keeps hot paths allocation-light: no per-frame hierarchy scans, no per-frame network sends, reusable per-frame spectator/presence scratch collections, dictionaries keyed by client id, and debug logs gated by both feature-specific debug switches and `Logging.EnableDebugLogging`.

## Model Inspection Layer

`ModelInspectionModule` is a default-off debug feature that runs one inspection pass when its configured key is pressed. It does not scan every frame.

`PlayerModelInspectionService` formats inspection snapshots into BepInEx logs. `LethalCompanyPlayerModelInspectionAdapter` is the only component that reads confirmed player model fields such as `StartOfRound.allPlayerScripts`, `meshContainer`, `playerGlobalHead`, `headCostumeContainer`, and `thisPlayerModel*`.

The inspection layer records ids, known transform paths, head-related transform names, and `SkinnedMeshRenderer` metadata. It never reads mesh vertex data, exports assets, instantiates clones, or patches gameplay.

`DeadBodyHeadSourceInspectionModule` is a separate default-off research feature for ghost-girl decapitation and `DeadBodyInfo.detachedHeadObject` feasibility. It is key-triggered, log-only, and uses `LethalCompanyDeadBodyHeadSourceInspectionAdapter` to read current player corpses through confirmed members such as `PlayerControllerB.deadBody`, `DeadBodyInfo.detachedHead`, `DeadBodyInfo.detachedHeadObject`, and `DeadBodyInfo.bodyParts`. It records transform paths, renderer metadata, and component counts needed for future clone strip rules. It does not instantiate ragdolls, clone renderers, create visuals, or mutate the vanilla corpse object.

## Patching Layer

Patch modules implement `IPatchModule`. `PatchBootstrapper` owns the Harmony instance and registers modules through a single entry point.

`SpectatorLifecyclePatchModule` primarily registers low-risk public Harmony patches. It also has one narrow publicized private prefix for `SpectateNextPlayer(bool)` so Space can be used as freecam ascend without vanilla target switching stealing the same input, and so the ESC quick menu can suppress local spectator target switching. During shutdown/disconnect, that prefix skips the vanilla call only in the unsafe teardown window where player arrays may already be partially destroyed. `SpectatorDisconnectTargetSwitchService` subscribes to Unity Netcode's public disconnect callback and also validates the current watched target at a low fixed interval while active, then asks GameInterop to invoke vanilla target switching if the local dead spectator is still watching a disconnected target and another living target exists. Patches do not contain camera movement logic.

Current public patch points:

- `PlayerControllerB.KillPlayer(...)` postfix.
- `PlayerControllerB.SetSpectatedPlayerEffects(bool)` postfix.
- `StartOfRound.SwitchCamera(Camera)` postfix.
- `StartOfRound.SetSpectateCameraToGameOverMode(bool, PlayerControllerB)` postfix.
- `StartOfRound.ReviveDeadPlayers()` postfix.
- `PlayerControllerB.SpectateNextPlayer(bool)` prefix, using publicized direct access, only to suppress vanilla target switching while a configured vertical freecam key is held or while the local quick menu is open.
- `PlayerControllerB.Interact_performed(...)` and `PlayerControllerB.ActivateItem_performed(...)` prefixes, using publicized direct access, only to suppress local gameplay input while the quick menu is open.

Private per-frame camera and look-input spectator methods remain unpatched in this MVP.

## GameInterop Layer

`GameInterop` is the boundary for Lethal Company API access. Feature code should depend on interfaces such as `IGameSpectatorAdapter`, not raw game classes.

`LethalCompanySpectatorAdapter` is the concrete adapter for confirmed spectator state. It reads `StartOfRound`, `PlayerControllerB`, the vanilla `spectateCamera`, the current `spectatedPlayerScript`, and safe target anchor transforms. It chooses the stable target player transform first for local spectator camera anchoring, with `lowerSpine` and then `playerGlobalHead` as fallbacks only when the root transform is unavailable.

`LethalCompanySpectatorTargetSwitchAdapter` is the concrete adapter for disconnect recovery target switching. It reads the local dead spectator, current `spectatedPlayerScript`, confirmed valid target predicates, and replacement target availability, then calls vanilla `SpectateNextPlayer(false)` only when the pure disconnect switch rules allow it.

Networking code receives spectator target ids through `ISpectatorTargetStateProvider` and freecam world pose through `ISpectatorPoseStateProvider`; it does not read Lethal Company fields directly.

`LethalCompanyConnectedPlayerStateRepairAdapter` is the concrete adapter for vanilla connected-player state repair. It reads confirmed `ClientPlayerList`, `allPlayerScripts`, `allPlayerObjects`, `actualClientId`, `playerClientId`, `isPlayerControlled`, `isPlayerDead`, `disconnectedMidGame`, `setPositionOfDeadPlayer`, player model renderers, and `playerUsername`, consumes mod-owned remote spectator target state to distinguish dead spectators from revived peers, re-enables live renderers for connected alive peers after death/disconnect/rejoin, and calls the public quick-menu player list API to refresh visible entries.

## Config and Logging

`EnhancedSpectatorConfig` owns all BepInEx config entries, including the default-off debug logging switch.

Networking config is under the `Networking` section and can disable the entire module, capability handshake, spectator target sync, or verbose network diagnostics.

`ModLog` is the only logging facade used by the mod code. This keeps log formatting and log source usage consistent. When debug logging is enabled, startup diagnostics also include the loaded plugin DLL path and SHA256 hash so multi-machine tests can verify both host and client loaded the same build.

## Adding Future Features

1. Add or extend a `GameInterop` interface for the required game data.
2. Implement the interface with publicized game assembly members after those members are confirmed.
3. Add a focused feature module under `Features`.
4. Register the feature in `FeatureBootstrapper`.
5. Add Harmony patches through `IPatchModule` only when a patch is required.
6. Build locally with `GameDir` pointing at the installed game.
