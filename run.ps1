<#
.SYNOPSIS
    Starts the silo, API host, and SvelteKit dev server.

.DESCRIPTION
    Order matters: the API host is an Orleans client and needs the cluster up
    before it can connect, so this waits for the silo to report ready rather
    than starting everything at once.

    Ctrl+C stops all three.

.PARAMETER NoWeb
    Backend only — skip the SvelteKit dev server.

.PARAMETER SkipInstall
    Don't run 'npm install' even if web/node_modules is missing.

.EXAMPLE
    .\run.ps1
.EXAMPLE
    .\run.ps1 -NoWeb
#>
[CmdletBinding()]
param(
    [switch]$NoWeb,
    [switch]$SkipInstall
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# Matches CORS in api-host/Program.cs and API_BASE in web/src/lib/server/api.ts.
# The launchSettings profile says 5250; passing --urls keeps those in sync.
$ApiUrl = 'http://localhost:5080'
$WebUrl = 'http://localhost:5173'

$logDir = Join-Path $root '.logs'
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }

$procs = @()

function Start-Logged {
    param(
        [string]$Name,
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$WorkingDirectory
    )

    $out = Join-Path $logDir "$Name.log"
    $err = Join-Path $logDir "$Name.err.log"
    Remove-Item $out, $err -ErrorAction SilentlyContinue

    $p = Start-Process -FilePath $FilePath -ArgumentList $Arguments `
        -WorkingDirectory $WorkingDirectory -PassThru -NoNewWindow `
        -RedirectStandardOutput $out -RedirectStandardError $err

    Write-Host "  $Name started (pid $($p.Id)) - $out" -ForegroundColor DarkGray
    return $p
}

function Wait-ForLog {
    param(
        [string]$Name,
        [string]$Pattern,
        [System.Diagnostics.Process]$Process,
        [int]$TimeoutSeconds = 90
    )

    $out = Join-Path $logDir "$Name.log"
    $err = Join-Path $logDir "$Name.err.log"
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        if ($Process.HasExited) {
            Write-Host "$Name exited with code $($Process.ExitCode)." -ForegroundColor Red
            foreach ($f in @($out, $err)) {
                if ((Test-Path $f) -and (Get-Item $f).Length -gt 0) {
                    Get-Content $f -Tail 30 | Write-Host -ForegroundColor DarkYellow
                }
            }
            return $false
        }

        if ((Test-Path $out) -and (Select-String -Path $out -Pattern $Pattern -Quiet -ErrorAction SilentlyContinue)) {
            return $true
        }

        Start-Sleep -Milliseconds 400
    }

    Write-Host "$Name did not report ready within ${TimeoutSeconds}s." -ForegroundColor Red
    return $false
}

function Stop-All {
    foreach ($p in $script:procs) {
        if ($p -and -not $p.HasExited) {
            # Kill the tree: 'dotnet run' and 'npm' both spawn the real child.
            & taskkill.exe /PID $p.Id /T /F *> $null
        }
    }
}

try {
    Write-Host ''
    Write-Host 'GrainTrade' -ForegroundColor Green

    # 1. Silo — must be up before the client connects.
    # Relative project paths (resolved against WorkingDirectory) avoid the space
    # in an absolute home-dir path breaking the unquoted Start-Process arg list.
    $silo = Start-Logged -Name 'silo' -FilePath 'dotnet' `
        -Arguments @('run', '--project', 'silo/GrainTrade.Silo') `
        -WorkingDirectory $root
    $procs += $silo

    if (-not (Wait-ForLog -Name 'silo' -Pattern 'Application started|Silo started' -Process $silo)) {
        Stop-All; exit 1
    }
    Write-Host '  silo ready' -ForegroundColor Green

    # 2. API host — Orleans client + REST.
    $api = Start-Logged -Name 'api' -FilePath 'dotnet' `
        -Arguments @('run', '--project', 'api-host', '--urls', $ApiUrl) `
        -WorkingDirectory $root
    $procs += $api

    if (-not (Wait-ForLog -Name 'api' -Pattern 'Now listening on' -Process $api)) {
        Stop-All; exit 1
    }
    Write-Host "  api ready - $ApiUrl" -ForegroundColor Green

    # 3. Web.
    if (-not $NoWeb) {
        $webDir = Join-Path $root 'web'

        if (-not $SkipInstall -and -not (Test-Path (Join-Path $webDir 'node_modules'))) {
            Write-Host '  installing npm dependencies...' -ForegroundColor DarkGray
            Push-Location $webDir
            try { & npm.cmd install } finally { Pop-Location }
        }

        $web = Start-Logged -Name 'web' -FilePath 'npm.cmd' `
            -Arguments @('run', 'dev') -WorkingDirectory $webDir
        $procs += $web

        if (-not (Wait-ForLog -Name 'web' -Pattern 'ready in|Local:' -Process $web)) {
            Stop-All; exit 1
        }
        Write-Host "  web ready - $WebUrl" -ForegroundColor Green
    }

    Write-Host ''
    Write-Host "Logs: $logDir" -ForegroundColor DarkGray
    Write-Host 'Ctrl+C to stop all.' -ForegroundColor DarkGray
    Write-Host ''

    # Block until Ctrl+C or any child dies.
    while ($true) {
        Start-Sleep -Seconds 1
        $dead = $procs | Where-Object { $_.HasExited }
        if ($dead) {
            Write-Host 'A process exited - shutting down.' -ForegroundColor Yellow
            break
        }
    }
}
finally {
    Stop-All
}
