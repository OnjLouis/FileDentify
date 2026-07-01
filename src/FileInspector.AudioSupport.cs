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
        private static string AudioSupportTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".sd2") return "Sound Designer II audio";
            if (ext == ".ses") return "Cool Edit / Adobe Audition session";
            if (LooksLikeBarrcodeAudio(header)) return "Barrcode AA audio resource";
            if ((ext == ".mp2" || ext == ".mpga") && LooksLikeMpegAudioFrame(header, 0)) return "MPEG audio stream";
            if (string.IsNullOrWhiteSpace(ext) && StartsWith(header, Encoding.ASCII.GetBytes("ID3"))) return "MP3 audio with ID3 tag";
            return null;
        }

        private static void AddAudioSupportInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".sd2")
                AddSoundDesignerInfo(sections, path, header, fileLength);
            if (ext == ".ses")
                AddAuditionSessionInfo(sections, path, header, fileLength);
            if (LooksLikeBarrcodeAudio(header))
                AddBarrcodeAudioInfo(sections, path, header, fileLength);
            if ((ext == ".mp2" || ext == ".mpga" || string.IsNullOrWhiteSpace(ext)) && LooksLikeMpegAudioFrame(header, 0))
                AddRawMpegAudioInfo(sections, path, header, fileLength);
        }

        private static void AddSoundDesignerInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var section = AddSection(sections, "Legacy audio resource");
            Add(section, "Format hint", "Sound Designer II audio");
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Extension", ".sd2");
            Add(section, "Likely payload", "Headerless sampled audio data");
            Add(section, "First sample bytes", HexPreview(header, Math.Min(header.Length, 32)));
            Add(section, "Notes", "Sound Designer II was a classic Mac audio format. Important metadata may live in a Mac resource fork, so Windows copies can look like raw sample data with no embedded header.");
        }

        private static void AddAuditionSessionInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var section = AddSection(sections, "Audio session");
            Add(section, "Format hint", "Cool Edit / Adobe Audition session");
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Extension", ".ses");
            var strings = FindReadableTextLines(header, 4, 80)
                .Where(value => value.IndexOf("Cool", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("Adobe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("Session", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf(".wav", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf(".mp3", StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(16)
                .ToArray();
            if (strings.Length > 0)
                Add(section, "Visible session strings", string.Join("\r\n", strings));
            Add(section, "Notes", "Session files can reference local media paths. FileDentify reports format identity and bounded visible strings only.");
        }

        private static bool LooksLikeBarrcodeAudio(byte[] header)
        {
            return header.Length >= 13 && IndexOfAscii(header.Take(Math.Min(header.Length, 64)).ToArray(), "Barrcode AA") >= 0;
        }

        private static void AddBarrcodeAudioInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var section = AddSection(sections, "Legacy audio resource");
            Add(section, "Format hint", "Barrcode AA audio resource");
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Header marker", "Barrcode AA");
            Add(section, "Extension", Path.GetExtension(path));
            Add(section, "Likely origin", "Barrcode broadcast playout software");
            Add(section, "Platform/context", "UK radio and TV broadcast automation / hard-disk media playout systems");
            Add(section, "Notes", "Barrcode is a UK broadcast-software company whose BCX system began as radio-station CD-jukebox control and became a hard-disk media playout system. FileDentify identifies this sample from the visible Barrcode AA marker and local broadcast-audio archive context; it does not decode the proprietary audio payload.");
        }

        private static void AddRawMpegAudioInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var section = AddSection(sections, "MPEG audio");
            Add(section, "Format hint", "MPEG audio stream");
            Add(section, "File size", FormatBytes(fileLength));
            AddMp3FrameEstimate(section, path, header, fileLength, 0);
            Add(section, "Notes", "Raw MPEG audio streams may use extensions such as .mp2, .mp3, .mpga, or no extension. FileDentify estimates basic stream fields from the first audio frame.");
        }

        private static bool LooksLikeMpegAudioFrame(byte[] data, int offset)
        {
            if (data == null || offset < 0 || offset + 4 > data.Length)
                return false;
            if (data[offset] != 0xff || (data[offset + 1] & 0xe0) != 0xe0)
                return false;
            return Mp3BitrateKbps(data[offset + 1], data[offset + 2]) > 0 &&
                Mp3SampleRate(data[offset + 1], data[offset + 2]) > 0;
        }
    }
}
