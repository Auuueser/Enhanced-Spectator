# Camera Agent runtime dependencies

Enhanced Spectator builds this package from Unity's official registry. The
version, archive SHA-256 value and source URL are pinned in
`dependencies.lock.json`. The package archive, transformed source and compiled
assembly stay in ignored `libs/camera-agent/`; no game assemblies are bundled.

- **Cinemachine 2.9.7**: copyright © 2021 Unity Technologies.

It is licensed under the [Unity Companion License for Unity-dependent
projects](https://unity.com/legal/licenses/unity-companion-license) and is
provided as-is without warranties. The private build changes its namespace to
`EnhancedSpectator.CameraRuntime.Cinemachine` to isolate runtime singleton state
from other mods. Editor-only, Input System, Timeline, post-processing and HDRP
adapters are not enabled in this runtime build.

The Cinemachine private Core has one caller-clock integration change:
`FrameCountOverride` defaults to `-1` (Unity's real frame). Its four existing
frame-count reads use this value during an explicitly driven Camera Agent tick.
The rig supplies a logical frame together with `UniformDeltaTimeOverride`, so
the same production Composer pipeline runs in game and deterministic headless
tests. Unity's clock and other Cinemachine assemblies are unchanged. Preparation
starts from the verified archive every time and records a digest of the
transformed runtime source in ignored `prepared-source.json`.

Cinemachine includes Clipper, copyright © 2010–2014 Angus Johnson, under the
Boost Software License 1.0. Exact upstream `LICENSE.md` and `Third Party
Notices.md` files are preserved in `licenses/` and beside the ignored source, and must accompany
redistributed runtime assemblies in release notices. Unity.Mathematics and
Unity.Collections remain references to the game's installed assemblies.

Prepare with `./tools/camera-agent-runtime/Prepare.ps1` before the main build.
The preparation script validates the archive hash before extracting runtime
source. Preparation does not run while playing or during camera updates.
