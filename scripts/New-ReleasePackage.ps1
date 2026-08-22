param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))

# The mod version lives in exactly one place per component; the server metadata
# is the source of truth for the archive name so it cannot drift behind a release.
$metadataPath = Join-Path $repositoryRoot "Airburst.Server\ModMetadata.cs"
$metadataText = Get-Content -LiteralPath $metadataPath -Raw
$versionMatch = [regex]::Match($metadataText, 'Version\s*\{[^}]*\}\s*=\s*new\("([0-9]+\.[0-9]+\.[0-9]+)"\)')
if (-not $versionMatch.Success) {
    throw "The mod version was not found in $metadataPath"
}
$modVersion = $versionMatch.Groups[1].Value

$pluginPath = Join-Path $repositoryRoot "Airburst.Client\Plugin.cs"
if (-not (Select-String -LiteralPath $pluginPath -Pattern ('PluginVersion = "' + [regex]::Escape($modVersion) + '"') -Quiet)) {
    throw "Client PluginVersion in $pluginPath does not match server version $modVersion"
}

$serverProjectPath = Join-Path $repositoryRoot "Airburst.Server\AirburstServer.csproj"
[xml]$serverProject = Get-Content -LiteralPath $serverProjectPath -Raw
$sptModName = [string](
    $serverProject.Project.PropertyGroup |
        ForEach-Object { $_.SptModName } |
        Where-Object { $_ } |
        Select-Object -First 1
)
if ([string]::IsNullOrWhiteSpace($sptModName)) {
    throw "SptModName is missing from $serverProjectPath"
}

$stageRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot "Airburst-v$modVersion"))
$archivePath = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot "maschine-Airburst-v$modVersion.zip"))
if (-not $stageRoot.StartsWith($artifactRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Release staging path escaped the repository artifact directory."
}

if (Test-Path -LiteralPath $stageRoot) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}
if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

# Packaging must never touch the live SPT installation.
dotnet build $serverProjectPath -c $Configuration --no-incremental -p:DeployToSpt=false
if ($LASTEXITCODE -ne 0) {
    throw "Airburst server build failed."
}
dotnet build (Join-Path $repositoryRoot "Airburst.Client\AirburstClient.csproj") -c $Configuration --no-incremental -p:DeployToSpt=false
if ($LASTEXITCODE -ne 0) {
    throw "Airburst client build failed."
}

function Find-BuildOutput([string]$projectDirectory, [string]$fileName) {
    $candidate = Get-ChildItem -LiteralPath (Join-Path $projectDirectory "bin\$Configuration") -Recurse -Filter $fileName |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if (-not $candidate) {
        throw "$fileName was not produced under $projectDirectory\bin\$Configuration"
    }
    return $candidate.FullName
}

$clientDll = Find-BuildOutput (Join-Path $repositoryRoot "Airburst.Client") "maschine-Airburst.Client.dll"
$serverDll = Find-BuildOutput (Join-Path $repositoryRoot "Airburst.Server") "maschine-Airburst.Server.dll"
# Fika satellite: built as a ProjectReference of the client; ships next to the client DLL and is only loaded when Fika is present.
$fikaDll = Find-BuildOutput (Join-Path $repositoryRoot "Airburst.Client.Fika") "maschine-Airburst.Client.Fika.dll"

$clientDirectory = Join-Path $stageRoot "BepInEx\plugins"
# SPT 4.1 server folder is SPT_Runtime, not SPT.
$serverDirectory = Join-Path $stageRoot "SPT_Runtime\user\mods\$sptModName"
$itemDirectory = Join-Path $serverDirectory "db\CustomItems"
New-Item -ItemType Directory -Path $clientDirectory, $serverDirectory, $itemDirectory -Force | Out-Null

Copy-Item -LiteralPath $clientDll -Destination $clientDirectory
Copy-Item -LiteralPath $fikaDll -Destination $clientDirectory
Copy-Item -LiteralPath $serverDll -Destination $serverDirectory
Copy-Item -Path (Join-Path $repositoryRoot "Airburst.Server\db\CustomItems\*.json") -Destination $itemDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot "README.md") -Destination $serverDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot "LICENSE") -Destination $serverDirectory

Compress-Archive -Path (Join-Path $stageRoot "*") -DestinationPath $archivePath -CompressionLevel Optimal
Write-Host "Created $archivePath"
