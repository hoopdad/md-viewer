[CmdletBinding()]
param(
    [ValidateSet("Release", "Debug")]
    [string] $Configuration = "Release",

    [ValidateSet("All", "x64", "arm64")]
    [string] $Architecture = "All"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$installerProject = Join-Path $repositoryRoot "installer\MdViewer.Installer\MdViewer.Installer.wixproj"
$appProject = Join-Path $repositoryRoot "src\MdViewer.App\MdViewer.App.csproj"

$architectures = if ($Architecture -eq "All") {
    @("x64", "arm64")
} else {
    @($Architecture)
}

foreach ($targetArchitecture in $architectures) {
    $runtimeIdentifier = "win-$targetArchitecture"
    $publishDirectory = Join-Path $repositoryRoot "artifacts\publish\$runtimeIdentifier"

    if (Test-Path -LiteralPath $publishDirectory) {
        Remove-Item -LiteralPath $publishDirectory -Recurse -Force
    }

    dotnet publish $appProject `
        --configuration $Configuration `
        --runtime $runtimeIdentifier `
        --self-contained true `
        --output $publishDirectory `
        -p:PublishSingleFile=false `
        -p:PublishReadyToRun=false `
        -p:DebugSymbols=false `
        -p:DebugType=None

    if ($LASTEXITCODE -ne 0) {
        throw "Publishing md-viewer for $targetArchitecture failed."
    }

    dotnet build $installerProject `
        --configuration $Configuration `
        -p:Platform=$targetArchitecture `
        -p:InstallerPlatform=$targetArchitecture `
        -p:PublishDir=$publishDirectory `
        -p:OutputName="md-viewer-setup-$targetArchitecture"

    if ($LASTEXITCODE -ne 0) {
        throw "Building the md-viewer $targetArchitecture installer failed."
    }

    $installer = Join-Path $repositoryRoot "installer\MdViewer.Installer\bin\$targetArchitecture\$Configuration\md-viewer-setup-$targetArchitecture.msi"
    Write-Host "Installer created: $installer"
}
