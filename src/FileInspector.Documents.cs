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
    {        private static void AddDmgInfo(List<ReportSection> sections, string path, long length)
        {
            if (!string.Equals(Path.GetExtension(path), ".dmg", StringComparison.OrdinalIgnoreCase))
                return;

            var section = AddSection(sections, "Apple disk image");
            Add(section, "Extension hint", "DMG / Apple disk image");
            var suffix = ReadSuffix(path, 8192);
            var offset = IndexOfAscii(suffix, "koly");
            if (offset < 0)
            {
                Add(section, "UDIF trailer", "Not found in the final 8192 bytes.");
                return;
            }

            Add(section, "UDIF trailer", "Found");
            Add(section, "Trailer offset from file end", (suffix.Length - offset).ToString(CultureInfo.InvariantCulture) + " bytes before end");
            if (offset + 512 <= suffix.Length)
            {
                Add(section, "UDIF version", ReadUInt32BigEndian(suffix, offset + 4).ToString(CultureInfo.InvariantCulture));
                Add(section, "UDIF header size", ReadUInt32BigEndian(suffix, offset + 8).ToString(CultureInfo.InvariantCulture) + " bytes");
                Add(section, "Trailer preview", HexPreview(suffix.Skip(offset).Take(64).ToArray(), 64));
            }
        }

        private static void AddVersionInfo(List<ReportSection> sections, string path)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                var items = new[]
                {
                    new { Name = "Description", Value = info.FileDescription },
                    new { Name = "Product", Value = info.ProductName },
                    new { Name = "Company", Value = info.CompanyName },
                    new { Name = "File version", Value = info.FileVersion },
                    new { Name = "Product version", Value = info.ProductVersion },
                    new { Name = "Copyright", Value = info.LegalCopyright },
                    new { Name = "Original filename", Value = info.OriginalFilename },
                    new { Name = "Internal name", Value = info.InternalName },
                    new { Name = "Language", Value = info.Language }
                }.Where(i => !string.IsNullOrWhiteSpace(i.Value)).ToArray();

                if (items.Length == 0)
                    return;

                var section = AddSection(sections, "Version information");
                foreach (var item in items)
                    Add(section, item.Name, item.Value);
            }
            catch
            {
            }
        }

        private static void AddImageInfo(List<ReportSection> sections, byte[] header)
        {
            try
            {
                if (header.Length >= 24 && StartsWith(header, new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0d, 0x0a, 0x1a, 0x0a }))
                {
                    var section = AddSection(sections, "Image");
                    Add(section, "Format", "PNG");
                    Add(section, "Dimensions", ReadUInt32BigEndian(header, 16) + " x " + ReadUInt32BigEndian(header, 20));
                }
                else if (header.Length >= 10 && (StartsWith(header, Encoding.ASCII.GetBytes("GIF87a")) || StartsWith(header, Encoding.ASCII.GetBytes("GIF89a"))))
                {
                    var section = AddSection(sections, "Image");
                    Add(section, "Format", "GIF");
                    Add(section, "Dimensions", BitConverter.ToUInt16(header, 6) + " x " + BitConverter.ToUInt16(header, 8));
                }
                else if (header.Length >= 26 && header[0] == 'B' && header[1] == 'M')
                {
                    var section = AddSection(sections, "Image");
                    Add(section, "Format", "BMP");
                    Add(section, "Dimensions", BitConverter.ToInt32(header, 18) + " x " + Math.Abs(BitConverter.ToInt32(header, 22)));
                    Add(section, "Bits per pixel", BitConverter.ToUInt16(header, 28).ToString(CultureInfo.InvariantCulture));
                }
                else if (header.Length >= 4 && header[0] == 0xff && header[1] == 0xd8)
                {
                    var dimensions = TryReadJpegDimensions(header);
                    if (dimensions != null)
                    {
                        var section = AddSection(sections, "Image");
                        Add(section, "Format", "JPEG");
                        Add(section, "Dimensions", dimensions);
                    }
                }
            }
            catch
            {
            }
        }

        private static uint ReadUInt32BigEndian(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];
        }

        private static ulong ReadUInt64BigEndian(byte[] data, int offset)
        {
            return ((ulong)data[offset] << 56) | ((ulong)data[offset + 1] << 48) | ((ulong)data[offset + 2] << 40) | ((ulong)data[offset + 3] << 32) |
                ((ulong)data[offset + 4] << 24) | ((ulong)data[offset + 5] << 16) | ((ulong)data[offset + 6] << 8) | data[offset + 7];
        }

        private static ushort ReadUInt16BigEndian(byte[] data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        private static ushort ReadUInt16LittleEndian(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static uint ReadUInt32LittleEndian(byte[] data, int offset)
        {
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

        private static ulong ReadUInt64LittleEndian(byte[] data, int offset)
        {
            return (ulong)data[offset] |
                ((ulong)data[offset + 1] << 8) |
                ((ulong)data[offset + 2] << 16) |
                ((ulong)data[offset + 3] << 24) |
                ((ulong)data[offset + 4] << 32) |
                ((ulong)data[offset + 5] << 40) |
                ((ulong)data[offset + 6] << 48) |
                ((ulong)data[offset + 7] << 56);
        }

        private static double ReadIeeeExtended80(byte[] data, int offset)
        {
            if (offset + 10 > data.Length)
                return 0;
            var exponent = ((data[offset] & 0x7f) << 8) | data[offset + 1];
            var sign = (data[offset] & 0x80) == 0 ? 1 : -1;
            if (exponent == 0)
                return 0;
            var mantissa = ReadUInt64BigEndian(data, offset + 2);
            return sign * (double)mantissa * Math.Pow(2, exponent - 16383 - 63);
        }

        private static string TryReadJpegDimensions(byte[] data)
        {
            var i = 2;
            while (i + 9 < data.Length)
            {
                if (data[i] != 0xff)
                {
                    i++;
                    continue;
                }
                while (i < data.Length && data[i] == 0xff)
                    i++;
                if (i >= data.Length)
                    return null;
                var marker = data[i++];
                if (marker == 0xd8 || marker == 0xd9 || marker == 0x01)
                    continue;
                if (i + 1 >= data.Length)
                    return null;
                var length = ReadUInt16BigEndian(data, i);
                if (length < 2 || i + length > data.Length)
                    return null;
                if ((marker >= 0xc0 && marker <= 0xc3) || (marker >= 0xc5 && marker <= 0xc7) || (marker >= 0xc9 && marker <= 0xcb) || (marker >= 0xcd && marker <= 0xcf))
                {
                    var height = ReadUInt16BigEndian(data, i + 3);
                    var width = ReadUInt16BigEndian(data, i + 5);
                    return width + " x " + height;
                }
                i += length;
            }
            return null;
        }

        private static void AddPdfInfo(List<ReportSection> sections, string path, byte[] header, long length)
        {
            if (header.Length < 5 || !StartsWith(header, Encoding.ASCII.GetBytes("%PDF-")))
                return;
            var section = AddSection(sections, "PDF");
            var firstLineEnd = Array.IndexOf(header, (byte)0x0a);
            if (firstLineEnd < 0 || firstLineEnd > 32)
                firstLineEnd = Math.Min(header.Length, 32);
            Add(section, "Header", Encoding.ASCII.GetString(header, 0, firstLineEnd).Trim());
            var text = Encoding.ASCII.GetString(header.Take(Math.Min(header.Length, 65536)).ToArray());
            Add(section, "Linearized hint", text.IndexOf("/Linearized", StringComparison.OrdinalIgnoreCase) >= 0 ? "Present" : "Not found in first sample");
            Add(section, "Encryption hint", text.IndexOf("/Encrypt", StringComparison.OrdinalIgnoreCase) >= 0 ? "Present" : "Not found in first sample");
            AddPdfMetadata(section, path, header, length);
        }

        private static void AddPdfMetadata(ReportSection section, string path, byte[] header, long length)
        {
            var sample = PdfMetadataSample(path, header, length);
            var fields = ExtractPdfMetadataFields(sample);
            if (fields.Count == 0)
            {
                Add(section, "Metadata", "No PDF Info or XMP metadata fields were found in the sampled data.");
                return;
            }

            foreach (var item in fields)
                Add(section, item.Key, item.Value);
        }

        private static string PdfMetadataSample(string path, byte[] header, long length)
        {
            const int metadataReadSize = 4 * 1024 * 1024;
            byte[] prefix = header.Length >= metadataReadSize ? header : ReadPrefix(path, metadataReadSize);
            byte[] suffix = length > prefix.Length ? ReadSuffix(path, metadataReadSize) : new byte[0];
            if (suffix.Length == 0)
                return Encoding.GetEncoding(28591).GetString(prefix);

            var combined = new byte[prefix.Length + suffix.Length];
            Buffer.BlockCopy(prefix, 0, combined, 0, prefix.Length);
            Buffer.BlockCopy(suffix, 0, combined, prefix.Length, suffix.Length);
            return Encoding.GetEncoding(28591).GetString(combined);
        }

        private static Dictionary<string, string> ExtractPdfMetadataFields(string sample)
        {
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AddPdfInfoField(fields, sample, "Title", "Title");
            AddPdfInfoField(fields, sample, "Author", "Author");
            AddPdfInfoField(fields, sample, "Subject", "Subject");
            AddPdfInfoField(fields, sample, "Keywords", "Keywords");
            AddPdfInfoField(fields, sample, "Creator", "Creator");
            AddPdfInfoField(fields, sample, "Producer", "Producer");
            AddPdfInfoField(fields, sample, "CreationDate", "Creation date");
            AddPdfInfoField(fields, sample, "ModDate", "Modification date");
            AddPdfInfoField(fields, sample, "Trapped", "Trapped");

            AddXmpField(fields, sample, "Title", "dc:title");
            AddXmpField(fields, sample, "Author", "dc:creator");
            AddXmpField(fields, sample, "Subject", "dc:description");
            AddXmpField(fields, sample, "Creator", "xmp:CreatorTool");
            AddXmpField(fields, sample, "Producer", "pdf:Producer");
            AddXmpField(fields, sample, "Creation date", "xmp:CreateDate");
            AddXmpField(fields, sample, "Modification date", "xmp:ModifyDate");
            AddXmpField(fields, sample, "Metadata date", "xmp:MetadataDate");
            return fields;
        }

        private static void AddPdfInfoField(Dictionary<string, string> fields, string sample, string pdfName, string displayName)
        {
            if (fields.ContainsKey(displayName))
                return;
            var value = FindPdfInfoValue(sample, pdfName);
            if (string.IsNullOrWhiteSpace(value))
                return;
            if (pdfName.EndsWith("Date", StringComparison.OrdinalIgnoreCase))
                value = FormatPdfDate(value);
            fields[displayName] = value;
        }

        private static string FindPdfInfoValue(string sample, string name)
        {
            var pattern = "/" + name;
            var index = 0;
            while (index >= 0 && index < sample.Length)
            {
                index = sample.IndexOf(pattern, index, StringComparison.Ordinal);
                if (index < 0)
                    return string.Empty;
                var cursor = index + pattern.Length;
                if (cursor < sample.Length && IsPdfNameChar(sample[cursor]))
                {
                    index = cursor;
                    continue;
                }
                while (cursor < sample.Length && char.IsWhiteSpace(sample[cursor]))
                    cursor++;
                if (cursor >= sample.Length)
                    return string.Empty;
                if (sample[cursor] == '(')
                    return DecodePdfLiteralString(sample, cursor);
                if (sample[cursor] == '<' && cursor + 1 < sample.Length && sample[cursor + 1] != '<')
                    return DecodePdfHexString(sample, cursor);
                index = cursor + 1;
            }
            return string.Empty;
        }

        private static bool IsPdfNameChar(char ch)
        {
            return char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.';
        }

        private static string DecodePdfLiteralString(string sample, int start)
        {
            var sb = new StringBuilder();
            var depth = 0;
            for (var i = start; i < sample.Length; i++)
            {
                var ch = sample[i];
                if (i == start)
                {
                    depth = 1;
                    continue;
                }
                if (ch == '\\' && i + 1 < sample.Length)
                {
                    var next = sample[++i];
                    switch (next)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case '(':
                        case ')':
                        case '\\':
                            sb.Append(next);
                            break;
                        case '\r':
                            if (i + 1 < sample.Length && sample[i + 1] == '\n')
                                i++;
                            break;
                        case '\n':
                            break;
                        default:
                            if (next >= '0' && next <= '7')
                            {
                                var octal = new StringBuilder();
                                octal.Append(next);
                                for (var j = 0; j < 2 && i + 1 < sample.Length && sample[i + 1] >= '0' && sample[i + 1] <= '7'; j++)
                                    octal.Append(sample[++i]);
                                sb.Append((char)Convert.ToInt32(octal.ToString(), 8));
                            }
                            else
                            {
                                sb.Append(next);
                            }
                            break;
                    }
                    continue;
                }
                if (ch == '(')
                {
                    depth++;
                    sb.Append(ch);
                    continue;
                }
                if (ch == ')')
                {
                    depth--;
                    if (depth == 0)
                        break;
                    sb.Append(ch);
                    continue;
                }
                sb.Append(ch);
                if (sb.Length >= 4096)
                    break;
            }
            return CleanPdfMetadataValue(DecodePdfStringBytes(Encoding.GetEncoding(28591).GetBytes(sb.ToString())));
        }

        private static string DecodePdfHexString(string sample, int start)
        {
            var end = sample.IndexOf('>', start + 1);
            if (end < 0)
                return string.Empty;
            var hex = Regex.Replace(sample.Substring(start + 1, end - start - 1), "\\s+", string.Empty);
            if (hex.Length == 0)
                return string.Empty;
            if ((hex.Length % 2) != 0)
                hex += "0";
            var bytes = new byte[hex.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                byte parsed;
                if (!byte.TryParse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed))
                    return string.Empty;
                bytes[i] = parsed;
            }
            return CleanPdfMetadataValue(DecodePdfStringBytes(bytes));
        }

        private static string DecodePdfStringBytes(byte[] bytes)
        {
            if (bytes.Length >= 2)
            {
                if (bytes[0] == 0xfe && bytes[1] == 0xff)
                    return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
                if (bytes[0] == 0xff && bytes[1] == 0xfe)
                    return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            }
            return Encoding.GetEncoding(1252).GetString(bytes);
        }

        private static void AddXmpField(Dictionary<string, string> fields, string sample, string displayName, string elementName)
        {
            if (fields.ContainsKey(displayName))
                return;
            var value = FindXmpValue(sample, elementName);
            if (string.IsNullOrWhiteSpace(value))
                return;
            if (displayName.IndexOf("date", StringComparison.OrdinalIgnoreCase) >= 0)
                value = FormatPdfDate(value);
            fields[displayName] = value;
        }

        private static string FindXmpValue(string sample, string elementName)
        {
            var escaped = Regex.Escape(elementName);
            var match = Regex.Match(sample, "<" + escaped + "\\b[^>]*>(.*?)</" + escaped + ">", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success)
                return string.Empty;
            var inner = match.Groups[1].Value;
            var li = Regex.Match(inner, "<rdf:li\\b[^>]*>(.*?)</rdf:li>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (li.Success)
                inner = li.Groups[1].Value;
            inner = Regex.Replace(inner, "<[^>]+>", " ");
            return CleanPdfMetadataValue(WebUtility.HtmlDecode(inner));
        }

        private static string CleanPdfMetadataValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            value = value.Replace("\0", string.Empty);
            value = Regex.Replace(value, "\\s+", " ").Trim();
            return value.Length > 500 ? value.Substring(0, 500) + "..." : value;
        }

        private static string FormatPdfDate(string value)
        {
            value = CleanPdfMetadataValue(value);
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var raw = value.StartsWith("D:", StringComparison.Ordinal) ? value.Substring(2) : value;
            if (!value.StartsWith("D:", StringComparison.Ordinal) && raw.IndexOf('-') >= 0)
            {
                DateTimeOffset parsed;
                if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
                    return parsed.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) + " (raw: " + value + ")";
                return value;
            }
            var match = Regex.Match(raw, "^(\\d{4})(\\d{2})?(\\d{2})?(\\d{2})?(\\d{2})?(\\d{2})?([Zz]|[+\\-]\\d{2}'?\\d{2}'?)?");
            if (!match.Success)
                return value;
            var parts = new[] { "01", "01", "00", "00", "00" };
            for (var i = 2; i <= 6; i++)
                if (match.Groups[i].Success)
                    parts[i - 2] = match.Groups[i].Value;
            var formatted = match.Groups[1].Value + "-" + parts[0] + "-" + parts[1] + " " + parts[2] + ":" + parts[3] + ":" + parts[4];
            if (match.Groups[7].Success)
                formatted += " " + match.Groups[7].Value.Replace("'", string.Empty);
            return formatted + " (raw: " + value + ")";
        }

        private static void AddFontInfo(List<ReportSection> sections, byte[] header)
        {
            var format = FontFormatName(header);
            if (format == null)
                return;

            var section = AddSection(sections, "Font");
            Add(section, "Format", format);
            if (header.Length >= 12)
            {
                if (StartsWith(header, Encoding.ASCII.GetBytes("wOFF")) || StartsWith(header, Encoding.ASCII.GetBytes("wOF2")))
                {
                    Add(section, "Flavor", FontFlavorName(Encoding.ASCII.GetString(header, 4, 4)));
                    Add(section, "Declared length", FormatBytes(ReadUInt32BigEndian(header, 8)));
                    Add(section, "Table count", ReadUInt16BigEndian(header, 12).ToString(CultureInfo.InvariantCulture));
                    if (header.Length >= 20)
                        Add(section, "Uncompressed sfnt size", FormatBytes(ReadUInt32BigEndian(header, 16)));
                }
                else
                {
                    Add(section, "Table count", ReadUInt16BigEndian(header, 4).ToString(CultureInfo.InvariantCulture));
                    var tables = new List<string>();
                    for (var offset = 12; offset + 16 <= header.Length && tables.Count < 20; offset += 16)
                    {
                        var tag = Encoding.ASCII.GetString(header, offset, 4);
                        if (!IsPrintableAscii(tag))
                            break;
                        tables.Add(tag + " at 0x" + ReadUInt32BigEndian(header, offset + 8).ToString("X", CultureInfo.InvariantCulture) + ", " + FormatBytes(ReadUInt32BigEndian(header, offset + 12)));
                    }
                    if (tables.Count > 0)
                        Add(section, "Table directory", string.Join("\r\n", tables.ToArray()));
                }
            }
        }

        private static void AddOleCompoundInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (!IsOleCompoundFile(header))
                return;

            var section = AddSection(sections, "OLE compound document");
            Add(section, "Container", "Microsoft Compound File Binary Format");
            var ext = Path.GetExtension(path);
            if (string.Equals(ext, ".msi", StringComparison.OrdinalIgnoreCase))
                Add(section, "Extension hint", "Windows Installer package");
            else if (string.Equals(ext, ".doc", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".xls", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".ppt", StringComparison.OrdinalIgnoreCase))
                Add(section, "Extension hint", "Legacy Microsoft Office document");
            if (header.Length >= 30)
            {
                Add(section, "Sector shift", ReadUInt16LittleEndian(header, 30).ToString(CultureInfo.InvariantCulture));
                Add(section, "Mini-sector shift", ReadUInt16LittleEndian(header, 32).ToString(CultureInfo.InvariantCulture));
            }
            if (header.Length >= 56)
                Add(section, "Directory sector count", ReadUInt32LittleEndian(header, 40).ToString(CultureInfo.InvariantCulture));
        }

    }
}

