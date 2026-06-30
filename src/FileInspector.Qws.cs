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
        private static string QwsTypeName(string path, byte[] header)
        {
            if (!LooksLikeQwsFile(path, header))
                return null;

            var name = Path.GetFileName(path) ?? string.Empty;
            if (name.Equals("qws.ini", StringComparison.OrdinalIgnoreCase))
                return "QWS sequencer settings";
            if (name.StartsWith("inst_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("INST_", StringComparison.Ordinal))
                return "QWS instrument definition";
            if (name.Equals("notexfrm.ini", StringComparison.OrdinalIgnoreCase) || name.Equals("userxfrm.ini", StringComparison.OrdinalIgnoreCase))
                return "QWS note transform table";
            if (name.Equals("qws.lng", StringComparison.OrdinalIgnoreCase))
                return "QWS language prompts";
            return "QWS sequencer support file";
        }

        private static void AddQwsInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (!LooksLikeQwsFile(path, header))
                return;

            var text = DecodeQwsText(header);
            var section = AddSection(sections, "QWS sequencer");
            Add(section, "File role", QwsTypeName(path, header));
            Add(section, "Application", "Quick Windows Sequencer (QWS)");

            var name = Path.GetFileName(path) ?? string.Empty;
            if (name.Equals("qws.ini", StringComparison.OrdinalIgnoreCase))
                AddQwsSettingsInfo(section, text);
            else if (name.StartsWith("inst_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("INST_", StringComparison.Ordinal))
                AddQwsInstrumentInfo(section, text);
            else if (name.Equals("notexfrm.ini", StringComparison.OrdinalIgnoreCase) || name.Equals("userxfrm.ini", StringComparison.OrdinalIgnoreCase))
                AddQwsTransformInfo(section, text);
            else if (name.Equals("qws.lng", StringComparison.OrdinalIgnoreCase))
                AddQwsLanguageInfo(section, text);

            Add(section, "Notes", "QWS normally saves songs as standard MIDI files. FileDentify reports QWS-specific support/config files separately when their names and contents make that clear.");
        }

        private static byte[] RedactSensitiveQwsSample(string path, byte[] data)
        {
            if (data == null || data.Length == 0)
                return data;

            var name = Path.GetFileName(path) ?? string.Empty;
            if (!name.Equals("qws.ini", StringComparison.OrdinalIgnoreCase))
                return data;

            var text = DecodeQwsText(data);
            if (string.IsNullOrWhiteSpace(text))
                return data;

            text = Regex.Replace(text, @"^(file\d+\s*=\s*).*$", "$1(redacted by FileDentify)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            text = Regex.Replace(text, @"^((?:songdir|libdir)\s*=\s*).*$", "$1(redacted by FileDentify)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            return Encoding.UTF8.GetBytes(text);
        }

        private static bool LooksLikeQwsFile(string path, byte[] header)
        {
            var fileName = Path.GetFileName(path) ?? string.Empty;
            var ext = Path.GetExtension(path);
            if (fileName.Equals("qws.lng", StringComparison.OrdinalIgnoreCase))
                return DecodeQwsText(header).IndexOf("Translation prompts for QWS", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!string.Equals(ext, ".ini", StringComparison.OrdinalIgnoreCase))
                return false;

            var lowerName = fileName.ToLowerInvariant();
            if (lowerName == "qws.ini" || lowerName == "notexfrm.ini" || lowerName == "userxfrm.ini" || lowerName.StartsWith("inst_") || lowerName.StartsWith("inst "))
            {
                var text = DecodeQwsText(header);
                return text.IndexOf("QWS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("[instruments]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("[notexfrm]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("[userxfrm]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("[OPTIONS]", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return false;
        }

        private static void AddQwsSettingsInfo(ReportSection section, string text)
        {
            Add(section, "Settings section", HasIniSection(text, "OPTIONS") ? "Present" : "Not found in sample");
            Add(section, "Configured output port", ValueOrNotReported(GetIniValue(text, "defport")));
            Add(section, "Metronome port", ValueOrNotReported(GetIniValue(text, "metfstport")));
            Add(section, "MIDI thru", ValueOrNotReported(GetIniValue(text, "midithru")));
            Add(section, "MIDI clock", ValueOrNotReported(GetIniValue(text, "midiclock")));
            Add(section, "Recent file entries", CountMatchingLines(text, @"^file\d+=").ToString(CultureInfo.InvariantCulture));
            Add(section, "Privacy note", "Recent file paths and song/library folders are counted but not printed.");
        }

        private static void AddQwsInstrumentInfo(ReportSection section, string text)
        {
            Add(section, "Instrument map name", ValueOrNotReported(GetIniValue(text, "name")));
            var patchLines = Regex.Matches(text ?? string.Empty, @"^\s*-?\d+\s*,\s*\d+\s*,\s*-?\d+\s*,\s*(?<name>.+?)\s*$", RegexOptions.Multiline)
                .Cast<Match>()
                .Select(match => CleanMetadataText(match.Groups["name"].Value))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(12)
                .ToArray();
            Add(section, "Patch entries", CountMatchingLines(text, @"^\s*-?\d+\s*,\s*\d+\s*,\s*-?\d+\s*,").ToString(CultureInfo.InvariantCulture));
            if (patchLines.Length > 0)
                Add(section, "Example patches", string.Join("\n", patchLines));
        }

        private static void AddQwsTransformInfo(ReportSection section, string text)
        {
            var role = HasIniSection(text, "userxfrm") ? "User note transforms" : "Built-in note transforms";
            Add(section, "Transform role", role);
            Add(section, "Transform entries", CountMatchingLines(text, @"^xfrm\d+=").ToString(CultureInfo.InvariantCulture));
            Add(section, "Expected row width", "QWS note transforms normally map 128 MIDI note values per row.");
        }

        private static void AddQwsLanguageInfo(ReportSection section, string text)
        {
            Add(section, "Language file", text.IndexOf("English", StringComparison.OrdinalIgnoreCase) >= 0 ? "English prompts" : "QWS prompts");
            Add(section, "Prompt entries", CountMatchingLines(text, @"^[A-Za-z0-9_.-]+=").ToString(CultureInfo.InvariantCulture));
            var prompts = Regex.Matches(text ?? string.Empty, @"^(?<key>[A-Za-z0-9_.-]+)=(?<value>.+)$", RegexOptions.Multiline)
                .Cast<Match>()
                .Select(match => match.Groups["key"].Value + " = " + CleanMetadataText(match.Groups["value"].Value))
                .Take(12)
                .ToArray();
            if (prompts.Length > 0)
                Add(section, "Example prompts", string.Join("\n", prompts));
        }

        private static string DecodeQwsText(byte[] data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;
            var count = Math.Min(data.Length, 1024 * 1024);
            var sample = data.Take(count).ToArray();
            return LooksLikeText(sample) ? Encoding.UTF8.GetString(sample) : Encoding.GetEncoding(1252).GetString(sample);
        }

        private static bool HasIniSection(string text, string section)
        {
            return Regex.IsMatch(text ?? string.Empty, @"^\s*\[" + Regex.Escape(section) + @"\]\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        private static string GetIniValue(string text, string key)
        {
            var match = Regex.Match(text ?? string.Empty, @"^\s*" + Regex.Escape(key) + @"\s*=\s*(?<value>.*?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            return match.Success ? CleanMetadataText(match.Groups["value"].Value) : string.Empty;
        }

        private static int CountMatchingLines(string text, string pattern)
        {
            return Regex.Matches(text ?? string.Empty, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline).Count;
        }

        private static string ValueOrNotReported(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(not reported)" : value;
        }
    }
}
