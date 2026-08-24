[CmdletBinding()]
param(
    [ValidateSet("Release", "Debug")]
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$publishDirectory = Join-Path $repositoryRoot "artifacts\publish\win-x64"
$installerProject = Join-Path $repositoryRoot "installer\MdViewer.Installer\MdViewer.Installer.wixproj"
$appProject = Join-Path $repositoryRoot "src\MdViewer.App\MdViewer.App.csproj"

dotnet publish $appProject `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDirectory `
    -p:PublishSingleFile=false `
    -p:DebugSymbols=false `
    -p:DebugType=None

if ($LASTEXITCODE -ne 0) {
    throw "Publishing md-viewer failed."
}

dotnet build $installerProject `
    --configuration $Configuration `
    -p:PublishDir=$publishDirectory

if ($LASTEXITCODE -ne 0) {
    throw "Building the md-viewer installer failed."
}

$installer = Join-Path $repositoryRoot "installer\MdViewer.Installer\bin\x64\$Configuration\md-viewer-setup.msi"
Write-Host "Installer created: $installer"
