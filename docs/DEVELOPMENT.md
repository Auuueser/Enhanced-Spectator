# Development

## Scope

This repository contains the first client-local spectator freecam MVP. New gameplay work should still be limited to confirmed Lethal Company APIs and routed through `GameInterop`.

## Build Commands

```powershell
dotnet restore
dotnet build
```

Use a custom game path when needed:

```powershell
dotnet build -p:GameDir="D:\Steam\steamapps\common\Lethal Company"
```

## Code Rules

- Keep plugin startup logic in `Plugin`.
- Keep config entries in `Config`.
- Keep log calls behind `ModLog`.
- Keep Harmony registration in `Patching`.
- Keep game API access behind `GameInterop`.
- Do not invent game member names.
- Do not use reflection helpers or Harmony member traversal helpers to access game members.

## Local Runtime Testing

After a successful build, copy the generated mod DLL into the local BepInEx plugin folder for manual testing:

```powershell
$(GameDir)\BepInEx\plugins
```

Do not commit the copied DLL or any files from the game directory.
