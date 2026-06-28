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
    {        private static void AddCompressedStreamInfo(List<ReportSection> sections, byte[] header)
        {
            var kind = CompressionFormatName(header);
            if (kind == null)
                return;

            var section = AddSection(sections, "Compressed stream");
            Add(section, "Format", kind);
            if (StartsWith(header, new byte[] { 0xFD, (byte)'7', (byte)'z', (byte)'X', (byte)'Z', 0x00 }) && header.Length >= 12)
                Add(section, "Header flags", "0x" + header[6].ToString("X2", CultureInfo.InvariantCulture) + " 0x" + header[7].ToString("X2", CultureInfo.InvariantCulture));
        }

        private static void AddIso9660Info(List<ReportSection> sections, string path, byte[] header)
        {
            if (!HasIso9660Descriptor(header))
                return;

            var section = AddSection(sections, "ISO 9660 volume");
            Add(section, "Descriptor marker", "CD001 at offset 0x8001");
            if (header.Length >= 0x8050)
            {
                var systemId = Encoding.ASCII.GetString(header, 0x8008, 32).Trim();
                var volumeId = Encoding.ASCII.GetString(header, 0x8028, 32).Trim();
                if (!string.IsNullOrWhiteSpace(systemId))
                    Add(section, "System id", systemId);
                if (!string.IsNullOrWhiteSpace(volumeId))
                    Add(section, "Volume id", volumeId);
            }
            if (string.Equals(Path.GetExtension(path), ".iso", StringComparison.OrdinalIgnoreCase))
                Add(section, "Extension hint", "Optical disc image");
        }

        private static void AddVirtualDiskInfo(List<ReportSection> sections, string path, byte[] header, long length)
        {
            var ext = Path.GetExtension(path);
            var kind = VirtualDiskFormatName(path, header);
            if (kind == null)
                return;

            var section = AddSection(sections, "Virtual disk");
            Add(section, "Format", kind);
            if (string.Equals(ext, ".vmdk", StringComparison.OrdinalIgnoreCase) && LooksLikeText(header))
            {
                var text = Encoding.ASCII.GetString(header.Take(Math.Min(header.Length, 8192)).ToArray());
                AddKeyValueLine(section, text, "createType");
                AddKeyValueLine(section, text, "CID");
                AddKeyValueLine(section, text, "parentCID");
                AddKeyValueLine(section, text, "ddb.virtualHWVersion");
                AddKeyValueLine(section, text, "ddb.adapterType");
            }
            if (string.Equals(ext, ".vhd", StringComparison.OrdinalIgnoreCase))
                Add(section, "VHD footer", length >= 512 ? "Expected in final 512 bytes" : "File is smaller than a normal VHD footer");
        }

        private static void AddMozillaLz4Info(List<ReportSection> sections, byte[] header)
        {
            if (!StartsWith(header, Encoding.ASCII.GetBytes("mozLz40\0")))
                return;

            var section = AddSection(sections, "Mozilla LZ4 JSON");
            Add(section, "Marker", "mozLz40");
            Add(section, "Common use", "Firefox/Thunderbird profile data compressed with Mozilla's LZ4 wrapper");
            var preview = AsciiPreview(header.Skip(8).Take(160).ToArray(), 160);
            if (!string.IsNullOrWhiteSpace(preview))
                Add(section, "Compressed payload preview", preview);
        }

        private static void AddUfsInfo(List<ReportSection> sections, string path, byte[] header)
        {
            var markerAtStart = header.Length >= 4 && StartsWith(header, Encoding.ASCII.GetBytes("UFS2"));
            var hasUfsExtension = string.Equals(Path.GetExtension(path), ".ufs", StringComparison.OrdinalIgnoreCase);
            if (!markerAtStart && !hasUfsExtension)
                return;

            var section = AddSection(sections, "UFS sample library container");
            Add(section, "Format marker", markerAtStart ? "UFS2" : "Not found in first header sample");
            Add(section, "Container family", markerAtStart ? "UVI/Falcon soundbank or sample-library container" : "Sample-library container using .ufs extension");

            var embeddedName = ReadNullTerminatedAscii(header, 0x30, 96);
            if (!string.IsNullOrWhiteSpace(embeddedName))
                Add(section, "Embedded name", embeddedName);

            Add(section, "Extension", Path.GetExtension(path));
        }

        private static void AddBlobInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (!string.Equals(Path.GetExtension(path), ".blob", StringComparison.OrdinalIgnoreCase))
                return;

            var section = AddSection(sections, "Binary blob");
            Add(section, "Format hint", "Binary blob asset or metadata container");
            var ascii = AsciiPreview(header, Math.Min(header.Length, 128));
            if (ascii.IndexOf("Native Instruments", StringComparison.OrdinalIgnoreCase) >= 0 ||
                ascii.IndexOf("Kontakt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                ascii.IndexOf("Reaktor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                ascii.IndexOf("Maschine", StringComparison.OrdinalIgnoreCase) >= 0)
                Add(section, "Family hint", "Native Instruments-related readable marker found in header sample.");
            Add(section, "Notes", "The .blob extension is generic. FileDentify reports visible strings and byte statistics elsewhere; this section records the container-style extension without claiming a complete parser.");
        }

        private static string ReadNullTerminatedAscii(byte[] data, int offset, int maxLength)
        {
            if (data == null || offset < 0 || offset >= data.Length)
                return string.Empty;
            var count = 0;
            while (offset + count < data.Length && count < maxLength)
            {
                var b = data[offset + count];
                if (b == 0)
                    break;
                if (b < 32 || b >= 127)
                    return string.Empty;
                count++;
            }
            return count == 0 ? string.Empty : Encoding.ASCII.GetString(data, offset, count);
        }

        private static void AddRiffInfo(List<ReportSection> sections, byte[] header)
        {
            if (header.Length < 12 || !StartsWith(header, Encoding.ASCII.GetBytes("RIFF")))
                return;
            var section = AddSection(sections, "RIFF");
            var riffSize = BitConverter.ToUInt32(header, 4);
            var form = Encoding.ASCII.GetString(header, 8, 4);
            Add(section, "Form type", form);
            Add(section, "Declared RIFF payload size", FormatBytes(riffSize) + " (" + riffSize.ToString(CultureInfo.InvariantCulture) + " bytes)");

            var offset = 12;
            uint wavByteRate = 0;
            uint wavDataSize = 0;
            var chunks = new List<string>();
            while (offset + 8 <= header.Length && chunks.Count < 20)
            {
                var id = Encoding.ASCII.GetString(header, offset, 4);
                var size = BitConverter.ToUInt32(header, offset + 4);
                chunks.Add(id + " (" + FormatBytes(size) + ")");
                if (id == "data")
                    wavDataSize = size;
                if (id == "fmt " && size >= 16 && offset + 24 <= header.Length)
                {
                    var format = BitConverter.ToUInt16(header, offset + 8);
                    var channels = BitConverter.ToUInt16(header, offset + 10);
                    var sampleRate = BitConverter.ToUInt32(header, offset + 12);
                    var byteRate = BitConverter.ToUInt32(header, offset + 16);
                    wavByteRate = byteRate;
                    var blockAlign = BitConverter.ToUInt16(header, offset + 20);
                    var bits = BitConverter.ToUInt16(header, offset + 22);
                    Add(section, "Audio format", WaveFormatName(format) + " (0x" + format.ToString("X4", CultureInfo.InvariantCulture) + ")");
                    Add(section, "Channels", channels.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Sample rate", sampleRate.ToString(CultureInfo.InvariantCulture) + " Hz");
                    Add(section, "Bits per sample", bits.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Byte rate", byteRate.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Block align", blockAlign.ToString(CultureInfo.InvariantCulture));
                }
                var next = offset + 8L + size + (size % 2);
                if (next <= offset || next > int.MaxValue)
                    break;
                offset = (int)next;
            }
            if (wavByteRate > 0 && wavDataSize > 0)
                Add(section, "Duration", FormatDuration((double)wavDataSize / wavByteRate));
            Add(section, "First chunks", string.Join("\r\n", chunks.ToArray()));
        }

        private static void AddIffInfo(List<ReportSection> sections, byte[] header)
        {
            if (header.Length < 12 || !StartsWith(header, Encoding.ASCII.GetBytes("FORM")))
                return;

            var section = AddSection(sections, "AIFF/IFF");
            var declaredSize = ReadUInt32BigEndian(header, 4);
            var formType = Encoding.ASCII.GetString(header, 8, 4);
            Add(section, "Form type", formType);
            Add(section, "Declared FORM payload size", FormatBytes(declaredSize) + " (" + declaredSize.ToString(CultureInfo.InvariantCulture) + " bytes)");

            var offset = 12;
            var chunks = new List<string>();
            while (offset + 8 <= header.Length && chunks.Count < 24)
            {
                var id = Encoding.ASCII.GetString(header, offset, 4);
                var size = ReadUInt32BigEndian(header, offset + 4);
                chunks.Add(id + " (" + FormatBytes(size) + ")");

                if (id == "COMM" && size >= 18 && offset + 26 <= header.Length)
                {
                    var channels = ReadUInt16BigEndian(header, offset + 8);
                    var frames = ReadUInt32BigEndian(header, offset + 10);
                    var bits = ReadUInt16BigEndian(header, offset + 14);
                    var sampleRate = ReadIeeeExtended80(header, offset + 16);
                    Add(section, "Channels", channels.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Sample frames", frames.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Bits per sample", bits.ToString(CultureInfo.InvariantCulture));
                    if (sampleRate > 0)
                    {
                        Add(section, "Sample rate", sampleRate.ToString("0.###", CultureInfo.InvariantCulture) + " Hz");
                        if (frames > 0)
                            Add(section, "Duration", FormatDuration(frames / sampleRate));
                    }
                    if (formType == "AIFC" && size >= 22 && offset + 30 <= header.Length)
                        Add(section, "Compression type", Encoding.ASCII.GetString(header, offset + 26, 4));
                }

                var next = offset + 8L + size + (size % 2);
                if (next <= offset || next > int.MaxValue)
                    break;
                offset = (int)next;
            }

            if (chunks.Count > 0)
                Add(section, "First chunks", string.Join("\r\n", chunks.ToArray()));
        }

        private static void AddMidiInfo(List<ReportSection> sections, byte[] header)
        {
            if (header.Length < 14 || !StartsWith(header, Encoding.ASCII.GetBytes("MThd")))
                return;

            var section = AddSection(sections, "MIDI");
            var headerLength = ReadUInt32BigEndian(header, 4);
            var format = ReadUInt16BigEndian(header, 8);
            var tracks = ReadUInt16BigEndian(header, 10);
            var division = ReadUInt16BigEndian(header, 12);
            Add(section, "Header length", headerLength.ToString(CultureInfo.InvariantCulture));
            Add(section, "Format", MidiFormatName(format) + " (" + format.ToString(CultureInfo.InvariantCulture) + ")");
            Add(section, "Declared track count", tracks.ToString(CultureInfo.InvariantCulture));
            Add(section, "Timing division", MidiDivisionDescription(division));

            var offset = 8 + (int)Math.Min(headerLength, int.MaxValue - 8);
            var chunks = new List<string>();
            while (offset + 8 <= header.Length && chunks.Count < 20)
            {
                var id = Encoding.ASCII.GetString(header, offset, 4);
                var size = ReadUInt32BigEndian(header, offset + 4);
                chunks.Add(id + " (" + FormatBytes(size) + ")");
                var next = offset + 8L + size;
                if (next <= offset || next > int.MaxValue)
                    break;
                offset = (int)next;
            }

            if (chunks.Count > 0)
                Add(section, "First chunks", string.Join("\r\n", chunks.ToArray()));
        }

        private static void AddPropertyListInfo(List<ReportSection> sections, byte[] header)
        {
            if (header.Length >= 8 && StartsWith(header, Encoding.ASCII.GetBytes("bplist00")))
            {
                var section = AddSection(sections, "Property list");
                Add(section, "Format", "Binary property list");
                Add(section, "Version marker", Encoding.ASCII.GetString(header, 6, 2));
                return;
            }

            if (!LooksLikeText(header))
                return;
            var text = Encoding.UTF8.GetString(header.Take(Math.Min(header.Length, 8192)).ToArray());
            if (text.IndexOf("<plist", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("DOCTYPE plist", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var section = AddSection(sections, "Property list");
                Add(section, "Format", "XML property list");
            }
        }

        private static void AddSqliteInfo(List<ReportSection> sections, byte[] header)
        {
            if (header.Length < 100 || !StartsWith(header, Encoding.ASCII.GetBytes("SQLite format 3\0")))
                return;

            var section = AddSection(sections, "SQLite database");
            var pageSize = ReadUInt16BigEndian(header, 16);
            Add(section, "Page size", (pageSize == 1 ? 65536 : pageSize).ToString(CultureInfo.InvariantCulture) + " bytes");
            Add(section, "Write version", SqliteJournalMode(header[18]));
            Add(section, "Read version", SqliteJournalMode(header[19]));
            Add(section, "Page count", ReadUInt32BigEndian(header, 28).ToString(CultureInfo.InvariantCulture));
            Add(section, "Schema cookie", ReadUInt32BigEndian(header, 40).ToString(CultureInfo.InvariantCulture));
            Add(section, "User version", ReadUInt32BigEndian(header, 60).ToString(CultureInfo.InvariantCulture));
            Add(section, "Application id", "0x" + ReadUInt32BigEndian(header, 68).ToString("X8", CultureInfo.InvariantCulture));
        }

        private static void AddRarInfo(List<ReportSection> sections, byte[] header)
        {
            var rar4 = Encoding.GetEncoding(28591).GetBytes("Rar!\x1A\x07\x00");
            var rar5 = Encoding.GetEncoding(28591).GetBytes("Rar!\x1A\x07\x01\x00");
            if (!StartsWith(header, rar4) && !StartsWith(header, rar5))
                return;

            var section = AddSection(sections, "RAR archive");
            Add(section, "Format version", StartsWith(header, rar5) ? "RAR 5" : "RAR 4 or earlier");
            Add(section, "Marker", HexPreview(header, StartsWith(header, rar5) ? 8 : 7));
        }

        private static void AddIsoBmffInfo(List<ReportSection> sections, byte[] header)
        {
            if (header.Length < 12 || Encoding.ASCII.GetString(header, 4, 4) != "ftyp")
                return;
            var section = AddSection(sections, "ISO base media");
            var boxSize = ReadUInt32BigEndian(header, 0);
            var major = Encoding.ASCII.GetString(header, 8, 4).TrimEnd('\0', ' ');
            var minor = ReadUInt32BigEndian(header, 12);
            Add(section, "Major brand", major);
            Add(section, "Minor version", minor.ToString(CultureInfo.InvariantCulture));
            Add(section, "File type box size", FormatBytes(boxSize));
            var brands = new List<string>();
            for (var i = 16; i + 4 <= header.Length && i < boxSize; i += 4)
            {
                var brand = Encoding.ASCII.GetString(header, i, 4).TrimEnd('\0', ' ');
                if (!string.IsNullOrWhiteSpace(brand))
                    brands.Add(brand);
                if (brands.Count >= 24)
                    break;
            }
            if (brands.Count > 0)
                Add(section, "Compatible brands", string.Join(", ", brands.ToArray()));
            Add(section, "Family hint", IsoBrandHint(major, brands));
        }

        private static string IsoBrandHint(string major, List<string> brands)
        {
            var all = new List<string>(brands ?? new List<string>());
            if (!string.IsNullOrWhiteSpace(major))
                all.Insert(0, major);
            if (all.Any(b => b.StartsWith("qt", StringComparison.OrdinalIgnoreCase))) return "QuickTime/MOV";
            if (all.Any(b => b.StartsWith("M4A", StringComparison.OrdinalIgnoreCase) || b.StartsWith("M4B", StringComparison.OrdinalIgnoreCase))) return "MPEG-4 audio";
            if (all.Any(b => b.StartsWith("isom", StringComparison.OrdinalIgnoreCase) || b.StartsWith("mp4", StringComparison.OrdinalIgnoreCase))) return "MP4/ISO media";
            if (all.Any(b => b.StartsWith("3g", StringComparison.OrdinalIgnoreCase))) return "3GPP media";
            if (all.Any(b => b.StartsWith("avif", StringComparison.OrdinalIgnoreCase))) return "AVIF image";
            if (all.Any(b => b.StartsWith("heic", StringComparison.OrdinalIgnoreCase) || b.StartsWith("heix", StringComparison.OrdinalIgnoreCase))) return "HEIF/HEIC image";
            return "ISO base media family";
        }

        private static string WaveFormatName(ushort format)
        {
            switch (format)
            {
                case 0x0001: return "PCM";
                case 0x0003: return "IEEE float";
                case 0x0006: return "A-law";
                case 0x0007: return "mu-law";
                case 0x0011: return "IMA ADPCM";
                case 0x0055: return "MP3";
                case 0xfffe: return "Extensible";
                default: return "Unknown";
            }
        }

        private static string MidiFormatName(ushort format)
        {
            switch (format)
            {
                case 0: return "Single track";
                case 1: return "Multiple synchronous tracks";
                case 2: return "Multiple asynchronous sequences";
                default: return "Unknown";
            }
        }

        private static string MidiDivisionDescription(ushort division)
        {
            if ((division & 0x8000) == 0)
                return (division & 0x7fff).ToString(CultureInfo.InvariantCulture) + " ticks per quarter note";

            var fpsByte = (sbyte)((division >> 8) & 0xff);
            var ticks = division & 0xff;
            return Math.Abs(fpsByte).ToString(CultureInfo.InvariantCulture) + " SMPTE frames per second, " + ticks.ToString(CultureInfo.InvariantCulture) + " ticks per frame";
        }

        private static string SqliteJournalMode(byte value)
        {
            switch (value)
            {
                case 1: return "Legacy rollback journal";
                case 2: return "WAL-capable";
                default: return "Unknown (" + value.ToString(CultureInfo.InvariantCulture) + ")";
            }
        }
    }
}

