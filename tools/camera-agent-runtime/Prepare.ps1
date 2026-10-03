param(
    [string]$ManagedDir = 'D:\Steam\steamapps\common\Lethal Company\Lethal Company_Data\Managed',
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath (Join-Path $ManagedDir 'UnityEngine.CoreModule.dll'))) {
    throw "ManagedDir does not contain the installed game's Unity assemblies: $ManagedDir"
}
& python (Join-Path $PSScriptRoot 'Prepare.py')
if ($LASTEXITCODE -ne 0) { throw 'Pinned camera runtime preparation failed.' }
foreach ($project in @('CameraRuntime.csproj')) {
    & dotnet build (Join-Path $PSScriptRoot $project) -c $Configuration -v quiet "-p:ManagedDir=$ManagedDir"
    if ($LASTEXITCODE -ne 0) { throw "Camera runtime build failed: $project" }
}
