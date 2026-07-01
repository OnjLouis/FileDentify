$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$mainFormFiles = Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter 'MainForm*.cs' | Sort-Object Name
$text = ($mainFormFiles | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`r`n"
$failures = New-Object System.Collections.Generic.List[string]

function Get-Block([string]$startPattern, [string]$endPattern) {
    $start = $text.IndexOf($startPattern, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Could not find menu block start: $startPattern" }
    $end = $text.IndexOf($endPattern, $start, [StringComparison]::Ordinal)
    if ($end -lt 0) { throw "Could not find menu block end: $endPattern" }
    return $text.Substring($start, $end - $start)
}

function Get-MenuItems([string]$block) {
    foreach ($line in $block -split "`r?`n") {
        if ($line -notmatch '((DropDownItems|Items)\.Add)\((new ToolStripMenuItem|CreateShortcutMenuItem)') {
            continue
        }
        $usesShortcutHelper = $line -match 'CreateShortcutMenuItem'
        $match = if ($usesShortcutHelper) {
            [regex]::Match($line, 'CreateShortcutMenuItem\("(?<label>(?:[^"\\]|\\.)*)"\s*,\s*"(?<helperShortcut>[^"]*)"')
        } else {
            [regex]::Match($line, 'new ToolStripMenuItem\("(?<label>(?:[^"\\]|\\.)*)"')
        }
        if (-not $match.Success) {
            continue
        }
        $label = $match.Groups['label'].Value
        $shortcut = if ($usesShortcutHelper) { $match.Groups['helperShortcut'].Value } else { Get-ShortcutDisplay $line }
        [pscustomobject]@{
            Label = $label
            PlainLabel = ($label -replace '&&', "`0" -replace '&', '' -replace "`0", '&')
            Mnemonic = Get-Mnemonic $label
            Shortcut = $shortcut
            UsesShortcutHelper = $usesShortcutHelper
            HasShortcutKeys = $line -match '(?<!Show)ShortcutKeys\s*='
        }
    }
}

function Get-Mnemonic([string]$label) {
    for ($i = 0; $i -lt $label.Length - 1; $i++) {
        if ($label[$i] -eq '&') {
            if ($label[$i + 1] -eq '&') {
                $i++
                continue
            }
            return [char]::ToUpperInvariant($label[$i + 1])
        }
    }
    return $null
}

function Get-ShortcutDisplay([string]$props) {
    $match = [regex]::Match($props, 'ShortcutKeyDisplayString\s*=\s*"(?<shortcut>[^"]+)"')
    if ($match.Success) { return $match.Groups['shortcut'].Value }
    return ''
}

function Test-Mnemonics([string]$name, [object[]]$items) {
    $seen = @{}
    foreach ($item in $items) {
        if (-not $item.Mnemonic) {
            continue
        }
        $key = [string]$item.Mnemonic
        if ($seen.ContainsKey($key)) {
            $failures.Add("$name menu mnemonic '$key' is used by both '$($seen[$key])' and '$($item.PlainLabel)'.")
        }
        else {
            $seen[$key] = $item.PlainLabel
        }
    }
}

function Test-ExpectedShortcut([string]$name, [object[]]$items, [string]$label, [string]$shortcut) {
    $item = $items | Where-Object { $_.PlainLabel -eq $label } | Select-Object -First 1
    if (-not $item) {
        $failures.Add("$name menu is missing expected item '$label'.")
        return
    }
    if ($item.Shortcut -ne $shortcut) {
        $failures.Add("$name menu item '$label' must display shortcut '$shortcut' but displays '$($item.Shortcut)'.")
    }
    if (-not $item.UsesShortcutHelper -and $item.Label -notlike "*``t$shortcut") {
        $failures.Add("$name menu item '$label' must include shortcut '$shortcut' in its visible Text using a tab, Sensor Readout style.")
    }
}

$fileItems = @(Get-MenuItems (Get-Block 'var fileMenu = new ToolStripMenuItem("&File");' 'var editMenu = new ToolStripMenuItem("&Edit");'))
$editItems = @(Get-MenuItems (Get-Block 'var editMenu = new ToolStripMenuItem("&Edit");' 'var optionsMenu = new ToolStripMenuItem("&Options");'))
$optionsItems = @(Get-MenuItems (Get-Block 'var optionsMenu = new ToolStripMenuItem("&Options");' 'var helpMenu = new ToolStripMenuItem("&Help");'))
$helpItems = @(Get-MenuItems (Get-Block 'var helpMenu = new ToolStripMenuItem("&Help");' 'menu.Items.Add(fileMenu);'))
$contextItems = @(Get-MenuItems (Get-Block 'private ContextMenuStrip CreateTreeContextMenu()' 'private void ResultsTree_NodeMouseClick'))

Test-Mnemonics 'File' $fileItems
Test-Mnemonics 'Edit' $editItems
Test-Mnemonics 'Options' $optionsItems
Test-Mnemonics 'Help' $helpItems
Test-Mnemonics 'Report tree context' $contextItems

foreach ($item in $fileItems + $editItems + $optionsItems + $helpItems + $contextItems) {
    if ($item.HasShortcutKeys) {
        $failures.Add("Menu item '$($item.PlainLabel)' uses ShortcutKeys. Use explicit ShortcutKeyDisplayString so the shortcut is always shown.")
    }
}

$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' -Recurse
foreach ($sourceFile in $sourceFiles) {
    $matches = Select-String -LiteralPath $sourceFile.FullName -Pattern '(?<!Show)ShortcutKeys\s*='
    foreach ($match in $matches) {
        $relative = $sourceFile.FullName.Substring($root.Length).TrimStart('\')
        $failures.Add("$relative line $($match.LineNumber) uses ShortcutKeys. Use visible tabbed shortcut text plus ShortcutKeyDisplayString instead.")
    }
}

$expectedMain = @{
    'Open files...' = 'Ctrl+O'
    'Open folder...' = 'Ctrl+Shift+L'
    'Append files to report...' = 'Ctrl+Shift+O'
    'Open FileDentify report...' = 'Ctrl+R'
    'Save report...' = 'Ctrl+S'
    'View HTML report' = 'Alt+V'
    'Refresh original files' = 'F5'
    'Advanced file viewer' = 'F4'
    'Open containing folder' = 'Alt+L'
    'Exit' = 'Esc'
    'Copy selected details' = 'Ctrl+C'
    'Copy full report' = 'Alt+C'
    'Collapse all' = 'Ctrl+Shift+Left'
    'Expand all' = 'Ctrl+Shift+Right'
    'Move section up' = 'Ctrl+Up'
    'Move section down' = 'Ctrl+Down'
    'Previous file' = 'Alt+Left'
    'Next file' = 'Alt+Right'
    'First file' = 'Alt+PageUp'
    'Last file' = 'Alt+PageDown'
    'Previous section' = 'Alt+Up'
    'Next section' = 'Alt+Down'
    'First section' = 'Alt+Home'
    'Last section' = 'Alt+End'
    'Preferences...' = 'Ctrl+,'
    'Help' = 'F1'
    'Check for Updates...' = 'Shift+F1'
    'Project page' = 'Ctrl+F1'
}

foreach ($entry in $expectedMain.GetEnumerator()) {
    $items = $fileItems + $editItems + $optionsItems + $helpItems
    Test-ExpectedShortcut 'Main' $items $entry.Key $entry.Value
}

$expectedContext = @{
    'Copy selected details' = 'Ctrl+C'
    'Copy full report' = 'Alt+C'
    'Collapse all' = 'Ctrl+Shift+Left'
    'Expand all' = 'Ctrl+Shift+Right'
    'Move section up' = 'Ctrl+Up'
    'Move section down' = 'Ctrl+Down'
    'Previous file' = 'Alt+Left'
    'Next file' = 'Alt+Right'
    'First file' = 'Alt+PageUp'
    'Last file' = 'Alt+PageDown'
    'Previous section' = 'Alt+Up'
    'Next section' = 'Alt+Down'
    'First section' = 'Alt+Home'
    'Last section' = 'Alt+End'
    'Open FileDentify report...' = 'Ctrl+R'
    'Open folder...' = 'Ctrl+Shift+L'
    'View HTML report' = 'Alt+V'
    'Save report...' = 'Ctrl+S'
    'Refresh original files' = 'F5'
    'Open containing folder' = 'Alt+L'
    'Advanced file viewer' = 'F4'
}

foreach ($entry in $expectedContext.GetEnumerator()) {
    Test-ExpectedShortcut 'Report tree context' $contextItems $entry.Key $entry.Value
}

if ($failures.Count -gt 0) {
    throw "Menu sanity check failed:`r`n - $($failures -join "`r`n - ")"
}

Write-Host 'Menu sanity check passed.'
