# AssetDumper Integration Test Script
# Purpose: Run comprehensive integration tests on GRIS sample and evaluate output
# Date: 2025-11-07

param(
    [string]$SamplePath = "C:\Users\kakut\Works\TaintUnity\joern\Samples\GRIS",
    [string]$OutputPath = "$PSScriptRoot\..\TestOutput",
    [switch]$SkipBuild,
    [switch]$SkipExport,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$StartTime = Get-Date

# Colors
function Write-Success { param($Message) Write-Host "✓ $Message" -ForegroundColor Green }
function Write-Info { param($Message) Write-Host "ℹ $Message" -ForegroundColor Cyan }
function Write-Warning2 { param($Message) Write-Host "⚠ $Message" -ForegroundColor Yellow }
function Write-Error2 { param($Message) Write-Host "✗ $Message" -ForegroundColor Red }
function Write-Section { param($Message) Write-Host "`n═══ $Message ═══" -ForegroundColor Magenta }

# Test Results
$TestResults = @{
    Passed = 0
    Failed = 0
    Warnings = 0
    Tests = @()
}

function Add-TestResult {
    param(
        [string]$TestName,
        [bool]$Passed,
        [string]$Message = "",
        [string]$Details = ""
    )
    
    $TestResults.Tests += @{
        Name = $TestName
        Passed = $Passed
        Message = $Message
        Details = $Details
        Timestamp = Get-Date
    }
    
    if ($Passed) {
        $TestResults.Passed++
        Write-Success "$TestName - $Message"
    } else {
        $TestResults.Failed++
        Write-Error2 "$TestName - $Message"
        if ($Details) {
            Write-Host "  Details: $Details" -ForegroundColor Gray
        }
    }
}

Write-Section "AssetDumper Integration Test Suite"
Write-Info "Start Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Write-Info "GRIS Sample: $SamplePath"
Write-Info "Output Path: $OutputPath"

# Step 1: Validate Prerequisites
Write-Section "Step 1: Prerequisites Validation"

if (-not (Test-Path $SamplePath)) {
    Write-Error2 "GRIS sample not found at: $SamplePath"
    exit 1
}
Write-Success "GRIS sample found"

$ProjectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$AssetDumperProject = Join-Path $ProjectRoot "AssetRipper.Tools.AssetDumper"
$BinaryPath = Join-Path $ProjectRoot "0Bins\AssetRipper.Tools.AssetDumper\Debug\AssetDumper.dll"

if (-not (Test-Path $AssetDumperProject)) {
    Write-Error2 "AssetDumper project not found at: $AssetDumperProject"
    exit 1
}
Write-Success "AssetDumper project found"

# Step 2: Build Project
if (-not $SkipBuild) {
    Write-Section "Step 2: Building AssetDumper"
    
    Push-Location $AssetDumperProject
    try {
        $BuildOutput = dotnet build --configuration Debug --no-incremental 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error2 "Build failed"
            Write-Host $BuildOutput
            exit 1
        }
        Write-Success "Build completed successfully"
    } finally {
        Pop-Location
    }
} else {
    Write-Info "Skipping build (-SkipBuild specified)"
}

if (-not (Test-Path $BinaryPath)) {
    Write-Error2 "Binary not found at: $BinaryPath"
    exit 1
}
Write-Success "AssetDumper binary found"

# Step 3: Clean and Prepare Output Directory
Write-Section "Step 3: Preparing Output Directory"

if (Test-Path $OutputPath) {
    Write-Info "Cleaning existing output directory..."
    Remove-Item -Path $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
Write-Success "Output directory ready: $OutputPath"

# Step 4: Run AssetDumper Export
if (-not $SkipExport) {
    Write-Section "Step 4: Running AssetDumper Export"
    
    $ExportArgs = @(
        $BinaryPath,
        $SamplePath,
        $OutputPath,
        "--facts",
        "--relations",
        "--indexes",
        "--metrics",
        "--manifest",
        "--compression", "zstd-seekable",
        "--compression-level", "3"
    )
    
    Write-Info "Command: dotnet $($ExportArgs -join ' ')"
    
    $ExportStart = Get-Date
    $ExportOutput = & dotnet @ExportArgs 2>&1
    $ExportDuration = (Get-Date) - $ExportStart
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error2 "Export failed with exit code: $LASTEXITCODE"
        Write-Host $ExportOutput
        exit 1
    }
    
    Write-Success "Export completed in $([math]::Round($ExportDuration.TotalSeconds, 2)) seconds"
    
    if ($Verbose) {
        Write-Host "`nExport Output:" -ForegroundColor Gray
        Write-Host $ExportOutput -ForegroundColor Gray
    }
} else {
    Write-Info "Skipping export (-SkipExport specified)"
}

# Step 5: Validate Directory Structure
Write-Section "Step 5: Directory Structure Validation"

$ExpectedDirs = @(
    "facts",
    "relations",
    "indexes",
    "metrics",
    "schemas"
)

foreach ($dir in $ExpectedDirs) {
    $dirPath = Join-Path $OutputPath $dir
    if (Test-Path $dirPath) {
        Add-TestResult -TestName "Directory: $dir" -Passed $true -Message "Exists"
    } else {
        Add-TestResult -TestName "Directory: $dir" -Passed $false -Message "Missing" -Details $dirPath
    }
}

# Check for manifest.json
$ManifestPath = Join-Path $OutputPath "manifest.json"
if (Test-Path $ManifestPath) {
    Add-TestResult -TestName "File: manifest.json" -Passed $true -Message "Exists"
} else {
    Add-TestResult -TestName "File: manifest.json" -Passed $false -Message "Missing"
}

# Step 6: Validate Facts Tables
Write-Section "Step 6: Facts Tables Validation"

$FactsTables = @(
    "collections.ndjson",
    "assets.ndjson",
    "types.ndjson",
    "bundles.ndjson"
)

$FactsDir = Join-Path $OutputPath "facts"
foreach ($table in $FactsTables) {
    $tablePath = Join-Path $FactsDir $table
    $compressedPath = "$tablePath.zst"
    
    if (Test-Path $tablePath) {
        $lineCount = (Get-Content $tablePath | Measure-Object -Line).Lines
        Add-TestResult -TestName "Facts: $table" -Passed $true -Message "$lineCount records" -Details $tablePath
    } elseif (Test-Path $compressedPath) {
        $fileSize = (Get-Item $compressedPath).Length
        Add-TestResult -TestName "Facts: $table" -Passed $true -Message "Compressed ($fileSize bytes)" -Details $compressedPath
    } else {
        Add-TestResult -TestName "Facts: $table" -Passed $false -Message "Missing" -Details "Neither $tablePath nor $compressedPath found"
    }
}

# Check for scripts subdirectory
$ScriptFactsDir = Join-Path $FactsDir "scripts"
if (Test-Path $ScriptFactsDir) {
    $scriptFiles = Get-ChildItem -Path $ScriptFactsDir -File -Recurse
    Add-TestResult -TestName "Facts: scripts" -Passed $true -Message "$($scriptFiles.Count) files" -Details $ScriptFactsDir
} else {
    Add-TestResult -TestName "Facts: scripts" -Passed $false -Message "Missing" -Details $ScriptFactsDir
}

# Step 7: Validate Relations Tables
Write-Section "Step 7: Relations Tables Validation"

$RelationsTables = @(
    "asset_dependencies.ndjson"
)

$RelationsDir = Join-Path $OutputPath "relations"
foreach ($table in $RelationsTables) {
    $tablePath = Join-Path $RelationsDir $table
    $compressedPath = "$tablePath.zst"
    
    if (Test-Path $tablePath) {
        $lineCount = (Get-Content $tablePath | Measure-Object -Line).Lines
        Add-TestResult -TestName "Relations: $table" -Passed $true -Message "$lineCount records" -Details $tablePath
    } elseif (Test-Path $compressedPath) {
        $fileSize = (Get-Item $compressedPath).Length
        Add-TestResult -TestName "Relations: $table" -Passed $true -Message "Compressed ($fileSize bytes)" -Details $compressedPath
    } else {
        Add-TestResult -TestName "Relations: $table" -Passed $false -Message "Missing" -Details "Neither $tablePath nor $compressedPath found"
    }
}

# Step 8: Validate Indexes
Write-Section "Step 8: Indexes Validation"

$IndexesDir = Join-Path $OutputPath "indexes"
$indexFiles = Get-ChildItem -Path $IndexesDir -Filter "*.kindex" -Recurse -ErrorAction SilentlyContinue

if ($indexFiles) {
    foreach ($indexFile in $indexFiles) {
        $fileSize = $indexFile.Length
        Add-TestResult -TestName "Index: $($indexFile.Name)" -Passed $true -Message "$fileSize bytes" -Details $indexFile.FullName
    }
} else {
    Add-TestResult -TestName "Indexes" -Passed $false -Message "No .kindex files found" -Details $IndexesDir
}

# Step 9: Validate Metrics
Write-Section "Step 9: Metrics Validation"

$MetricsDir = Join-Path $OutputPath "metrics"
$metricsFiles = Get-ChildItem -Path $MetricsDir -Filter "*.json" -ErrorAction SilentlyContinue

if ($metricsFiles) {
    foreach ($metricFile in $metricsFiles) {
        $content = Get-Content $metricFile.FullName -Raw | ConvertFrom-Json
        $recordCount = if ($content.PSObject.Properties['recordCount']) { $content.recordCount } else { "N/A" }
        Add-TestResult -TestName "Metric: $($metricFile.Name)" -Passed $true -Message "Records: $recordCount" -Details $metricFile.FullName
    }
} else {
    Add-TestResult -TestName "Metrics" -Passed $false -Message "No .json files found" -Details $MetricsDir
}

# Step 10: Validate Manifest
Write-Section "Step 10: Manifest Validation"

if (Test-Path $ManifestPath) {
    try {
        $Manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
        
        # Check version
        if ($Manifest.PSObject.Properties['version']) {
            Add-TestResult -TestName "Manifest: version" -Passed $true -Message $Manifest.version
        } else {
            Add-TestResult -TestName "Manifest: version" -Passed $false -Message "Missing"
        }
        
        # Check tables
        if ($Manifest.PSObject.Properties['tables']) {
            $tableCount = $Manifest.tables.Count
            Add-TestResult -TestName "Manifest: tables" -Passed $true -Message "$tableCount tables registered"
            
            # Validate each table
            foreach ($table in $Manifest.tables) {
                $tableName = $table.name
                $shardCount = if ($table.shards) { $table.shards.Count } else { 0 }
                $recordCount = if ($table.PSObject.Properties['recordCount']) { $table.recordCount } else { 0 }
                
                Write-Info "  - $tableName`: $recordCount records in $shardCount shards"
            }
        } else {
            Add-TestResult -TestName "Manifest: tables" -Passed $false -Message "Missing"
        }
        
        # Check formats
        if ($Manifest.PSObject.Properties['formats']) {
            $formatCount = $Manifest.formats.Count
            Add-TestResult -TestName "Manifest: formats" -Passed $true -Message "$formatCount formats registered"
        } else {
            Add-TestResult -TestName "Manifest: formats" -Passed $false -Message "Missing"
        }
        
        # Check schemas
        if ($Manifest.PSObject.Properties['schemas']) {
            $schemaCount = $Manifest.schemas.Count
            Add-TestResult -TestName "Manifest: schemas" -Passed $true -Message "$schemaCount schemas registered"
        } else {
            Add-TestResult -TestName "Manifest: schemas" -Passed $false -Message "Missing"
        }
        
    } catch {
        Add-TestResult -TestName "Manifest: parsing" -Passed $false -Message "Failed to parse" -Details $_.Exception.Message
    }
} else {
    Add-TestResult -TestName "Manifest: existence" -Passed $false -Message "File not found"
}

# Step 11: Calculate Statistics
Write-Section "Step 11: Output Statistics"

$TotalSize = 0
Get-ChildItem -Path $OutputPath -Recurse -File | ForEach-Object { $TotalSize += $_.Length }

$Stats = @{
    TotalFiles = (Get-ChildItem -Path $OutputPath -Recurse -File).Count
    TotalSize = $TotalSize
    TotalSizeMB = [math]::Round($TotalSize / 1MB, 2)
    DirectoryCount = (Get-ChildItem -Path $OutputPath -Recurse -Directory).Count
}

Write-Info "Total Files: $($Stats.TotalFiles)"
Write-Info "Total Size: $($Stats.TotalSizeMB) MB"
Write-Info "Directories: $($Stats.DirectoryCount)"

# Find largest files
Write-Host "`nLargest Files:" -ForegroundColor Cyan
Get-ChildItem -Path $OutputPath -Recurse -File | 
    Sort-Object Length -Descending | 
    Select-Object -First 10 | 
    ForEach-Object {
        $sizeKB = [math]::Round($_.Length / 1KB, 2)
        $relativePath = $_.FullName.Replace($OutputPath, "").TrimStart('\')
        Write-Host "  $sizeKB KB - $relativePath" -ForegroundColor Gray
    }

# Step 12: Generate Test Report
Write-Section "Step 12: Test Results Summary"

$EndTime = Get-Date
$TotalDuration = $EndTime - $StartTime

Write-Host "`n╔════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║           INTEGRATION TEST RESULTS                  ║" -ForegroundColor Cyan
Write-Host "╠════════════════════════════════════════════════════╣" -ForegroundColor Cyan
Write-Host "║  Total Tests: $($TestResults.Passed + $TestResults.Failed)".PadRight(52) + "║" -ForegroundColor Cyan
Write-Host "║  Passed:      $($TestResults.Passed)".PadRight(52) + "║" -ForegroundColor Green
Write-Host "║  Failed:      $($TestResults.Failed)".PadRight(52) + "║" -ForegroundColor $(if ($TestResults.Failed -gt 0) { "Red" } else { "Cyan" })
Write-Host "║  Duration:    $([math]::Round($TotalDuration.TotalSeconds, 2))s".PadRight(52) + "║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════╝" -ForegroundColor Cyan

# Generate JSON report
$ReportPath = Join-Path $OutputPath "test-report.json"
$Report = @{
    TestSuite = "AssetDumper Integration Tests"
    StartTime = $StartTime.ToString("yyyy-MM-dd HH:mm:ss")
    EndTime = $EndTime.ToString("yyyy-MM-dd HH:mm:ss")
    Duration = $TotalDuration.TotalSeconds
    Results = $TestResults
    Statistics = $Stats
    Configuration = @{
        SamplePath = $SamplePath
        OutputPath = $OutputPath
        SkipBuild = $SkipBuild.IsPresent
        SkipExport = $SkipExport.IsPresent
    }
}

$Report | ConvertTo-Json -Depth 10 | Set-Content $ReportPath
Write-Success "Test report saved to: $ReportPath"

# Exit with appropriate code
if ($TestResults.Failed -gt 0) {
    Write-Error2 "`nIntegration tests FAILED with $($TestResults.Failed) failures"
    exit 1
} else {
    Write-Success "`nAll integration tests PASSED!"
    exit 0
}
