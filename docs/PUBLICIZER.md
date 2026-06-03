# Publicizer

## Why Publicizer Is Used

Enhanced Spectator needs efficient access to confirmed Lethal Company members. Runtime reflection adds overhead and makes failures easier to hide until runtime. `BepInEx.AssemblyPublicizer.MSBuild` lets the project compile against publicized game assemblies so code can use direct member access after the members are confirmed.

## MSBuild Configuration

The publicizer package is configured in:

```text
src/EnhancedSpectator/EnhancedSpectator.csproj
```

The scaffold includes the package with private assets:

```xml
<PackageReference Include="BepInEx.AssemblyPublicizer.MSBuild" Version="0.4.3" PrivateAssets="all" />
```

The initial game assembly reference is:

```xml
<Reference Include="Assembly-CSharp"
           HintPath="$(ManagedDir)\Assembly-CSharp.dll"
           Private="false"
           Publicize="true"
           Condition="Exists('$(ManagedDir)\Assembly-CSharp.dll')" />
```

Only game assemblies should use `Publicize="true"`. Do not apply it to BepInEx, Harmony, or UnityEngine dependencies.

The MVP also references `Unity.Netcode.Runtime.dll` as a normal conditional dependency because confirmed game types derive from Netcode classes:

```xml
<Reference Include="Unity.Netcode.Runtime"
           HintPath="$(ManagedDir)\Unity.Netcode.Runtime.dll"
           Private="false"
           Condition="Exists('$(ManagedDir)\Unity.Netcode.Runtime.dll')" />
```

Do not add `Publicize="true"` to this Netcode reference unless a future feature has a confirmed need for non-public Netcode members.

## Local Paths

Default game path:

```powershell
D:\Steam\steamapps\common\Lethal Company
```

Default managed assembly path:

```powershell
$(GameDir)\Lethal Company_Data\Managed
```

Override `GameDir` at build time:

```powershell
dotnet build -p:GameDir="D:\Steam\steamapps\common\Lethal Company"
```

## Avoiding Reflection

Production code must not use reflection APIs or Harmony helper wrappers to access game fields, properties, or methods. If non-public game members are needed, confirm the members, publicize the game assembly, and access the members directly.

Forbidden examples include `System.Reflection`, `BindingFlags`, `Type.GetField`, `Type.GetMethod`, `FieldInfo`, `MethodInfo`, `PropertyInfo`, `HarmonyLib.AccessTools`, and `HarmonyLib.Traverse`.

## Codex Cloud or Missing Local DLLs

The game assembly references are conditional. Package restore can still run without the local game install, but full MVP builds require the local managed game DLLs because the mod now references confirmed Lethal Company types.

When future code references game types, build failures in cloud environments are expected unless the required local game DLLs are available. Do not work around this by committing DLLs.

## Extending the List

After confirming additional required game assemblies, add conditional references in the same style:

```xml
<Reference Include="SomeGameAssembly"
           HintPath="$(ManagedDir)\SomeGameAssembly.dll"
           Private="false"
           Publicize="true"
           Condition="Exists('$(ManagedDir)\SomeGameAssembly.dll')" />
```
