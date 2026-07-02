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
        private static string MercuryMailTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var text = DecodePersonalText(header);
            var inMercuryPath = PathHasSegment(path, "mercury") || PathHasSegment(path, "mercury32");
            var inPegasusPath = PathHasSegment(path, "pegasus") || PathHasSegment(path, "pmail");

            if (ext == ".mer" && (inMercuryPath || text.IndexOf("Mercury", StringComparison.OrdinalIgnoreCase) >= 0))
                return "Mercury/32 rule or template file";
            if (ext == ".mlf" && (inMercuryPath || text.IndexOf("Mercury List Membership File", StringComparison.OrdinalIgnoreCase) >= 0))
                return "Mercury mailing-list membership file";
            if (ext == ".cac" && (inMercuryPath || text.IndexOf("Mercury/32 new mail cache file", StringComparison.OrdinalIgnoreCase) >= 0))
                return "Mercury/32 new-mail cache file";
            if (ext == ".pnc" && inMercuryPath && text.IndexOf("SetName", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Mercury content-control set";
            if (ext == ".smp" && inMercuryPath)
                return "Mercury sample configuration file";
            if (ext == ".fff" && (text.IndexOf("Form Fact File", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Pegasus Mail", StringComparison.OrdinalIgnoreCase) >= 0))
                return "Pegasus Mail form fact file";
            if ((ext == ".pmi" || ext == ".pmj" || ext == ".pmm" || ext == ".pnm") && (inMercuryPath || inPegasusPath || PathHasSegment(path, "mail")))
                return "Pegasus/Mercury mailbox support file";

            return null;
        }

        private static void AddMercuryMailInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var type = MercuryMailTypeName(path, header);
            if (type == null)
                return;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            var text = DecodePersonalText(header);
            var section = AddSection(sections, "Mercury/Pegasus mail");
            Add(section, "Format hint", type);
            Add(section, "File role", MercuryMailRole(path, header, type));
            Add(section, "Detection basis", MercuryMailDetectionBasis(path, text));
            Add(section, "File size", FormatBytes(fileLength));

            if (ext == ".mer")
                AddMercuryRuleDetails(section, text);
            else if (ext == ".mlf")
                AddMercuryListDetails(section, path, text);
            else if (ext == ".cac")
                AddMercuryCacheDetails(section, header);
            else if (ext == ".pnc")
                AddMercuryContentControlDetails(section, text);
            else if (ext == ".fff")
                AddPegasusFormFactDetails(section, text);
            else if (ext == ".smp")
                AddMercurySampleConfigDetails(section, text);
            else if (ext == ".pmi" || ext == ".pmj" || ext == ".pmm" || ext == ".pnm")
                AddPegasusMailboxSupportDetails(section, path, header, text);

            Add(section, "Notes", "Mercury/32 and Pegasus Mail files can contain private addresses, folder names, subjects, and message metadata. FileDentify reports bounded structural clues here and does not expose mailing-list members or message bodies in this section.");
        }

        private static string MercuryMailRole(string path, byte[] header, string type)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".mer": return "Rule, template, policy, greeting, or server-script text used by Mercury/32.";
                case ".mlf": return "Mailing-list membership/control text used by Mercury.";
                case ".cac": return "New-mail cache sidecar used by Mercury/32 mailboxes.";
                case ".pnc": return "Mercury content-control definition.";
                case ".fff": return "Pegasus Mail extension form metadata.";
                case ".smp": return "Sample Mercury configuration text.";
                case ".pmi": return "Pegasus/Mercury mailbox index metadata.";
                case ".pmj": return "Pegasus/Mercury mailbox state text.";
                case ".pmm": return "Pegasus/Mercury mailbox folder map or summary sidecar.";
                case ".pnm": return "Pegasus/Mercury mailbox folder-name sidecar.";
                default: return type;
            }
        }

        private static string MercuryMailDetectionBasis(string path, string text)
        {
            var parts = new List<string>();
            var ext = Path.GetExtension(path);
            if (!string.IsNullOrWhiteSpace(ext))
                parts.Add("extension " + ext);
            if (PathHasSegment(path, "mercury") || PathHasSegment(path, "mercury32"))
                parts.Add("Mercury path context");
            if (PathHasSegment(path, "pegasus") || PathHasSegment(path, "pmail"))
                parts.Add("Pegasus path context");
            if (text.IndexOf("Mercury", StringComparison.OrdinalIgnoreCase) >= 0)
                parts.Add("visible Mercury marker");
            if (text.IndexOf("Pegasus Mail", StringComparison.OrdinalIgnoreCase) >= 0)
                parts.Add("visible Pegasus Mail marker");
            return string.Join(", ", parts.ToArray());
        }

        private static void AddMercuryRuleDetails(ReportSection section, string text)
        {
            var lines = MeaningfulLines(text).ToArray();
            Add(section, "Text lines in sample", lines.Length.ToString(CultureInfo.InvariantCulture));
            Add(section, "Rule-like lines", lines.Count(line => Regex.IsMatch(line, @"^\s*(If|And|Or|Else|Delete|Forward|Move|Copy|Header|Relay|Reject)\b", RegexOptions.IgnoreCase)).ToString(CultureInfo.InvariantCulture));
            Add(section, "Header tests", Regex.Matches(text, @"\bheader\s+""", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Delete actions", CountWord(text, "Delete").ToString(CultureInfo.InvariantCulture));
            Add(section, "Forward actions", CountWord(text, "Forward").ToString(CultureInfo.InvariantCulture));
        }

        private static void AddMercuryListDetails(ReportSection section, string path, string text)
        {
            var lines = MeaningfulLines(text).ToArray();
            Add(section, "List file", ValueOrNotReported(Path.GetFileName(path)));
            Add(section, "Comment/control lines", lines.Count(line => line.StartsWith("#", StringComparison.Ordinal)).ToString(CultureInfo.InvariantCulture));
            Add(section, "Non-comment entries", lines.Count(line => !line.StartsWith("#", StringComparison.Ordinal)).ToString(CultureInfo.InvariantCulture));
            Add(section, "Contains address-like text", Regex.IsMatch(text, @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase) ? "yes" : "no");
        }

        private static void AddMercuryCacheDetails(ReportSection section, byte[] header)
        {
            Add(section, "Header marker", ReadAsciiUntilControl(header, 0, 40));
            if (header.Length >= 80)
            {
                Add(section, "Likely message count field", ReadUInt16LittleEndian(header, 0x40).ToString(CultureInfo.InvariantCulture));
                Add(section, "Likely cache size field", ReadUInt32LittleEndian(header, 0x48).ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void AddMercuryContentControlDetails(ReportSection section, string text)
        {
            Add(section, "Set name", ValueOrNotReported(FindSimpleAssignment(text, "SetName")));
            Add(section, "Enabled", ValueOrNotReported(FindSimpleAssignment(text, "Enabled")));
            Add(section, "Blacklist", ValueOrNotReported(Path.GetFileName(FindSimpleAssignment(text, "Blacklist"))));
            Add(section, "Whitelist", ValueOrNotReported(Path.GetFileName(FindSimpleAssignment(text, "Whitelist"))));
            Add(section, "Rule lines", MeaningfulLines(text).Count().ToString(CultureInfo.InvariantCulture));
        }

        private static void AddPegasusFormFactDetails(ReportSection section, string text)
        {
            Add(section, "Form fact marker", text.IndexOf("Form Fact File", StringComparison.OrdinalIgnoreCase) >= 0 ? "present" : "not found in sample");
            Add(section, "Pegasus marker", text.IndexOf("Pegasus Mail", StringComparison.OrdinalIgnoreCase) >= 0 ? "present" : "not found in sample");
            Add(section, "Definition lines", MeaningfulLines(text).Count(line => !line.StartsWith(";", StringComparison.Ordinal)).ToString(CultureInfo.InvariantCulture));
            Add(section, "Comment lines", MeaningfulLines(text).Count(line => line.StartsWith(";", StringComparison.Ordinal)).ToString(CultureInfo.InvariantCulture));
        }

        private static void AddMercurySampleConfigDetails(ReportSection section, string text)
        {
            Add(section, "Sample marker", "Mercury sample configuration");
            Add(section, "Assignment lines", MeaningfulLines(text).Count(line => line.IndexOf('=') >= 0).ToString(CultureInfo.InvariantCulture));
            Add(section, "Comment lines", MeaningfulLines(text).Count(line => line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal)).ToString(CultureInfo.InvariantCulture));
        }

        private static void AddPegasusMailboxSupportDetails(ReportSection section, string path, byte[] header, string text)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".pmj")
            {
                Add(section, "Mailbox ID section", text.IndexOf("[Mailbox_IDs]", StringComparison.OrdinalIgnoreCase) >= 0 ? "present" : "not found in sample");
                Add(section, "State lines", MeaningfulLines(text).Count().ToString(CultureInfo.InvariantCulture));
            }
            else if (ext == ".pmm" || ext == ".pnm")
            {
                var label = FirstReadableAsciiString(header, 3, 64);
                Add(section, "Visible folder label", string.IsNullOrWhiteSpace(label) ? "(not reported)" : CleanMetadataText(label));
            }
            else if (ext == ".pmi")
            {
                Add(section, "Binary index", "Mailbox index metadata; visible subject/address strings are intentionally not summarized here.");
                Add(section, "Header bytes available", header.Length.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static IEnumerable<string> MeaningfulLines(string text)
        {
            return (text ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => CleanMetadataText(line.Trim()))
                .Where(line => !string.IsNullOrWhiteSpace(line));
        }

        private static string FindSimpleAssignment(string text, string key)
        {
            var match = Regex.Match(text ?? string.Empty, @"(?im)^\s*" + Regex.Escape(key) + @"\s*=\s*(?<value>[^\r\n]+)");
            return match.Success ? CleanMetadataText(match.Groups["value"].Value.Trim()) : string.Empty;
        }

        private static int CountWord(string text, string word)
        {
            return Regex.Matches(text ?? string.Empty, @"\b" + Regex.Escape(word) + @"\b", RegexOptions.IgnoreCase).Count;
        }

        private static bool PathHasSegment(string path, string segment)
        {
            return (path ?? string.Empty)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => string.Equals(part, segment, StringComparison.OrdinalIgnoreCase));
        }

        private static string ReadAsciiUntilControl(byte[] data, int offset, int maxLength)
        {
            if (data == null || offset >= data.Length)
                return string.Empty;
            var chars = new List<char>();
            for (var i = offset; i < data.Length && chars.Count < maxLength; i++)
            {
                var b = data[i];
                if (b < 32 || b >= 127)
                    break;
                chars.Add((char)b);
            }
            return new string(chars.ToArray()).Trim();
        }

        private static string FirstReadableAsciiString(byte[] data, int minLength, int maxLength)
        {
            var strings = FindAsciiStrings(data, minLength, 8)
                .Select(s => s.Value)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Length > maxLength ? s.Substring(0, maxLength) : s)
                .ToArray();
            return strings.FirstOrDefault() ?? string.Empty;
        }
    }
}
