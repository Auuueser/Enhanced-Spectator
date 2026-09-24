# nativefade.bundle

Contains only `Assets/NativeFadeComposite.shader`, authored in this repository at
`tools/native-fade/NativeFadeComposite.shader` and licensed with this project.
It contains no game-derived shaders, textures, meshes or assemblies.

Compiler: Unity 2022.3.62f2 (7670c08855a9), StandaloneWindows64, LZ4 chunk compression.
Build and synthetic GPU checks: `tools/native-fade/Build.ps1`.
SHA-256: `3DE16F512D8BB4E78A1DF41736B9667D87DA13E3BFB99108FCDED002A1C2CC61`.

The byte-for-byte shipping shader was loaded and exercised on D3D11, including
independent 2D/array color and depth, 1/2/4/8x samples and resolved color. This does
not certify HDRP integration or native V81 model appearance. See the version
work order for remaining validation.
