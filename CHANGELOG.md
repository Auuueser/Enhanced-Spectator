# Changelog

## 0.2.0

### Spectator Camera

- Reduced spectator-view movement inherited from watched-player walk and run animation by preferring stable player-root camera anchors when available.
- Preserved existing animated-body fallbacks for compatibility when a stable root anchor cannot be resolved.

### Disconnect Recovery

- Added automatic spectator target recovery when the currently watched player disconnects and another living player is available.
- Improved non-host client recovery for disconnected targets so client spectators no longer remain attached to off-map disconnected-player models.
- Kept mod-owned target recovery compatible with vanilla spectator switching and enhanced-freecam input suppression.

### Remote Spectator Visibility

- Improved remote floating-head visibility for dead compatible clients across host and client perspectives.
- Stabilized floating-head and name-tag updates during target changes, disconnect windows, and enhanced-freecam to vanilla-view transitions.

### Performance

- Reduced steady-state lookup and allocation pressure in spectator input, camera snapshots, floating-head name text, runtime dispatch, network sampling, and voice-routing player lookup paths.
- Kept verbose diagnostics behind debug configuration gates so normal play avoids avoidable log formatting and message churn.

## 0.1.3

### Spectator Visibility

- Kept remote floating-head visuals visible when a dead spectator toggles from enhanced freecam to vanilla spectator view.
- Restored enhanced freecam pose sync cleanly after toggling back from vanilla spectator view.
- Continued publishing vanilla spectator camera pose while enhanced freecam is disabled and the player is still spectating.

### Stability

- Added regression coverage for the enhanced-freecam to vanilla-spectator to enhanced-freecam cycle.

## 0.1.2

### Spectator Stability

- Improved local-only spectator behavior when joining an unmodded host.
- Repaired cases where revived connected players could remain unavailable as spectator targets on another installed client.
- Improved fallback name repair for generic `Player #n` labels when compatible peer identity data is unavailable.
- Kept enhanced freecam active after valid vanilla spectator target selection in local-only sessions.

### Compatibility

- Local freecam remains available for installed clients when the host is unmodded.
- Multiplayer presence, floating-head visuals, name tags, and routed spectator voice continue to require compatible Enhanced Spectator peers and a modded relay host.

## 0.1.1

### Visuals

- Runtime detached-head spectator visuals are now enabled by default.
- Placeholder sphere visuals remain available as the fallback when the runtime head source is unavailable.

## 0.1.0

Initial public test release.

### Spectator Freecam

- Client-local enhanced spectator freecam with configurable movement, recenter, reset, and speed controls.
- Camera movement around the current vanilla spectator target with radius limiting.
- Configurable toggle, recenter, reset, fast-move, and slow-move controls.

### Multiplayer Presence

- Modded-peer capability handshake.
- Spectator target sync and spectator pose sync.
- Host-mediated relay for compatible client-to-client spectator visibility.
- Remote and dead spectator visibility for compatible modded peers.
- Peer identity sync for spectator name tags.

### Visuals

- Runtime floating-head spectator visuals.
- Runtime name tags above spectator visuals.
- Runtime detached-head visual mode with placeholder fallback.
- Voice-activity driven head scale and pulse.

### Voice

- Configurable dead-spectator voice routing for compatible peers.
- Positional spectator voice based on synced spectator pose.
- Distance attenuation for routed spectator voice.

### Diagnostics

- Debug and diagnostic controls for networking, player model inspection, head-source inspection, voice diagnostics, and visual lifecycle checks.
