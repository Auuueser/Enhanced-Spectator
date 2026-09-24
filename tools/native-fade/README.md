# Native fade shader build

This directory contains the authored shader and the scripts needed to rebuild
`src/EnhancedSpectator/Resources/nativefade.bundle`. It has no test-suite dependency.

Use an installed, licensed Unity 2022.3.62f2 editor with Windows build support:

```powershell
./tools/native-fade/Build.ps1 -Unity "D:/Unity/2022.3.62f2/Editor/Unity.exe"
```

The script runs Unity in hidden batch mode, builds and verifies the shader bundle,
and copies it to the plugin resource directory. Rebuild the plugin afterward.
Use `-OutputPath` to verify a build without replacing the bundled resource.
Temporary Unity files are written under the ignored `release/native-fade-build/`.
No game installation, game assets, gameplay components or test fixtures are copied.
