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
        private static string EbookTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (StartsWith(header, Encoding.ASCII.GetBytes("ITOLITLS")) || ext == ".lit") return "Microsoft Reader LIT ebook";
            if (LooksLikePalmMobi(header) || ext == ".mobi") return "Mobipocket/Kindle ebook";
            if (ext == ".prc" && LooksLikePalmDatabase(header)) return "Palm/Mobipocket resource database";
            if (ext == ".pdb" && LooksLikePalmDatabase(header)) return "Palm/eReader database ebook";
            if (LooksLikeSonyLrf(header) || ext == ".lrf") return "Sony BroadBand eBook LRF";
            if (ext == ".brf") return "Braille Ready Format text";
            if (ext == ".cbr") return "Comic Book RAR archive";
            if (ext == ".cbz") return "Comic Book ZIP archive";
            if (StartsWith(header, Encoding.ASCII.GetBytes("ITSF")) || ext == ".chm") return "Compiled HTML Help file";
            if (ext == ".hlp") return "WinHelp help file";
            return null;
        }

        private static void AddEbookInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var type = EbookTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Ebook / help file");
            Add(section, "Format hint", type);
            Add(section, "File size", FormatBytes(fileLength));

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (StartsWith(header, Encoding.ASCII.GetBytes("ITOLITLS")) || ext == ".lit")
                AddLitInfo(section, header);
            else if (LooksLikePalmDatabase(header) || ext == ".mobi" || ext == ".prc" || ext == ".pdb")
                AddPalmEbookInfo(section, header);
            else if (LooksLikeSonyLrf(header) || ext == ".lrf")
                AddSonyLrfInfo(section, header);
            else if (ext == ".brf")
                AddBrfInfo(section, header);
            else if (ext == ".cbr" || ext == ".cbz")
                AddComicBookArchiveInfo(section, path, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("ITSF")) || ext == ".chm")
                AddChmInfo(section, header);
            else if (ext == ".hlp")
                AddWinHelpInfo(section, header);

            Add(section, "Notes", "Ebook and help files are reported from headers, package markers, and visible metadata only. FileDentify does not remove DRM, render pages, or extract book text.");
        }

        private static void AddLitInfo(ReportSection section, byte[] header)
        {
            Add(section, "Header marker", StartsWith(header, Encoding.ASCII.GetBytes("ITOLITLS")) ? "ITOLITLS" : "extension-level match");
            if (header.Length >= 24)
                Add(section, "Directory count-like field", ReadUInt32LittleEndian(header, 16).ToString(CultureInfo.InvariantCulture));
            Add(section, "Container note", "Microsoft Reader .lit is a legacy ebook container. Many files may be DRM-protected.");
        }

        private static void AddPalmEbookInfo(ReportSection section, byte[] header)
        {
            if (header.Length >= 32)
                Add(section, "Database name", ReadPalmString(header, 0, 32));
            if (header.Length >= 78)
            {
                Add(section, "Database type", ReadPalmString(header, 60, 4));
                Add(section, "Creator", ReadPalmString(header, 64, 4));
                Add(section, "Record count", ReadUInt16BigEndian(header, 76).ToString(CultureInfo.InvariantCulture));
            }
            if (IndexOfAscii(header, "MOBI") >= 0)
                Add(section, "MOBI marker", "MOBI marker found in Palm database header.");
            if (IndexOfAscii(header, "BOOK") >= 0)
                Add(section, "Palm type marker", "BOOK marker found.");
        }

        private static void AddSonyLrfInfo(ReportSection section, byte[] header)
        {
            Add(section, "Header marker", LooksLikeSonyLrf(header) ? "LRF" : "extension-level match");
            if (header.Length >= 16)
                Add(section, "Version/flags-like field", "0x" + ReadUInt32LittleEndian(header, 8).ToString("X8", CultureInfo.InvariantCulture));
            Add(section, "Container note", "Sony LRF is a legacy Sony Reader ebook format.");
        }

        private static void AddBrfInfo(ReportSection section, byte[] header)
        {
            var text = DecodeWindowsText(header);
            var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .Take(8)
                .ToArray();
            Add(section, "Role", "Plain braille cells encoded as ASCII characters.");
            if (lines.Length > 0)
                Add(section, "First nonblank lines", string.Join("\r\n", lines));
        }

        private static void AddComicBookArchiveInfo(ReportSection section, string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".cbr" && StartsWith(header, Encoding.ASCII.GetBytes("Rar!")))
                Add(section, "Archive container", "RAR");
            else if (ext == ".cbz" && IsZipHeader(header))
                Add(section, "Archive container", "ZIP");
            else
                Add(section, "Archive container", "extension-level comic archive hint");
            Add(section, "Role", "Comic Book archive: an ordered image archive, commonly pages as JPEG/PNG files.");
        }

        private static void AddChmInfo(ReportSection section, byte[] header)
        {
            Add(section, "Header marker", StartsWith(header, Encoding.ASCII.GetBytes("ITSF")) ? "ITSF" : "extension-level match");
            if (header.Length >= 16)
            {
                Add(section, "Version", ReadUInt32LittleEndian(header, 4).ToString(CultureInfo.InvariantCulture));
                Add(section, "Header size", FormatBytes(ReadUInt32LittleEndian(header, 8)));
            }
            if (IndexOfAscii(header, "ITSP") >= 0)
                Add(section, "Directory marker", "ITSP marker found in sampled header bytes.");
        }

        private static void AddWinHelpInfo(ReportSection section, byte[] header)
        {
            Add(section, "Role", "Legacy Windows Help file used by older Windows applications.");
            if (header.Length >= 4)
                Add(section, "Header bytes", BitConverter.ToString(header.Take(4).ToArray()).Replace("-", " "));
            var strings = FindReadableTextLines(header, 4, 12).Take(12).ToArray();
            if (strings.Length > 0)
                Add(section, "Visible help strings", string.Join("\r\n", strings));
            Add(section, "Compatibility note", "WinHelp is a legacy help format. Modern Windows versions may not open it without optional old components.");
        }

        private static bool LooksLikePalmMobi(byte[] header)
        {
            return LooksLikePalmDatabase(header) &&
                (ReadPalmString(header, 60, 4) == "BOOK" || IndexOfAscii(header, "MOBI") >= 0);
        }

        private static bool LooksLikePalmDatabase(byte[] header)
        {
            if (header.Length < 78)
                return false;
            var type = ReadPalmString(header, 60, 4);
            var creator = ReadPalmString(header, 64, 4);
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(creator))
                return false;
            return type.All(ch => ch >= 32 && ch < 127) && creator.All(ch => ch >= 32 && ch < 127);
        }

        private static bool LooksLikeSonyLrf(byte[] header)
        {
            return header.Length >= 6 &&
                header[0] == (byte)'L' &&
                header[1] == 0 &&
                header[2] == (byte)'R' &&
                header[3] == 0 &&
                header[4] == (byte)'F' &&
                header[5] == 0;
        }

        private static string ReadPalmString(byte[] data, int offset, int count)
        {
            if (offset < 0 || offset >= data.Length)
                return string.Empty;
            var actual = Math.Min(count, data.Length - offset);
            return Encoding.ASCII.GetString(data, offset, actual).TrimEnd('\0', ' ');
        }
    }
}
