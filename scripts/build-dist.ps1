param(
    [string]$Variant
)

$ErrorActionPreference = "Stop"

$dotnetTargetSyncTrayzor = "net8.0-windows10.0.17763.0"
. "$PSScriptRoot\helpers\get-arch.ps1"
$arch = Get-Arch
$dotnetArch = switch ($arch) {
    "amd64" { "win-x64" }
    "arm64" { "win-arm64" }
    default { throw "Unknown architecture: $arch" }
}
$syncthingExe = ".\syncthing\syncthing.exe"
$publishDir = ".\src\SyncTrayzor\bin\Release\$dotnetTargetSyncTrayzor\$dotnetArch\"
$mergedDir = ".\dist"

Write-Host "Building SyncTrayzor for $Variant"
dotnet build -c Release -p:DebugType=None -p:DebugSymbols=false -p:AppConfigVariant=$Variant src/SyncTrayzor/SyncTrayzor.csproj
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build SyncTrayzor. Exiting."
    exit $LASTEXITCODE
}

# Remove and recreate merged directory
if (Test-Path $mergedDir) {
    Remove-Item $mergedDir -Recurse -Force
}
New-Item -ItemType Directory -Path $mergedDir | Out-Null
Copy-Item "$publishDir\*" $mergedDir -Recurse -Force
Copy-Item $syncthingExe $mergedDir -Force
