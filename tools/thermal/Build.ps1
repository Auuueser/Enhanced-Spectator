param([string]$Unity='D:\unity\2022.3.62f2\Editor\Unity.exe')
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project=Join-Path $repo 'release/thermal-unity'
New-Item -ItemType Directory -Force -Path "$project/Assets/Editor","$project/Packages","$project/ProjectSettings" | Out-Null
Copy-Item -LiteralPath "$PSScriptRoot/ThermalComposite.shader","$PSScriptRoot/ThermalSurface.shader","$repo/tools/native-fade/NativeFadeGpuTest.shader" -Destination "$project/Assets"
Copy-Item -LiteralPath "$PSScriptRoot/ThermalArchitectureTest.shader" -Destination "$project/Assets"
Copy-Item -LiteralPath "$PSScriptRoot/ThermalBuild.cs" -Destination "$project/Assets/Editor"
Copy-Item -LiteralPath "$repo/src/EnhancedSpectator/GameInterop/NativeFadeBuffers.cs" -Destination "$project/Assets/Editor"
Set-Content -LiteralPath "$project/Packages/manifest.json" -Value '{"dependencies":{"com.unity.modules.assetbundle":"1.0.0"}}' -Encoding utf8
Set-Content -LiteralPath "$project/ProjectSettings/ProjectVersion.txt" -Value 'm_EditorVersion: 2022.3.62f2' -Encoding utf8
if(Test-Path -LiteralPath "$project/gpu-results.json") { Remove-Item -LiteralPath "$project/gpu-results.json" }
$argsList=@('-batchmode','-quit','-force-d3d11','-projectPath',('"'+$project+'"'),'-executeMethod','ThermalBuild.Run','-logFile',('"'+$project+'/build.log"'))
$p=Start-Process -FilePath $Unity -ArgumentList $argsList -WindowStyle Hidden -PassThru -Wait
if($p.ExitCode -ne 0) { throw "Thermal GPU validation failed; see $project/build.log" }
$result=Get-Content -Raw -LiteralPath "$project/gpu-results.json" | ConvertFrom-Json
if(!$result.passed) { throw 'Thermal tests failed' }
Copy-Item -LiteralPath "$project/Output/thermal.bundle" -Destination "$repo/src/EnhancedSpectator/Resources/thermal.bundle"
$result | ConvertTo-Json
