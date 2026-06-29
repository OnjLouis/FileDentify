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

        private static string CabinetTypeName(string path, byte[] header)
        {
            if (!StartsWith(header, Encoding.ASCII.GetBytes("MSCF")))
                return null;
            var ext = Path.GetExtension(path);
            return ext.Length == 4 && ext[3] == '_' ? "Windows setup compressed file" : "Microsoft Cabinet archive";
        }

        private static void AddCabinetInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (!StartsWith(header, Encoding.ASCII.GetBytes("MSCF")) || header.Length < 36)
                return;

            var section = AddSection(sections, "Microsoft Cabinet archive");
            var cabinetSize = ReadUInt32LittleEndian(header, 8);
            var fileTableOffset = ReadUInt32LittleEndian(header, 16);
            var folders = ReadUInt16LittleEndian(header, 26);
            var files = ReadUInt16LittleEndian(header, 28);
            var flags = ReadUInt16LittleEndian(header, 30);
            Add(section, "Format", CabinetTypeName(path, header) ?? "Microsoft Cabinet archive");
            Add(section, "Declared cabinet size", FormatBytes(cabinetSize) + " (" + cabinetSize.ToString(CultureInfo.InvariantCulture) + " bytes)");
            Add(section, "Version", header[25].ToString(CultureInfo.InvariantCulture) + "." + header[24].ToString(CultureInfo.InvariantCulture));
            Add(section, "Folder count", folders.ToString(CultureInfo.InvariantCulture));
            Add(section, "File count", files.ToString(CultureInfo.InvariantCulture));
            Add(section, "Flags", CabinetFlags(flags));
            Add(section, "Set id", ReadUInt16LittleEndian(header, 32).ToString(CultureInfo.InvariantCulture));
            Add(section, "Cabinet index", ReadUInt16LittleEndian(header, 34).ToString(CultureInfo.InvariantCulture));

            var names = ReadCabinetFileNames(header, (int)fileTableOffset, Math.Min(files, (ushort)20));
            if (names.Count > 0)
            {
                Add(section, files == 1 ? "Stored file" : "First files", string.Join("\r\n", names.ToArray()));
                if (Path.GetExtension(path).EndsWith("_", StringComparison.OrdinalIgnoreCase))
                    Add(section, "Setup-compressed hint", "Underscore extensions such as .DL_, .EX_, and .SY_ are usually single-file CAB-compressed Windows setup files.");
            }
            Add(section, "Notes", "Header and file-table summary only; FileDentify does not extract Cabinet contents.");
        }

        private static List<string> ReadCabinetFileNames(byte[] data, int offset, int maxNames)
        {
            var names = new List<string>();
            while (offset >= 0 && offset + 16 <= data.Length && names.Count < maxNames)
            {
                var size = ReadUInt32LittleEndian(data, offset);
                var attribs = ReadUInt16LittleEndian(data, offset + 14);
                var nameOffset = offset + 16;
                var end = nameOffset;
                while (end < data.Length && data[end] != 0 && end - nameOffset < 260)
                    end++;
                if (end <= nameOffset || end >= data.Length)
                    break;
                var name = CleanMetadataText(Encoding.GetEncoding(28591).GetString(data, nameOffset, end - nameOffset));
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name + " (" + FormatBytes(size) + ", attributes 0x" + attribs.ToString("X4", CultureInfo.InvariantCulture) + ")");
                offset = end + 1;
            }
            return names;
        }

        private static string CabinetFlags(ushort flags)
        {
            var parts = new List<string>();
            if ((flags & 0x0001) != 0) parts.Add("previous cabinet");
            if ((flags & 0x0002) != 0) parts.Add("next cabinet");
            if ((flags & 0x0004) != 0) parts.Add("reserved areas");
            if (parts.Count == 0) parts.Add("none");
            parts.Add("0x" + flags.ToString("X4", CultureInfo.InvariantCulture));
            return string.Join(", ", parts.ToArray());
        }

        private static string WindowsImageTypeName(string path, byte[] header)
        {
            if (!StartsWith(header, Encoding.ASCII.GetBytes("MSWIM\0\0\0")))
                return null;
            return string.Equals(Path.GetExtension(path), ".esd", StringComparison.OrdinalIgnoreCase) ? "Windows imaging ESD image" : "Windows imaging WIM image";
        }

        private static void AddWindowsImageInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (!StartsWith(header, Encoding.ASCII.GetBytes("MSWIM\0\0\0")) || header.Length < 48)
                return;

            var section = AddSection(sections, "Windows imaging");
            var flags = ReadUInt32LittleEndian(header, 16);
            Add(section, "Format", WindowsImageTypeName(path, header) ?? "Windows imaging file");
            Add(section, "Header size", FormatBytes(ReadUInt32LittleEndian(header, 8)));
            Add(section, "Raw version", "0x" + ReadUInt32LittleEndian(header, 12).ToString("X8", CultureInfo.InvariantCulture));
            Add(section, "Image count", ReadUInt32LittleEndian(header, 44).ToString(CultureInfo.InvariantCulture));
            Add(section, "Part", ReadUInt16LittleEndian(header, 40).ToString(CultureInfo.InvariantCulture) + " of " + ReadUInt16LittleEndian(header, 42).ToString(CultureInfo.InvariantCulture));
            Add(section, "Compression chunk size", FormatBytes(ReadUInt32LittleEndian(header, 20)));
            Add(section, "Flags", WindowsImageFlags(flags));
            Add(section, "Common use", string.Equals(Path.GetExtension(path), ".esd", StringComparison.OrdinalIgnoreCase)
                ? "Electronic Software Download image used by Windows setup and recovery media."
                : "Windows Imaging Format image used by Windows setup, deployment, and recovery media.");
            Add(section, "Notes", "Header-level WIM/ESD summary only; image names and package details live in internal XML beyond this lightweight parser.");
        }

        private static string WindowsImageFlags(uint flags)
        {
            var parts = new List<string>();
            if ((flags & 0x00000002) != 0) parts.Add("compressed");
            if ((flags & 0x00020000) != 0) parts.Add("XPRESS compression");
            if ((flags & 0x00040000) != 0) parts.Add("LZX compression");
            if ((flags & 0x00080000) != 0) parts.Add("LZMS/ESD-style compression");
            if ((flags & 0x00000020) != 0) parts.Add("spanned");
            if ((flags & 0x00000080) != 0) parts.Add("read-only");
            if (parts.Count == 0) parts.Add("none decoded");
            parts.Add("0x" + flags.ToString("X8", CultureInfo.InvariantCulture));
            return string.Join(", ", parts.ToArray());
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

        private static void AddNeroImageInfo(List<ReportSection> sections, string path, long fileLength)
        {
            if (!string.Equals(Path.GetExtension(path), ".nrg", StringComparison.OrdinalIgnoreCase))
                return;

            var section = AddSection(sections, "Nero disc image");
            Add(section, "Format hint", "Nero Burning ROM disc image");
            Add(section, "File size", FormatBytes(fileLength) + " (" + fileLength.ToString(CultureInfo.InvariantCulture) + " bytes)");

            var suffix = ReadSuffix(path, 4096);
            var marker = FindNeroFooter(suffix);
            if (marker.Marker == null)
            {
                Add(section, "Footer marker", "Not found in final 4 KiB sample");
                Add(section, "Notes", "NRG stores its table of contents near the end of the file. This file may still be an NRG image, but the sampled trailer did not expose the marker.");
                return;
            }

            Add(section, "Footer marker", marker.Marker);
            Add(section, "Chunk table offset", "0x" + marker.Offset.ToString("X", CultureInfo.InvariantCulture));
            var chunkNames = ReadNeroFooterChunkNames(path, marker.Offset);
            if (chunkNames.Count > 0)
                Add(section, "Visible trailer chunks", string.Join("\r\n", chunkNames.ToArray()));
            Add(section, "Notes", "NRG support is trailer-level identification. FileDentify reports the Nero marker and visible chunk names without extracting tracks.");
        }

        private static NeroFooter FindNeroFooter(byte[] suffix)
        {
            for (var i = suffix.Length - 12; i >= 0; i--)
            {
                if (i + 12 <= suffix.Length && Encoding.ASCII.GetString(suffix, i, 4) == "NER5")
                    return new NeroFooter { Marker = "NER5", Offset = ReadUInt64BigEndian(suffix, i + 4) };
                if (i + 8 <= suffix.Length && Encoding.ASCII.GetString(suffix, i, 4) == "NERO")
                    return new NeroFooter { Marker = "NERO", Offset = ReadUInt32BigEndian(suffix, i + 4) };
            }
            return new NeroFooter();
        }

        private static List<string> ReadNeroFooterChunkNames(string path, ulong offset)
        {
            var names = new List<string>();
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (offset >= (ulong)fs.Length)
                        return names;
                    fs.Position = (long)offset;
                    var data = new byte[(int)Math.Min(65536, fs.Length - fs.Position)];
                    var read = fs.Read(data, 0, data.Length);
                    if (read != data.Length)
                        Array.Resize(ref data, read);
                    var pos = 0;
                    while (pos + 8 <= data.Length && names.Count < 40)
                    {
                        var id = Encoding.ASCII.GetString(data, pos, 4);
                        if (!id.All(ch => ch >= 32 && ch < 127))
                            break;
                        var size = ReadUInt32BigEndian(data, pos + 4);
                        names.Add(id + " (" + FormatBytes(size) + ")");
                        var next = pos + 8L + size;
                        if (next <= pos || next > data.Length)
                            break;
                        pos = (int)next;
                    }
                }
            }
            catch (Exception ex)
            {
                names.Add("Read note: " + ex.Message);
            }
            return names;
        }

        private struct NeroFooter
        {
            public string Marker;
            public ulong Offset;
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
            if (ascii.IndexOf("UJAM", StringComparison.OrdinalIgnoreCase) >= 0)
                Add(section, "Family hint", "UJAM-readable marker found in header sample.");
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

        private static void AddRiffInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
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

            if (form == "RMID")
                AddRmidInfo(sections, header);
            if (form == "sfbk")
                AddSoundFontInfo(sections, path, header, fileLength);
            if (form == "DLS ")
                AddDlsInfo(sections, header);
        }

        private static void AddRmidInfo(List<ReportSection> sections, byte[] header)
        {
            var section = AddSection(sections, "RIFF MIDI");
            Add(section, "Format", "RIFF RMID wrapper containing Standard MIDI data");
            var dataOffset = FindRiffChunkDataOffset(header, "data");
            if (dataOffset < 0)
            {
                Add(section, "MIDI payload", "data chunk not found in sampled RIFF header");
                return;
            }
            Add(section, "MIDI payload offset", "0x" + dataOffset.ToString("X", CultureInfo.InvariantCulture));
            if (dataOffset + 14 <= header.Length && Encoding.ASCII.GetString(header, dataOffset, 4) == "MThd")
            {
                var headerLength = ReadUInt32BigEndian(header, dataOffset + 4);
                var format = ReadUInt16BigEndian(header, dataOffset + 8);
                var tracks = ReadUInt16BigEndian(header, dataOffset + 10);
                var division = ReadUInt16BigEndian(header, dataOffset + 12);
                Add(section, "MIDI header length", headerLength.ToString(CultureInfo.InvariantCulture));
                Add(section, "MIDI format", MidiFormatName(format) + " (" + format.ToString(CultureInfo.InvariantCulture) + ")");
                Add(section, "Declared track count", tracks.ToString(CultureInfo.InvariantCulture));
                Add(section, "Timing division", MidiDivisionDescription(division));
            }
        }

        private static int FindRiffChunkDataOffset(byte[] header, string chunkId)
        {
            var offset = 12;
            while (offset + 8 <= header.Length)
            {
                var id = Encoding.ASCII.GetString(header, offset, 4);
                var size = BitConverter.ToUInt32(header, offset + 4);
                if (id == chunkId)
                    return offset + 8;
                var next = offset + 8L + size + (size % 2);
                if (next <= offset || next > int.MaxValue)
                    break;
                offset = (int)next;
            }
            return -1;
        }

        private static void AddSoundFontInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var section = AddSection(sections, "SoundFont / SBK");
            Add(section, "Format", string.Equals(Path.GetExtension(path), ".sbk", StringComparison.OrdinalIgnoreCase) ? "Sound Blaster / E-mu SBK sound bank" : "SoundFont sound bank");

            var metadata = ReadSoundFontInfoList(header);
            if (metadata.Count > 0)
            {
                foreach (var pair in metadata)
                    Add(section, pair.Key, pair.Value);
            }

            var countSource = header;
            if (fileLength > header.Length)
            {
                var suffix = ReadSuffix(path, 1024 * 1024);
                countSource = CombineSamples(header, suffix);
            }

            AddSoundFontCount(section, countSource, "phdr", "Preset headers", 38, true);
            AddSoundFontCount(section, countSource, "inst", "Instrument headers", 22, true);
            AddSoundFontCount(section, countSource, "shdr", "Sample headers", 46, true);

            var visibleNames = ReadSoundFontNames(countSource, "phdr", 38, 20, 20);
            if (visibleNames.Count > 0)
                Add(section, "Visible preset names", string.Join("\r\n", visibleNames.ToArray()));

            Add(section, "Parsing note", "Counts are read from visible SF2 header chunks. Very large banks may keep some metadata beyond the sampled ranges.");
        }

        private static void AddDlsInfo(List<ReportSection> sections, byte[] header)
        {
            var section = AddSection(sections, "DLS instrument bank");
            Add(section, "Format", "Downloadable Sounds (DLS) instrument bank");

            var metadata = ReadSoundFontInfoList(header);
            foreach (var pair in metadata)
                Add(section, pair.Key, pair.Value);

            AddDlsChunkValue(section, header, "colh", "Collection instrument count");
            AddDlsVersion(section, header);
            var instrumentChunks = CountAsciiOccurrences(header, "ins ");
            var regionChunks = CountAsciiOccurrences(header, "rgn ") + CountAsciiOccurrences(header, "rgn2");
            var waveSampleChunks = CountAsciiOccurrences(header, "wsmp");
            if (instrumentChunks > 0)
                Add(section, "Visible instrument chunks", instrumentChunks.ToString(CultureInfo.InvariantCulture));
            if (regionChunks > 0)
                Add(section, "Visible region chunks", regionChunks.ToString(CultureInfo.InvariantCulture));
            if (waveSampleChunks > 0)
                Add(section, "Visible wave-sample chunks", waveSampleChunks.ToString(CultureInfo.InvariantCulture));
            Add(section, "Parsing note", "Counts are based on DLS chunks visible in the sampled header. Large banks may contain more instruments or regions later in the file.");
        }

        private static void AddDlsChunkValue(ReportSection section, byte[] data, string chunkId, string title)
        {
            var offset = IndexOfAscii(data, chunkId);
            if (offset < 0 || offset + 12 > data.Length)
                return;
            var size = BitConverter.ToUInt32(data, offset + 4);
            if (size >= 4)
                Add(section, title, ReadUInt32LittleEndian(data, offset + 8).ToString(CultureInfo.InvariantCulture));
        }

        private static void AddDlsVersion(ReportSection section, byte[] data)
        {
            var offset = IndexOfAscii(data, "vers");
            if (offset < 0 || offset + 16 > data.Length)
                return;
            var size = BitConverter.ToUInt32(data, offset + 4);
            if (size < 8)
                return;
            var versionMs = ReadUInt32LittleEndian(data, offset + 8);
            var versionLs = ReadUInt32LittleEndian(data, offset + 12);
            var major = (versionMs >> 16) & 0xffff;
            var minor = versionMs & 0xffff;
            Add(section, "DLS version", major.ToString(CultureInfo.InvariantCulture) + "." + minor.ToString(CultureInfo.InvariantCulture));
            if (versionLs != 0)
                Add(section, "DLS revision value", "0x" + versionLs.ToString("X8", CultureInfo.InvariantCulture));
        }

        private static int CountAsciiOccurrences(byte[] data, string text)
        {
            var count = 0;
            var offset = 0;
            while (offset >= 0 && offset < data.Length)
            {
                offset = IndexOfAsciiFrom(data, text, offset);
                if (offset < 0)
                    break;
                count++;
                offset += text.Length;
            }
            return count;
        }

        private static byte[] CombineSamples(byte[] first, byte[] second)
        {
            if (first == null || first.Length == 0)
                return second ?? new byte[0];
            if (second == null || second.Length == 0)
                return first;

            var combined = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, combined, 0, first.Length);
            Buffer.BlockCopy(second, 0, combined, first.Length, second.Length);
            return combined;
        }

        private static Dictionary<string, string> ReadSoundFontInfoList(byte[] data)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var infoOffset = FindSoundFontListType(data, "INFO");
            if (infoOffset < 0)
                return values;

            var offset = infoOffset + 4;
            var end = Math.Min(data.Length, infoOffset + 65536);
            while (offset + 8 <= end)
            {
                var id = Encoding.ASCII.GetString(data, offset, 4);
                var size = BitConverter.ToUInt32(data, offset + 4);
                var valueOffset = offset + 8;
                if (!IsSoundFontInfoId(id) || valueOffset + size > data.Length)
                    break;

                var value = (id == "ifil" || id == "IVER") && size >= 4
                    ? ReadUInt16LittleEndian(data, valueOffset).ToString(CultureInfo.InvariantCulture) + "." + ReadUInt16LittleEndian(data, valueOffset + 2).ToString(CultureInfo.InvariantCulture)
                    : Encoding.GetEncoding(28591)
                        .GetString(data, valueOffset, (int)Math.Min(size, 512))
                        .TrimEnd('\0', ' ', '\r', '\n', '\t');
                if (!string.IsNullOrWhiteSpace(value))
                    values[SoundFontInfoName(id)] = value;

                var next = valueOffset + (long)size + (size % 2);
                if (next <= offset || next > int.MaxValue)
                    break;
                offset = (int)next;
            }

            return values;
        }

        private static bool IsSoundFontInfoId(string id)
        {
            switch (id)
            {
                case "ifil":
                case "isng":
                case "INAM":
                case "IROM":
                case "IVER":
                case "ICRD":
                case "IENG":
                case "IPRD":
                case "ICOP":
                case "ICMT":
                case "ISFT":
                    return true;
                default:
                    return false;
            }
        }

        private static string SoundFontInfoName(string id)
        {
            switch (id)
            {
                case "ifil": return "SF2 version";
                case "isng": return "Target sound engine";
                case "INAM": return "Bank name";
                case "IROM": return "ROM name";
                case "IVER": return "ROM version";
                case "ICRD": return "Created";
                case "IENG": return "Engineer";
                case "IPRD": return "Product";
                case "ICOP": return "Copyright";
                case "ICMT": return "Comment";
                case "ISFT": return "Software";
                default: return id;
            }
        }

        private static void AddSoundFontCount(ReportSection section, byte[] data, string chunkId, string title, int recordSize, bool subtractTerminalRecord)
        {
            var offset = FindSoundFontPdtaChunk(data, chunkId, recordSize);
            if (offset < 0 || offset + 8 > data.Length)
                return;

            var size = BitConverter.ToUInt32(data, offset + 4);
            var records = (int)(size / (uint)recordSize);
            if (subtractTerminalRecord && records > 0)
                records--;
            Add(section, title, records.ToString(CultureInfo.InvariantCulture));
        }

        private static List<string> ReadSoundFontNames(byte[] data, string chunkId, int recordSize, int nameLength, int maxNames)
        {
            var names = new List<string>();
            var offset = FindSoundFontPdtaChunk(data, chunkId, recordSize);
            if (offset < 0 || offset + 8 > data.Length)
                return names;

            var size = BitConverter.ToUInt32(data, offset + 4);
            var valueOffset = offset + 8;
            var records = (int)(size / (uint)recordSize);
            for (var i = 0; i < records - 1 && names.Count < maxNames; i++)
            {
                var recordOffset = valueOffset + i * recordSize;
                if (recordOffset + nameLength > data.Length)
                    break;
                var name = ReadPrintableAsciiField(data, recordOffset, nameLength);
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }
            return names;
        }

        private static string ReadPrintableAsciiField(byte[] data, int offset, int count)
        {
            var sb = new StringBuilder();
            var end = Math.Min(data.Length, offset + count);
            for (var i = offset; i < end; i++)
            {
                var b = data[i];
                if (b == 0)
                    break;
                if (b < 32 || b >= 127)
                    break;
                sb.Append((char)b);
            }
            return sb.ToString().Trim();
        }

        private static int FindSoundFontListType(byte[] data, string listType)
        {
            var offset = 0;
            while (offset >= 0 && offset + 12 <= data.Length)
            {
                offset = IndexOfAsciiFrom(data, "LIST", offset);
                if (offset < 0 || offset + 12 > data.Length)
                    return -1;
                if (Encoding.ASCII.GetString(data, offset + 8, 4) == listType)
                    return offset + 8;
                offset += 4;
            }
            return -1;
        }

        private static int FindSoundFontPdtaChunk(byte[] data, string chunkId, int recordSize)
        {
            var pdtaOffset = FindSoundFontListType(data, "pdta");
            if (pdtaOffset < 0)
                return -1;

            var offset = pdtaOffset + 4;
            while (offset >= 0 && offset + 8 <= data.Length)
            {
                offset = IndexOfAsciiFrom(data, chunkId, offset);
                if (offset < 0 || offset + 8 > data.Length)
                    return -1;

                var size = BitConverter.ToUInt32(data, offset + 4);
                if (size > 0 && size % recordSize == 0 && offset + 8L + size <= data.Length)
                    return offset;

                offset += 4;
            }

            return -1;
        }

        private static int IndexOfAsciiFrom(byte[] data, string text, int start)
        {
            if (data == null || string.IsNullOrEmpty(text))
                return -1;
            var needle = Encoding.ASCII.GetBytes(text);
            for (var i = Math.Max(0, start); i <= data.Length - needle.Length; i++)
            {
                var found = true;
                for (var j = 0; j < needle.Length; j++)
                {
                    if (data[i + j] != needle[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return i;
            }
            return -1;
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

