using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static string LogicProLibraryTypeName(string path, byte[] header)
        {
            if (!IsLogicProLibraryPath(path))
                return null;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".cst": return "Logic Pro channel strip setting";
                case ".aaz": return StartsWith(header, Encoding.ASCII.GetBytes("AAZ")) ? "Apple Alchemy sample payload" : "Apple Alchemy sample payload";
                case ".acp": return "Apple Alchemy preset";
                case ".pst": return "Logic Pro plug-in setting";
                case ".zxml": return "Logic Pro patch cache XML";
                case ".ubs": return "Logic Ultrabeat sample";
                case ".sdir_1": return "Apple Space Designer impulse response sidecar";
                default:
                    if (Path.GetFileName(path).Equals("LibraryUUID", StringComparison.OrdinalIgnoreCase))
                        return "Logic Pro library UUID";
                    if (string.IsNullOrEmpty(ext) && Path.GetFileName(path).Equals("DisplayStateArchive", StringComparison.OrdinalIgnoreCase))
                        return "Logic Pro display-state archive";
                    return null;
            }
        }

        private static void AddLogicProLibraryInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = LogicProLibraryTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Logic Pro library");
            Add(section, "Format hint", type);
            Add(section, "Library area", LogicProLibraryArea(path));
            Add(section, "Role", LogicProLibraryRole(path));
            Add(section, "File size", FormatBytes(fileLength));

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".aaz" && StartsWith(header, Encoding.ASCII.GetBytes("AAZ")))
            {
                Add(section, "Header marker", "AAZ");
                var sampleName = ReadAsciiZ(header, 4, Math.Min(80, Math.Max(0, header.Length - 4))).Trim();
                if (!string.IsNullOrWhiteSpace(sampleName))
                    Add(section, "Visible sample name", CleanMetadataText(sampleName));
            }
            else if (ext == ".acp")
            {
                AddAlchemyPresetFields(section, sample);
            }
            else if (ext == ".pst" || ext == ".cst" || ext == ".ubs" || ext == ".exs")
            {
                AddLogicVisibleResourceStrings(section, sample);
            }
            else if (ext == ".zxml")
            {
                AddLogicPatchCacheFields(section, sample);
            }
            else if (ext == ".sdir_1")
            {
                if (StartsWith(header, Encoding.ASCII.GetBytes("FORM")))
                    Add(section, "Container marker", "FORM");
                AddLogicVisibleResourceStrings(section, sample);
            }
            else if (Path.GetFileName(path).Equals("LibraryUUID", StringComparison.OrdinalIgnoreCase))
            {
                var uuid = DecodeTextSample(sample).Trim();
                if (Regex.IsMatch(uuid, "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", RegexOptions.IgnoreCase))
                    Add(section, "Library UUID", uuid);
            }

            Add(section, "Notes", "Logic Pro library files hold Apple loops, patches, plug-in settings, Alchemy data, sampler instruments, Ultrabeat samples, and Space Designer impulse responses. FileDentify reports library context and visible names only; it does not decode proprietary sound payloads or load Logic components.");
        }

        private static void AddLogicProLibraryDirectoryInfo(List<ReportSection> sections, string path)
        {
            if (!IsLogicProLibraryBundle(path))
                return;

            var section = AddSection(sections, "Logic Pro library");
            Add(section, "Format hint", "Logic Pro library package");
            Add(section, "Package name", Path.GetFileName(path));

            foreach (var folder in new[] { "Apple Loops", "Application Support", "Impulse Responses", "Patches", "Plug-In Settings", "Projects", "Samples" })
            {
                var sub = Path.Combine(path, folder);
                if (!Directory.Exists(sub))
                    continue;
                var stats = BoundedDirectoryStats(sub, 3000);
                Add(section, folder, stats.FileCount.ToString(CultureInfo.InvariantCulture) + (stats.Truncated ? " or more" : string.Empty) + " files, " + FormatBytes(stats.TotalBytes) + (stats.Truncated ? " or more" : string.Empty));
            }

            var extensions = SafeDirectoryFilesRecursive(path, 8000)
                .Select(file => Path.GetExtension(file).ToLowerInvariant())
                .Select(ext => string.IsNullOrWhiteSpace(ext) ? "(none)" : ext)
                .GroupBy(ext => ext, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .Select(group => group.Key + " " + group.Count().ToString(CultureInfo.InvariantCulture))
                .ToArray();
            if (extensions.Length > 0)
                Add(section, "Sampled extension mix", string.Join(Environment.NewLine, extensions));

            Add(section, "Notes", "Logic Pro Library.bundle is a user content package for Logic Pro. Windows shows it as a folder, but it contains Apple Loops, patches, plug-in settings, projects, samples, impulse responses, and support metadata. FileDentify reports bounded structure and extension mix without opening projects or decoding sound libraries.");
        }

        private static bool IsLogicProLibraryBundle(string path)
        {
            return string.Equals(Path.GetFileName(path), "Logic Pro Library.bundle", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLogicProLibraryPath(string path)
        {
            return path.IndexOf("Logic Pro Library.bundle", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string LogicProLibraryArea(string path)
        {
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            for (var i = 0; i < parts.Length; i++)
                if (parts[i].Equals("Logic Pro Library.bundle", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length)
                    return parts[i + 1];
            return ParentName(path);
        }

        private static string LogicProLibraryRole(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (path.IndexOf("\\Alchemy Samples\\", StringComparison.OrdinalIgnoreCase) >= 0 || ext == ".aaz")
                return "Alchemy sample payload";
            if (path.IndexOf("\\Plug-In Settings\\Alchemy\\", StringComparison.OrdinalIgnoreCase) >= 0 || ext == ".acp")
                return "Alchemy preset";
            if (ext == ".cst")
                return "channel strip setting inside a Logic patch";
            if (ext == ".pst")
                return "plug-in setting or Drum Kit Designer mapping";
            if (ext == ".zxml")
                return "intermediate patch cache XML";
            if (ext == ".ubs")
                return "Ultrabeat sample/instrument payload";
            if (ext == ".sdir_1")
                return "Space Designer impulse response sidecar";
            if (Path.GetFileName(path).Equals("LibraryUUID", StringComparison.OrdinalIgnoreCase))
                return "library identifier";
            if (Path.GetFileName(path).Equals("DisplayStateArchive", StringComparison.OrdinalIgnoreCase))
                return "project display-state archive";
            return "Logic Pro library support file";
        }

        private static void AddAlchemyPresetFields(ReportSection section, byte[] sample)
        {
            var text = DecodeTextSample(sample);
            AddSimpleAssignment(section, text, "Version", "Preset version");
            AddSimpleAssignment(section, text, "Name", "Preset name");
            AddSimpleAssignment(section, text, "Product", "Product");
            AddSimpleAssignment(section, text, "Author", "Author");
            AddSimpleAssignment(section, text, "PresetId", "Preset ID");
        }

        private static void AddLogicPatchCacheFields(ReportSection section, byte[] sample)
        {
            var text = DecodeTextSample(sample);
            AddXmlishAttribute(section, text, "Channel", "name", "Channel name");
            AddXmlishAttribute(section, text, "Channel", "type", "Channel type");
            AddXmlishAttribute(section, text, "Plugin", "name", "Plug-in name");
            AddXmlishAttribute(section, text, "Patch", "name", "Patch name");
            var channelCount = Regex.Matches(text, "<Channel\\b", RegexOptions.IgnoreCase).Count;
            if (channelCount > 0)
                Add(section, "Channel count in cache", channelCount.ToString(CultureInfo.InvariantCulture));
        }

        private static void AddLogicVisibleResourceStrings(ReportSection section, byte[] sample)
        {
            var visible = FindReadableTextLines(sample, 4, 120)
                .Where(IsUsefulLogicProLibraryString)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(24)
                .ToArray();
            if (visible.Length > 0)
                Add(section, "Visible resource strings", string.Join(Environment.NewLine, visible));
        }

        private static bool IsUsefulLogicProLibraryString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            var text = value.Trim();
            return text.IndexOf(".patch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf(".exs", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf(".aif", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf(".wav", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Alchemy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("SOBT", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("TBOS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Creator:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Logic", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddSimpleAssignment(ReportSection section, string text, string key, string title)
        {
            var match = Regex.Match(text ?? string.Empty, "^\\s*" + Regex.Escape(key) + "\\s*=\\s*(?<value>.*?)\\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (match.Success)
                Add(section, title, CleanMetadataText(match.Groups["value"].Value));
        }
    }
}
