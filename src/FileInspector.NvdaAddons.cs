using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static string NvdaAddonTypeName(string path, byte[] header)
        {
            if (LooksLikeInstalledNvdaAddonManifest(path, header))
                return "Installed NVDA add-on manifest";
            if (!IsZipHeader(header) || !string.Equals(Path.GetExtension(path), ".nvda-addon", StringComparison.OrdinalIgnoreCase))
                return null;
            return "NVDA add-on package";
        }

        private static void AddNvdaAddonInfo(List<ReportSection> sections, string path, byte[] header)
        {
            var type = NvdaAddonTypeName(path, header);
            if (type == null)
                return;

            if (LooksLikeInstalledNvdaAddonManifest(path, header))
            {
                var section = AddSection(sections, "NVDA add-on");
                Add(section, "Format hint", type);
                Add(section, "Container", "Installed add-on folder metadata");
                var text = DecodeTextSample(header, 64 * 1024);
                AddNvdaManifestValue(section, text, "name", "Name");
                AddNvdaManifestValue(section, text, "summary", "Summary");
                AddNvdaManifestValue(section, text, "description", "Description");
                AddNvdaManifestValue(section, text, "author", "Author");
                AddNvdaManifestValue(section, text, "version", "Version");
                AddNvdaManifestValue(section, text, "url", "URL");
                AddNvdaManifestValue(section, text, "minimumNVDAVersion", "Minimum NVDA version");
                AddNvdaManifestValue(section, text, "lastTestedNVDAVersion", "Last tested NVDA version");
                AddNvdaManifestValue(section, text, "docFileName", "Documentation file");
                AddNvdaManifestValue(section, text, "updateChannel", "Update channel");
                Add(section, "Add-on folder", Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty));
                Add(section, "Notes", "Installed NVDA add-on manifests describe an add-on already unpacked under NVDA's add-ons folder. FileDentify reads manifest.ini only; it does not import or run add-on code.");
                return;
            }

            try
            {
                using (var archive = ZipFile.OpenRead(path))
                {
                    var manifest = archive.GetEntry("manifest.ini");
                    var section = AddSection(sections, "NVDA add-on");
                    Add(section, "Container", "NVDA add-on ZIP package");
                    if (manifest == null)
                    {
                        Add(section, "Manifest", "manifest.ini not found");
                    }
                    else
                    {
                        var text = ReadZipEntryText(manifest, 64 * 1024);
                        AddNvdaManifestValue(section, text, "name", "Name");
                        AddNvdaManifestValue(section, text, "summary", "Summary");
                        AddNvdaManifestValue(section, text, "description", "Description");
                        AddNvdaManifestValue(section, text, "author", "Author");
                        AddNvdaManifestValue(section, text, "version", "Version");
                        AddNvdaManifestValue(section, text, "url", "URL");
                        AddNvdaManifestValue(section, text, "minimumNVDAVersion", "Minimum NVDA version");
                        AddNvdaManifestValue(section, text, "lastTestedNVDAVersion", "Last tested NVDA version");
                        AddNvdaManifestValue(section, text, "docFileName", "Documentation file");
                        AddNvdaManifestValue(section, text, "updateChannel", "Update channel");
                    }

                    Add(section, "Entries", archive.Entries.Count.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Python modules", archive.Entries.Count(e => HasZipExtension(e.FullName, ".py")).ToString(CultureInfo.InvariantCulture));
                    Add(section, "Documentation files", archive.Entries.Count(e => HasZipExtension(e.FullName, ".html") || HasZipExtension(e.FullName, ".htm") || HasZipExtension(e.FullName, ".md") || HasZipExtension(e.FullName, ".txt")).ToString(CultureInfo.InvariantCulture));
                    var roots = archive.Entries
                        .Select(e => FirstPathPart(e.FullName))
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(20)
                        .ToArray();
                    if (roots.Length > 0)
                        Add(section, "Top-level entries", string.Join("\r\n", roots));
                    Add(section, "Notes", "NVDA add-ons are installable ZIP packages. FileDentify reads manifest.ini and package structure only; it does not run the add-on.");
                }
            }
            catch (Exception ex)
            {
                Add(AddSection(sections, "NVDA add-on"), "Metadata read error", ex.Message);
            }
        }

        private static bool LooksLikeInstalledNvdaAddonManifest(string path, byte[] header)
        {
            if (!string.Equals(Path.GetFileName(path), "manifest.ini", StringComparison.OrdinalIgnoreCase))
                return false;
            if (path.IndexOf("\\nvda\\addons\\", StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            var text = DecodeTextSample(header, 8192);
            return text.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0 &&
                text.IndexOf("summary", StringComparison.OrdinalIgnoreCase) >= 0 &&
                text.IndexOf("lastTestedNVDAVersion", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddNvdaManifestValue(ReportSection section, string text, string key, string label)
        {
            var value = GetSimpleIniValue(text, key);
            if (string.IsNullOrWhiteSpace(value))
                return;

            value = CleanMetadataText(value.Trim().Trim('"'));
            if (key.Equals("lastTestedNVDAVersion", StringComparison.OrdinalIgnoreCase))
                value = FormatNvdaManifestVersion(value);
            Add(section, label, value);
        }

        private static string FormatNvdaManifestVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;
            var first = value.Split('.')[0];
            int year;
            if (int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out year) &&
                year > DateTime.Now.Year + 1)
                return value + " (manifest value; future/nonstandard, reported as written)";
            return value;
        }

        private static string GetSimpleIniValue(string text, string key)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            foreach (var rawLine in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                var line = rawLine.Trim();
                var separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;
                var lineKey = line.Substring(0, separator).Trim();
                if (lineKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                    return line.Substring(separator + 1).Trim();
            }
            return string.Empty;
        }

        private static bool HasZipExtension(string path, string extension)
        {
            return path != null && path.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
        }

        private static string FirstPathPart(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            var normalized = path.Replace('\\', '/').Trim('/');
            var index = normalized.IndexOf('/');
            return index >= 0 ? normalized.Substring(0, index) : normalized;
        }
    }
}
