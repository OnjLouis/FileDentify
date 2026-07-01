$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$mainFormFiles = Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter 'MainForm*.cs' | Sort-Object Name
$text = ($mainFormFiles | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`r`n"
$failures = New-Object System.Collections.Generic.List[string]

function Get-Block([string]$startPattern, [string]$endPattern) {
    $start = $text.IndexOf($startPattern, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Could not find block start: $startPattern" }
    $end = $text.IndexOf($endPattern, $start, [StringComparison]::Ordinal)
    if ($end -lt 0) { throw "Could not find block end after $startPattern`: $endPattern" }
    return $text.Substring($start, $end - $start)
}

function Require-Contains([string]$name, [string]$block, [string]$needle) {
    if ($block.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) {
        $failures.Add("$name must contain '$needle'.")
    }
}

$keyDownBlock = Get-Block 'private void MainForm_KeyDown' 'protected override bool ProcessCmdKey'
$processBlock = Get-Block 'protected override bool ProcessCmdKey' 'private bool TryHandleGlobalShortcut'
$shortcutBlock = Get-Block 'private bool TryHandleGlobalShortcut' 'public bool PreFilterMessage'
$preFilterBlock = Get-Block 'public bool PreFilterMessage' 'private bool ReturnToHtmlDetailsAfterMenuEscape'
$previewBlock = Get-Block 'private void DetailsBrowser_PreviewKeyDown' 'private void ApplyDetailsViewMode'

Require-Contains 'MainForm_KeyDown' $keyDownBlock 'TryHandleGlobalShortcut(e.KeyData, false'
Require-Contains 'ProcessCmdKey' $processBlock 'TryHandleGlobalShortcut(keyData, false'
Require-Contains 'HTML PreFilterMessage' $preFilterBlock 'TryHandleGlobalShortcut(keyData, true'
Require-Contains 'HTML PreviewKeyDown' $previewBlock 'IsHtmlDetailsGlobalShortcut(e.KeyData)'

$requiredShortcutEvidence = @(
    'Keys.F1',
    'Keys.F4',
    'Keys.F5',
    'Keys.F6',
    'Keys.F7',
    'Keys.Escape',
    'Keys.N',
    'Keys.O',
    'Keys.L',
    'Keys.R',
    'Keys.T',
    'Keys.S',
    'Keys.C',
    'Keys.Oemcomma',
    'Keys.Back',
    'Keys.PageUp',
    'Keys.PageDown',
    'Keys.Home',
    'Keys.End',
    'SelectAdjacentFile',
    'SelectAdjacentSection',
    'SelectFileByPosition',
    'SelectSectionByPosition',
    'SelectReportOverview',
    'SelectTreeNodeByShortcut',
    'SelectFileByShortcut',
    'ShowHelp',
    'CheckForUpdates',
    'OpenProjectPage',
    'OpenAdvancedFileViewer',
    'ViewHtmlReport',
    'OpenContainingFolder',
    'ShowPreferences'
)

foreach ($needle in $requiredShortcutEvidence) {
    Require-Contains 'TryHandleGlobalShortcut' $shortcutBlock $needle
}

$requiredHtmlPreviewEvidence = @(
    'Keys.F1',
    'Keys.F4',
    'Keys.F5',
    'Keys.F6',
    'Keys.F7',
    'Keys.Escape',
    'Keys.N',
    'Keys.O',
    'Keys.L',
    'Keys.R',
    'Keys.T',
    'Keys.S',
    'Keys.C',
    'Keys.Oemcomma',
    'Keys.Back',
    'Keys.PageUp',
    'Keys.PageDown',
    'Keys.Home',
    'Keys.End',
    'DigitFromShortcutKey',
    'FileIndexFromShortcutKey'
)

foreach ($needle in $requiredHtmlPreviewEvidence) {
    Require-Contains 'IsHtmlDetailsGlobalShortcut' $previewBlock $needle
}

if ($failures.Count -gt 0) {
    throw "Shortcut parity check failed:`r`n - $($failures -join "`r`n - ")"
}

Write-Host 'Shortcut parity check passed.'
