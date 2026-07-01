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
        private static string LegacyMusicTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (StartsWith(header, Encoding.ASCII.GetBytes("Vgm "))) return "VGM game music";
            if (ext == ".vgz" && StartsWith(header, new byte[] { 0x1F, 0x8B })) return "gzip-compressed VGM game music";
            if (StartsWith(header, Encoding.ASCII.GetBytes("PSID"))) return "PlaySID C64 music";
            if (StartsWith(header, Encoding.ASCII.GetBytes("RSID"))) return "Real C64 SID music";
            if (StartsWith(header, Encoding.ASCII.GetBytes("S98"))) return "S98 chiptune log";
            if (StartsWith(header, Encoding.ASCII.GetBytes("GF1PATCH110"))) return "Gravis UltraSound GF1 instrument patch";
            if (ext == ".mxmf" && StartsWith(header, Encoding.ASCII.GetBytes("XMF_"))) return "Mobile XMF music container";
            if (StartsWith(header, Encoding.ASCII.GetBytes("QSEQ"))) return "QSEQ DOS MIDI sequencer song/project";
            if (StartsWith(header, Encoding.ASCII.GetBytes("XMF_")) || ext == ".xmf") return "XMF extensible music container";
            if (StartsWith(header, Encoding.ASCII.GetBytes("melo")) || ext == ".mld") return "MFi/MLD mobile melody";
            if (IsQcpHeader(header) || ext == ".qcp") return "Qualcomm PureVoice QCP audio";
            if (StartsWith(header, Encoding.ASCII.GetBytes("PSF$")) || ext == ".mini2sf") return "Nintendo DS mini2SF music";
            if (ext == ".rsn") return "RSN SNES music archive";
            if (StartsWith(header, Encoding.ASCII.GetBytes("IREZ")) || ext == ".rmf") return "Beatnik RMF rich music file";
            if (StartsWith(header, Encoding.ASCII.GetBytes("RCM-PC98")) || ext == ".rcp") return "Recomposer RCP sequence";
            if (StartsWith(header, Encoding.ASCII.GetBytes("COME ON MUSIC RECOMPOSER")) || ext == ".g36") return "Recomposer G36 sequence";
            if (ext == ".hed") return "Recomposer header/metadata sidecar";
            if (ext == ".wrd") return "MIDI WRD lyric/graphics script";
            if (StartsWith(header, Encoding.ASCII.GetBytes("MAKI")) || ext == ".mag") return "Maki-chan/MAG graphics file";
            if (ext == ".lyc") return "Roland/Karaoke lyric sidecar";
            if (ext == ".zel" || ext == ".gmc") return "Text music macro/source file";
            if (ext == ".ovw") return "Cubase waveform overview sidecar";
            if (ext == ".sfk") return "Sound Forge waveform overview sidecar";
            if (ext == ".peak") return "Audio waveform peak sidecar";
            if (ext == ".tun" && LooksLikeText(header)) return "Microtuning map file";
            if (StartsWith(header, Encoding.ASCII.GetBytes("RIFF")) && header.Length >= 12 && Encoding.ASCII.GetString(header, 8, 4) == "SFIP") return "SFI impulse-response data";
            if (ext == ".sfi") return "Sampler/impulse-response SFI file";
            if (ext == ".sam") return "Raw sampler sample";
            return null;
        }

        private static void AddLegacyMusicInfo(List<ReportSection> sections, string path, byte[] header, byte[] stringSample, long fileLength)
        {
            var type = LegacyMusicTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, type.IndexOf("Gravis UltraSound", StringComparison.OrdinalIgnoreCase) >= 0 ? "Gravis Ultrasound patch" : "Legacy music/game audio");
            Add(section, "Format hint", type);
            Add(section, "File size", FormatBytes(fileLength));

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (StartsWith(header, Encoding.ASCII.GetBytes("Vgm ")))
                AddVgmInfo(section, header);
            else if (ext == ".vgz" && StartsWith(header, new byte[] { 0x1F, 0x8B }))
                AddVgzInfo(section, path);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("PSID")) || StartsWith(header, Encoding.ASCII.GetBytes("RSID")))
                AddSidInfo(section, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("S98")))
                AddS98Info(section, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("GF1PATCH110")))
                AddGravisPatchInfo(section, header);
            else if (IsQcpHeader(header))
                AddQcpInfo(section, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("XMF_")) || ext == ".xmf" || ext == ".mxmf")
                AddXmfInfo(section, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("melo")) || ext == ".mld")
                AddMldInfo(section, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("PSF$")) || ext == ".mini2sf")
                AddPsfInfo(section, header);
            else if (ext == ".rsn")
                AddRsnInfo(section, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("IREZ")) || ext == ".rmf")
                AddBeatnikRmfInfo(section, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("QSEQ")))
                AddQseqInfo(section, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("RCM-PC98")) || StartsWith(header, Encoding.ASCII.GetBytes("COME ON MUSIC RECOMPOSER")) || ext == ".rcp" || ext == ".g36")
                AddRecomposerInfo(section, header, type);
            else if (ext == ".hed" || ext == ".wrd" || ext == ".mag" || ext == ".lyc" || ext == ".zel" || ext == ".gmc")
                AddLegacyMusicTextInfo(section, header, type);
            else if (ext == ".ovw" || ext == ".sfk" || ext == ".peak")
                AddWaveformSidecarInfo(section, path, type);
            else if (ext == ".tun")
                AddTuningMapInfo(section, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("RIFF")) && header.Length >= 12 && Encoding.ASCII.GetString(header, 8, 4) == "SFIP")
                AddSfiInfo(section, header);
            else if (ext == ".sam")
                Add(section, "Notes", "Old sampler .sam files are often raw PCM or tracker-era sample payloads with little or no header. FileDentify identifies the likely role but does not guess playback parameters.");

            var strings = FindAsciiStrings(stringSample, 4, 12).Select(s => s.Value).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToArray();
            if (strings.Length > 0)
                Add(section, "Visible strings", string.Join("\r\n", strings));
        }

        private static void AddVgmInfo(ReportSection section, byte[] data)
        {
            if (data.Length < 0x40)
                return;
            Add(section, "Version", VgmVersion(ReadUInt32LittleEndian(data, 0x08)));
            var eof = ReadUInt32LittleEndian(data, 0x04) + 4;
            Add(section, "File size from header", FormatUnsignedBytes(eof));
            AddClock(section, "SN76489 clock", ReadUInt32LittleEndian(data, 0x0C));
            AddClock(section, "YM2413 clock", ReadUInt32LittleEndian(data, 0x10));
            AddClock(section, "YM2612 clock", ReadUInt32LittleEndian(data, 0x2C));
            AddClock(section, "YM2151 clock", ReadUInt32LittleEndian(data, 0x30));
            var totalSamples = ReadUInt32LittleEndian(data, 0x18);
            if (totalSamples > 0)
                Add(section, "Approximate duration", FormatAudioSamples(totalSamples));
            var loopSamples = ReadUInt32LittleEndian(data, 0x20);
            if (loopSamples > 0)
                Add(section, "Loop duration", FormatAudioSamples(loopSamples));
            var gd3 = ReadUInt32LittleEndian(data, 0x14);
            if (gd3 > 0)
                Add(section, "GD3 tag offset", "0x" + (gd3 + 0x14).ToString("X", CultureInfo.InvariantCulture));
        }

        private static void AddVgzInfo(ReportSection section, string path)
        {
            Add(section, "Compression", "gzip wrapper around VGM data.");
            try
            {
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var gzip = new GZipStream(file, CompressionMode.Decompress))
                {
                    var buffer = new byte[256];
                    var offset = 0;
                    while (offset < buffer.Length)
                    {
                        var read = gzip.Read(buffer, offset, buffer.Length - offset);
                        if (read == 0)
                            break;
                        offset += read;
                    }
                    if (offset > 0)
                    {
                        if (offset < buffer.Length)
                            Array.Resize(ref buffer, offset);
                        if (StartsWith(buffer, Encoding.ASCII.GetBytes("Vgm ")))
                            AddVgmInfo(section, buffer);
                    }
                }
            }
            catch (Exception ex)
            {
                Add(section, "Decompression note", "Could not read the embedded VGM header: " + ex.Message);
            }
        }

        private static void AddSidInfo(ReportSection section, byte[] data)
        {
            if (data.Length < 0x76)
                return;
            Add(section, "SID marker", Encoding.ASCII.GetString(data, 0, 4));
            Add(section, "Version", ReadUInt16BigEndian(data, 4).ToString(CultureInfo.InvariantCulture));
            Add(section, "Data offset", "0x" + ReadUInt16BigEndian(data, 6).ToString("X", CultureInfo.InvariantCulture));
            Add(section, "Load address", "0x" + ReadUInt16BigEndian(data, 8).ToString("X4", CultureInfo.InvariantCulture));
            Add(section, "Init address", "0x" + ReadUInt16BigEndian(data, 10).ToString("X4", CultureInfo.InvariantCulture));
            Add(section, "Play address", "0x" + ReadUInt16BigEndian(data, 12).ToString("X4", CultureInfo.InvariantCulture));
            Add(section, "Songs", ReadUInt16BigEndian(data, 14).ToString(CultureInfo.InvariantCulture));
            Add(section, "Start song", ReadUInt16BigEndian(data, 16).ToString(CultureInfo.InvariantCulture));
            AddFixedAscii(section, data, 0x16, 32, "Title");
            AddFixedAscii(section, data, 0x36, 32, "Author");
            AddFixedAscii(section, data, 0x56, 32, "Released");
        }

        private static void AddS98Info(ReportSection section, byte[] data)
        {
            if (data.Length < 0x40)
                return;
            Add(section, "Marker", Encoding.ASCII.GetString(data, 0, Math.Min(4, data.Length)));
            Add(section, "Timer numerator", ReadUInt32LittleEndian(data, 4).ToString(CultureInfo.InvariantCulture));
            Add(section, "Timer denominator", ReadUInt32LittleEndian(data, 8).ToString(CultureInfo.InvariantCulture));
            Add(section, "Compression", ReadUInt32LittleEndian(data, 12) == 0 ? "none/unspecified" : ReadUInt32LittleEndian(data, 12).ToString(CultureInfo.InvariantCulture));
            var text = ExtractCleanAscii(data, 0x40, Math.Min(data.Length - 0x40, 256));
            if (!string.IsNullOrWhiteSpace(text))
                Add(section, "Header text", text);
        }

        private static void AddGravisPatchInfo(ReportSection section, byte[] data)
        {
            Add(section, "Header marker", "GF1PATCH110");
            AddFixedAscii(section, data, 0x0C, 10, "Manufacturer ID");
            var description = BestGravisDescription(data);
            if (!string.IsNullOrWhiteSpace(description))
                Add(section, "Description", description);
            if (data.Length > 0x56)
            {
                var instrumentCount = data[0x54];
                if (instrumentCount > 0 && instrumentCount < 128)
                    Add(section, "Instrument count", instrumentCount.ToString(CultureInfo.InvariantCulture));
                var channels = data[0x56];
                if (channels == 1 || channels == 2)
                    Add(section, "Output channels", channels == 1 ? "mono" : "stereo");
            }
            if (data.Length >= 0x5B)
            {
                var waveformCount = ReadUInt16LittleEndian(data, 0x57);
                if (waveformCount > 0 && waveformCount < 4096)
                    Add(section, "Waveform count", waveformCount.ToString(CultureInfo.InvariantCulture));
                var masterVolume = ReadUInt16LittleEndian(data, 0x59);
                if (masterVolume > 0)
                    Add(section, "Master volume", masterVolume.ToString(CultureInfo.InvariantCulture));
            }
            Add(section, "Notes", "Gravis UltraSound .pat files are GF1 instrument patch/sample files used for MIDI playback by GUS-compatible cards and software synths. FileDentify reports header fields and visible names; it does not play or resample the patch.");
        }

        private static string BestGravisDescription(byte[] data)
        {
            var candidates = new[]
            {
                ReadFixedAscii(data, 0x16, Math.Min(62, Math.Max(0, data.Length - 0x16))),
                ReadFixedAscii(data, 0x18, Math.Min(60, Math.Max(0, data.Length - 0x18)))
            };
            return candidates
                .Select(CleanGravisText)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .OrderByDescending(s => s.Length)
                .FirstOrDefault();
        }

        private static string CleanGravisText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var sb = new StringBuilder();
            foreach (var ch in value)
            {
                if (ch == '\0')
                    break;
                if (ch >= 32 && ch != 127)
                    sb.Append(ch);
            }
            return sb.ToString().Trim();
        }

        private static void AddQcpInfo(ReportSection section, byte[] data)
        {
            if (data.Length < 12)
                return;
            Add(section, "RIFF form", Encoding.ASCII.GetString(data, 8, 4));
            if (data.Length >= 8)
                Add(section, "RIFF payload size", FormatBytes(ReadUInt32LittleEndian(data, 4)));
            if (IndexOfAscii(data, "Qcelp") >= 0)
                Add(section, "Codec marker", "QCELP/PureVoice marker found.");
        }

        private static void AddXmfInfo(ReportSection section, byte[] data)
        {
            if (data.Length >= 8 && StartsWith(data, Encoding.ASCII.GetBytes("XMF_")))
                Add(section, "XMF marker", Encoding.ASCII.GetString(data, 0, 8).TrimEnd('\0'));
            if (IndexOfAscii(data, "MThd") >= 0)
                Add(section, "Embedded MIDI", "MThd marker found in sampled bytes.");
            if (IndexOfAscii(data, "DLS ") >= 0)
                Add(section, "Embedded DLS", "DLS soundbank marker found in sampled bytes.");
            var riff = IndexOfAscii(data, "RIFF");
            if (riff >= 0)
                Add(section, "Embedded RIFF offset", "0x" + riff.ToString("X", CultureInfo.InvariantCulture));
        }

        private static void AddMldInfo(ReportSection section, byte[] data)
        {
            Add(section, "Marker", StartsWith(data, Encoding.ASCII.GetBytes("melo")) ? "melo" : "extension-level match");
            var strings = FindAsciiStrings(data, 4, 20).Select(s => s.Value).Take(20).ToArray();
            if (strings.Length > 0)
                Add(section, "Metadata strings", string.Join("\r\n", strings));
        }

        private static void AddPsfInfo(ReportSection section, byte[] data)
        {
            if (data.Length >= 4)
                Add(section, "PSF version byte", "0x" + data[3].ToString("X2", CultureInfo.InvariantCulture));
            var tag = IndexOfAscii(data, "[TAG]");
            if (tag >= 0)
            {
                var text = ExtractCleanAscii(data, tag, Math.Min(512, data.Length - tag));
                Add(section, "Tags", BreakDelimitedText(text.Replace(".", "\r\n")));
            }
            Add(section, "Platform note", "mini2SF files are small Nintendo DS music stubs that usually reference a companion .2sflib library.");
        }

        private static void AddRsnInfo(ReportSection section, byte[] data)
        {
            if (StartsWith(data, Encoding.ASCII.GetBytes("Rar!")))
                Add(section, "Archive container", "RAR archive containing SNES SPC music files.");
            Add(section, "Notes", "RSN is commonly used for Super Nintendo / Super Famicom music sets. FileDentify identifies the archive role but does not unpack or play the SPC files.");
        }

        private static void AddBeatnikRmfInfo(ReportSection section, byte[] data)
        {
            if (StartsWith(data, Encoding.ASCII.GetBytes("IREZ")))
                Add(section, "Marker", "IREZ");
            var useful = FindAsciiStrings(data, 4, 16)
                .Select(s => s.Value)
                .Where(s => s.Length > 2)
                .Take(16)
                .ToArray();
            if (useful.Length > 0)
                Add(section, "Visible RMF resources", string.Join("\r\n", useful));
        }

        private static void AddQseqInfo(ReportSection section, byte[] data)
        {
            Add(section, "Marker", "QSEQ");
            if (data.Length >= 8)
                Add(section, "Header values", ReadUInt16LittleEndian(data, 4).ToString(CultureInfo.InvariantCulture) + ", " + ReadUInt16LittleEndian(data, 6).ToString(CultureInfo.InvariantCulture));
            var useful = FindAsciiStrings(data, 4, 16)
                .Select(s => s.Value)
                .Where(s => s.Length > 2 && !s.Equals("QSEQ", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(16)
                .ToArray();
            if (useful.Length > 0)
                Add(section, "Visible song/project strings", string.Join("\r\n", useful));
            Add(section, "Notes", "QSEQ .QSQ files are old DOS MIDI sequencer song/project files. FileDentify reports the marker and visible strings; it does not convert the sequence to MIDI or play it.");
        }

        private static void AddRecomposerInfo(ReportSection section, byte[] data, string type)
        {
            Add(section, "Family", type);
            var text = ExtractCleanAscii(data, 0, Math.Min(data.Length, 512));
            if (!string.IsNullOrWhiteSpace(text))
                Add(section, "Header text", BreakDelimitedText(text));
            Add(section, "Notes", "Recomposer files are MIDI-sequencer data used heavily in older Japanese PC and Sound Canvas collections.");
        }

        private static void AddLegacyMusicTextInfo(ReportSection section, byte[] data, string type)
        {
            Add(section, "Role", type);
            var text = ExtractCleanAscii(data, 0, Math.Min(data.Length, 2048));
            if (!string.IsNullOrWhiteSpace(text))
                Add(section, "Text preview", BreakDelimitedText(text));
            Add(section, "Notes", "These files are usually companions to old MIDI/sequencer collections: lyrics, graphics scripts, macro files, or metadata rather than standalone audio.");
        }

        private static void AddWaveformSidecarInfo(ReportSection section, string path, string type)
        {
            Add(section, "Role", type);
            Add(section, "Possible source file", Path.GetFileNameWithoutExtension(path));
            Add(section, "Notes", "Waveform overview and peak files are generated sidecars used to draw audio waveforms quickly. They are usually safe to regenerate and are not the original audio.");
        }

        private static void AddSfiInfo(ReportSection section, byte[] data)
        {
            Add(section, "RIFF form", "SFIP");
            Add(section, "RIFF payload size", FormatBytes(ReadUInt32LittleEndian(data, 4)));
            Add(section, "Notes", "SFI/SFIP appears here as impulse-response or sampler payload data. FileDentify reports the container marker and visible strings only.");
        }

        private static void AddTuningMapInfo(ReportSection section, byte[] data)
        {
            var text = DecodeWindowsText(data);
            var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .Take(16)
                .ToArray();
            Add(section, "Role", "Microtuning or alternate tuning-map file used by synthesizers and sample libraries.");
            var noteLines = Regex.Matches(text, @"(?im)^\s*note\s+\d+\s*=").Count;
            if (noteLines > 0)
                Add(section, "Note tuning entries in sample", noteLines.ToString(CultureInfo.InvariantCulture));
            if (lines.Length > 0)
                Add(section, "Text preview", string.Join("\r\n", lines));
        }

        private static bool IsQcpHeader(byte[] data)
        {
            return data.Length >= 12 &&
                StartsWith(data, Encoding.ASCII.GetBytes("RIFF")) &&
                Encoding.ASCII.GetString(data, 8, 4) == "QLCM";
        }

        private static void AddClock(ReportSection section, string title, uint value)
        {
            if (value == 0)
                return;
            Add(section, title, value.ToString(CultureInfo.InvariantCulture) + " Hz");
        }

        private static string VgmVersion(uint value)
        {
            var text = value.ToString("X8", CultureInfo.InvariantCulture).TrimStart('0');
            if (text.Length <= 2)
                return "0." + text.PadLeft(2, '0');
            return text.Substring(0, text.Length - 2) + "." + text.Substring(text.Length - 2);
        }

        private static string FormatAudioSamples(uint samples)
        {
            var seconds = samples / 44100.0;
            var span = TimeSpan.FromSeconds(seconds);
            return span.TotalHours >= 1
                ? span.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
                : span.ToString(@"m\:ss", CultureInfo.InvariantCulture);
        }

        private static void AddFixedAscii(ReportSection section, byte[] data, int offset, int length, string title)
        {
            if (offset >= data.Length)
                return;
            var count = Math.Min(length, data.Length - offset);
            var value = Encoding.ASCII.GetString(data, offset, count).TrimEnd('\0', ' ');
            if (!string.IsNullOrWhiteSpace(value))
                Add(section, title, value);
        }

        private static string BreakDelimitedText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            return Regex.Replace(value.Trim(), @"\s*(\||;|\r?\n)\s*", "\r\n");
        }
    }
}
