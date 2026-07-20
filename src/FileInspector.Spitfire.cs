using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static string SpitfireAudioTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".spitfire": return "Spitfire Audio sample container";
                case ".zmulti": return "Spitfire Audio multi/patch data";
                case ".zpreset": return "Spitfire Audio preset data";
                case ".zconfig": return "Spitfire Audio configuration data";
                case ".lm":
                    if (IsSpitfireAudioPath(path)) return "Spitfire Audio licence or library metadata";
                    break;
                case ".db":
                    if (IsSpitfireAudioPath(path)) return "Spitfire Audio SQLite catalogue";
                    break;
                case ".nksf":
                    if (IsSpitfireAudioPath(path)) return "Spitfire Audio NKS preset";
                    break;
            }
            if (StartsWith(header, Encoding.ASCII.GetBytes("Spitfire")))
                return "Spitfire Audio sample container or metadata";
            return null;
        }

        private static void AddSpitfireAudioInfo(List<ReportSection> sections, string path, byte[] header, byte[] stringSample, long fileLength)
        {
            var typeName = SpitfireAudioTypeName(path, header);
            if (typeName == null && !IsSpitfireAudioPath(path))
                return;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (typeName == null && ext != ".json" && ext != ".meta")
                return;

            var section = AddSection(sections, "Spitfire Audio");
            Add(section, "Format hint", typeName ?? "Spitfire Audio support or metadata file");

            var library = SpitfireLibraryFromPath(path);
            if (!string.IsNullOrWhiteSpace(library))
                Add(section, "Library", library);

            var role = SpitfireRoleFromPath(path, ext);
            if (!string.IsNullOrWhiteSpace(role))
                Add(section, "Role", role);

            var versionFolder = SpitfireVersionFolder(path);
            if (!string.IsNullOrWhiteSpace(versionFolder))
                Add(section, "Version folder", versionFolder);

            var cleanName = CleanSpitfireName(Path.GetFileNameWithoutExtension(path));
            if (!string.IsNullOrWhiteSpace(cleanName))
                Add(section, "File name as title", cleanName);

            if (StartsWith(header, Encoding.ASCII.GetBytes("Spitfire")))
                Add(section, "Header marker", "Spitfire");

            if (ext == ".db" && StartsWith(header, Encoding.ASCII.GetBytes("SQLite format 3\0")))
                Add(section, "Database", "SQLite catalogue/database used by Spitfire Audio libraries.");

            if (ext == ".nksf")
                Add(section, "NKS note", "Native Kontrol Standard preset associated with a Spitfire library.");

            var visibleNames = SpitfireVisibleNames(stringSample);
            if (visibleNames.Count > 0)
                Add(section, "Visible names", string.Join(Environment.NewLine, visibleNames.ToArray()));

            Add(section, "Notes", "Spitfire formats are mostly proprietary or compressed. FileDentify reports bounded headers, visible strings, folder role, inferred library name, and catalogue hints; it does not decode the sample payload.");
        }

        private static bool IsSpitfireAudioPath(string path)
        {
            return path.IndexOf("Spitfire Audio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("SpitfireAudio", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string SpitfireLibraryFromPath(string path)
        {
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            for (var i = 0; i < parts.Length; i++)
            {
                if (!parts[i].Equals("Spitfire Audio", StringComparison.OrdinalIgnoreCase) &&
                    !parts[i].Equals("SpitfireAudio", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (i + 1 < parts.Length)
                {
                    var first = parts[i + 1];
                    if (first.Equals("Spitfire Audio - LABS", StringComparison.OrdinalIgnoreCase) && i + 2 < parts.Length)
                        return first + " / " + parts[i + 2];
                    return first;
                }
            }
            return string.Empty;
        }

        private static string SpitfireRoleFromPath(string path, string ext)
        {
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var start = 0;
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Equals("Spitfire Audio", StringComparison.OrdinalIgnoreCase) ||
                    parts[i].Equals("SpitfireAudio", StringComparison.OrdinalIgnoreCase))
                {
                    start = Math.Min(parts.Length, i + 2);
                    if (i + 1 < parts.Length && parts[i + 1].Equals("Spitfire Audio - LABS", StringComparison.OrdinalIgnoreCase))
                        start = Math.Min(parts.Length, i + 3);
                    break;
                }
            }

            for (var i = start; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part.Equals("Samples", StringComparison.OrdinalIgnoreCase))
                    return ext == ".db" ? "Sample catalogue database" : "Sample payload or sample-side metadata";
                if (part.Equals("Presets", StringComparison.OrdinalIgnoreCase))
                    return "Preset";
                if (part.Equals("Patches", StringComparison.OrdinalIgnoreCase))
                    return "Patch";
                if (part.Equals("NKS", StringComparison.OrdinalIgnoreCase))
                    return "NKS browser preset";
                if (part.Equals("PAResources", StringComparison.OrdinalIgnoreCase))
                    return "Player/resource metadata";
                if (part.Equals("dist_database", StringComparison.OrdinalIgnoreCase))
                    return "Distribution database";
            }

            switch (ext)
            {
                case ".spitfire": return "Spitfire sample container";
                case ".zmulti": return "Multi or patch data";
                case ".zpreset": return "Preset";
                case ".zconfig": return "Configuration";
                case ".lm": return "Licence or library metadata";
                case ".db": return "Catalogue database";
                default: return string.Empty;
            }
        }

        private static string SpitfireVersionFolder(string path)
        {
            foreach (var part in path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                if (Regex.IsMatch(part, "^v\\d+(\\.\\d+)+$", RegexOptions.IgnoreCase))
                    return part;
            return string.Empty;
        }

        private static string CleanSpitfireName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;
            var cleaned = Regex.Replace(name, "[_\\-]+", " ").Trim();
            cleaned = Regex.Replace(cleaned, "\\s+", " ");
            return cleaned;
        }

        private static List<string> SpitfireVisibleNames(byte[] sample)
        {
            var names = new List<string>();
            if (sample == null || sample.Length == 0)
                return names;
            foreach (var item in FindAsciiStrings(sample, 5, 80))
            {
                var value = item.Value.Trim();
                if (value.Length == 0)
                    continue;
                if (value.IndexOf(".wav", StringComparison.OrdinalIgnoreCase) < 0 &&
                    value.IndexOf(".flac", StringComparison.OrdinalIgnoreCase) < 0 &&
                    value.IndexOf(".spitfire", StringComparison.OrdinalIgnoreCase) < 0 &&
                    value.IndexOf("Spitfire", StringComparison.OrdinalIgnoreCase) < 0 &&
                    value.IndexOf("LABS", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (!names.Contains(value))
                    names.Add(value);
                if (names.Count >= 12)
                    break;
            }
            return names;
        }
    }
}
