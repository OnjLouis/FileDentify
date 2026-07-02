param(
    [Parameter(Mandatory = $true)]
    [string[]]$Roots,

    [string]$RepoRoot = $PSScriptRoot,

    [string]$OutputDirectory,

    [int]$MaxSamplesPerExtension = 12,

    [switch]$IncludeKnownExtensions
)

$ErrorActionPreference = 'Stop'

function New-KnownExtensionSet {
    param([string]$Root)

    $known = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $builtIn = @(
        '.exe', '.dll', '.sys', '.ocx', '.cpl', '.scr', '.drv', '.vxd', '.com',
        '.txt', '.log', '.ini', '.cfg', '.conf', '.xml', '.json', '.csv',
        '.htm', '.html', '.css', '.js', '.ps1', '.bat', '.cmd', '.reg'
    )
    foreach ($ext in $builtIn) { [void]$known.Add($ext) }

    if (-not (Test-Path -LiteralPath $Root)) {
        return $known
    }

    Get-ChildItem -LiteralPath $Root -Recurse -File -Include *.cs,*.ps1,*.md -ErrorAction SilentlyContinue |
        ForEach-Object {
            $text = Get-Content -LiteralPath $_.FullName -Raw -ErrorAction SilentlyContinue
            if ([string]::IsNullOrWhiteSpace($text)) { return }

            foreach ($match in [regex]::Matches($text, '(?<![A-Za-z0-9])\.[A-Za-z0-9][A-Za-z0-9_\-]{0,18}(?![A-Za-z0-9_\-])')) {
                $value = $match.Value.ToLowerInvariant()
                if ($value -match '^\.[0-9]+$') { continue }
                [void]$known.Add($value)
            }
        }

    return $known
}

function Get-SampleRecord {
    param([string]$Path)

    try {
        $item = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
        return [pscustomobject]@{
            Path = $item.FullName
            Size = $item.Length
            Modified = $item.LastWriteTime
        }
    } catch {
        return $null
    }
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path ([System.IO.Path]::GetTempPath()) 'FileDentify-candidates'
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$known = New-KnownExtensionSet -Root $RepoRoot

$groups = @{}
$extensionless = New-Object System.Collections.Generic.List[object]
$errors = New-Object System.Collections.Generic.List[object]
$allCandidatePath = Join-Path $OutputDirectory "candidate-paths-$stamp.txt"
$candidateWriter = [System.IO.StreamWriter]::new($allCandidatePath, $false, [System.Text.Encoding]::UTF8)

try {
    foreach ($root in $Roots) {
        if (-not (Test-Path -LiteralPath $root)) {
            $errors.Add([pscustomobject]@{ Root = $root; Error = 'Root not found'; Path = '' })
            continue
        }

        $stack = New-Object System.Collections.Generic.Stack[string]
        $stack.Push((Resolve-Path -LiteralPath $root).Path)

        while ($stack.Count -gt 0) {
            $dir = $stack.Pop()
            try {
                foreach ($child in [System.IO.Directory]::EnumerateDirectories($dir)) {
                    $stack.Push($child)
                }

                foreach ($file in [System.IO.Directory]::EnumerateFiles($dir)) {
                    $ext = [System.IO.Path]::GetExtension($file)
                    $normalized = if ([string]::IsNullOrWhiteSpace($ext)) { '<extensionless>' } else { $ext.ToLowerInvariant() }
                    $isKnown = $normalized -ne '<extensionless>' -and $known.Contains($normalized)
                    if ($isKnown -and -not $IncludeKnownExtensions) {
                        continue
                    }

                    $sample = Get-SampleRecord -Path $file
                    if ($null -eq $sample) {
                        continue
                    }

                    if ($normalized -eq '<extensionless>') {
                        if ($extensionless.Count -lt 500) {
                            $extensionless.Add($sample)
                        }
                        $candidateWriter.WriteLine($file)
                        continue
                    }

                    if (-not $groups.ContainsKey($normalized)) {
                        $groups[$normalized] = [pscustomobject]@{
                            Extension = $normalized
                            Count = 0
                            TotalBytes = [int64]0
                            Samples = New-Object System.Collections.Generic.List[object]
                        }
                    }

                    $group = $groups[$normalized]
                    $group.Count++
                    $group.TotalBytes += $sample.Size
                    if ($group.Samples.Count -lt $MaxSamplesPerExtension) {
                        $group.Samples.Add($sample)
                    }
                    $candidateWriter.WriteLine($file)
                }
            } catch {
                $errors.Add([pscustomobject]@{ Root = $root; Error = $_.Exception.Message; Path = $dir })
            }
        }
    }
} finally {
    $candidateWriter.Dispose()
}

$summary = $groups.Values | Sort-Object -Property @{ Expression = 'Count'; Descending = $true }, @{ Expression = 'Extension'; Descending = $false }
$summaryPath = Join-Path $OutputDirectory "candidate-summary-$stamp.csv"
$samplesPath = Join-Path $OutputDirectory "candidate-samples-$stamp.txt"
$knownPath = Join-Path $OutputDirectory "known-extensions-$stamp.txt"
$errorsPath = Join-Path $OutputDirectory "candidate-errors-$stamp.csv"

$summary |
    Select-Object Extension, Count, TotalBytes |
    Export-Csv -LiteralPath $summaryPath -NoTypeInformation

$known | Sort-Object | Set-Content -LiteralPath $knownPath

$writer = [System.IO.StreamWriter]::new($samplesPath, $false, [System.Text.Encoding]::UTF8)
try {
    $writer.WriteLine('Roots')
    $Roots | ForEach-Object { $writer.WriteLine('  ' + $_) }
    $writer.WriteLine()
    $writer.WriteLine('Unknown or not-yet-known extensions')
    foreach ($group in $summary) {
        $writer.WriteLine()
        $writer.WriteLine($group.Extension + '  count=' + $group.Count + '  totalBytes=' + $group.TotalBytes)
        foreach ($sample in $group.Samples) {
            $writer.WriteLine('  ' + $sample.Size + "`t" + $sample.Modified + "`t" + $sample.Path)
        }
    }

    $writer.WriteLine()
    $writer.WriteLine('Extensionless samples')
    foreach ($sample in $extensionless) {
        $writer.WriteLine('  ' + $sample.Size + "`t" + $sample.Modified + "`t" + $sample.Path)
    }
} finally {
    $writer.Dispose()
}

if ($errors.Count -gt 0) {
    $errors | Export-Csv -LiteralPath $errorsPath -NoTypeInformation
}

[pscustomobject]@{
    Summary = $summaryPath
    Samples = $samplesPath
    KnownExtensions = $knownPath
    CandidatePaths = $allCandidatePath
    Errors = if ($errors.Count -gt 0) { $errorsPath } else { $null }
    UnknownExtensionGroups = $summary.Count
    ExtensionlessSamples = $extensionless.Count
    KnownExtensionCount = $known.Count
}
