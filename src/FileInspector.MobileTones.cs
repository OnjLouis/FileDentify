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
{
    internal static partial class FileInspector
    {
        private static string MobilePhoneToneTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (StartsWith(header, Encoding.ASCII.GetBytes("BEGIN:IMELODY")) || ext == ".imy")
                return "iMelody mobile ringtone";
            if (StartsWith(header, Encoding.ASCII.GetBytes("MMMD")) || ext == ".mmf")
                return "Yamaha SMAF/MMF mobile audio or ringtone";
            if (StartsWith(header, Encoding.ASCII.GetBytes("cmid")) || ext == ".pmd")
                return "Qualcomm CMX/PMD mobile audio";
            if (StartsWith(header, Encoding.ASCII.GetBytes("#!AMR")) || ext == ".amr")
                return "Adaptive Multi-Rate mobile speech/audio";
            if (ext == ".ota")
                return "Nokia OTA ringtone or operator-logo data";
            if (ext == ".rtttl")
                return "RTTTL ringtone text";
            if (ext == ".rtx")
                return "Nokia RTX ringtone text";
            return null;
        }

        private static void AddMobilePhoneToneInfo(List<ReportSection> sections, string path, byte[] header)
        {
            var type = MobilePhoneToneTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Mobile phone tone");
            Add(section, "Format hint", type);
            Add(section, "Detection basis", MobileToneDetectionBasis(path, header));

            if (StartsWith(header, Encoding.ASCII.GetBytes("BEGIN:IMELODY")) || Path.GetExtension(path).Equals(".imy", StringComparison.OrdinalIgnoreCase))
                AddIMelodyInfo(section, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("MMMD")) || Path.GetExtension(path).Equals(".mmf", StringComparison.OrdinalIgnoreCase))
                AddSmafInfo(section, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("cmid")) || Path.GetExtension(path).Equals(".pmd", StringComparison.OrdinalIgnoreCase))
                AddPmdInfo(section, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("#!AMR")))
                Add(section, "Header marker", "#!AMR");

            Add(section, "Notes", "Old phone-tone formats are often small and proprietary. FileDentify reports header fields and readable metadata where the sampled bytes expose them; it does not synthesize or play the ringtone.");
        }

        private static string MobileToneDetectionBasis(string path, byte[] header)
        {
            var ext = Path.GetExtension(path);
            var basis = new List<string>();
            if (!string.IsNullOrEmpty(ext))
                basis.Add("extension " + ext);
            if (StartsWith(header, Encoding.ASCII.GetBytes("BEGIN:IMELODY")))
                basis.Add("BEGIN:IMELODY marker");
            if (StartsWith(header, Encoding.ASCII.GetBytes("MMMD")))
                basis.Add("MMMD marker");
            if (StartsWith(header, Encoding.ASCII.GetBytes("cmid")))
                basis.Add("cmid marker");
            if (StartsWith(header, Encoding.ASCII.GetBytes("#!AMR")))
                basis.Add("#!AMR marker");
            return basis.Count == 0 ? "Extension-level mobile tone hint." : string.Join(", ", basis.ToArray());
        }

        private static void AddIMelodyInfo(ReportSection section, byte[] header)
        {
            var text = Encoding.ASCII.GetString(header.Take(Math.Min(header.Length, 8192)).ToArray());
            var values = ParseColonLines(text);
            AddValue(section, values, "VERSION", "Version");
            AddValue(section, values, "FORMAT", "Format");
            AddValue(section, values, "NAME", "Name");
            AddValue(section, values, "COMPOSER", "Composer");
            AddValue(section, values, "BEAT", "Beat");
            AddValue(section, values, "STYLE", "Style");
            AddValue(section, values, "VOLUME", "Volume");
            string melody;
            if (values.TryGetValue("MELODY", out melody))
            {
                Add(section, "Melody preview", melody.Length > 160 ? melody.Substring(0, 160) + "..." : melody);
                var noteCount = Regex.Matches(melody, @"[a-gprA-GPR][#&]?\d*").Count;
                Add(section, "Approximate melody tokens", noteCount.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void AddSmafInfo(ReportSection section, byte[] header)
        {
            if (header.Length >= 8)
                Add(section, "Declared MMMD payload size", FormatBytes(ReadUInt32BigEndian(header, 4)));

            var chunks = new List<string>();
            var offset = 8;
            while (offset + 8 <= header.Length && chunks.Count < 16)
            {
                var id = Encoding.ASCII.GetString(header, offset, 4);
                if (!IsFourCc(id))
                    break;
                var size = ReadUInt32BigEndian(header, offset + 4);
                chunks.Add(id + " (" + FormatBytes(size) + ")");
                if (id == "CNTI")
                    AddSmafContentInfo(section, header, offset + 8, (int)Math.Min(size, (uint)Math.Max(0, header.Length - offset - 8)));
                var next = offset + 8L + size;
                if (next <= offset || next > header.Length)
                    break;
                offset = (int)next;
            }
            if (chunks.Count > 0)
                Add(section, "Top-level chunks", string.Join("\r\n", chunks.ToArray()));
        }

        private static void AddSmafContentInfo(ReportSection section, byte[] data, int offset, int length)
        {
            var text = ExtractCleanAscii(data, offset, length);
            if (string.IsNullOrWhiteSpace(text))
                return;
            Add(section, "Content information", text);
            foreach (var part in text.Split(','))
            {
                var split = part.IndexOf(':');
                if (split <= 0)
                    continue;
                var key = part.Substring(0, split).Trim();
                var value = part.Substring(split + 1).Trim();
                if (key.Equals("ST", StringComparison.OrdinalIgnoreCase))
                    Add(section, "Title", value);
                else if (key.Equals("VN", StringComparison.OrdinalIgnoreCase))
                    Add(section, "Vendor/tool", value);
            }
        }

        private static void AddPmdInfo(ReportSection section, byte[] header)
        {
            if (header.Length >= 8)
                Add(section, "Declared cmid payload size", FormatBytes(ReadUInt32BigEndian(header, 4)));
            Add(section, "Container family", "Qualcomm CMX/PMD mobile audio; this sample family can contain MIDI-like events and embedded DLS/RIFF sample data.");

            var values = ParsePmdFields(header);
            foreach (var pair in values.Take(20))
                Add(section, PmdFieldName(pair.Key), pair.Value);

            var riffOffset = IndexOfAscii(header, "RIFF");
            if (riffOffset >= 0)
                Add(section, "Embedded RIFF offset", "0x" + riffOffset.ToString("X", CultureInfo.InvariantCulture));
            if (IndexOfAscii(header, "DLS ") >= 0)
                Add(section, "Embedded sample bank", "DLS soundbank marker found in sampled bytes.");
            if (IndexOfAscii(header, "SONG") >= 0)
                Add(section, "Song data marker", "SONG marker found in sampled bytes.");
        }

        private static Dictionary<string, string> ParseColonLines(string text)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var split = line.IndexOf(':');
                if (split <= 0)
                    continue;
                var key = line.Substring(0, split).Trim();
                var value = line.Substring(split + 1).Trim();
                if (key.Length > 0 && value.Length > 0 && !values.ContainsKey(key))
                    values.Add(key, value);
            }
            return values;
        }

        private static void AddValue(ReportSection section, Dictionary<string, string> values, string key, string label)
        {
            string value;
            if (values.TryGetValue(key, out value))
                Add(section, label, value);
        }

        private static Dictionary<string, string> ParsePmdFields(byte[] data)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var offset = 8; offset + 6 <= data.Length && values.Count < 32; offset++)
            {
                var id = Encoding.ASCII.GetString(data, offset, 4);
                if (!IsKnownPmdField(id))
                    continue;
                var length = ReadUInt16BigEndian(data, offset + 4);
                if (length == 0 || offset + 6L + length > data.Length)
                    continue;
                var value = ExtractCleanAscii(data, offset + 6, length);
                if (!string.IsNullOrWhiteSpace(value) && !values.ContainsKey(id))
                    values.Add(id, value);
            }
            return values;
        }

        private static bool IsKnownPmdField(string id)
        {
            switch (id)
            {
                case "vers":
                case "titl":
                case "sorc":
                case "copy":
                case "date":
                case "code":
                case "prot":
                case "wave":
                case "poly":
                case "tool":
                case "note":
                case "cnts":
                    return true;
                default:
                    return false;
            }
        }

        private static string PmdFieldName(string id)
        {
            switch (id)
            {
                case "vers": return "Version";
                case "titl": return "Title";
                case "sorc": return "Source";
                case "copy": return "Copyright";
                case "date": return "Date";
                case "code": return "Coding";
                case "prot": return "Provider";
                case "wave": return "Wave support";
                case "poly": return "Polyphony";
                case "tool": return "Tool version";
                case "cnts": return "Content marker";
                default: return id.Trim();
            }
        }

        private static bool IsFourCc(string value)
        {
            return value.Length == 4 && value.All(ch => ch >= 32 && ch < 127);
        }

        private static string ExtractCleanAscii(byte[] data, int offset, int length)
        {
            var end = Math.Min(data.Length, offset + Math.Max(0, length));
            var sb = new StringBuilder();
            for (var i = Math.Max(0, offset); i < end; i++)
            {
                var b = data[i];
                if (b >= 32 && b < 127)
                    sb.Append((char)b);
                else if (sb.Length > 0 && sb[sb.Length - 1] != ' ')
                    sb.Append(' ');
            }
            return CleanMetadataText(sb.ToString());
        }
    }
}
