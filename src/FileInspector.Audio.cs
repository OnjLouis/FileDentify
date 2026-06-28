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
    {        private static void AddAudioHeaderInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            if (header.Length >= 4 && StartsWith(header, Encoding.ASCII.GetBytes("fLaC")))
                AddFlacHeaderInfo(sections, header);
            if (header.Length >= 4 && StartsWith(header, Encoding.ASCII.GetBytes("OggS")))
                AddOggHeaderInfo(sections, header);
            if (header.Length >= 10 && StartsWith(header, Encoding.ASCII.GetBytes("ID3")))
                AddId3HeaderInfo(sections, path, header, fileLength);
        }

        private static void AddFlacHeaderInfo(List<ReportSection> sections, byte[] header)
        {
            var section = AddSection(sections, "FLAC");
            var offset = 4;
            var blocks = new List<string>();
            while (offset + 4 <= header.Length && blocks.Count < 12)
            {
                var blockHeader = header[offset];
                var last = (blockHeader & 0x80) != 0;
                var type = blockHeader & 0x7f;
                var length = (header[offset + 1] << 16) | (header[offset + 2] << 8) | header[offset + 3];
                blocks.Add(FlacBlockName(type) + " (" + length.ToString(CultureInfo.InvariantCulture) + " bytes)" + (last ? ", last" : ""));
                if (type == 0 && length >= 34 && offset + 4 + 18 <= header.Length)
                {
                    var p = offset + 4 + 10;
                    var sampleRate = (header[p] << 12) | (header[p + 1] << 4) | ((header[p + 2] & 0xf0) >> 4);
                    var channels = ((header[p + 2] & 0x0e) >> 1) + 1;
                    var bits = (((header[p + 2] & 0x01) << 4) | ((header[p + 3] & 0xf0) >> 4)) + 1;
                    var totalSamples =
                        ((long)(header[p + 3] & 0x0f) << 32) |
                        ((long)header[p + 4] << 24) |
                        ((long)header[p + 5] << 16) |
                        ((long)header[p + 6] << 8) |
                        header[p + 7];
                    Add(section, "Sample rate", sampleRate.ToString(CultureInfo.InvariantCulture) + " Hz");
                    Add(section, "Channels", channels.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Bits per sample", bits.ToString(CultureInfo.InvariantCulture));
                    if (sampleRate > 0 && totalSamples > 0)
                        Add(section, "Duration", FormatDuration((double)totalSamples / sampleRate));
                }
                else if (type == 4 && offset + 4 + length <= header.Length)
                {
                    var comments = ParseVorbisComments(header, offset + 4, length);
                    if (comments.Count > 0)
                        Add(section, "Tags", string.Join("\r\n", comments.ToArray()));
                }
                offset += 4 + length;
                if (last)
                    break;
            }
            Add(section, "Metadata blocks", string.Join("\r\n", blocks.ToArray()));
        }

        private static string FlacBlockName(int type)
        {
            switch (type)
            {
                case 0: return "STREAMINFO";
                case 1: return "PADDING";
                case 2: return "APPLICATION";
                case 3: return "SEEKTABLE";
                case 4: return "VORBIS_COMMENT";
                case 5: return "CUESHEET";
                case 6: return "PICTURE";
                default: return "Block type " + type;
            }
        }

        private static void AddOggHeaderInfo(List<ReportSection> sections, byte[] header)
        {
            if (header.Length < 27)
                return;
            var section = AddSection(sections, "Ogg");
            Add(section, "Stream structure version", header[4].ToString(CultureInfo.InvariantCulture));
            Add(section, "Header type flags", "0x" + header[5].ToString("X2", CultureInfo.InvariantCulture));
            Add(section, "Serial number", BitConverter.ToUInt32(header, 14).ToString(CultureInfo.InvariantCulture));
            Add(section, "Page sequence", BitConverter.ToUInt32(header, 18).ToString(CultureInfo.InvariantCulture));
            Add(section, "Page segments", header[26].ToString(CultureInfo.InvariantCulture));
            var sample = Encoding.ASCII.GetString(header.Take(Math.Min(header.Length, 4096)).ToArray());
            if (sample.Contains("OpusHead")) Add(section, "Codec hint", "Opus");
            else if (sample.Contains("vorbis")) Add(section, "Codec hint", "Vorbis");
            else if (sample.Contains("Speex")) Add(section, "Codec hint", "Speex");
        }

        private static void AddId3HeaderInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var section = AddSection(sections, "ID3");
            var major = header[3];
            Add(section, "Version", "ID3v2." + major.ToString(CultureInfo.InvariantCulture) + "." + header[4].ToString(CultureInfo.InvariantCulture));
            Add(section, "Flags", "0x" + header[5].ToString("X2", CultureInfo.InvariantCulture));
            var size = ((header[6] & 0x7f) << 21) | ((header[7] & 0x7f) << 14) | ((header[8] & 0x7f) << 7) | (header[9] & 0x7f);
            Add(section, "Tag size", FormatBytes(size) + " (" + size.ToString(CultureInfo.InvariantCulture) + " bytes)");
            if ((header[5] & 0x80) != 0)
                Add(section, "Unsynchronisation", "Flag is set. Some frame values may be skipped.");

            var frames = ParseId3Frames(header, major, size);
            foreach (var frame in frames)
                Add(section, frame.Key, frame.Value);

            AddMp3FrameEstimate(section, path, header, fileLength, 10 + size);
        }

        private static List<KeyValuePair<string, string>> ParseId3Frames(byte[] header, int major, int tagSize)
        {
            var fields = new List<KeyValuePair<string, string>>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var limit = Math.Min(header.Length, 10 + tagSize);
            var offset = 10;
            while (offset + 10 <= limit && fields.Count < 24)
            {
                var id = Encoding.ASCII.GetString(header, offset, 4);
                if (!Regex.IsMatch(id, "^[A-Z0-9]{4}$"))
                    break;
                var frameSize = major == 4 ? ReadSyncSafeInt(header, offset + 4) : (int)ReadUInt32BigEndian(header, offset + 4);
                if (frameSize <= 0 || offset + 10L + frameSize > limit)
                    break;
                var frameData = new byte[frameSize];
                Buffer.BlockCopy(header, offset + 10, frameData, 0, frameSize);
                var label = Id3FrameLabel(id);
                var value = DecodeId3Frame(id, frameData);
                if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(value) && seen.Add(label))
                    fields.Add(new KeyValuePair<string, string>(label, value));
                offset += 10 + frameSize;
            }
            return fields;
        }

        private static string Id3FrameLabel(string id)
        {
            switch (id)
            {
                case "TIT2": return "Title";
                case "TPE1": return "Artist";
                case "TPE2": return "Album artist";
                case "TALB": return "Album";
                case "TRCK": return "Track";
                case "TPOS": return "Disc";
                case "TDRC":
                case "TYER": return "Date";
                case "TCON": return "Genre";
                case "TCOM": return "Composer";
                case "COMM": return "Comment";
                default:
                    if (id.StartsWith("T", StringComparison.Ordinal) && id != "TXXX")
                        return id;
                    return string.Empty;
            }
        }

        private static string DecodeId3Frame(string id, byte[] data)
        {
            if (data.Length == 0)
                return string.Empty;
            if (id == "COMM" && data.Length > 4)
            {
                var text = DecodeId3EncodedText(data[0], data, 4, data.Length - 4);
                var parts = SplitTextFields(text);
                return parts.Count == 0 ? text : parts[parts.Count - 1];
            }
            if (id.StartsWith("T", StringComparison.Ordinal) && data.Length > 1)
                return string.Join("; ", SplitTextFields(DecodeId3EncodedText(data[0], data, 1, data.Length - 1)).ToArray());
            return string.Empty;
        }

        private static string DecodeId3EncodedText(byte encoding, byte[] data, int offset, int count)
        {
            if (offset < 0 || count <= 0 || offset >= data.Length)
                return string.Empty;
            count = Math.Min(count, data.Length - offset);
            string text;
            switch (encoding)
            {
                case 1: text = Encoding.Unicode.GetString(data, offset, count); break;
                case 2: text = Encoding.BigEndianUnicode.GetString(data, offset, count); break;
                case 3: text = Encoding.UTF8.GetString(data, offset, count); break;
                default: text = Encoding.GetEncoding(28591).GetString(data, offset, count); break;
            }
            return CleanMetadataText(text);
        }

        private static List<string> SplitTextFields(string text)
        {
            return CleanMetadataText(text)
                .Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => v.Length > 0)
                .Take(12)
                .ToList();
        }

        private static List<string> ParseVorbisComments(byte[] data, int offset, int length)
        {
            var comments = new List<string>();
            var end = Math.Min(data.Length, offset + length);
            if (offset + 8 > end)
                return comments;
            var vendorLength = BitConverter.ToInt32(data, offset);
            offset += 4;
            if (vendorLength < 0 || offset + vendorLength + 4 > end)
                return comments;
            var vendor = CleanMetadataText(Encoding.UTF8.GetString(data, offset, vendorLength));
            offset += vendorLength;
            if (!string.IsNullOrWhiteSpace(vendor))
                comments.Add("Vendor: " + vendor);
            var count = BitConverter.ToInt32(data, offset);
            offset += 4;
            for (var i = 0; i < count && i < 24 && offset + 4 <= end; i++)
            {
                var itemLength = BitConverter.ToInt32(data, offset);
                offset += 4;
                if (itemLength < 0 || offset + itemLength > end)
                    break;
                var value = CleanMetadataText(Encoding.UTF8.GetString(data, offset, itemLength));
                offset += itemLength;
                if (!string.IsNullOrWhiteSpace(value))
                    comments.Add(value);
            }
            return comments;
        }

        private static void AddMp3FrameEstimate(ReportSection section, string path, byte[] header, long fileLength, int startOffset)
        {
            for (var i = Math.Max(0, startOffset); i + 4 <= header.Length && i < startOffset + 8192; i++)
            {
                if (header[i] != 0xff || (header[i + 1] & 0xe0) != 0xe0)
                    continue;
                var bitrate = Mp3BitrateKbps(header[i + 1], header[i + 2]);
                var sampleRate = Mp3SampleRate(header[i + 1], header[i + 2]);
                if (bitrate <= 0 || sampleRate <= 0)
                    continue;
                Add(section, "First MPEG audio frame", "Offset " + i.ToString(CultureInfo.InvariantCulture));
                Add(section, "MPEG bitrate", bitrate.ToString(CultureInfo.InvariantCulture) + " kbps");
                Add(section, "MPEG sample rate", sampleRate.ToString(CultureInfo.InvariantCulture) + " Hz");
                var audioBytes = Math.Max(0, fileLength - i);
                if (audioBytes > 0)
                    Add(section, "Estimated duration", FormatDuration((audioBytes * 8.0) / (bitrate * 1000.0)) + " (from first constant-bitrate frame)");
                return;
            }
        }

        private static int Mp3BitrateKbps(byte versionLayer, byte bitrateSample)
        {
            var versionBits = (versionLayer >> 3) & 0x03;
            var layerBits = (versionLayer >> 1) & 0x03;
            var index = (bitrateSample >> 4) & 0x0f;
            if (versionBits == 1 || layerBits == 0 || index == 0 || index == 15)
                return 0;
            int[] table;
            if (versionBits == 3 && layerBits == 3) table = new[] { 0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448 };
            else if (versionBits == 3 && layerBits == 2) table = new[] { 0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384 };
            else if (versionBits == 3) table = new[] { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320 };
            else if (layerBits == 3) table = new[] { 0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256 };
            else table = new[] { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160 };
            return table[index];
        }

        private static int Mp3SampleRate(byte versionLayer, byte bitrateSample)
        {
            var versionBits = (versionLayer >> 3) & 0x03;
            var index = (bitrateSample >> 2) & 0x03;
            if (versionBits == 1 || index == 3)
                return 0;
            var baseRates = new[] { 44100, 48000, 32000 };
            if (versionBits == 3) return baseRates[index];
            if (versionBits == 2) return baseRates[index] / 2;
            return baseRates[index] / 4;
        }

        private static int ReadSyncSafeInt(byte[] data, int offset)
        {
            return ((data[offset] & 0x7f) << 21) | ((data[offset + 1] & 0x7f) << 14) | ((data[offset + 2] & 0x7f) << 7) | (data[offset + 3] & 0x7f);
        }

        private static string CleanMetadataText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            text = text.Replace("\ufeff", string.Empty).Replace("\0", "\n");
            text = Regex.Replace(text, "[\r\n\t ]+", " ").Trim();
            return text.Length > 500 ? text.Substring(0, 500) + "..." : text;
        }

        private static string FormatDuration(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0)
                return string.Empty;
            var span = TimeSpan.FromSeconds(seconds);
            return span.TotalHours >= 1
                ? span.ToString(@"h\:mm\:ss\.fff", CultureInfo.InvariantCulture)
                : span.ToString(@"m\:ss\.fff", CultureInfo.InvariantCulture);
        }
    }
}

