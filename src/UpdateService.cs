using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FileDentify
{    internal static class UpdateService
    {
        public static GitHubReleaseInfo FetchLatestRelease(string projectUrl, string version)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            using (var client = CreateGitHubClient(version))
                return new JavaScriptSerializer().Deserialize<GitHubReleaseInfo>(client.DownloadString(ApiUrl(projectUrl) + "/releases/latest"));
        }

        public static List<GitHubReleaseInfo> FetchReleases(string projectUrl, string version)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            using (var client = CreateGitHubClient(version))
                return new JavaScriptSerializer().Deserialize<List<GitHubReleaseInfo>>(client.DownloadString(ApiUrl(projectUrl) + "/releases?per_page=100")) ?? new List<GitHubReleaseInfo>();
        }

        public static GitHubReleaseInfo LatestVersionedRelease(IEnumerable<GitHubReleaseInfo> releases)
        {
            return (releases ?? new List<GitHubReleaseInfo>())
                .Select(r => new { Release = r, Version = ReleaseVersion(r) })
                .Where(i => i.Version != null)
                .OrderByDescending(i => i.Version)
                .Select(i => i.Release)
                .FirstOrDefault();
        }

        public static GitHubReleaseAsset FindPortableZipAsset(GitHubReleaseInfo release)
        {
            if (release == null)
                return null;
            return (release.assets ?? new List<GitHubReleaseAsset>())
                .Where(a => a != null && !string.IsNullOrWhiteSpace(a.browser_download_url) && !string.IsNullOrWhiteSpace(a.name))
                .Where(a => a.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(a => a.name.IndexOf("portable", StringComparison.OrdinalIgnoreCase) >= 0)
                .ThenByDescending(a => a.name.IndexOf("FileDentify", StringComparison.OrdinalIgnoreCase) >= 0)
                .FirstOrDefault();
        }

        public static string BuildReleaseNotes(IEnumerable<GitHubReleaseInfo> releases, Version current, Version latest, string currentVersion)
        {
            var newer = (releases ?? new List<GitHubReleaseInfo>())
                .Select(r => new { Release = r, Version = ReleaseVersion(r) })
                .Where(i => i.Version != null && i.Version > current && i.Version <= latest)
                .OrderByDescending(i => i.Version)
                .ToList();
            var builder = new StringBuilder();
            builder.AppendLine("Your version: " + currentVersion);
            builder.AppendLine("New version: " + latest);
            builder.AppendLine();
            builder.AppendLine("Changes between " + currentVersion + " and " + latest);
            builder.AppendLine();
            if (newer.Count == 0)
            {
                builder.AppendLine("No release notes were provided for this update.");
                return builder.ToString();
            }
            foreach (var item in newer)
            {
                builder.AppendLine(item.Release.tag_name);
                builder.AppendLine(FormatReleaseNotesForDialog(item.Release.body, "No release notes were provided for this update."));
                builder.AppendLine();
            }
            return builder.ToString();
        }

        public static string FormatReleaseNotesForDialog(string text, string emptyText)
        {
            if (string.IsNullOrWhiteSpace(text))
                return emptyText;
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine).Trim();
        }

        public static string NormalizeUpdateCheckFrequency(string value)
        {
            if (string.Equals(value, "Hourly", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "Hour", StringComparison.OrdinalIgnoreCase)) return "Hourly";
            if (string.Equals(value, "6Hours", StringComparison.OrdinalIgnoreCase)) return "6Hours";
            if (string.Equals(value, "12Hours", StringComparison.OrdinalIgnoreCase)) return "12Hours";
            if (string.Equals(value, "Daily", StringComparison.OrdinalIgnoreCase)) return "Daily";
            if (string.Equals(value, "Weekly", StringComparison.OrdinalIgnoreCase)) return "Weekly";
            if (string.Equals(value, "Never", StringComparison.OrdinalIgnoreCase)) return "Never";
            return "Startup";
        }

        public static TimeSpan? AutomaticUpdateInterval(string frequency)
        {
            switch (NormalizeUpdateCheckFrequency(frequency))
            {
                case "Hourly": return TimeSpan.FromHours(1);
                case "6Hours": return TimeSpan.FromHours(6);
                case "12Hours": return TimeSpan.FromHours(12);
                case "Daily": return TimeSpan.FromDays(1);
                case "Weekly": return TimeSpan.FromDays(7);
                default: return null;
            }
        }

        public static string GetUpdaterTempDirectory(string appDir)
        {
            var candidates = new List<string>();
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
                candidates.Add(Path.Combine(localAppData, "Temp"));
            candidates.Add(Path.GetTempPath());
            candidates.Add(Path.Combine(appDir, "Update Temp"));
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;
                try
                {
                    var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(candidate));
                    Directory.CreateDirectory(fullPath);
                    return fullPath;
                }
                catch { }
            }
            throw new InvalidOperationException("Could not create a temporary folder for the updater.");
        }

        public static string BuildUpdaterScript(string zipUrl, string targetDir, string exePath, string tempDir, int processId, string version)
        {
            return
                "$ErrorActionPreference = 'Stop'\r\n" +
                "Add-Type -AssemblyName System.Windows.Forms\r\n" +
                "$zipUrl = " + PowerShellQuote(zipUrl) + "\r\n" +
                "$userAgent = " + PowerShellQuote("FileDentify " + version) + "\r\n" +
                "$target = " + PowerShellQuote(targetDir) + "\r\n" +
                "$exe = " + PowerShellQuote(exePath) + "\r\n" +
                "$tempBase = " + PowerShellQuote(tempDir) + "\r\n" +
                "$pidToWait = " + processId.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                "try {\r\n" +
                "  [System.IO.Directory]::CreateDirectory($tempBase) | Out-Null\r\n" +
                "  $root = Join-Path $tempBase ('FileDentifyUpdate_' + [guid]::NewGuid().ToString('N'))\r\n" +
                "  $zip = Join-Path $root 'update.zip'\r\n" +
                "  $stage = Join-Path $root 'stage'\r\n" +
                "  [System.IO.Directory]::CreateDirectory($root) | Out-Null\r\n" +
                "  [System.IO.Directory]::CreateDirectory($stage) | Out-Null\r\n" +
                "  Invoke-WebRequest -Uri $zipUrl -OutFile $zip -UseBasicParsing -UserAgent $userAgent\r\n" +
                "  Expand-Archive -LiteralPath $zip -DestinationPath $stage -Force\r\n" +
                "  $source = $stage\r\n" +
                "  if (-not (Test-Path -LiteralPath (Join-Path $source 'FileDentify.exe'))) {\r\n" +
                "    $candidate = Get-ChildItem -LiteralPath $stage -Recurse -Filter 'FileDentify.exe' -File | Select-Object -First 1\r\n" +
                "    if ($candidate) { $source = $candidate.DirectoryName }\r\n" +
                "  }\r\n" +
                "  if (-not (Test-Path -LiteralPath (Join-Path $source 'FileDentify.exe'))) { throw 'The downloaded ZIP does not contain FileDentify.exe.' }\r\n" +
                "  if (-not (Test-Path -LiteralPath (Join-Path $source 'fd.com'))) { throw 'The downloaded ZIP does not contain fd.com beside FileDentify.exe.' }\r\n" +
                "  Get-Process -Id $pidToWait -ErrorAction SilentlyContinue | Wait-Process\r\n" +
                "  Remove-Item -LiteralPath (Join-Path $target 'README.md') -Force -ErrorAction SilentlyContinue\r\n" +
                "  Get-ChildItem -LiteralPath $source -Force | ForEach-Object {\r\n" +
                "    if ($_.name -ieq 'FileDentify.ini' -or $_.name -ieq 'Update Temp') { return }\r\n" +
                "    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $target $_.name) -Recurse -Force\r\n" +
                "  }\r\n" +
                "  Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue\r\n" +
                "  Start-Process -FilePath $exe\r\n" +
                "} catch {\r\n" +
                "  [System.Windows.Forms.MessageBox]::Show('FileDentify update failed:' + [Environment]::NewLine + [Environment]::NewLine + $_.Exception.Message, 'FileDentify updater', 'OK', 'Error') | Out-Null\r\n" +
                "}\r\n" +
                "Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue\r\n";
        }

        private static WebClient CreateGitHubClient(string version)
        {
            var client = new WebClient();
            client.Headers.Add("User-Agent", "FileDentify " + version);
            return client;
        }

        private static string ApiUrl(string projectUrl)
        {
            return projectUrl.Replace("https://github.com/", "https://api.github.com/repos/");
        }

        private static Version ReleaseVersion(GitHubReleaseInfo release)
        {
            if (release == null || string.IsNullOrWhiteSpace(release.tag_name))
                return null;
            Version version;
            return Version.TryParse(release.tag_name.Trim().TrimStart('v', 'V'), out version) ? version : null;
        }

        private static string PowerShellQuote(string value)
        {
            return "'" + (value ?? string.Empty).Replace("'", "''") + "'";
        }
    }
}

