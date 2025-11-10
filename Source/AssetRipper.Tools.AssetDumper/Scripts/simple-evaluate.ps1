# Simple Output Evaluation Script
param(
    [Parameter(Mandatory=$true)]
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "AssetDumper Output Evaluation" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$Score = 0
$MaxScore = 0
$Issues = @()

function Test-Item {
    param([string]$Name, [int]$Points, [scriptblock]$Test)
    
    $script:MaxScore += $Points
    try {
        $result = & $Test
        if ($result) {
            $script:Score += $Points
            Write-Host "  [PASS] $Name - $Points points" -ForegroundColor Green
            return $true
        } else {
            Write-Host "  [FAIL] $Name - 0 points" -ForegroundColor Red
            $script:Issues += $Name
            return $false
        }
    } catch {
        Write-Host "  [ERROR] $Name - $($_.Exception.Message)" -ForegroundColor Red
        $script:Issues += "$Name - ERROR"
        return $false
    }
}

# Check output path
if (-not (Test-Path $OutputPath)) {
    Write-Host "ERROR: Output path not found: $OutputPath" -ForegroundColor Red
    exit 1
}

Write-Host "Output Path: $OutputPath" -ForegroundColor Cyan
Write-Host ""

# Category 1: Structure (30 points)
Write-Host "[Category 1] Directory Structure (30 pts)" -ForegroundColor Yellow

Test-Item "facts directory exists" 5 { Test-Path (Join-Path $OutputPath "facts") }
Test-Item "relations directory exists" 5 { Test-Path (Join-Path $OutputPath "relations") }
Test-Item "indexes directory exists" 5 { Test-Path (Join-Path $OutputPath "indexes") }
Test-Item "metrics directory exists" 5 { Test-Path (Join-Path $OutputPath "metrics") }
Test-Item "manifest.json exists" 10 { Test-Path (Join-Path $OutputPath "manifest.json") }

# Category 2: Manifest (40 points)
Write-Host "`n[Category 2] Manifest Validation (40 pts)" -ForegroundColor Yellow

$ManifestPath = Join-Path $OutputPath "manifest.json"
if (Test-Path $ManifestPath) {
    $Manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
    
    Test-Item "Manifest has version" 5 { $Manifest.PSObject.Properties['version'] -and $Manifest.version }
    Test-Item "Manifest has tables" 10 { $Manifest.PSObject.Properties['tables'] -and $Manifest.tables }
    Test-Item "Manifest has formats" 5 { $Manifest.PSObject.Properties['formats'] -and $Manifest.formats }
    Test-Item "Manifest has statistics" 10 { $Manifest.PSObject.Properties['statistics'] }
    Test-Item "Manifest has metadata" 10 { $Manifest.PSObject.Properties['metadata'] }
} else {
    Write-Host "  Skipping manifest checks - file not found" -ForegroundColor Gray
    $MaxScore += 40
}

# Category 3: Data Files (60 points)
Write-Host "`n[Category 3] Data Files (60 pts)" -ForegroundColor Yellow

$FactsDir = Join-Path $OutputPath "facts"

# Check for any facts data
$hasBundles = (Get-ChildItem -Path (Join-Path $FactsDir "bundles") -Recurse -File -ErrorAction SilentlyContinue).Count -gt 0
$hasScripts = (Get-ChildItem -Path (Join-Path $FactsDir "scripts") -Recurse -File -ErrorAction SilentlyContinue).Count -gt 0

Test-Item "Bundle facts exist" 20 { $hasBundles }
Test-Item "Script facts exist" 20 { $hasScripts }

# Check indexes
$IndexesDir = Join-Path $OutputPath "indexes"
$hasIndexes = (Get-ChildItem -Path $IndexesDir -Filter "*.kindex" -ErrorAction SilentlyContinue).Count -gt 0
Test-Item "Index files exist" 10 { $hasIndexes }

# Check metrics
$MetricsDir = Join-Path $OutputPath "metrics"
$hasMetrics = (Get-ChildItem -Path $MetricsDir -Filter "*.json" -ErrorAction SilentlyContinue).Count -gt 0
Test-Item "Metrics files exist" 10 { $hasMetrics }

# Category 4: Manifest Integrity (40 points)
Write-Host "`n[Category 4] Manifest Integrity (40 pts)" -ForegroundColor Yellow

if (Test-Path $ManifestPath) {
    $Manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
    
    # Check if registered files exist
    $allFilesExist = $true
    $tableCount = 0
    
    if ($Manifest.tables) {
        foreach ($tableName in $Manifest.tables.PSObject.Properties.Name) {
            $table = $Manifest.tables.$tableName
            
            if ($table.shards) {
                foreach ($shard in $table.shards) {
                    $shardPath = Join-Path $OutputPath $shard.path
                    if (-not (Test-Path $shardPath)) {
                        $allFilesExist = $false
                        Write-Host "    Missing: $($shard.path)" -ForegroundColor Gray
                    }
                }
                $tableCount++
            } elseif ($table.file) {
                $filePath = Join-Path $OutputPath $table.file
                if (-not (Test-Path $filePath)) {
                    $allFilesExist = $false
                    Write-Host "    Missing: $($table.file)" -ForegroundColor Gray
                }
                $tableCount++
            }
        }
    }
    
    Test-Item "All manifest-registered files exist" 20 { $allFilesExist }
    Test-Item "Manifest has table registrations" 10 { $tableCount -gt 0 }
    
    # Check indexes
    $allIndexesExist = $true
    if ($Manifest.indexes) {
        foreach ($indexName in $Manifest.indexes.PSObject.Properties.Name) {
            $index = $Manifest.indexes.$indexName
            if ($index.path) {
                $indexPath = Join-Path $OutputPath $index.path
                if (-not (Test-Path $indexPath)) {
                    $allIndexesExist = $false
                }
            }
        }
    }
    
    Test-Item "All manifest-registered indexes exist" 10 { $allIndexesExist }
} else {
    Write-Host "  Skipping manifest integrity checks" -ForegroundColor Gray
    $MaxScore += 30
}

# Calculate statistics
Write-Host "`n[Statistics]" -ForegroundColor Cyan

$TotalSize = 0
Get-ChildItem -Path $OutputPath -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object { $TotalSize += $_.Length }
$TotalSizeMB = [math]::Round($TotalSize / 1MB, 2)
$FileCount = (Get-ChildItem -Path $OutputPath -Recurse -File -ErrorAction SilentlyContinue).Count

Write-Host "  Total files: $FileCount" -ForegroundColor Gray
Write-Host "  Total size: $TotalSizeMB MB" -ForegroundColor Gray

# Final score
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "EVALUATION RESULTS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$Percentage = if ($MaxScore -gt 0) { [math]::Round(($Score / $MaxScore) * 100, 1) } else { 0 }

$Grade = switch ($Percentage) {
    {$_ -ge 90} { "A+ (Excellent)" }
    {$_ -ge 80} { "A (Very Good)" }
    {$_ -ge 70} { "B (Good)" }
    {$_ -ge 60} { "C (Acceptable)" }
    {$_ -ge 50} { "D (Poor)" }
    default { "F (Failed)" }
}

Write-Host "Score: $Score / $MaxScore points" -ForegroundColor Cyan
Write-Host "Percentage: $Percentage%" -ForegroundColor Cyan
Write-Host "Grade: $Grade" -ForegroundColor $(if ($Percentage -ge 70) { "Green" } elseif ($Percentage -ge 50) { "Yellow" } else { "Red" })

if ($Issues.Count -gt 0) {
    Write-Host "`nIssues Found:" -ForegroundColor Red
    $Issues | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
}

# Save report
$ReportPath = Join-Path $OutputPath "evaluation-report.json"
$Report = @{
    Timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    OutputPath = $OutputPath
    Score = $Score
    MaxScore = $MaxScore
    Percentage = $Percentage
    Grade = $Grade
    Issues = $Issues
    Statistics = @{
        FileCount = $FileCount
        TotalSizeMB = $TotalSizeMB
    }
}

$Report | ConvertTo-Json -Depth 5 | Set-Content $ReportPath
Write-Host "`nEvaluation report saved: $ReportPath" -ForegroundColor Cyan

Write-Host "========================================`n" -ForegroundColor Cyan

# Exit code
if ($Percentage -ge 70) {
    exit 0
} else {
    exit 1
}
