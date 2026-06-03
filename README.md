# Enhanced Spectator

Enhanced Spectator is a public test-version BepInEx mod for Lethal Company. It improves dead-player spectating with local freecam controls, compatible-player spectator presence, floating spectator visuals, name tags, and configurable dead-spectator voice routing.

The mod preserves the game's vanilla death state, spectator target selection, connection approval, and round lifecycle.

## Status

Version: `0.2.0`

Current public test features:

- Client-local enhanced spectator freecam.
- Stable spectator camera anchoring that reduces view movement inherited from watched-player walk/run animation.
- Automatic target recovery when the watched player disconnects and another living player is available.
- Modded-peer capability handshake, spectator state sync, identity sync, and voice-activity sync.
- Host-mediated relay for compatible client-to-client spectator visibility.
- Runtime floating-head spectator visuals and name tags.
- Remote/dead spectator visibility for compatible modded peers.
- Speaking head scale and pulse driven by synced voice activity.
- Configurable routed dead-spectator voice, positional playback, and distance attenuation.
- Local-only spectator stability improvements for unmodded-host sessions.
- Cached runtime paths for routine input, camera, visual, name-tag, network sampling, and voice-routing work.

This repository does not contain game DLLs, Unity assemblies, exported assets, BepInEx runtime files, logs, or packaged Thunderstore output.

## Installation

Use the Thunderstore release when available.

For manual testing, build the project and place `EnhancedSpectator.dll` in a BepInEx plugin folder for Lethal Company.

## Default Controls

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

Forward/backward movement follows the camera look direction.

## Build Requirements

- .NET SDK 8 or newer.
- A local Lethal Company install for compiling against game assemblies.
- BepInEx installed in the local game folder for runtime testing.

The project references game assemblies from the local install path. These files are not included in this repository.

## Build

Restore packages:

```powershell
dotnet restore
```

Build with the default `GameDir`:

```powershell
dotnet build
```

Build with an explicit game install path:

```powershell
dotnet build -p:GameDir="D:\Steam\steamapps\common\Lethal Company"
```

## Configuration

BepInEx creates the configuration file after the plugin has run once:

```text
BepInEx/config/Auuueser.EnhancedSpectator.cfg
```

See [docs/USAGE.md](docs/USAGE.md) for controls, common options, known limits, and recommended testing settings.

## License

Enhanced Spectator is licensed under the GNU General Public License v3.0. See [LICENSE](LICENSE).

## Development Rules

- Do not commit game DLLs, decompiled game files, Unity assemblies, BepInEx runtime contents, logs, or generated package output.
- Do not use reflection to access Lethal Company members.
- Prefer publicized game assemblies and direct access after a member has been confirmed.
- Keep game API access isolated behind `GameInterop` adapters.
- Keep patch code under the patching layer.
- Route mod logs through the shared logging helper.

## Known Limits

- Multiplayer spectator visuals and voice routing require compatible Enhanced Spectator peers.
- Client-to-client spectator visibility requires a compatible modded host with host relay enabled.
- Runtime detached-head visuals are runtime-only and fall back to placeholders when the required runtime source is unavailable.
- The mod does not export or redistribute Lethal Company assets.
- Other camera, voice, or spectator mods may conflict if they write the same runtime systems later in the frame.
