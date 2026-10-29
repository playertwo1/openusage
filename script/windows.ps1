param(
    [ValidateSet('build', 'test', 'run', 'publish')]
    [string]$Action = 'run',
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $projectRoot 'Windows\OpenUsage.Windows\OpenUsage.Windows.csproj'
$testProject = Join-Path $projectRoot 'Windows\OpenUsage.Windows.Tests\OpenUsage.Windows.Tests.csproj'
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnetPath = if ($dotnetCommand) { $dotnetCommand.Source } else { $null }
if (-not $dotnetPath -or -not (& $dotnetPath --list-sdks)) {
    $localSdk = Join-Path $env:USERPROFILE '.codex\tools\dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $localSdk) { $dotnetPath = $localSdk }
    else { throw 'Install The .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0' }
}

switch ($Action) {
    'build' { & $dotnetPath build $appProject --configuration Release --nologo }
    'test' { & $dotnetPath run --project $testProject --configuration Release --nologo }
    'run' { & $dotnetPath run --project $appProject --configuration Release --no-launch-profile }
    'publish' {
        $outputDirectory = Join-Path $projectRoot "dist\windows\$Runtime"
        & $dotnetPath publish $appProject --configuration Release --runtime $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None --output $outputDirectory --nologo
        if ($LASTEXITCODE -eq 0) { Write-Output "OpenUsage: $outputDirectory\OpenUsage.exe" }
    }
}
if ($LASTEXITCODE -ne 0) { throw "Windows $Action Failed (Exit $LASTEXITCODE)." }
