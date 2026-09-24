param(
    [string]$Unity = 'D:\unity\2022.3.62f2\Editor\Unity.exe',
    [string]$OutputPath = ''
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $repo 'release/native-fade-build'
New-Item -ItemType Directory -Force -Path "$project/Assets/Editor", "$project/Packages", "$project/ProjectSettings" | Out-Null
Copy-Item -LiteralPath "$PSScriptRoot/NativeFadeComposite.shader" -Destination "$project/Assets/NativeFadeComposite.shader"
Copy-Item -LiteralPath "$PSScriptRoot/NativeFadeBuild.cs" -Destination "$project/Assets/Editor/NativeFadeBuild.cs"
Set-Content -LiteralPath "$project/Packages/manifest.json" -Value '{"dependencies":{"com.unity.modules.assetbundle":"1.0.0"}}' -Encoding utf8
Set-Content -LiteralPath "$project/ProjectSettings/ProjectVersion.txt" -Value 'm_EditorVersion: 2022.3.62f2' -Encoding utf8
$log = Join-Path $project 'build.log'
$arguments = @('-batchmode', '-quit', '-force-d3d11', '-projectPath', ('"' + $project + '"'), '-executeMethod', 'NativeFadeBuild.Run', '-logFile', ('"' + $log + '"'))
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Shader build failed ($($process.ExitCode)); see $log" }
$bundle = Join-Path $project 'Output/nativefade.bundle'
if (!(Test-Path -LiteralPath $bundle)) { throw "No shader bundle produced; see $log" }
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repo 'src/EnhancedSpectator/Resources/nativefade.bundle'
}
$destination = [IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($destination)) | Out-Null
Copy-Item -LiteralPath $bundle -Destination $destination -Force
Get-FileHash -Algorithm SHA256 -LiteralPath $destination
