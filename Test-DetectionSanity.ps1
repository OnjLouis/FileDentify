param(
    [Parameter(Mandatory = $true)]
    [string]$Executable
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$testRoot = if ([string]::IsNullOrWhiteSpace($env:FILEDENTIFY_TEST_ROOT)) { [IO.Path]::GetTempPath() } else { $env:FILEDENTIFY_TEST_ROOT }
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$testDir = Join-Path $testRoot ('FileDentify-DetectionSanity-' + [guid]::NewGuid().ToString('N'))
if (Test-Path -LiteralPath $testDir) {
    Remove-Item -LiteralPath $testDir -Recurse -Force
}
New-Item -ItemType Directory -Path $testDir -Force | Out-Null
trap {
    $failureMessage = $_.Exception.Message
    Remove-Item -LiteralPath $testDir -Recurse -Force -ErrorAction SilentlyContinue
    throw $failureMessage
}

function Write-AsciiAt {
    param(
        [byte[]]$Bytes,
        [int]$Offset,
        [string]$Text
    )

    $encoded = [System.Text.Encoding]::ASCII.GetBytes($Text)
    [Array]::Copy($encoded, 0, $Bytes, $Offset, $encoded.Length)
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Expected
    )

    if ($Text.IndexOf($Expected, [System.StringComparison]::Ordinal) -lt 0) {
        throw "Detection sanity check failed: expected '$Expected'."
    }
}

function Assert-Item {
    param(
        [string]$Text,
        [string]$Title,
        [string]$ExpectedValue
    )

    $pattern = [regex]::Escape($Title) + ':\s*\r?\n' + [regex]::Escape($ExpectedValue)
    if (-not [regex]::IsMatch($Text, $pattern, [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
        throw "Detection sanity check failed: expected '${Title}: $ExpectedValue'."
    }
}

function Get-FileDentifyReport {
    param(
        [string]$InputPath,
        [string]$ReportName
    )

    $reportPath = Join-Path $testDir $ReportName
    $process = Start-Process -FilePath $Executable -ArgumentList @('--report', $reportPath, $InputPath) -Wait -PassThru
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $reportPath)) {
        throw "Detection sanity check failed: FileDentify did not generate $ReportName."
    }
    return Get-Content -LiteralPath $reportPath -Raw
}

function Assert-MutationInvariant {
    param(
        [string]$InputPath,
        [string]$ExpectedType,
        [string[]]$ForbiddenSections = @(
            'Native Instruments', 'Steinberg Cubase', 'Speech voice', 'Binary blob',
            'UFS sample library container', 'Game/ROM data'
        )
    )

    $aliases = @(
        'downloaded-file', 'downloaded-file.bin', 'downloaded-file.db', 'downloaded-file.db3',
        'downloaded-file.box', 'downloaded-file.pdf', 'downloaded-file.scr', 'downloaded-file.nki',
        'downloaded-file.fxp', 'downloaded-file.sav', 'downloaded-file.syn', 'downloaded-file.blob',
        'downloaded-file.ufs', 'downloaded-file.rom', 'downloaded-file.pat'
    )
    foreach ($alias in $aliases) {
        $aliasPath = Join-Path $testDir $alias
        Copy-Item -LiteralPath $InputPath -Destination $aliasPath -Force
        $aliasText = Get-FileDentifyReport $aliasPath ('mutation-' + ($alias -replace '[^A-Za-z0-9]', '-') + '-' + [guid]::NewGuid().ToString('N') + '.txt')
        Assert-Item $aliasText 'Likely type' $ExpectedType
        foreach ($section in $ForbiddenSections) {
            if ([regex]::IsMatch($aliasText, '(?m)^' + [regex]::Escape($section) + '\s*$')) {
                throw "Detection sanity check failed: $ExpectedType renamed as $alias produced contradictory section '$section'."
            }
        }
        if ($alias -eq 'downloaded-file.box') {
            Assert-Contains $aliasText 'Mismatch:'
            Assert-Contains $aliasText "Detected content: $ExpectedType."
            Assert-Contains $aliasText 'Filename extension: .box.'
            Assert-Contains $aliasText 'The content match does not depend on the filename.'
            Assert-Contains $aliasText 'FileDentify identifies file structure, not whether a file is harmless.'
            foreach ($stalePhrase in @('Content identification:', 'Header and extension mismatch:', 'Expected extension(s):', 'Safety boundary:', 'Recommendation:')) {
                if ($aliasText.IndexOf($stalePhrase, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    throw "Detection sanity check failed: redundant safety item '$stalePhrase' returned for $ExpectedType."
                }
            }
        }
    }
}

$firmware = New-Object byte[] 256
Write-AsciiAt $firmware 0x20 'OR-20-02'
Write-AsciiAt $firmware 0x28 'A1.02.00'
Write-AsciiAt $firmware 0x30 'A1.02.00.09r62'
$firmwarePath = Join-Path $testDir 'orbit-firmware'
$firmwareReport = Join-Path $testDir 'orbit-report.txt'
[System.IO.File]::WriteAllBytes($firmwarePath, $firmware)

$firmwareProcess = Start-Process -FilePath $Executable -ArgumentList @('--report', $firmwareReport, $firmwarePath) -Wait -PassThru
if ($firmwareProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $firmwareReport)) {
    throw 'Detection sanity check failed: FileDentify did not generate the Orbit firmware report.'
}

$reportText = Get-Content -LiteralPath $firmwareReport -Raw
Assert-Item $reportText 'Likely type' 'Orbit Reader 20 Plus firmware image'
Assert-Item $reportText 'Best match' 'Orbit Reader 20 Plus firmware image'
Assert-Item $reportText 'Device' 'Orbit Reader 20 Plus braille display and notetaker'
Assert-Item $reportText 'Compatible firmware family' 'A1.02.00'
Assert-Item $reportText 'Firmware version' 'A1.02.00.09r62'
Assert-Item $reportText 'Source' 'LibFileDentify 0.2.0'
Assert-Item $reportText 'Match confidence' 'High: content evidence confirmed by the embedded LibFileDentify engine'
Assert-Item $reportText 'Extensionless file' 'The filename has no extension, so FileDentify relied on header, filename, path, or structure clues.'
Assert-Contains $reportText 'OR-20-02 marker at offset 0x20'
Assert-MutationInvariant $firmwarePath 'Orbit Reader 20 Plus firmware image'

$unrelated = New-Object byte[] 256
Write-AsciiAt $unrelated 0x20 'NOT-OR20'
$unrelatedPath = Join-Path $testDir 'unrelated.bin'
$unrelatedReport = Join-Path $testDir 'unrelated-report.txt'
[System.IO.File]::WriteAllBytes($unrelatedPath, $unrelated)

$unrelatedProcess = Start-Process -FilePath $Executable -ArgumentList @('--report', $unrelatedReport, $unrelatedPath) -Wait -PassThru
if ($unrelatedProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $unrelatedReport)) {
    throw 'Detection sanity check failed: FileDentify did not generate the unrelated-file report.'
}

$unrelatedText = Get-Content -LiteralPath $unrelatedReport -Raw
if ($unrelatedText.IndexOf('Orbit Reader 20 Plus firmware', [System.StringComparison]::Ordinal) -ge 0) {
    throw 'Detection sanity check failed: an unrelated .bin file was identified as Orbit Reader firmware.'
}

$zoom = New-Object byte[] (1024 * 1024)
Write-AsciiAt $zoom 0 'F6 System Data  Copyright (c) ZOOM Corporation 2019. All rights reserved.'
Write-AsciiAt $zoom 256 'Low Battery !'
$zoomPath = Join-Path $testDir 'renamed-zoom-firmware'
[System.IO.File]::WriteAllBytes($zoomPath, $zoom)
$zoomText = Get-FileDentifyReport $zoomPath 'zoom-report.txt'
Assert-Item $zoomText 'Likely type' 'ZOOM F6 recorder firmware image'
Assert-Item $zoomText 'Best match' 'ZOOM F6 recorder firmware image'
Assert-Item $zoomText 'Recorder' 'F6'
Assert-Item $zoomText 'Product family' 'F series field recorder'
Assert-Contains $zoomText 'filename and extension are not required'
if ($zoomText.IndexOf("`r`nNative Instruments`r`n", [System.StringComparison]::Ordinal) -ge 0) {
    throw 'Detection sanity check failed: ZOOM firmware produced an unrelated Native Instruments section.'
}
Assert-MutationInvariant $zoomPath 'ZOOM F6 recorder firmware image' @('Native Instruments', 'Steinberg/VST', 'Steinberg Cubase')

$pdfPath = Join-Path $testDir 'manual.pdf'
[IO.File]::WriteAllBytes($pdfPath, [Text.Encoding]::ASCII.GetBytes("%PDF-1.7`r`n1 0 obj`r`n<<>>`r`nendobj`r`n"))
Assert-MutationInvariant $pdfPath 'PDF document'

$pe = New-Object byte[] 128
Write-AsciiAt $pe 0 'MZ'
$pePath = Join-Path $testDir 'program.exe'
[IO.File]::WriteAllBytes($pePath, $pe)
Assert-MutationInvariant $pePath 'Windows executable or DLL'

$sqlite = New-Object byte[] 128
Write-AsciiAt $sqlite 0 "SQLite format 3`0"
$sqlitePath = Join-Path $testDir 'records.sqlite'
[IO.File]::WriteAllBytes($sqlitePath, $sqlite)
Assert-MutationInvariant $sqlitePath 'SQLite database'

$markdownPath = Join-Path $testDir 'notes.md'
$markdownReport = Join-Path $testDir 'markdown-report.txt'
[System.IO.File]::WriteAllText($markdownPath, "# Notes`r`n`r`nThis is ordinary Markdown text.", [System.Text.Encoding]::UTF8)
$markdownProcess = Start-Process -FilePath $Executable -ArgumentList @('--report', $markdownReport, $markdownPath) -Wait -PassThru
if ($markdownProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $markdownReport)) {
    throw 'Detection sanity check failed: FileDentify did not generate the Markdown report.'
}
$markdownText = Get-Content -LiteralPath $markdownReport -Raw
if ($markdownText.IndexOf('Sega Mega Drive', [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
    throw 'Detection sanity check failed: an ordinary Markdown .md file was identified as a Sega ROM.'
}

$sega = New-Object byte[] 512
Write-AsciiAt $sega 0x100 'SEGA'
$segaPath = Join-Path $testDir 'game.md'
$segaReport = Join-Path $testDir 'sega-report.txt'
[System.IO.File]::WriteAllBytes($segaPath, $sega)
$segaProcess = Start-Process -FilePath $Executable -ArgumentList @('--report', $segaReport, $segaPath) -Wait -PassThru
if ($segaProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $segaReport)) {
    throw 'Detection sanity check failed: FileDentify did not generate the Sega ROM report.'
}
$segaText = Get-Content -LiteralPath $segaReport -Raw
Assert-Item $segaText 'Likely type' 'Sega Mega Drive/Genesis ROM'

$structuredSamples = @(
    @('renamed-mum', '<?xml version="1.0"?><assembly xmlns="urn:schemas-microsoft-com:asm.v3"><assemblyIdentity name="Test.Package" version="1.2.3.4" processorArchitecture="amd64" language="neutral" /></assembly>', 'Windows servicing package manifest'),
    @('renamed-admx', '<?xml version="1.0"?><policyDefinitions xmlns="http://schemas.microsoft.com/GroupPolicy/2006/07/PolicyDefinitions"><categories><category name="Test" /></categories><policies><policy name="Example" /></policies></policyDefinitions>', 'Group Policy administrative template'),
    @('renamed-adml', '<?xml version="1.0"?><policyDefinitionResources xmlns="http://schemas.microsoft.com/GroupPolicy/2006/07/PolicyDefinitions"><resources><stringTable><string id="Test">Example</string></stringTable></resources></policyDefinitionResources>', 'Group Policy language resource'),
    @('renamed-resw', '<?xml version="1.0"?><root><!-- Microsoft ResX Schema --><data name="Greeting"><value>Hello</value></data></root>', 'Windows XML resource file'),
    @('renamed-diagpkg', '<?xml version="1.0"?><d:DiagnosticPackage xmlns:d="http://schemas.microsoft.com/diagnostics/2010/08/diagnosticpackage" SchemaVersion="1.0" Localized="true"><d:Interactions><d:Interaction /></d:Interactions></d:DiagnosticPackage>', 'Windows diagnostic package definition'),
    @('renamed-opf', '<?xml version="1.0"?><package xmlns="http://www.idpf.org/2007/opf" version="3.0"><metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:title>Test Book</dc:title><dc:creator>Test Author</dc:creator></metadata><manifest><item id="chapter" href="chapter.xhtml" media-type="application/xhtml+xml" /></manifest><spine><itemref idref="chapter" /></spine></package>', 'EPUB package document'),
    @('renamed-mtl', "newmtl TestMaterial`r`nKd 1.0 0.5 0.25`r`nmap_Kd texture.png`r`n", 'Wavefront OBJ material library')
)

foreach ($sample in $structuredSamples) {
    $samplePath = Join-Path $testDir $sample[0]
    [System.IO.File]::WriteAllText($samplePath, $sample[1], [System.Text.Encoding]::UTF8)
    $sampleText = Get-FileDentifyReport $samplePath ($sample[0] + '.txt')
    Assert-Item $sampleText 'Likely type' $sample[2]
    Assert-Item $sampleText 'Best match' $sample[2]
    Assert-Item $sampleText 'Source' 'LibFileDentify 0.2.0'
    Assert-Item $sampleText 'Match confidence' 'High: content evidence confirmed by the embedded LibFileDentify engine'
}

$cdf = New-Object byte[] 64
Write-AsciiAt $cdf 0 'PcmH'
$cdfPath = Join-Path $testDir 'renamed-cdf'
[System.IO.File]::WriteAllBytes($cdfPath, $cdf)
$cdfText = Get-FileDentifyReport $cdfPath 'cdf-report.txt'
Assert-Item $cdfText 'Likely type' 'Windows component definition metadata'

$jmod = New-Object byte[] 64
Write-AsciiAt $jmod 0 'JM'
$jmod[2] = 1
$jmod[4] = [byte][char]'P'
$jmod[5] = [byte][char]'K'
$jmod[6] = 3
$jmod[7] = 4
$jmodPath = Join-Path $testDir 'module.jmod'
[System.IO.File]::WriteAllBytes($jmodPath, $jmod)
$jmodText = Get-FileDentifyReport $jmodPath 'jmod-report.txt'
Assert-Item $jmodText 'Likely type' 'Java JMOD module archive'

$lua = New-Object byte[] 64
$lua[0] = 0x1B
Write-AsciiAt $lua 1 'Lua'
$lua[4] = 0x54
$luaPath = Join-Path $testDir 'module.luac'
[System.IO.File]::WriteAllBytes($luaPath, $lua)
$luaText = Get-FileDentifyReport $luaPath 'lua-report.txt'
Assert-Item $luaText 'Likely type' 'Lua precompiled bytecode'
Assert-Item $luaText 'Lua version' '5.4 (header 0x54)'

$fakeOpfPath = Join-Path $testDir 'not-an-ebook.opf'
[System.IO.File]::WriteAllText($fakeOpfPath, '<settings><package>ordinary application data</package></settings>', [System.Text.Encoding]::UTF8)
$fakeOpfText = Get-FileDentifyReport $fakeOpfPath 'fake-opf-report.txt'
if ($fakeOpfText.IndexOf('EPUB package document', [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
    throw 'Detection sanity check failed: unrelated XML with an .opf extension was identified as an EPUB package document.'
}

$efi = New-Object byte[] 256
Write-AsciiAt $efi 0 'MZ'
$efiPath = Join-Path $testDir 'boot.efi'
[System.IO.File]::WriteAllBytes($efiPath, $efi)
$efiText = Get-FileDentifyReport $efiPath 'efi-report.txt'
Assert-Item $efiText 'Likely type' 'UEFI executable image'
Assert-Item $efiText 'Role' 'UEFI firmware executable or boot-service application.'

$signedDataOid = [byte[]](0x06,0x09,0x2A,0x86,0x48,0x86,0xF7,0x0D,0x01,0x07,0x02)
$p7x = New-Object byte[] 64
Write-AsciiAt $p7x 0 'PKCX'
[Array]::Copy($signedDataOid, 0, $p7x, 8, $signedDataOid.Length)
$p7xPath = Join-Path $testDir 'signature.p7x'
[System.IO.File]::WriteAllBytes($p7xPath, $p7x)
$p7xText = Get-FileDentifyReport $p7xPath 'p7x-report.txt'
Assert-Item $p7xText 'Likely type' 'Windows AppX PKCX signature container'
Assert-Item $p7xText 'Source' 'LibFileDentify 0.2.0'
Assert-Item $p7xText 'Match confidence' 'High: content evidence confirmed by the embedded LibFileDentify engine'

$p7b = New-Object byte[] 64
$p7b[0] = 0x30
$p7b[1] = 0x82
[Array]::Copy($signedDataOid, 0, $p7b, 8, $signedDataOid.Length)
$p7bPath = Join-Path $testDir 'certificates.p7b'
[System.IO.File]::WriteAllBytes($p7bPath, $p7b)
$p7bText = Get-FileDentifyReport $p7bPath 'p7b-report.txt'
Assert-Item $p7bText 'Likely type' 'PKCS #7 certificate bundle'

$pfx = New-Object byte[] 64
$pfx[0] = 0x30
$pfx[1] = 0x82
$pfx[4] = 0x02
$pfx[5] = 0x01
$pfx[6] = 0x03
$dataOid = [byte[]](0x06,0x09,0x2A,0x86,0x48,0x86,0xF7,0x0D,0x01,0x07,0x01)
[Array]::Copy($dataOid, 0, $pfx, 12, $dataOid.Length)
$pfxPath = Join-Path $testDir 'identity.pfx'
[System.IO.File]::WriteAllBytes($pfxPath, $pfx)
$pfxText = Get-FileDentifyReport $pfxPath 'pfx-report.txt'
Assert-Item $pfxText 'Likely type' 'PKCS #12 personal information exchange'
Assert-Contains $pfxText 'FileDentify does not request a password'

$pemPath = Join-Path $testDir 'private.pem'
[System.IO.File]::WriteAllText($pemPath, "-----BEGIN PRIVATE KEY-----`r`nAA==`r`n-----END PRIVATE KEY-----`r`n", [System.Text.Encoding]::ASCII)
$pemText = Get-FileDentifyReport $pemPath 'pem-report.txt'
Assert-Item $pemText 'Likely type' 'PEM certificate or key bundle'
Assert-Contains $pemText 'Treat this file as confidential.'

$discoveredXmlSamples = @(
    @('renamed-contact', '<?xml version="1.0"?><c:contact xmlns:c="http://schemas.microsoft.com/Contact"><c:FormattedName>Test Person</c:FormattedName><c:EmailAddress>test@example.invalid</c:EmailAddress></c:contact>', 'Windows Contact file'),
    @('renamed-smil', '<?xml version="1.0"?><smil xmlns="http://www.w3.org/2001/SMIL20/Language"><body><seq><par><text src="chapter.xhtml"/><audio src="chapter.mp3"/></par></seq></body></smil>', 'SMIL synchronized multimedia document'),
    @('renamed-acsm', '<?xml version="1.0"?><fulfillmentToken xmlns="http://ns.adobe.com/adept"><title>Test Book</title><creator>Test Author</creator><resource>private-token</resource></fulfillmentToken>', 'Adobe ebook fulfillment token'),
    @('renamed-aup', '<?xml version="1.0"?><project xmlns="http://audacity.sourceforge.net/xml/" audacityversion="2.4.2" version="1.3.0" projname="project_data" rate="44100"><wavetrack><waveclip/></wavetrack></project>', 'Audacity legacy project'),
    @('renamed-kmmacros', '<?xml version="1.0"?><plist version="1.0"><dict><key>Keyboard Maestro Version</key><string>11</string><key>Macros</key><array/></dict></plist>', 'Keyboard Maestro macro library')
)

foreach ($sample in $discoveredXmlSamples) {
    $samplePath = Join-Path $testDir $sample[0]
    [System.IO.File]::WriteAllText($samplePath, $sample[1], [System.Text.Encoding]::UTF8)
    $sampleText = Get-FileDentifyReport $samplePath ($sample[0] + '.txt')
    Assert-Item $sampleText 'Likely type' $sample[2]
    Assert-Item $sampleText 'Best match' $sample[2]
    Assert-Item $sampleText 'Source' 'LibFileDentify 0.2.0'
    Assert-Item $sampleText 'Match confidence' 'High: content evidence confirmed by the embedded LibFileDentify engine'
}

$mamd = New-Object byte[] 128
Write-AsciiAt $mamd 0 'FORM'
Write-AsciiAt $mamd 8 'AIFF'
Write-AsciiAt $mamd 16 'COMM'
Write-AsciiAt $mamd 32 'LGBM'
Write-AsciiAt $mamd 48 'Creator: Logic Pro X 10.5'
$mamdPath = Join-Path $testDir 'renamed-mamd'
[System.IO.File]::WriteAllBytes($mamdPath, $mamd)
$mamdText = Get-FileDentifyReport $mamdPath 'mamd-report.txt'
Assert-Item $mamdText 'Likely type' 'Logic Pro audio metadata sidecar'

$zdt = New-Object byte[] 128
Write-AsciiAt $zdt 0 'ZOOM L-20    PROJECT DATA VER0001'
Write-AsciiAt $zdt 64 'CH01 CH02 CH03'
$zdtPath = Join-Path $testDir 'renamed-zdt'
[System.IO.File]::WriteAllBytes($zdtPath, $zdt)
$zdtText = Get-FileDentifyReport $zdtPath 'zdt-report.txt'
Assert-Item $zdtText 'Likely type' 'Zoom LiveTrak L-20 project data'

$dbb = New-Object byte[] 64
Write-AsciiAt $dbb 0 'l33l'
$dbbPath = Join-Path $testDir 'renamed-dbb'
[System.IO.File]::WriteAllBytes($dbbPath, $dbb)
$dbbText = Get-FileDentifyReport $dbbPath 'dbb-report.txt'
Assert-Item $dbbText 'Likely type' 'Legacy Skype database'
Assert-Contains $dbbText 'Legacy Skype databases can contain'

$scpt = New-Object byte[] 64
Write-AsciiAt $scpt 0 'FasdUAS 1.101.10'
$scptPath = Join-Path $testDir 'renamed-scpt'
[System.IO.File]::WriteAllBytes($scptPath, $scpt)
$scptText = Get-FileDentifyReport $scptPath 'scpt-report.txt'
Assert-Item $scptText 'Likely type' 'Compiled AppleScript'

$pdd = New-Object byte[] 64
$pdd[0] = 0x79
$pdd[3] = 0x10
$pdd[4] = 0xD0
$pdd[5] = 0x39
$pdd[7] = 0x10
$pdd[8] = 0x25
$pdd[9] = 0x1D
$pdd[11] = 0x10
$pddPath = Join-Path $testDir 'driver.pdd'
[System.IO.File]::WriteAllBytes($pddPath, $pdd)
$pddText = Get-FileDentifyReport $pddPath 'pdd-report.txt'
Assert-Item $pddText 'Likely type' 'Symbian physical device driver'

$kbd = [byte[]]$pdd.Clone()
Write-AsciiAt $kbd 24 'EPOC'
$kbdPath = Join-Path $testDir 'english.kbd'
[System.IO.File]::WriteAllBytes($kbdPath, $kbd)
$kbdText = Get-FileDentifyReport $kbdPath 'kbd-report.txt'
Assert-Item $kbdText 'Likely type' 'Symbian keyboard layout'

$pmlPath = Join-Path $testDir 'renamed-pmml'
[System.IO.File]::WriteAllText($pmlPath, "include(`"gs`")`r`ntitle(`"Test`")`r`ntempo(120)`r`nnewqwstrack(`"Piano`")", [System.Text.Encoding]::UTF8)
$pmlText = Get-FileDentifyReport $pmlPath 'pml-report.txt'
Assert-Item $pmlText 'Likely type' 'PMML music macro source'

$fakePmlPath = Join-Path $testDir 'unrelated.pml'
[System.IO.File]::WriteAllText($fakePmlPath, 'Ordinary Process Monitor or markup data.', [System.Text.Encoding]::UTF8)
$fakePmlText = Get-FileDentifyReport $fakePmlPath 'fake-pml-report.txt'
if ($fakePmlText.IndexOf('PMML music macro source', [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
    throw 'Detection sanity check failed: unrelated .pml data was identified as PMML music source.'
}

$thm = New-Object byte[] 64
$thm[0] = 0xFF
$thm[1] = 0xD8
$thm[2] = 0xFF
$thmPath = Join-Path $testDir 'camera.thm'
[System.IO.File]::WriteAllBytes($thmPath, $thm)
$thmText = Get-FileDentifyReport $thmPath 'thm-report.txt'
Assert-Item $thmText 'Likely type' 'Camera thumbnail JPEG image'
if ([regex]::IsMatch($thmText, '(?m)^Safety hints\s*$')) {
    throw 'Detection sanity check failed: a JPEG camera .thm file received a false safety warning.'
}

$efiMismatchText = Get-FileDentifyReport $efiPath 'efi-safety-report.txt'
if ([regex]::IsMatch($efiMismatchText, '(?m)^Safety hints\s*$')) {
    throw 'Detection sanity check failed: a UEFI .efi image received a false safety warning.'
}

$srtPath = Join-Path $testDir 'renamed-srt'
[System.IO.File]::WriteAllText($srtPath, "1`r`n00:00:01,000 --> 00:00:03,500`r`nTest caption`r`n", [System.Text.Encoding]::UTF8)
$srtText = Get-FileDentifyReport $srtPath 'srt-report.txt'
Assert-Item $srtText 'Likely type' 'SubRip subtitle file'

$fakeSrtPath = Join-Path $testDir 'unrelated.srt'
[System.IO.File]::WriteAllText($fakeSrtPath, 'Ordinary text without subtitle timings.', [System.Text.Encoding]::UTF8)
$fakeSrtText = Get-FileDentifyReport $fakeSrtPath 'fake-srt-report.txt'
if ($fakeSrtText.IndexOf('SubRip subtitle file', [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
    throw 'Detection sanity check failed: unrelated .srt text was identified as SubRip subtitles.'
}

$tga = New-Object byte[] 64
$tga[2] = 2
$tga[12] = 0x40
$tga[13] = 0x01
$tga[14] = 0xF0
$tga[15] = 0x00
$tga[16] = 24
$tgaPath = Join-Path $testDir 'image.tga'
[System.IO.File]::WriteAllBytes($tgaPath, $tga)
$tgaText = Get-FileDentifyReport $tgaPath 'tga-report.txt'
Assert-Item $tgaText 'Likely type' 'Truevision Targa image'
Assert-Item $tgaText 'Dimensions' '320 x 240'

$wmf = New-Object byte[] 64
$wmf[0] = 0xD7
$wmf[1] = 0xCD
$wmf[2] = 0xC6
$wmf[3] = 0x9A
$wmf[10] = 0x80
$wmf[12] = 0x60
$wmf[14] = 0xA0
$wmf[15] = 0x05
$wmfPath = Join-Path $testDir 'renamed-wmf'
[System.IO.File]::WriteAllBytes($wmfPath, $wmf)
$wmfText = Get-FileDentifyReport $wmfPath 'wmf-report.txt'
Assert-Item $wmfText 'Likely type' 'Windows Metafile image'
Assert-Item $wmfText 'Units per inch' '1440'

$ujamJson = '{"instruments":[{"at_byte":13680,"name":"FIN-NEO","size_bytes":305742}],"sample_index":{"at_byte":0,"size_bytes":13680},"sample_starts_msec":0,"uuid":"59eea25c-617e-4ee6-a7e7-ca8f7da7113a","version":{"major":1,"minor":1,"patch":0}}'
$ujamJsonBytes = [Text.Encoding]::UTF8.GetBytes($ujamJson)
$ujamIndex = New-Object byte[] (512)
$ujamIndex[0] = 0x67
$ujamIndex[1] = 0x45
$ujamIndex[2] = 0xB1
$ujamIndex[3] = 0x62
[Array]::Copy([BitConverter]::GetBytes([uint32]$ujamJsonBytes.Length), 0, $ujamIndex, 4, 4)
[Array]::Copy($ujamJsonBytes, 0, $ujamIndex, 8, $ujamJsonBytes.Length)
$ujamIndexPath = Join-Path $testDir 'renamed-ujam-index'
[IO.File]::WriteAllBytes($ujamIndexPath, $ujamIndex)
$ujamIndexText = Get-FileDentifyReport $ujamIndexPath 'ujam-index-report.txt'
Assert-Item $ujamIndexText 'Likely type' 'UJAM content index blob'
Assert-Item $ujamIndexText 'Instruments' 'FIN-NEO'
Assert-Item $ujamIndexText 'Format version' '1.1.0'
Assert-Item $ujamIndexText 'Source' 'LibFileDentify 0.2.0'

$ujamPayload = New-Object byte[] (1MB)
Write-AsciiAt $ujamPayload 0 '59eea25c-617e-4ee6-a7e7-ca8f7da7113a'
for ($i = 36; $i -lt 4096; $i++) { $ujamPayload[$i] = [byte]($i % 251) }
$ujamPayloadPath = Join-Path $testDir 'renamed-ujam-payload'
[IO.File]::WriteAllBytes($ujamPayloadPath, $ujamPayload)
$ujamPayloadText = Get-FileDentifyReport $ujamPayloadPath 'ujam-payload-report.txt'
Assert-Item $ujamPayloadText 'Likely type' 'UJAM-style content payload blob'
Assert-Item $ujamPayloadText 'Match confidence' 'Medium: structural and content clues support this match, but do not prove the exact origin'

$doubleTalk = New-Object byte[] 524288
Write-AsciiAt $doubleTalk 7288 '5.41.DoubleTalk (C) 1988-95 RC Systems'
$doubleTalkPath = Join-Path $testDir 'renamed-doubletalk-rom'
[IO.File]::WriteAllBytes($doubleTalkPath, $doubleTalk)
$doubleTalkText = Get-FileDentifyReport $doubleTalkPath 'doubletalk-report.txt'
Assert-Item $doubleTalkText 'Likely type' 'RC Systems DoubleTalk PC speech ROM image'
Assert-Item $doubleTalkText 'Firmware version' '5.41'
Assert-Item $doubleTalkText 'Detection basis' 'Embedded DoubleTalk copyright marker; filename and folder context are not required.'

$doubleTalkDictionaryPath = Join-Path $testDir 'renamed-doubletalk-dictionary'
[IO.File]::WriteAllText($doubleTalkDictionaryPath, "# RC Systems base pronunciation dictionary`r`n# Oracle-validated against DoubleTalk PC firmware; spelling<TAB>phonetics`r`nSHIVE`tSH'AYv`r`n", [Text.Encoding]::UTF8)
$doubleTalkDictionaryText = Get-FileDentifyReport $doubleTalkDictionaryPath 'doubletalk-dictionary-report.txt'
Assert-Item $doubleTalkDictionaryText 'Likely type' 'DoubleTalk pronunciation dictionary'
Assert-Item $doubleTalkDictionaryText 'Role' 'pronunciation dictionary'
Assert-Item $doubleTalkDictionaryText 'Pronunciation entries in sample' '1'

$bandPath = Join-Path $testDir '2a'
$bandStream = [IO.File]::Open($bandPath, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    $bandStream.SetLength(8MB)
    $bandStream.Position = 0
    $bandStream.WriteByte(1)
} finally {
    $bandStream.Dispose()
}
$bandText = Get-FileDentifyReport $bandPath 'sparse-band-report.txt'
Assert-Item $bandText 'Likely type' 'Possible Apple sparse-bundle band file'
Assert-Item $bandText 'Match confidence' 'Medium: structural and content clues support this match, but do not prove the exact origin'

Write-Host 'Detection sanity check passed.'
Remove-Item -LiteralPath $testDir -Recurse -Force -ErrorAction SilentlyContinue
