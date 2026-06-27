$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root 'src\Filedentify.cs'
$output = '<local-path>'
$installedOutput = Join-Path $env:USERPROFILE 'encoders\FileDentify.exe'
$oldPackageOutput = '<local-path>'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$libmagicRoot = Join-Path $root 'third_party\libmagic\extracted'

if (-not (Test-Path -LiteralPath $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}

if (-not (Test-Path -LiteralPath $csc)) {
    throw 'Could not find the .NET Framework C# compiler.'
}

foreach ($oldPath in @($oldPackageOutput)) {
    if ($oldPath -and (Test-Path -LiteralPath $oldPath)) {
        Remove-Item -LiteralPath $oldPath -Force
        Write-Host "Removed old casing $oldPath"
    }
}

$resources = @(
    ('/resource:"{0}",FileDentify.Embedded.file.exe' -f (Join-Path $libmagicRoot 'file.exe')),
    ('/resource:"{0}",FileDentify.Embedded.libmagic-1.dll' -f (Join-Path $libmagicRoot 'libmagic-1.dll')),
    ('/resource:"{0}",FileDentify.Embedded.libgnurx-0.dll' -f (Join-Path $libmagicRoot 'libgnurx-0.dll')),
    ('/resource:"{0}",FileDentify.Embedded.magic.mgc' -f (Join-Path $libmagicRoot 'magic.mgc')),
    ('/resource:"{0}",FileDentify.Embedded.COPYING.file' -f (Join-Path $libmagicRoot 'COPYING.file')),
    ('/resource:"{0}",FileDentify.Embedded.COPYING.libgnurx' -f (Join-Path $libmagicRoot 'COPYING.libgnurx'))
)

foreach ($resource in $resources) {
    $path = $resource -replace '^/resource:"([^"]+)".*$', '$1'
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required embedded resource is missing: $path"
    }
}

& $csc /nologo /target:winexe /optimize+ /out:$output /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll $resources $source
if ($LASTEXITCODE -ne 0) {
    throw "C# compile failed with exit code $LASTEXITCODE."
}

Write-Host "Built $output"

if (Test-Path -LiteralPath (Split-Path -Parent $installedOutput)) {
    try {
        if (Test-Path -LiteralPath $installedOutput) {
            Remove-Item -LiteralPath $installedOutput -Force
        }
        Copy-Item -LiteralPath $output -Destination $installedOutput -Force
        Write-Host "Updated installed copy $installedOutput"
    }
    catch {
        Write-Warning "Built package output, but could not update installed copy $installedOutput. Close any running FileDentify window and run Build.ps1 again. $($_.Exception.Message)"
    }
}
