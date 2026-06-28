param(
    [string]$OutFile = '',
    [switch]$OpenInBrowser
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot

function Get-GitHubHeaders {
    $token = $env:GH_TOKEN
    if ([string]::IsNullOrWhiteSpace($token)) {
        $token = $env:GITHUB_TOKEN
    }
    if ([string]::IsNullOrWhiteSpace($token)) {
        $tokenFile = Join-Path $repoRoot 'token.txt'
        if (Test-Path -LiteralPath $tokenFile) {
            $token = (Get-Content -LiteralPath $tokenFile -Raw).Trim()
        }
        }
    }

    $headers = @{
        'Accept' = 'application/vnd.github+json'
        'User-Agent' = 'FileDentify-CommunitySearch'
    }
    if (![string]::IsNullOrWhiteSpace($token)) {
        $headers['Authorization'] = "Bearer $token"
        $headers['X-GitHub-Api-Version'] = '2022-11-28'
    }
    return $headers
}

function Invoke-GitHubSearch([string]$kind, [string]$query) {
    $encoded = [Uri]::EscapeDataString($query)
    $uri = "https://api.github.com/search/${kind}?q=$encoded&per_page=10"
    try {
        $result = Invoke-RestMethod -Uri $uri -Headers (Get-GitHubHeaders)
        return @($result.items)
    }
    catch {
        return @([pscustomobject]@{
            Error = $_.Exception.Message
            Query = $query
            Kind = $kind
        })
    }
}

function SearchUrl([string]$query) {
    return 'https://www.google.com/search?q=' + [Uri]::EscapeDataString($query)
}

$queries = @(
    '"FileDentify"',
    '"FileDentify" "OnjLouis"',
    '"OnjLouis/FileDentify"',
    '"FileDentify" "Andre Louis"',
    '"FileDentify" "file identification"',
    '"FileDentify" "libmagic"',
    '"FileDentify" "Windows" "file command"',
    '"FileDentify" "SendTo"',
    '"FileDentify" "screen reader"',
    '"FileDentify" "NVDA"',
    '"FileDentify" "JAWS"',
    '"FileDentify" "fd.com"',
    'site:groups.io "FileDentify"',
    'site:freelists.org "FileDentify"',
    'site:reddit.com "FileDentify"',
    'site:forum.audiogames.net "FileDentify"',
    'site:applevis.com "FileDentify"',
    'site:mastodon.social "FileDentify"',
    'site:bsky.app "FileDentify"'
)

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# FileDentify community search")
$lines.Add("")
$lines.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("")
$lines.Add("Purpose: check for public feedback that has not arrived as a GitHub issue. FileDentify is a niche name, so exact-name searches should have fewer false positives than broader app names.")
$lines.Add("")
$lines.Add("## GitHub repository issues")
$repoIssues = Invoke-GitHubSearch 'issues' 'repo:OnjLouis/FileDentify is:issue'
if ($repoIssues.Count -eq 0) {
    $lines.Add("- No GitHub issues found by search.")
} else {
    foreach ($item in $repoIssues) {
        if ($item.Error) {
            $lines.Add("- GitHub issue search failed: $($item.Error)")
        } else {
            $lines.Add("- #$($item.number) $($item.title) - $($item.html_url)")
        }
    }
}
$lines.Add("")
$lines.Add("## GitHub pull requests")
$repoPulls = Invoke-GitHubSearch 'issues' 'repo:OnjLouis/FileDentify is:pr'
if ($repoPulls.Count -eq 0) {
    $lines.Add("- No GitHub pull requests found by search.")
} else {
    foreach ($item in $repoPulls) {
        if ($item.Error) {
            $lines.Add("- GitHub pull request search failed: $($item.Error)")
        } else {
            $lines.Add("- PR #$($item.number) $($item.title) - $($item.html_url)")
        }
    }
}
$lines.Add("")
$lines.Add("## GitHub public mention search")
$mentionQueries = @(
    '"FileDentify"',
    '"OnjLouis/FileDentify"',
    '"FileDentify" "OnjLouis"',
    '"FileDentify" "Andre Louis"'
)
foreach ($query in $mentionQueries) {
    $lines.Add("### $query")
    $items = Invoke-GitHubSearch 'issues' $query
    if ($items.Count -eq 0) {
        $lines.Add("- No matching GitHub issues or discussions surfaced through issue search.")
    } else {
        foreach ($item in $items) {
            if ($item.Error) {
                $lines.Add("- Search failed: $($item.Error)")
            } else {
                $lines.Add("- $($item.repository_url -replace '^https://api.github.com/repos/','') #$($item.number) $($item.title) - $($item.html_url)")
            }
        }
    }
    $lines.Add("")
}

$lines.Add("## Web and community searches")
foreach ($query in $queries) {
    $url = SearchUrl $query
    $lines.Add("- $query")
    $lines.Add("  $url")
    if ($OpenInBrowser) {
        Start-Process $url
    }
}
$lines.Add("")
$lines.Add("## What to look for")
$lines.Add("- Accessibility complaints: screen-reader focus, tree/detail review, edit-field line breaks, keyboard traps, missing shortcuts, menu mnemonics, terminal-mode key handling.")
$lines.Add("- Identification gaps: files that libmagic recognizes but FileDentify summaries undersell, common formats showing as unknown, unsupported archive/media/sample-library/game/project formats.")
$lines.Add("- Reporting problems: HTML tables that repeat useless data, hard-to-parse multi-file overviews, missing useful metadata, confusing scan notes.")
$lines.Add('- Workflow problems: Send To setup, folder scans, desktop shortcut, update checks, `fd.com` and `FileDentify.exe` needing to live together, reports from multiple non-contiguous files.')
$lines.Add("- Safety/privacy concerns: Clipman files, encrypted containers, password handling, paths in shared reports, third-party notice clarity.")
$lines.Add("- Repeated feature requests: deeper readable-string search, more bespoke file families, export/report comparison, shell integration, context-menu/property-sheet ideas.")

if ([string]::IsNullOrWhiteSpace($OutFile)) {
    $OutFile = Join-Path $repoRoot 'CommunitySearch.md'
}

$lines | Set-Content -LiteralPath $OutFile -Encoding UTF8
Write-Host "Community search checklist written to $OutFile"
