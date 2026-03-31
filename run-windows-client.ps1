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

if ($null -eq $targetFrameworkNode) {
    throw "Cannot resolve TargetFramework node from $projectPath"
}

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

function Stop-RunningAppInstance {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath
    )

    if (-not (Test-Path -LiteralPath $ExecutablePath)) {
        return
    }

    $normalizedExecutablePath = [System.IO.Path]::GetFullPath($ExecutablePath)
    $matchingProcesses = Get-Process -Name "SnapLingoWindows" -ErrorAction SilentlyContinue | Where-Object {
        try {
            $_.Path -and ([System.IO.Path]::GetFullPath($_.Path) -ieq $normalizedExecutablePath)
        }
        catch {
            $false
        }
    }

    foreach ($process in $matchingProcesses) {
        Write-Host "Stopping running instance $($process.Id) before rebuild..."
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit(5000) | Out-Null
    }
}

Push-Location $repoRoot
try {
    $exePath = Join-Path $repoRoot "SnapLingoWindows\bin\$Platform\$Configuration\$targetFramework\$runtimeIdentifier\SnapLingoWindows.exe"

    if (-not $NoBuild) {
        Stop-RunningAppInstance -ExecutablePath $exePath
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
    Start-Process -FilePath $exePath -WorkingDirectory (Split-Path -Path $exePath -Parent) | Out-Null
}
finally {
    Pop-Location
}
