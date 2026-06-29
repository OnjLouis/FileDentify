$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceRoot = Join-Path $root 'src'
$sources = Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -File | Sort-Object Name | ForEach-Object { $_.FullName }
$stubSource = Join-Path $root 'stub\FdConsoleStub.cs'
$packageDir = $env:FILEDENTIFY_PACKAGE_DIR
if ([string]::IsNullOrWhiteSpace($packageDir)) {
    $packageDir = Join-Path $root 'bin\Release'
}
$installDir = $env:FILEDENTIFY_INSTALL_DIR
if ([string]::IsNullOrWhiteSpace($installDir)) {
    $installDir = Join-Path $env:USERPROFILE 'encoders'
}
$output = Join-Path $packageDir 'FileDentify.exe'
$consoleOutput = Join-Path $packageDir 'fd.com'
$installedOutput = Join-Path $installDir 'FileDentify.exe'
$installedConsoleOutput = Join-Path $installDir 'fd.com'
$oldPackageOutput = Join-Path $packageDir 'Filedentify.exe'
$oldPackageConsoleOutput = Join-Path $packageDir 'FileDentify.com'
$oldInstalledConsoleOutput = Join-Path $installDir 'FileDentify.com'
$packageStartupError = Join-Path $packageDir 'FileDentify-startup-error.txt'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$libmagicRoot = Join-Path $root 'third_party\libmagic\extracted'
$tolkRoot = Join-Path $root 'third_party\tolk'

if (-not (Test-Path -LiteralPath $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}

if (-not (Test-Path -LiteralPath $csc)) {
    throw 'Could not find the .NET Framework C# compiler.'
}

New-Item -ItemType Directory -Force -Path $packageDir | Out-Null

foreach ($oldPath in @($oldPackageOutput, $oldPackageConsoleOutput, $oldInstalledConsoleOutput, $packageStartupError)) {
    if ($oldPath -and (Test-Path -LiteralPath $oldPath)) {
        Remove-Item -LiteralPath $oldPath -Force
        Write-Host "Removed old output $oldPath"
    }
}

$resources = @(
    ('/resource:"{0}",FileDentify.Embedded.file.exe' -f (Join-Path $libmagicRoot 'file.exe')),
    ('/resource:"{0}",FileDentify.Embedded.libmagic-1.dll' -f (Join-Path $libmagicRoot 'libmagic-1.dll')),
    ('/resource:"{0}",FileDentify.Embedded.libsystre-0.dll' -f (Join-Path $libmagicRoot 'libsystre-0.dll')),
    ('/resource:"{0}",FileDentify.Embedded.libtre-5.dll' -f (Join-Path $libmagicRoot 'libtre-5.dll')),
    ('/resource:"{0}",FileDentify.Embedded.libintl-8.dll' -f (Join-Path $libmagicRoot 'libintl-8.dll')),
    ('/resource:"{0}",FileDentify.Embedded.libiconv-2.dll' -f (Join-Path $libmagicRoot 'libiconv-2.dll')),
    ('/resource:"{0}",FileDentify.Embedded.magic.mgc' -f (Join-Path $libmagicRoot 'magic.mgc')),
    ('/resource:"{0}",FileDentify.Embedded.COPYING.file' -f (Join-Path $libmagicRoot 'COPYING.file')),
    ('/resource:"{0}",FileDentify.Embedded.COPYING.libsystre' -f (Join-Path $libmagicRoot 'COPYING.libsystre')),
    ('/resource:"{0}",FileDentify.Embedded.COPYING.libtre' -f (Join-Path $libmagicRoot 'COPYING.libtre')),
    ('/resource:"{0}",FileDentify.Embedded.COPYING.gettext-runtime' -f (Join-Path $libmagicRoot 'COPYING.gettext-runtime')),
    ('/resource:"{0}",FileDentify.Embedded.COPYING.libintl' -f (Join-Path $libmagicRoot 'COPYING.libintl')),
    ('/resource:"{0}",FileDentify.Embedded.COPYING.libiconv-gpl' -f (Join-Path $libmagicRoot 'COPYING.libiconv-gpl')),
    ('/resource:"{0}",FileDentify.Embedded.COPYING.libiconv-lgpl' -f (Join-Path $libmagicRoot 'COPYING.libiconv-lgpl')),
    ('/resource:"{0}",FileDentify.Embedded.Tolk.dll' -f (Join-Path $tolkRoot 'Tolk.dll')),
    ('/resource:"{0}",FileDentify.Embedded.nvdaControllerClient64.dll' -f (Join-Path $tolkRoot 'nvdaControllerClient64.dll')),
    ('/resource:"{0}",FileDentify.Embedded.Tolk.LICENSE.txt' -f (Join-Path $tolkRoot 'Tolk.LICENSE.txt'))
)

foreach ($resource in $resources) {
    $path = $resource -replace '^/resource:"([^"]+)".*$', '$1'
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required embedded resource is missing: $path"
    }
}

if (-not (Test-Path -LiteralPath $stubSource)) {
    throw "Console stub source is missing: $stubSource"
}

& (Join-Path $root 'Test-MenuSanity.ps1')

& $csc /nologo /target:winexe /optimize+ /out:$output /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Xml.dll $resources $sources
if ($LASTEXITCODE -ne 0) {
    throw "C# compile failed with exit code $LASTEXITCODE."
}

Write-Host "Built $output"

& $csc /nologo /target:exe /optimize+ /out:$consoleOutput /reference:System.dll $stubSource
if ($LASTEXITCODE -ne 0) {
    throw "Console C# compile failed with exit code $LASTEXITCODE."
}

Write-Host "Built $consoleOutput"

if (Test-Path -LiteralPath $installDir) {
    try {
        if (Test-Path -LiteralPath $installedOutput) {
            Remove-Item -LiteralPath $installedOutput -Force
        }
        Copy-Item -LiteralPath $output -Destination $installedOutput -Force
        if (Test-Path -LiteralPath $installedConsoleOutput) {
            Remove-Item -LiteralPath $installedConsoleOutput -Force
        }
        Copy-Item -LiteralPath $consoleOutput -Destination $installedConsoleOutput -Force
        Write-Host "Updated installed copy $installedOutput"
        Write-Host "Updated installed console copy $installedConsoleOutput"
    }
    catch {
        Write-Warning "Built package output, but could not update installed copy $installedOutput. Close any running FileDentify window and run Build.ps1 again. $($_.Exception.Message)"
    }
}
