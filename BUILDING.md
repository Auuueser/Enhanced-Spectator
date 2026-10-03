# Building Enhanced Spectator

Requires .NET SDK 8 or newer, PowerShell 7, Python 3 and a local Lethal Company V81 installation.
The optional MoreCompany adapter also needs a local MoreCompany.dll to compile;
MoreCompany is optional when running the mod. Do not redistribute these dependencies.

```powershell
./tools/camera-agent-runtime/Prepare.ps1 -ManagedDir "D:/Steam/steamapps/common/Lethal Company/Lethal Company_Data/Managed"
dotnet restore EnhancedSpectator.sln --source https://nuget.bepinex.dev/v3/index.json --source https://api.nuget.org/v3/index.json
dotnet build src/EnhancedSpectator/EnhancedSpectator.csproj -c Release -p:GameDir="D:/Steam/steamapps/common/Lethal Company" -p:MoreCompanyPath="D:/Mods/MoreCompany.dll"
```

Replace the example paths with your local installations. Preparation downloads the
hash-pinned official Cinemachine 2.9.7 source archive and builds the private
`EnhancedSpectator.CameraRuntime.dll`; source caches stay under ignored `libs/camera-agent/`.
Game access is isolated behind `GameInterop` using publicized assemblies.

Install both output assemblies from `src/EnhancedSpectator/bin/Release/netstandard2.1/` together:

- `EnhancedSpectator.dll`
- `EnhancedSpectator.CameraRuntime.dll`

Ship `tools/camera-agent-runtime/THIRD-PARTY-NOTICES.md` and its `licenses/` directory
with the runtime assembly. Do not package game `Unity.*` DLLs, MoreCompany,
publicized/decompiled game code or source caches.

The bundled fade and thermal shaders are maintained in `tools/native-fade/` and
`tools/thermal/`. See their READMEs for rebuilding with Unity 2022.3.62f2. The source
tree includes the authored shader bundles and verified V81 resource layout/hash
metadata required by the build; they are not disposable build caches. Automated
tests and offline validation fixtures are maintained separately.
