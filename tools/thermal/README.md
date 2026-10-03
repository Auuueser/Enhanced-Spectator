# Offline thermal imaging checks

Run `./tools/thermal/Build.ps1` with the installed Unity 2022.3.62f2. It launches a hidden standalone D3D11 Unity fixture, tests the authored shaders, builds `src/EnhancedSpectator/Resources/thermal.bundle`, reloads the shipping bundle and repeats the tests. It does not start Lethal Company.

The bundle contains only two authored shaders; no game meshes, textures, shaders or assemblies. Generated projects, logs, GPU results and local dependency copies stay in ignored `release/thermal-unity`.

Checks cover fixed black/purple/red/orange/yellow/white palette, dark and bright backgrounds, near/far actor occlusion against scene depth, cutout holes, actual fallback mesh/texture alpha, 2D/array source buffers and resolution changes. Tests use synthetic depth and meshes. Actual native HDRP material variants, fur/transparent enemy surfaces, modded renderers, multiplayer and low-end GPU frame time remain live-game acceptance items.

Runtime registers only living player body renderers and actual living EnemyAI renderers through confirmed game fields. The independent after-post-process pass follows the existing before-post-process whole-model fade. Both share the working-camera custom-pass compatibility gate; unrelated disabled custom passes are not enabled. Body sources are refreshed in small batches and materials/buffers are cached. F9 toggles the saved preference in every eligible spectator view, including vanilla; living cameras and overlay HUD are unaffected.

The visual is a thermal simulation, not temperature measurement. Scene luminance contributes only capped cool detail; actual actors get a fixed warmer range. No per-frame exposure normalization, thermal wall penetration, material replacement, scene light changes or network messages.
