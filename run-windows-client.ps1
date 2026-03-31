param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [ValidateSet("x64", "x86", "ARM64")]
    [string]$Platform = "x64",

    [switch]$NoBuild,

    [switch]$RequireBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = $PSScriptRoot
$projectPath = Join-Path $repoRoot "SnapLingoWindows\SnapLingoWindows.csproj"
$dotnetCliHome = Join-Path $repoRoot ".dotnet-cli"

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Cannot find Windows project: $projectPath"
}

$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnetCommand) {
    throw "dotnet SDK is not installed or not available on PATH."
}

New-Item -ItemType Directory -Force -Path $dotnetCliHome | Out-Null
$env:DOTNET_CLI_HOME = $dotnetCliHome
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"

[xml]$projectXml = Get-Content -LiteralPath $projectPath
$targetFrameworkNode = $projectXml.SelectSingleNode("//Project/PropertyGroup/TargetFramework")
$targetFramework = $targetFrameworkNode.InnerText

if ([string]::IsNullOrWhiteSpace($targetFramework)) {
    throw "Cannot resolve TargetFramework from $projectPath"
}

$runtimeIdentifier = switch ($Platform) {
    "x64" { "win-x64" }
    "x86" { "win-x86" }
    "ARM64" { "win-arm64" }
    default { throw "Unsupported platform: $Platform" }
}

Push-Location $repoRoot
try {
    $exePath = Join-Path $repoRoot "SnapLingoWindows\bin\$Platform\$Configuration\$targetFramework\$runtimeIdentifier\SnapLingoWindows.exe"

    if (-not $NoBuild) {
        $buildFailed = $false

        try {
            Write-Host "Building SnapLingoWindows ($Configuration, $Platform)..."
            & $dotnetCommand.Source build $projectPath -c $Configuration "-p:Platform=$Platform"
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet build failed with exit code $LASTEXITCODE"
            }
        }
        catch {
            $buildFailed = $true
            $buildError = $_
        }

        if ($buildFailed) {
            if ($RequireBuild -or -not (Test-Path -LiteralPath $exePath)) {
                throw $buildError
            }

            Write-Warning "Build failed. Launching the last successful local build instead."
        }
    }

    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "Build output not found: $exePath"
    }

    Write-Host "Launching $exePath"
    $process = Start-Process -FilePath $exePath -WorkingDirectory (Split-Path -Path $exePath -Parent) -PassThru

    Start-Sleep -Seconds 5
    $process.Refresh()

    if ($process.HasExited) {
        throw "SnapLingoWindows exited before showing a usable window."
    }

    if ($process.MainWindowTitle -like "*could not be started*") {
        try {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
        catch {
        }

        throw "Windows reported a startup failure: $($process.MainWindowTitle)"
    }

    if ($process.MainWindowHandle -eq 0 -and [string]::IsNullOrWhiteSpace($process.MainWindowTitle)) {
        Write-Warning "The app process started, but no main window was detected yet."
    }
}
finally {
    Pop-Location
}
