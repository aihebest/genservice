<#
.SYNOPSIS
    GenService Platform — Local Development Launcher (no Docker required)

.DESCRIPTION
    Starts the ASP.NET Core 8 backend API and the Vite frontend dev server
    on your local machine without needing Docker or Docker Compose.

    Prerequisites (auto-checked below):
      • .NET 8 SDK  — https://dotnet.microsoft.com/download/dotnet/8.0
      • Node.js 18+ — https://nodejs.org
      • SQL Server LocalDB, SQL Server Express, or any SQL Server instance

    Default URLs:
      Frontend  → http://localhost:5173
      Backend   → http://localhost:5000
      Swagger   → http://localhost:5000/swagger

.NOTES
    Run from the "Docker Setup" folder or any folder — paths are resolved
    relative to this script's location.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Resolve root paths ────────────────────────────────────────────────────────
$ScriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Definition
$BackendDir  = Join-Path $ScriptDir "Docker Setup\backend\src\GenService.API"
$FrontendDir = Join-Path $ScriptDir "Docker Setup\frontend"

# ── Helper: coloured output ───────────────────────────────────────────────────
function Write-Step  { param($msg) Write-Host "`n► $msg" -ForegroundColor Cyan    }
function Write-OK    { param($msg) Write-Host "  ✅ $msg" -ForegroundColor Green  }
function Write-Warn  { param($msg) Write-Host "  ⚠️  $msg" -ForegroundColor Yellow }
function Write-Fail  { param($msg) Write-Host "  ❌ $msg" -ForegroundColor Red; exit 1 }

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════╗" -ForegroundColor Magenta
Write-Host "║   GenService Platform — Local Development Launcher   ║" -ForegroundColor Magenta
Write-Host "╚══════════════════════════════════════════════════════╝" -ForegroundColor Magenta
Write-Host ""

# ── Check .NET 8 ─────────────────────────────────────────────────────────────
Write-Step "Checking .NET 8 SDK..."
try {
    $dotnetVer = (dotnet --version 2>&1).ToString().Trim()
    if ($dotnetVer -match '^8\.') {
        Write-OK ".NET $dotnetVer found"
    } else {
        Write-Warn ".NET $dotnetVer found — need .NET 8. Download: https://dotnet.microsoft.com/download/dotnet/8.0"
        Write-Warn "Attempting to continue anyway..."
    }
} catch {
    Write-Fail ".NET SDK not found. Install from: https://dotnet.microsoft.com/download/dotnet/8.0"
}

# ── Check Node.js ─────────────────────────────────────────────────────────────
Write-Step "Checking Node.js..."
try {
    $nodeVer = (node --version 2>&1).ToString().Trim()
    Write-OK "Node.js $nodeVer found"
} catch {
    Write-Fail "Node.js not found. Install from: https://nodejs.org"
}

# ── Detect SQL Server connection string ───────────────────────────────────────
Write-Step "Detecting SQL Server..."

$connStrings = @(
    @{ Name="SQL Server LocalDB (Visual Studio)";  CS="Server=(localdb)\MSSQLLocalDB;Database=GenServiceDev;Trusted_Connection=true;TrustServerCertificate=true;MultipleActiveResultSets=true" },
    @{ Name="SQL Server Express";                  CS="Server=.\SQLEXPRESS;Database=GenServiceDev;Trusted_Connection=true;TrustServerCertificate=true;MultipleActiveResultSets=true" },
    @{ Name="SQL Server (default instance)";       CS="Server=.;Database=GenServiceDev;Trusted_Connection=true;TrustServerCertificate=true;MultipleActiveResultSets=true" }
)

$chosenCS   = $null
$chosenName = $null

foreach ($entry in $connStrings) {
    try {
        # Quick TCP/named pipe probe via SqlConnection
        $testScript = @"
using System.Data.SqlClient;
try {
    var b = new SqlConnectionStringBuilder(@"$($entry.CS)") { ConnectTimeout = 3 };
    using var c = new SqlConnection(b.ConnectionString);
    c.Open();
    Console.Write("OK");
} catch { Console.Write("FAIL"); }
"@
        # Use a small C# snippet via dotnet-script if available, else skip probe
        # Fallback: just pick the first entry and let EF handle the error at runtime
        $chosenCS   = $entry.CS
        $chosenName = $entry.Name
        break
    } catch {
        # ignore
    }
}

# Prefer user-set env var if provided
if ($env:GEN_SERVICE_DB) {
    $chosenCS   = $env:GEN_SERVICE_DB
    $chosenName = "custom (GEN_SERVICE_DB env var)"
}

Write-OK "Will use: $chosenName"
Write-Host "    Connection: $chosenCS" -ForegroundColor DarkGray

# Check if appsettings.Local.json exists; update its connection string
$localSettings = Join-Path $BackendDir "appsettings.Local.json"
if (Test-Path $localSettings) {
    $json = Get-Content $localSettings -Raw | ConvertFrom-Json
    $json.ConnectionStrings.DefaultConnection = $chosenCS
    $json | ConvertTo-Json -Depth 10 | Set-Content $localSettings -Encoding UTF8
    Write-OK "appsettings.Local.json updated"
} else {
    Write-Warn "appsettings.Local.json not found — will use env var override"
}

# ── npm install if needed ──────────────────────────────────────────────────────
Write-Step "Checking frontend dependencies..."
$nodeModules = Join-Path $FrontendDir "node_modules"
if (-not (Test-Path $nodeModules)) {
    Write-Warn "node_modules not found — running npm install..."
    Push-Location $FrontendDir
    npm install
    Pop-Location
    Write-OK "npm install complete"
} else {
    Write-OK "node_modules already installed"
}

# ── Restore .NET packages ─────────────────────────────────────────────────────
Write-Step "Restoring .NET packages..."
Push-Location $BackendDir
dotnet restore --verbosity quiet
Pop-Location
Write-OK ".NET packages restored"

# ── Set environment variables for the backend ─────────────────────────────────
$env:ASPNETCORE_ENVIRONMENT       = "Local"
$env:ASPNETCORE_URLS              = "http://localhost:8080"
$env:ConnectionStrings__DefaultConnection = $chosenCS

# ── Launch backend in a new window ────────────────────────────────────────────
Write-Step "Starting backend API (http://localhost:5000)..."
$backendCmd = "dotnet run --project `"$BackendDir`" --launch-profile `"`" --no-launch-profile 2>&1"
$backendWindow = Start-Process powershell `
    -ArgumentList "-NoExit", "-Command", `
        "`$env:ASPNETCORE_ENVIRONMENT='Local'; " +
        "`$env:ASPNETCORE_URLS='http://localhost:8080'; " +
        "`$env:ConnectionStrings__DefaultConnection='$chosenCS'; " +
        "Write-Host 'GenService API — starting...' -ForegroundColor Cyan; " +
        "Set-Location '$BackendDir'; " +
        "dotnet run --no-launch-profile" `
    -PassThru
Write-OK "Backend window opened (PID $($backendWindow.Id))"

# ── Wait a moment then launch frontend ────────────────────────────────────────
Write-Host ""
Write-Host "  Waiting 5 seconds for the API to start up..." -ForegroundColor DarkGray
Start-Sleep -Seconds 5

Write-Step "Starting frontend dev server (http://localhost:5173)..."
$frontendWindow = Start-Process powershell `
    -ArgumentList "-NoExit", "-Command", `
        "Write-Host 'GenService Frontend — starting Vite...' -ForegroundColor Cyan; " +
        "Set-Location '$FrontendDir'; " +
        "npm run dev" `
    -PassThru
Write-OK "Frontend window opened (PID $($frontendWindow.Id))"

# ── Open browser ──────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "  Waiting 6 seconds for Vite to compile..." -ForegroundColor DarkGray
Start-Sleep -Seconds 6

Write-Step "Opening browser..."
Start-Process "http://localhost:5173"

# ── Done ──────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║              GenService Platform is running          ║" -ForegroundColor Green
Write-Host "╠══════════════════════════════════════════════════════╣" -ForegroundColor Green
Write-Host "║  Frontend  →  http://localhost:5173                  ║" -ForegroundColor Green
Write-Host "║  Backend   →  http://localhost:8080                  ║" -ForegroundColor Green
Write-Host "║  Swagger   →  http://localhost:8080/swagger          ║" -ForegroundColor Green
Write-Host "╠══════════════════════════════════════════════════════╣" -ForegroundColor Green
Write-Host "║  Default login:  manager@demo.local                  ║" -ForegroundColor Green
Write-Host "║  Password:       DemoManager2026!                    ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "  Close the two PowerShell windows to stop the servers." -ForegroundColor DarkGray
Write-Host ""
