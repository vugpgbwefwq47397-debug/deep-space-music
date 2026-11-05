param([string]$GameDir='X:\SteamLibrary\steamapps\common\Probably Stolen',[string]$Dotnet='dotnet')
$ErrorActionPreference='Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
 if (!(Test-Path -LiteralPath "$GameDir\Probably Stolen_Data\Managed\Assembly-CSharp.dll")) {throw 'GameDir must point to an installed full game'}
 & $Dotnet build src/GameMod -c Release "-p:GameDir=$GameDir"
 if($LASTEXITCODE) {throw 'Mod build failed'}
 & $Dotnet run --project tests/CoreTests.csproj -c Release
 if($LASTEXITCODE) {throw 'Tests failed'}
 & $Dotnet publish src/MusicBridge -c Release -r win-x64 --self-contained true -o dist/MusicBridge
 if($LASTEXITCODE) {throw 'Bridge publish failed'}
 New-Item -ItemType Directory -Force dist/Mods/DeepSpaceMusic | Out-Null
 Copy-Item src/GameMod/bin/Release/netstandard2.1/DeepSpaceMusic.dll,src/GameMod/manifest.xml dist/Mods/DeepSpaceMusic
 Copy-Item src/GameMod/Fonts dist/Mods/DeepSpaceMusic -Recurse -Force
 $sdkRoot=Split-Path (Get-Command $Dotnet).Source
 foreach($notice in @('LICENSE.txt','ThirdPartyNotices.txt')) {if(Test-Path -LiteralPath "$sdkRoot/$notice") {Copy-Item -LiteralPath "$sdkRoot/$notice" -Destination dist/MusicBridge}}
 Copy-Item README.md dist
 Write-Output 'Built dist. Keep MusicBridge OUTSIDE the game Mods directory.'
} finally {Pop-Location}
