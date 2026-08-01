$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceRoot = Join-Path $root 'src'
$policyPath = Join-Path $sourceRoot 'ReportNotePolicy.cs'
$helperPath = Join-Path $sourceRoot 'FileInspector.Helpers.cs'
$safetyPath = Join-Path $sourceRoot 'FileInspector.Safety.cs'
$detailsPath = Join-Path $sourceRoot 'MainForm.Details.cs'
$reportTextPath = Join-Path $sourceRoot 'FileInspector.ReportText.cs'
$savedReportPath = Join-Path $sourceRoot 'SavedReportStore.cs'

foreach ($path in @($policyPath, $helperPath, $safetyPath, $detailsPath, $reportTextPath, $savedReportPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Note sanity check failed: required source file is missing: $path"
    }
}

$policy = Get-Content -LiteralPath $policyPath -Raw
foreach ($category in @('About this format', 'Limitations', 'Compatibility', 'Privacy', 'Safety', 'Uncertainty', 'Advice', 'Viewing note')) {
    if ($policy.IndexOf('"' + $category + '"', [StringComparison]::Ordinal) -lt 0) {
        throw "Note sanity check failed: canonical category '$category' is missing."
    }
}

$safety = Get-Content -LiteralPath $safetyPath -Raw
foreach ($staleTitle in @('"Content identification"', '"Header and extension mismatch"', '"Expected extension(s)"', '"Safety boundary"', '"Recommendation"')) {
    if ($safety.IndexOf($staleTitle, [StringComparison]::Ordinal) -ge 0) {
        throw "Note sanity check failed: redundant safety item $staleTitle was reintroduced."
    }
}
foreach ($requiredTitle in @('"Mismatch"', '"Risk"', '"Advice"')) {
    if ($safety.IndexOf($requiredTitle, [StringComparison]::Ordinal) -lt 0) {
        throw "Note sanity check failed: safety item $requiredTitle is missing."
    }
}

$allSource = Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = $allSource -join "`n"
if ($combined.IndexOf('"Header note"', [StringComparison]::Ordinal) -ge 0) {
    throw 'Note sanity check failed: a terse Header note remains. Use an ordinary Payload or Header data row instead.'
}

$noteBodies = @{}
foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Filter 'FileInspector*.cs' -File) {
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $file.FullName) {
        $lineNumber++
        if ($line -match 'Add\([^,]+,\s*"(?:Notes|[^"]+ note|[^"]+ notes)"\s*,\s*"([^"]+)"') {
            $normalized = (($Matches[1] -replace '\s+', ' ').Trim()).ToLowerInvariant()
            if ($noteBodies.ContainsKey($normalized)) {
                throw "Note sanity check failed: duplicate literal note text in $($file.Name):$lineNumber and $($noteBodies[$normalized])."
            }
            $noteBodies[$normalized] = "$($file.Name):$lineNumber"
        }
    }
}

$helper = Get-Content -LiteralPath $helperPath -Raw
$details = Get-Content -LiteralPath $detailsPath -Raw
$reportText = Get-Content -LiteralPath $reportTextPath -Raw
$savedReport = Get-Content -LiteralPath $savedReportPath -Raw
if ($helper.IndexOf('IsNote = true', [StringComparison]::Ordinal) -lt 0 -or
    $details.IndexOf('item.IsNote', [StringComparison]::Ordinal) -lt 0 -or
    $reportText.IndexOf('item.IsNote', [StringComparison]::Ordinal) -lt 0 -or
    $savedReport.IndexOf('IsNote = item.IsNote', [StringComparison]::Ordinal) -lt 0) {
    throw 'Note sanity check failed: note classification is not preserved through creation, HTML rendering, and saved reports.'
}

Write-Host "Note sanity check passed: $($noteBodies.Count) literal notes audited."
