# Quick Integration Test for AssetDumper
# Simplified version without complex string escaping issues

param(
    [string]$SamplePath = "C:\Users\kakut\Works\TaintUnity\joern\Samples\GRIS",
    [string]$OutputPath = "$PSScriptRoot\..\TestOutput"
)

$ErrorActionPreference = "Stop"

Write-Host "`n=====================================" -ForegroundColor Cyan
Write-Host "AssetDumper Quick Integration Test" -ForegroundColor Cyan
Write-Host "=====================================`n" -ForegroundColor Cyan

# Check prerequisites
Write-Host "[1/6] Checking prerequisites..." -ForegroundColor Yellow

if (-not (Test-Path $SamplePath)) {
    Write-Host "ERROR: GRIS sample not found at: $SamplePath" -ForegroundColor Red
    exit 1
}
Write-Host "  OK: GRIS sample found" -ForegroundColor Green

$ProjectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$AssetDumperProject = Join-Path $ProjectRoot "AssetRipper.Tools.AssetDumper"
$BinaryPath = Join-Path $ProjectRoot "0Bins\AssetRipper.Tools.AssetDumper\Debug\AssetDumper.dll"

if (-not (Test-Path $AssetDumperProject)) {
    Write-Host "ERROR: AssetDumper project not found" -ForegroundColor Red
    exit 1
}
Write-Host "  OK: AssetDumper project found" -ForegroundColor Green

# Build
Write-Host "`n[2/6] Building AssetDumper..." -ForegroundColor Yellow

Push-Location $AssetDumperProject
try {
    $BuildOutput = dotnet build --configuration Debug --no-incremental 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Build failed" -ForegroundColor Red
        Write-Host $BuildOutput
        exit 1
    }
    Write-Host "  OK: Build successful" -ForegroundColor Green
} finally {
    Pop-Location
}

if (-not (Test-Path $BinaryPath)) {
    Write-Host "ERROR: Binary not found at: $BinaryPath" -ForegroundColor Red
    exit 1
}
Write-Host "  OK: Binary found" -ForegroundColor Green

# Prepare output
Write-Host "`n[3/6] Preparing output directory..." -ForegroundColor Yellow

if (Test-Path $OutputPath) {
    Remove-Item -Path $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
Write-Host "  OK: Output directory ready" -ForegroundColor Green

# Run export
Write-Host "`n[4/6] Running AssetDumper export..." -ForegroundColor Yellow

$ExportArgs = @(
    $BinaryPath,
    "--input", $SamplePath,
    "--output", $OutputPath,
    "--facts",
    "--relations",
    "--enable-index",
    "--indexes",
    "--metrics",
    "--manifest",
    "--compression", "zstd"
)

Write-Host "  Command: dotnet $($ExportArgs -join ' ')" -ForegroundColor Gray

$ExportStart = Get-Date
$ExportOutput = & dotnet @ExportArgs 2>&1
$ExportDuration = (Get-Date) - $ExportStart

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Export failed with exit code: $LASTEXITCODE" -ForegroundColor Red
    Write-Host $ExportOutput
    exit 1
}

$ExportSeconds = [math]::Round($ExportDuration.TotalSeconds, 2)
Write-Host "  OK: Export completed in $ExportSeconds seconds" -ForegroundColor Green

# Validate structure
Write-Host "`n[5/6] Validating output structure..." -ForegroundColor Yellow

$TestResults = @{
    Passed = 0
    Failed = 0
}

function Test-Path2 {
    param([string]$Path, [string]$Name)
    if (Test-Path $Path) {
        Write-Host "  OK: $Name" -ForegroundColor Green
        $TestResults.Passed++
        return $true
    } else {
        Write-Host "  FAIL: $Name missing" -ForegroundColor Red
        $TestResults.Failed++
        return $false
    }
}

# Check directories
Test-Path2 (Join-Path $OutputPath "facts") "facts directory" | Out-Null
Test-Path2 (Join-Path $OutputPath "relations") "relations directory" | Out-Null
Test-Path2 (Join-Path $OutputPath "indexes") "indexes directory" | Out-Null
Test-Path2 (Join-Path $OutputPath "metrics") "metrics directory" | Out-Null
Test-Path2 (Join-Path $OutputPath "schemas") "schemas directory" | Out-Null

# Check manifest
$ManifestPath = Join-Path $OutputPath "manifest.json"
if (Test-Path2 $ManifestPath "manifest.json") {
    try {
        $Manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
        
        if ($Manifest.PSObject.Properties['version']) {
            Write-Host "  OK: Manifest version: $($Manifest.version)" -ForegroundColor Green
            $TestResults.Passed++
        } else {
            Write-Host "  FAIL: Manifest missing version" -ForegroundColor Red
            $TestResults.Failed++
        }
        
        if ($Manifest.PSObject.Properties['tables']) {
            Write-Host "  OK: Manifest has $($Manifest.tables.Count) tables" -ForegroundColor Green
            $TestResults.Passed++
        } else {
            Write-Host "  FAIL: Manifest missing tables" -ForegroundColor Red
            $TestResults.Failed++
        }
    } catch {
        Write-Host "  FAIL: Cannot parse manifest: $($_.Exception.Message)" -ForegroundColor Red
        $TestResults.Failed++
    }
}

# Check facts
$FactsDir = Join-Path $OutputPath "facts"
$factFiles = @("collections.ndjson", "assets.ndjson", "types.ndjson", "bundles.ndjson")
foreach ($file in $factFiles) {
    $filePath = Join-Path $FactsDir $file
    $compressedPath = "$filePath.zst"
    
    if ((Test-Path $filePath) -or (Test-Path $compressedPath)) {
        Write-Host "  OK: $file found" -ForegroundColor Green
        $TestResults.Passed++
    } else {
        Write-Host "  FAIL: $file missing" -ForegroundColor Red
        $TestResults.Failed++
    }
}

# Check scripts facts
$ScriptFactsDir = Join-Path $FactsDir "scripts"
if (Test-Path $ScriptFactsDir) {
    $scriptFiles = Get-ChildItem -Path $ScriptFactsDir -File -Recurse
    Write-Host "  OK: scripts has $($scriptFiles.Count) files" -ForegroundColor Green
    $TestResults.Passed++
} else {
    Write-Host "  FAIL: scripts missing" -ForegroundColor Red
    $TestResults.Failed++
}

# Check indexes
$IndexesDir = Join-Path $OutputPath "indexes"
$indexFiles = Get-ChildItem -Path $IndexesDir -Filter "*.kindex" -Recurse -ErrorAction SilentlyContinue
if ($indexFiles -and $indexFiles.Count -gt 0) {
    Write-Host "  OK: Found $($indexFiles.Count) index files" -ForegroundColor Green
    $TestResults.Passed++
} else {
    Write-Host "  FAIL: No index files found" -ForegroundColor Red
    $TestResults.Failed++
}

# Check metrics
$MetricsDir = Join-Path $OutputPath "metrics"
$metricsFiles = Get-ChildItem -Path $MetricsDir -Filter "*.json" -ErrorAction SilentlyContinue
if ($metricsFiles -and $metricsFiles.Count -gt 0) {
    Write-Host "  OK: Found $($metricsFiles.Count) metrics files" -ForegroundColor Green
    $TestResults.Passed++
} else {
    Write-Host "  FAIL: No metrics files found" -ForegroundColor Red
    $TestResults.Failed++
}

# Calculate statistics
Write-Host "`n[6/6] Calculating statistics..." -ForegroundColor Yellow

$TotalSize = 0
Get-ChildItem -Path $OutputPath -Recurse -File | ForEach-Object { $TotalSize += $_.Length }
$TotalSizeMB = [math]::Round($TotalSize / 1MB, 2)

$FileCount = (Get-ChildItem -Path $OutputPath -Recurse -File).Count

Write-Host "  Total files: $FileCount" -ForegroundColor Cyan
Write-Host "  Total size: $TotalSizeMB MB" -ForegroundColor Cyan
Write-Host "  Export time: $ExportSeconds seconds" -ForegroundColor Cyan

# Final results
Write-Host "`n=====================================" -ForegroundColor Cyan
Write-Host "TEST RESULTS" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Passed: $($TestResults.Passed)" -ForegroundColor Green
Write-Host "Failed: $($TestResults.Failed)" -ForegroundColor $(if ($TestResults.Failed -gt 0) { "Red" } else { "Green" })
Write-Host "=====================================" -ForegroundColor Cyan

if ($TestResults.Failed -gt 0) {
    Write-Host "`nIntegration test FAILED" -ForegroundColor Red
    exit 1
} else {
    Write-Host "`nAll tests PASSED!" -ForegroundColor Green
    exit 0
}
