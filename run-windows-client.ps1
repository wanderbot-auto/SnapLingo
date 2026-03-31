param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [ValidateSet("x64", "x86", "ARM64")]
    [string]$Platform = "x64",

    [switch]$NoBuild,

    [switch]$RequireBuild,

    [switch]$Watch,

    [int]$WatchDelayMs = 800
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = $PSScriptRoot
$projectRoot = Join-Path $repoRoot "SnapLingoWindows"
$projectPath = Join-Path $projectRoot "SnapLingoWindows.csproj"
$dotnetCliHome = Join-Path $repoRoot ".dotnet-cli"

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Cannot find Windows project: $projectPath"
}

if ($WatchDelayMs -lt 200) {
    throw "WatchDelayMs must be at least 200."
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

$exePath = Join-Path $repoRoot "SnapLingoWindows\bin\$Platform\$Configuration\$targetFramework\$runtimeIdentifier\SnapLingoWindows.exe"

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

function Invoke-BuildAndLaunch {
    param(
        [switch]$SkipBuild
    )

    if (-not $SkipBuild) {
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

function Resolve-WatchedPath {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.WaitForChangedResult]$Result
    )

    if ([string]::IsNullOrWhiteSpace($Result.Name)) {
        return $null
    }

    $candidatePath = Join-Path $projectRoot $Result.Name
    $normalizedPath = [System.IO.Path]::GetFullPath($candidatePath)

    if ($normalizedPath -match '\\(bin|obj)(\\|$)') {
        return $null
    }

    return $normalizedPath
}

function Start-WatchLoop {
    $watcher = [System.IO.FileSystemWatcher]::new($projectRoot)
    $watcher.IncludeSubdirectories = $true
    $watcher.NotifyFilter = [System.IO.NotifyFilters]'FileName, DirectoryName, LastWrite, CreationTime'

    $changeTypes =
        [System.IO.WatcherChangeTypes]::Changed `
        -bor [System.IO.WatcherChangeTypes]::Created `
        -bor [System.IO.WatcherChangeTypes]::Deleted `
        -bor [System.IO.WatcherChangeTypes]::Renamed

    try {
        Write-Host "Watching $projectRoot for changes. Press Ctrl+C to stop."

        while ($true) {
            $change = $watcher.WaitForChanged($changeTypes, 250)
            if ($change.TimedOut) {
                continue
            }

            $changedPath = Resolve-WatchedPath -Result $change
            if ($null -eq $changedPath) {
                continue
            }

            while ($true) {
                $nextChange = $watcher.WaitForChanged($changeTypes, $WatchDelayMs)
                if ($nextChange.TimedOut) {
                    break
                }

                $nextChangedPath = Resolve-WatchedPath -Result $nextChange
                if ($null -ne $nextChangedPath) {
                    $changedPath = $nextChangedPath
                }
            }

            Write-Host ""
            Write-Host "Detected change: $changedPath"

            try {
                Invoke-BuildAndLaunch
            }
            catch {
                Write-Error $_
            }
        }
    }
    finally {
        $watcher.Dispose()
    }
}

Push-Location $repoRoot
try {
    Invoke-BuildAndLaunch -SkipBuild:$NoBuild

    if ($Watch) {
        Start-WatchLoop
    }
}
finally {
    Pop-Location
}
