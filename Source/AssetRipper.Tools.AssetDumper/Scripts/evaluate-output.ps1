# AssetDumper Output Evaluation Script
# Purpose: Deep validation and quality assessment of exported data
# Date: 2025-11-07

param(
    [Parameter(Mandatory=$true)]
    [string]$OutputPath,
    [switch]$Verbose,
    [switch]$SkipSchemaValidation,
    [switch]$SkipDataIntegrity
)

$ErrorActionPreference = "Stop"

# Colors
function Write-Success { param($Message) Write-Host "✓ $Message" -ForegroundColor Green }
function Write-Info { param($Message) Write-Host "ℹ $Message" -ForegroundColor Cyan }
function Write-Warning2 { param($Message) Write-Host "⚠ $Message" -ForegroundColor Yellow }
function Write-Error2 { param($Message) Write-Host "✗ $Message" -ForegroundColor Red }
function Write-Section { param($Message) Write-Host "`n═══ $Message ═══" -ForegroundColor Magenta }

# Evaluation Results
$EvalResults = @{
    Score = 0
    MaxScore = 0
    Checks = @()
    Issues = @()
    Warnings = @()
}

function Add-Check {
    param(
        [string]$Category,
        [string]$CheckName,
        [int]$Points,
        [bool]$Passed,
        [string]$Message = "",
        [string]$Severity = "Error"  # Error, Warning, Info
    )
    
    $EvalResults.MaxScore += $Points
    if ($Passed) {
        $EvalResults.Score += $Points
    }
    
    $Check = @{
        Category = $Category
        Name = $CheckName
        Points = $Points
        Passed = $Passed
        Message = $Message
        Severity = $Severity
    }
    
    $EvalResults.Checks += $Check
    
    if (-not $Passed) {
        if ($Severity -eq "Error") {
            $EvalResults.Issues += "$Category :: $CheckName - $Message"
            Write-Error2 "[$Category] $CheckName (0/$Points pts) - $Message"
        } elseif ($Severity -eq "Warning") {
            $EvalResults.Warnings += "$Category :: $CheckName - $Message"
            Write-Warning2 "[$Category] $CheckName ($Points pts) - $Message"
        }
    } else {
        Write-Success "[$Category] $CheckName ($Points pts)"
    }
}

Write-Section "AssetDumper Output Evaluation"
Write-Info "Output Path: $OutputPath"
Write-Info "Start Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

if (-not (Test-Path $OutputPath)) {
    Write-Error2 "Output path not found: $OutputPath"
    exit 1
}

# ════════════════════════════════════════════════════════════
# CATEGORY 1: Structural Integrity (30 points)
# ════════════════════════════════════════════════════════════
Write-Section "Category 1: Structural Integrity (30 pts)"

# Check required directories (10 pts)
$RequiredDirs = @("facts", "relations", "indexes", "metrics", "schemas")
$missingDirs = @()
foreach ($dir in $RequiredDirs) {
    if (-not (Test-Path (Join-Path $OutputPath $dir))) {
        $missingDirs += $dir
    }
}
Add-Check -Category "Structure" -CheckName "Required Directories" -Points 10 `
    -Passed ($missingDirs.Count -eq 0) `
    -Message $(if ($missingDirs.Count -gt 0) { "Missing: $($missingDirs -join ', ')" } else { "All present" })

# Check manifest.json (10 pts)
$ManifestPath = Join-Path $OutputPath "manifest.json"
$manifestExists = Test-Path $ManifestPath
Add-Check -Category "Structure" -CheckName "Manifest File" -Points 10 `
    -Passed $manifestExists `
    -Message $(if ($manifestExists) { "Present" } else { "Missing manifest.json" })

# Check facts subdirectory structure (10 pts)
$FactsDir = Join-Path $OutputPath "facts"
$ScriptMetadataDir = Join-Path $FactsDir "script_metadata"
$hasScriptMetadata = Test-Path $ScriptMetadataDir
Add-Check -Category "Structure" -CheckName "Facts Subdirectories" -Points 10 `
    -Passed $hasScriptMetadata `
    -Message $(if ($hasScriptMetadata) { "script_metadata present" } else { "script_metadata missing" })

# ════════════════════════════════════════════════════════════
# CATEGORY 2: Manifest Completeness (40 points)
# ════════════════════════════════════════════════════════════
Write-Section "Category 2: Manifest Completeness (40 pts)"

if ($manifestExists) {
    try {
        $Manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
        
        # Check version field (5 pts)
        $hasVersion = $Manifest.PSObject.Properties['version'] -and $Manifest.version
        Add-Check -Category "Manifest" -CheckName "Version Field" -Points 5 `
            -Passed $hasVersion `
            -Message $(if ($hasVersion) { "v$($Manifest.version)" } else { "Missing or empty" })
        
        # Check tables array (10 pts)
        $hasTables = $Manifest.PSObject.Properties['tables'] -and $Manifest.tables.Count -gt 0
        Add-Check -Category "Manifest" -CheckName "Tables Array" -Points 10 `
            -Passed $hasTables `
            -Message $(if ($hasTables) { "$($Manifest.tables.Count) tables" } else { "Missing or empty" })
        
        # Check formats array (5 pts)
        $hasFormats = $Manifest.PSObject.Properties['formats'] -and $Manifest.formats.Count -gt 0
        Add-Check -Category "Manifest" -CheckName "Formats Array" -Points 5 `
            -Passed $hasFormats `
            -Message $(if ($hasFormats) { "$($Manifest.formats.Count) formats" } else { "Missing or empty" })
        
        # Check schemas array (10 pts)
        $hasSchemas = $Manifest.PSObject.Properties['schemas'] -and $Manifest.schemas.Count -gt 0
        Add-Check -Category "Manifest" -CheckName "Schemas Array" -Points 10 `
            -Passed $hasSchemas `
            -Message $(if ($hasSchemas) { "$($Manifest.schemas.Count) schemas" } else { "Missing or empty" })
        
        # Validate table entries (10 pts)
        if ($hasTables) {
            $invalidTables = @()
            foreach ($table in $Manifest.tables) {
                if (-not $table.name) { $invalidTables += "Unnamed table" }
                if (-not $table.domain) { $invalidTables += "$($table.name): missing domain" }
                if (-not $table.format) { $invalidTables += "$($table.name): missing format" }
                if (-not $table.shards) { $invalidTables += "$($table.name): missing shards" }
            }
            Add-Check -Category "Manifest" -CheckName "Table Entries Valid" -Points 10 `
                -Passed ($invalidTables.Count -eq 0) `
                -Message $(if ($invalidTables.Count -eq 0) { "All valid" } else { "$($invalidTables.Count) issues" })
        }
        
    } catch {
        Add-Check -Category "Manifest" -CheckName "JSON Parsing" -Points 40 `
            -Passed $false `
            -Message "Failed to parse: $($_.Exception.Message)"
    }
} else {
    Add-Check -Category "Manifest" -CheckName "Manifest Checks" -Points 40 `
        -Passed $false `
        -Message "Skipped - manifest.json not found"
}

# ════════════════════════════════════════════════════════════
# CATEGORY 3: Data Completeness (60 points)
# ════════════════════════════════════════════════════════════
Write-Section "Category 3: Data Completeness (60 pts)"

# Check facts tables (30 pts)
$FactsTables = @("collections.ndjson", "assets.ndjson", "types.ndjson", "bundles.ndjson")
$factsScore = 0
$factsMax = 30
foreach ($table in $FactsTables) {
    $tablePath = Join-Path $FactsDir $table
    $compressedPath = "$tablePath.zst"
    
    $exists = (Test-Path $tablePath) -or (Test-Path $compressedPath)
    $points = [math]::Floor($factsMax / $FactsTables.Count)
    
    if ($exists) {
        $factsScore += $points
        if (Test-Path $tablePath) {
            $lines = (Get-Content $tablePath | Measure-Object -Line).Lines
            Write-Success "  $table`: $lines records"
        } else {
            $size = (Get-Item $compressedPath).Length
            Write-Success "  $table`.zst: $([math]::Round($size/1KB, 2)) KB compressed"
        }
    } else {
        Write-Error2 "  $table`: Missing"
    }
}
Add-Check -Category "Data" -CheckName "Facts Tables" -Points $factsMax `
    -Passed ($factsScore -eq $factsMax) `
    -Message "$factsScore/$factsMax points earned"

# Check script_metadata shards (15 pts)
if (Test-Path $ScriptMetadataDir) {
    $scriptFiles = Get-ChildItem -Path $ScriptMetadataDir -File -Recurse
    $hasScriptFiles = $scriptFiles.Count -gt 0
    Add-Check -Category "Data" -CheckName "Script Metadata Shards" -Points 15 `
        -Passed $hasScriptFiles `
        -Message $(if ($hasScriptFiles) { "$($scriptFiles.Count) files" } else { "No files found" })
} else {
    Add-Check -Category "Data" -CheckName "Script Metadata Shards" -Points 15 `
        -Passed $false `
        -Message "Directory not found"
}

# Check relations tables (15 pts)
$RelationsDir = Join-Path $OutputPath "relations"
$relationsTable = "asset_dependencies.ndjson"
$relationsPath = Join-Path $RelationsDir $relationsTable
$compressedRelations = "$relationsPath.zst"

$relationsExist = (Test-Path $relationsPath) -or (Test-Path $compressedRelations)
Add-Check -Category "Data" -CheckName "Relations Tables" -Points 15 `
    -Passed $relationsExist `
    -Message $(if ($relationsExist) { "asset_dependencies present" } else { "Missing" })

# ════════════════════════════════════════════════════════════
# CATEGORY 4: Indexes & Metrics (40 points)
# ════════════════════════════════════════════════════════════
Write-Section "Category 4: Indexes & Metrics (40 pts)"

# Check index files (20 pts)
$IndexesDir = Join-Path $OutputPath "indexes"
$indexFiles = @()
if (Test-Path $IndexesDir) {
    $indexFiles = Get-ChildItem -Path $IndexesDir -Filter "*.kindex" -Recurse
}
$hasIndexes = $indexFiles.Count -gt 0
Add-Check -Category "Indexes" -CheckName "Index Files Present" -Points 20 `
    -Passed $hasIndexes `
    -Message $(if ($hasIndexes) { "$($indexFiles.Count) .kindex files" } else { "No .kindex files found" })

# Check metrics files (20 pts)
$MetricsDir = Join-Path $OutputPath "metrics"
$metricsFiles = @()
if (Test-Path $MetricsDir) {
    $metricsFiles = Get-ChildItem -Path $MetricsDir -Filter "*.json" -Recurse
}
$hasMetrics = $metricsFiles.Count -gt 0
Add-Check -Category "Metrics" -CheckName "Metrics Files Present" -Points 20 `
    -Passed $hasMetrics `
    -Message $(if ($hasMetrics) { "$($metricsFiles.Count) .json files" } else { "No .json files found" })

# ════════════════════════════════════════════════════════════
# CATEGORY 5: Schema Validation (30 points)
# ════════════════════════════════════════════════════════════
Write-Section "Category 5: Schema Validation (30 pts)"

if (-not $SkipSchemaValidation) {
    $SchemasDir = Join-Path $OutputPath "schemas"
    
    # Check schema files exist (15 pts)
    $schemaFiles = @()
    if (Test-Path $SchemasDir) {
        $schemaFiles = Get-ChildItem -Path $SchemasDir -Filter "*.schema.json" -Recurse
    }
    $hasSchemaFiles = $schemaFiles.Count -gt 0
    Add-Check -Category "Schema" -CheckName "Schema Files Present" -Points 15 `
        -Passed $hasSchemaFiles `
        -Message $(if ($hasSchemaFiles) { "$($schemaFiles.Count) schema files" } else { "No schema files found" })
    
    # Validate schema files are valid JSON (15 pts)
    if ($hasSchemaFiles) {
        $invalidSchemas = @()
        foreach ($schemaFile in $schemaFiles) {
            try {
                $schema = Get-Content $schemaFile.FullName -Raw | ConvertFrom-Json
                if (-not $schema.PSObject.Properties['$schema']) {
                    $invalidSchemas += "$($schemaFile.Name): Missing `$schema property"
                }
            } catch {
                $invalidSchemas += "$($schemaFile.Name): $($_.Exception.Message)"
            }
        }
        Add-Check -Category "Schema" -CheckName "Schema Files Valid" -Points 15 `
            -Passed ($invalidSchemas.Count -eq 0) `
            -Message $(if ($invalidSchemas.Count -eq 0) { "All schemas valid" } else { "$($invalidSchemas.Count) invalid" })
    }
} else {
    Write-Info "Schema validation skipped"
    Add-Check -Category "Schema" -CheckName "Schema Validation" -Points 30 `
        -Passed $true -Severity "Info" `
        -Message "Skipped (-SkipSchemaValidation)"
}

# ════════════════════════════════════════════════════════════
# CATEGORY 6: Data Integrity (50 points)
# ════════════════════════════════════════════════════════════
Write-Section "Category 6: Data Integrity (50 pts)"

if (-not $SkipDataIntegrity) {
    
    # Load and parse sample records
    $AssetsPath = Join-Path $FactsDir "assets.ndjson"
    $TypesPath = Join-Path $FactsDir "types.ndjson"
    
    if ((Test-Path $AssetsPath) -and (Test-Path $TypesPath)) {
        
        # Check assets have valid type references (25 pts)
        try {
            $assetSample = Get-Content $AssetsPath -First 100
            $typeIds = @{}
            Get-Content $TypesPath | ForEach-Object {
                $type = $_ | ConvertFrom-Json
                if ($type.typeId) {
                    $typeIds[$type.typeId] = $true
                }
            }
            
            $invalidAssets = 0
            foreach ($line in $assetSample) {
                $asset = $line | ConvertFrom-Json
                if ($asset.typeId -and -not $typeIds.ContainsKey($asset.typeId)) {
                    $invalidAssets++
                }
            }
            
            $assetsValid = $invalidAssets -eq 0
            Add-Check -Category "Integrity" -CheckName "Asset Type References" -Points 25 `
                -Passed $assetsValid `
                -Message $(if ($assetsValid) { "All valid (sample: 100)" } else { "$invalidAssets invalid references" })
        } catch {
            Add-Check -Category "Integrity" -CheckName "Asset Type References" -Points 25 `
                -Passed $false `
                -Message "Check failed: $($_.Exception.Message)"
        }
        
        # Check for duplicate asset IDs (25 pts)
        try {
            $assetIds = @{}
            $duplicates = 0
            Get-Content $AssetsPath | ForEach-Object {
                $asset = $_ | ConvertFrom-Json
                if ($asset.assetId) {
                    if ($assetIds.ContainsKey($asset.assetId)) {
                        $duplicates++
                    } else {
                        $assetIds[$asset.assetId] = $true
                    }
                }
            }
            
            $noDuplicates = $duplicates -eq 0
            Add-Check -Category "Integrity" -CheckName "No Duplicate Asset IDs" -Points 25 `
                -Passed $noDuplicates `
                -Message $(if ($noDuplicates) { "No duplicates found" } else { "$duplicates duplicates" })
        } catch {
            Add-Check -Category "Integrity" -CheckName "No Duplicate Asset IDs" -Points 25 `
                -Passed $false `
                -Message "Check failed: $($_.Exception.Message)"
        }
        
    } else {
        Add-Check -Category "Integrity" -CheckName "Data Integrity Checks" -Points 50 `
            -Passed $false `
            -Message "Skipped - assets.ndjson or types.ndjson not found"
    }
    
} else {
    Write-Info "Data integrity checks skipped"
    Add-Check -Category "Integrity" -CheckName "Data Integrity" -Points 50 `
        -Passed $true -Severity "Info" `
        -Message "Skipped (-SkipDataIntegrity)"
}

# ════════════════════════════════════════════════════════════
# CATEGORY 7: Performance & Efficiency (20 points)
# ════════════════════════════════════════════════════════════
Write-Section "Category 7: Performance & Efficiency (20 pts)"

# Calculate total output size
$TotalSize = 0
Get-ChildItem -Path $OutputPath -Recurse -File | ForEach-Object { $TotalSize += $_.Length }
$TotalSizeMB = [math]::Round($TotalSize / 1MB, 2)

Write-Info "Total output size: $TotalSizeMB MB"

# Check compression effectiveness (10 pts)
$CompressedFiles = Get-ChildItem -Path $OutputPath -Recurse -Filter "*.zst"
if ($CompressedFiles.Count -gt 0) {
    $compressedSize = 0
    $CompressedFiles | ForEach-Object { $compressedSize += $_.Length }
    $compressionRatio = [math]::Round(($compressedSize / $TotalSize) * 100, 1)
    
    # Good compression: < 50% of total size
    $goodCompression = $compressionRatio -lt 50
    Add-Check -Category "Performance" -CheckName "Compression Effectiveness" -Points 10 `
        -Passed $goodCompression `
        -Message "Compressed files are $compressionRatio% of total"
} else {
    Add-Check -Category "Performance" -CheckName "Compression Effectiveness" -Points 10 `
        -Passed $true -Severity "Warning" `
        -Message "No compressed files found"
}

# Check output size is reasonable (10 pts)
# For GRIS sample, expect < 500 MB total
$reasonableSize = $TotalSizeMB -lt 500
Add-Check -Category "Performance" -CheckName "Reasonable Output Size" -Points 10 `
    -Passed $reasonableSize `
    -Message $(if ($reasonableSize) { "$TotalSizeMB MB" } else { "$TotalSizeMB MB (> 500 MB threshold)" })

# ════════════════════════════════════════════════════════════
# FINAL EVALUATION
# ════════════════════════════════════════════════════════════
Write-Section "Evaluation Summary"

$Percentage = if ($EvalResults.MaxScore -gt 0) {
    [math]::Round(($EvalResults.Score / $EvalResults.MaxScore) * 100, 1)
} else {
    0
}

$Grade = switch ($Percentage) {
    {$_ -ge 90} { "A+ (Excellent)" }
    {$_ -ge 80} { "A (Very Good)" }
    {$_ -ge 70} { "B (Good)" }
    {$_ -ge 60} { "C (Acceptable)" }
    {$_ -ge 50} { "D (Poor)" }
    default { "F (Failed)" }
}

Write-Host "`n╔═══════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║           OUTPUT QUALITY EVALUATION                   ║" -ForegroundColor Cyan
Write-Host "╠═══════════════════════════════════════════════════════╣" -ForegroundColor Cyan
Write-Host "║  Score:       $($EvalResults.Score)/$($EvalResults.MaxScore) points ($Percentage%)".PadRight(55) + "║" -ForegroundColor Cyan
Write-Host "║  Grade:       $Grade".PadRight(55) + "║" -ForegroundColor $(if ($Percentage -ge 70) { "Green" } elseif ($Percentage -ge 50) { "Yellow" } else { "Red" })
Write-Host "║  Checks:      $($EvalResults.Checks.Count) total".PadRight(55) + "║" -ForegroundColor Cyan
Write-Host "║  Issues:      $($EvalResults.Issues.Count)".PadRight(55) + "║" -ForegroundColor $(if ($EvalResults.Issues.Count -eq 0) { "Cyan" } else { "Red" })
Write-Host "║  Warnings:    $($EvalResults.Warnings.Count)".PadRight(55) + "║" -ForegroundColor $(if ($EvalResults.Warnings.Count -eq 0) { "Cyan" } else { "Yellow" })
Write-Host "╚═══════════════════════════════════════════════════════╝" -ForegroundColor Cyan

# Show issues
if ($EvalResults.Issues.Count -gt 0) {
    Write-Host "`nCRITICAL ISSUES:" -ForegroundColor Red
    $EvalResults.Issues | ForEach-Object { Write-Host "  • $_" -ForegroundColor Red }
}

# Show warnings
if ($EvalResults.Warnings.Count -gt 0) {
    Write-Host "`nWARNINGS:" -ForegroundColor Yellow
    $EvalResults.Warnings | ForEach-Object { Write-Host "  • $_" -ForegroundColor Yellow }
}

# Category breakdown
Write-Host "`nCategory Breakdown:" -ForegroundColor Cyan
$Categories = $EvalResults.Checks | Group-Object -Property Category
foreach ($cat in $Categories) {
    $catScore = ($cat.Group | Where-Object { $_.Passed } | Measure-Object -Property Points -Sum).Sum
    $catMax = ($cat.Group | Measure-Object -Property Points -Sum).Sum
    $catPercent = if ($catMax -gt 0) { [math]::Round(($catScore / $catMax) * 100, 1) } else { 0 }
    Write-Host "  $($cat.Name): $catScore/$catMax pts ($catPercent%)" -ForegroundColor $(if ($catPercent -ge 80) { "Green" } elseif ($catPercent -ge 60) { "Yellow" } else { "Red" })
}

# Save evaluation report
$ReportPath = Join-Path $OutputPath "evaluation-report.json"
$Report = @{
    EvaluationDate = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    OutputPath = $OutputPath
    Score = $EvalResults.Score
    MaxScore = $EvalResults.MaxScore
    Percentage = $Percentage
    Grade = $Grade
    Checks = $EvalResults.Checks
    Issues = $EvalResults.Issues
    Warnings = $EvalResults.Warnings
    Statistics = @{
        TotalSizeMB = $TotalSizeMB
        FileCount = (Get-ChildItem -Path $OutputPath -Recurse -File).Count
    }
}

$Report | ConvertTo-Json -Depth 10 | Set-Content $ReportPath
Write-Success "`nEvaluation report saved to: $ReportPath"

# Exit with grade-based code
if ($Percentage -ge 70) {
    Write-Success "Output quality: PASS ($Grade)"
    exit 0
} elseif ($Percentage -ge 50) {
    Write-Warning2 "Output quality: MARGINAL ($Grade)"
    exit 1
} else {
    Write-Error2 "Output quality: FAIL ($Grade)"
    exit 2
}
