param(
    [string]$ReleaseZip = '',
    [switch]$AllHistory
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot

function Fail([string]$message) {
    Write-Error $message
    exit 1
}

function Get-TextFiles([string]$root) {
    $extensions = @(
        '.bat',
        '.cmd',
        '.config',
        '.cs',
        '.csproj',
        '.htm',
        '.html',
        '.json',
        '.md',
        '.ps1',
        '.sln',
        '.txt',
        '.xml'
    )

    if (Test-Path -LiteralPath (Join-Path $root '.git')) {
        $trackedOrPublicUntracked = git -C $root ls-files --cached --others --exclude-standard
        return $trackedOrPublicUntracked |
            ForEach-Object { Join-Path $root $_ } |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
            ForEach-Object { Get-Item -LiteralPath $_ } |
            Where-Object {
                $extensions -contains $_.Extension.ToLowerInvariant() -or
                $_.Name -eq '.gitignore'
            }
    }

    Get-ChildItem -LiteralPath $root -Recurse -File -Force |
        Where-Object {
            $_.FullName -notmatch '\\(\.git|release|bin|obj)(\\|$)' -and
            (
                $extensions -contains $_.Extension.ToLowerInvariant() -or
                $_.Name -eq '.gitignore'
            )
        }
}

function Get-RelativePath([string]$root, [string]$path) {
    $rootFull = [IO.Path]::GetFullPath($root).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $pathFull = [IO.Path]::GetFullPath($path)
    if ($pathFull.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase)) {
        return $pathFull.Substring($rootFull.Length)
    }
    return $pathFull
}

function Test-TextFile([string]$path, [string]$displayPath) {
    $text = Get-Content -LiteralPath $path -Raw
    $bs = [string][char]92
    $privateSyncName = 'Drop' + 'box'
    $privateWorkspace = 'backups' + $bs + 'Codex'
    $privateCodexCurrent = 'Codex' + $bs + 'current'
    $privateUser = 'Onj' + 'Lo'
    $privateMachineOne = 'Mer' + 'jille'
    $privateMachineTwo = 'Ko' + 'bo'
    $privateMachineThree = 'VIP' + '40'
    $fileTokenMarker = 'token' + 'File'
    $sharedTokenMarker = 'shared' + 'Token' + 'File'
    $privateNamesPattern = '(?<![A-Za-z0-9])(?:' +
        [regex]::Escape($privateUser) + '|' +
        [regex]::Escape($privateMachineOne) + '|' +
        [regex]::Escape($privateMachineTwo) + '|' +
        [regex]::Escape($privateMachineThree) +
        ')(?![A-Za-z0-9])'
    $checks = [ordered]@{
        'absolute Windows local path' = ('(?<![A-Za-z0-9])(?:[A-Z]:' + [regex]::Escape($bs) + '|[A-Z]:/)')
        'local user profile path' = ('Users' + [regex]::Escape($bs) + '[A-Za-z0-9_.-]+')
        'private sync/workspace path wording' = ([regex]::Escape($privateSyncName) + '|' + [regex]::Escape($privateWorkspace) + '|' + [regex]::Escape($privateCodexCurrent))
        'local user or machine name' = $privateNamesPattern
        'token loaded from a file in public docs/source' = ('GH_TOKEN\s*=.*Get-Content|GITHUB_TOKEN\s*=.*Get-Content|' + [regex]::Escape($fileTokenMarker) + '|' + [regex]::Escape($sharedTokenMarker))
    }

    foreach ($description in $checks.Keys) {
        if ($text -match $checks[$description]) {
            Fail "$displayPath contains forbidden private/personal release text: $description"
        }
    }
}

function Test-Directory([string]$root, [string]$label) {
    if (-not (Test-Path -LiteralPath $root)) {
        Fail "$label does not exist: $root"
    }

    foreach ($file in Get-TextFiles $root) {
        $relative = Get-RelativePath $root $file.FullName
        Test-TextFile $file.FullName "$label\$relative"
    }
}

function Test-ManualChangelog {
    $manualPath = Join-Path $repoRoot 'src\ManualService.cs'
    if (-not (Test-Path -LiteralPath $manualPath)) {
        Fail "ManualService.cs does not exist."
    }

    $text = Get-Content -LiteralPath $manualPath -Raw
    $matches = [regex]::Matches($text, 'AppendLine\("<h3>([0-9]+(?:\.[0-9]+)*)</h3>"\)')
    $versions = @($matches | ForEach-Object { $_.Groups[1].Value })
    $requiredVersions = @('1.5', '1.4.1', '1.4', '1.3', '1.2', '1.1.1', '1.1', '1.0')

    foreach ($version in $requiredVersions) {
        $count = @($versions | Where-Object { $_ -eq $version }).Count
        if ($count -ne 1) {
            Fail "Manual changelog must contain exactly one $version section; found $count."
        }
    }

    for ($i = 0; $i -lt $requiredVersions.Count; $i++) {
        $actual = $versions[$i]
        if ($actual -ne $requiredVersions[$i]) {
            Fail "Manual changelog section order is wrong. Expected $($requiredVersions -join ', '); found $($versions -join ', ')."
        }
    }
}

function Test-Zip([string]$zipPath) {
    if (-not (Test-Path -LiteralPath $zipPath)) {
        Fail "Release ZIP does not exist: $zipPath"
    }

    $extractRoot = Join-Path ([IO.Path]::GetTempPath()) ('FileDentify-release-privacy-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $extractRoot | Out-Null
    try {
        Expand-Archive -LiteralPath $zipPath -DestinationPath $extractRoot -Force
        Test-Directory $extractRoot 'release ZIP'
    }
    finally {
        Remove-Item -LiteralPath $extractRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Test-AllHistory {
    $bs = [string][char]92
    $privateSyncName = 'Drop' + 'box'
    $privateWorkspace = 'backups' + $bs + 'Codex'
    $privateCodexCurrent = 'Codex' + $bs + 'current'
    $sharedTokenMarker = 'shared' + 'Token' + 'File'
    $patterns = @(
        ('D:' + [regex]::Escape($bs)),
        ('E:' + [regex]::Escape($bs)),
        ('C:' + [regex]::Escape($bs) + 'Users' + [regex]::Escape($bs) + 'Onj' + 'Lo'),
        [regex]::Escape($privateSyncName),
        [regex]::Escape($privateWorkspace),
        [regex]::Escape($privateCodexCurrent),
        [regex]::Escape($sharedTokenMarker),
        'GH_TOKEN\s*=.*Get-Content',
        'GITHUB_TOKEN\s*=.*Get-Content'
    )

    $revisions = git -C $repoRoot rev-list --all
    foreach ($pattern in $patterns) {
        $matches = git -C $repoRoot grep -n -I -E $pattern $revisions 2>$null
        if ($LASTEXITCODE -eq 0) {
            $matches | Select-Object -First 20 | ForEach-Object { Write-Error $_ }
            Fail "Git history contains forbidden private/personal release text matching: $pattern"
        }
    }
}

Test-Directory $repoRoot 'working tree'
Test-ManualChangelog

if (-not [string]::IsNullOrWhiteSpace($ReleaseZip)) {
    Test-Zip $ReleaseZip
}

if ($AllHistory) {
    Test-AllHistory
}

Write-Host 'FileDentify release privacy check passed.'
