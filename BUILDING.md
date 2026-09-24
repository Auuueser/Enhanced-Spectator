# Building Enhanced Spectator

Requires .NET SDK 8 or newer and a local Lethal Company V81 installation.
The optional MoreCompany adapter also needs a local MoreCompany.dll to compile;
MoreCompany is optional when running the mod. Do not redistribute these dependencies.

```powershell
dotnet restore EnhancedSpectator.sln --source https://nuget.bepinex.dev/v3/index.json --source https://api.nuget.org/v3/index.json
dotnet build src/EnhancedSpectator/EnhancedSpectator.csproj -c Release -p:GameDir="D:/Steam/steamapps/common/Lethal Company" -p:MoreCompanyPath="D:/Mods/MoreCompany.dll"
```

Replace the example paths with your local installations. Output is
`src/EnhancedSpectator/bin/Release/netstandard2.1/EnhancedSpectator.dll`.
Game access is isolated behind `GameInterop` using publicized assemblies.

The bundled fade shader is maintained in `tools/native-fade/`. See its README
for rebuilding with Unity 2022.3.62f2. The public source tree contains only the
shader and its build scripts; automated tests and offline validation fixtures are
maintained separately.

The source tree includes the authored shader bundle and verified V81 resource
layout/hash metadata required by the build. They are not disposable build caches.
Only the built EnhancedSpectator DLL, package metadata and license notices belong
in an installation package. The shader build sources remain available for rebuilding the bundled resource.
