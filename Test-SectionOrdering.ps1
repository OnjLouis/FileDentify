$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePath = Join-Path $root 'src\ReportSectionOrdering.cs'
if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "ReportSectionOrdering.cs not found."
}

$source = Get-Content -LiteralPath $sourcePath -Raw
$stub = @'
namespace FileDentify
{
    internal sealed class FileReport
    {
        public System.Collections.Generic.List<ReportSection> Sections = new System.Collections.Generic.List<ReportSection>();
        public string FullText;
    }

    internal sealed class ReportSection
    {
        public string Title;
        public ReportSection(string title) { Title = title; }
    }

    internal static partial class FileInspector
    {
        public static string BuildReportText(FileReport report) { return string.Empty; }
    }

    public static class SectionOrderingSmokeHarness
    {
        public static int Main()
        {
            var report = new FileReport();
            report.Sections.Add(new ReportSection("Summary"));
            report.Sections.Add(new ReportSection("Filesystem"));
            report.Sections.Add(new ReportSection("Byte statistics"));
            report.Sections.Add(new ReportSection("Game/ROM data"));
            report.Sections.Add(new ReportSection("Hashes"));

            ReportSectionOrdering.Apply(
                new[] { report },
                new[] { "Byte statistics", "Hashes", "Game/ROM data", "Filesystem" });

            var actual = string.Join("|", report.Sections.ConvertAll(section => section.Title).ToArray());
            var expected = "Summary|Game/ROM data|Byte statistics|Hashes|Filesystem";
            if (!string.Equals(actual, expected, System.StringComparison.Ordinal))
            {
                System.Console.Error.WriteLine("Section ordering smoke failed. Expected '" + expected + "', found '" + actual + "'.");
                return 1;
            }

            System.Console.WriteLine("Section ordering smoke check passed.");
            return 0;
        }
    }
}
'@

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $csc)) {
    throw 'Could not find the .NET Framework C# compiler.'
}

$testDir = Join-Path $root 'obj\SectionOrderingSmoke'
New-Item -ItemType Directory -Force -Path $testDir | Out-Null
$testSource = Join-Path $testDir 'SectionOrderingSmoke.cs'
$testExe = Join-Path $testDir 'SectionOrderingSmoke.exe'
Set-Content -LiteralPath $testSource -Value ($source + "`r`n" + $stub) -Encoding UTF8

& $csc /nologo /target:exe /out:$testExe /reference:System.dll /reference:System.Core.dll $testSource
if ($LASTEXITCODE -ne 0) {
    throw "Section ordering smoke compile failed with exit code $LASTEXITCODE."
}

& $testExe
if ($LASTEXITCODE -ne 0) {
    throw "Section ordering smoke failed with exit code $LASTEXITCODE."
}
