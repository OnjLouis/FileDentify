$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$changelog = Join-Path $root 'src\ManualService.Changelog.cs'
$text = Get-Content -LiteralPath $changelog -Raw

$versionMatches = [regex]::Matches($text, '<h3>(?<version>[^<]+)</h3>')
for ($i = 0; $i -lt $versionMatches.Count; $i++) {
    $current = $versionMatches[$i]
    $start = $current.Index
    $end = if ($i + 1 -lt $versionMatches.Count) { $versionMatches[$i + 1].Index } else { $text.Length }
    $section = $text.Substring($start, $end - $start)
    $linkCount = [regex]::Matches($section, '#supported-file-types').Count
    if ($linkCount -gt 1) {
        throw "Changelog version $($current.Groups['version'].Value) links to the supported file types table $linkCount times. Keep at most one supported-file-types callback per version section."
    }
}

Write-Host 'Changelog sanity check passed.'
