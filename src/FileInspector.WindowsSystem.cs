using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static string WindowsSystemTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var name = Path.GetFileName(path) ?? string.Empty;
            if (LooksLikeEvtx(header)) return "Windows Event Log";
            if (ext == ".pnf") return "Windows precompiled INF";
            if (ext == ".inf_loc") return "Windows driver INF localization data";
            if (ext == ".gpd") return "Windows printer GPD description";
            if (ext == ".gdl") return "Windows printer GDL description";
            if (ext == ".xrm-ms" && LooksLikeText(header)) return "Windows licensing XML";
            if (LooksLikeSdb(header)) return "Windows app compatibility database";
            if (LooksLikeCatDb(path, header)) return "Windows catalog database";
            if (ext == ".cat") return "Windows security catalog";
            if (IsAppxLikePackage(path, header)) return "Windows app package";
            if (LooksLikeGettextMo(header)) return "GNU gettext message catalog";
            if (LooksLikeQtQm(path, header)) return "Qt translation catalog";
            if (LooksLikeWindowsNetworkConfig(path, header)) return "Windows network configuration text";
            if (StartsWith(header, Encoding.ASCII.GetBytes("$SDI0001"))) return "Windows boot SDI image";
            if (ext == ".man" && LooksLikeWindowsInstrumentationManifest(header)) return "Windows instrumentation manifest";
            if (name.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase) && LooksLikeText(header)) return "Windows application manifest";
            return null;
        }

        private static void AddWindowsSystemInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var type = WindowsSystemTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Windows/system data");
            Add(section, "Format", type);
            Add(section, "File size", FormatBytes(fileLength));

            if (LooksLikeEvtx(header))
                AddEvtxInfo(section, header);
            else if (Path.GetExtension(path).Equals(".pnf", StringComparison.OrdinalIgnoreCase))
                AddPnfInfo(section, path, header);
            else if (Path.GetExtension(path).Equals(".inf_loc", StringComparison.OrdinalIgnoreCase))
                AddInfLocInfo(section, path, header);
            else if (Path.GetExtension(path).Equals(".gpd", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".gdl", StringComparison.OrdinalIgnoreCase))
                AddPrinterDescriptionInfo(section, path, header);
            else if (Path.GetExtension(path).Equals(".xrm-ms", StringComparison.OrdinalIgnoreCase))
                AddXrmInfo(section, header);
            else if (LooksLikeSdb(header))
                AddSdbInfo(section, header);
            else if (LooksLikeCatDb(path, header))
                AddCatDbInfo(section, path, header);
            else if (Path.GetExtension(path).Equals(".cat", StringComparison.OrdinalIgnoreCase))
                Add(section, "Container note", "Windows security catalog, normally a signed catalog containing file hashes and trust metadata.");
            else if (IsAppxLikePackage(path, header))
                AddAppxInfo(section, path);
            else if (LooksLikeGettextMo(header))
                AddGettextMoInfo(section, header);
            else if (LooksLikeQtQm(path, header))
                AddQtQmInfo(section, path, header);
            else if (LooksLikeWindowsNetworkConfig(path, header))
                AddNetworkConfigInfo(section, path, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("$SDI0001")))
                AddWindowsSdiInfo(section, path, fileLength);
            else if (Path.GetExtension(path).Equals(".man", StringComparison.OrdinalIgnoreCase) && LooksLikeWindowsInstrumentationManifest(header))
                AddWindowsInstrumentationManifestInfo(section, header);
            else if ((Path.GetFileName(path) ?? string.Empty).EndsWith(".manifest", StringComparison.OrdinalIgnoreCase))
                AddManifestInfo(section, header);

            Add(section, "Notes", "Windows system files are reported from headers, filenames, and safe text/XML structure only. FileDentify does not install, import, execute, or modify them.");
        }

        private static bool LooksLikeEvtx(byte[] header)
        {
            return StartsWith(header, Encoding.ASCII.GetBytes("ElfFile\0"));
        }

        private static void AddWindowsSdiInfo(ReportSection section, string path, long fileLength)
        {
            Add(section, "Header marker", "$SDI0001");
            Add(section, "Role", "Windows boot SDI ramdisk image, commonly used by Windows setup, recovery, and boot environments.");
            if ((Path.GetFileName(path) ?? string.Empty).Equals("boot.sdi", StringComparison.OrdinalIgnoreCase))
                Add(section, "Common name", "boot.sdi");
            Add(section, "Image size", FormatBytes(fileLength));
        }

        private static bool LooksLikeWindowsInstrumentationManifest(byte[] header)
        {
            var text = DecodeWindowsText(header);
            return text.IndexOf("<instrumentationManifest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("<instrumentation", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddWindowsInstrumentationManifestInfo(ReportSection section, byte[] header)
        {
            var text = DecodeWindowsText(header);
            Add(section, "Role", "Windows event tracing / instrumentation manifest XML.");
            Add(section, "Provider elements", Regex.Matches(text, "<provider\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Event elements", Regex.Matches(text, "<event\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Channel elements", Regex.Matches(text, "<channel\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            AddExampleLines(section, "Example manifest lines", text, @"(?im)<(provider|event|channel)\b[^>]*>", 8);
        }

        private static void AddEvtxInfo(ReportSection section, byte[] header)
        {
            Add(section, "Magic", "ElfFile");
            if (header.Length >= 0x28)
            {
                Add(section, "First chunk number", ReadUInt64LittleEndianText(header, 8));
                Add(section, "Last chunk number", ReadUInt64LittleEndianText(header, 16));
                Add(section, "Next record identifier", ReadUInt64LittleEndianText(header, 24));
                Add(section, "Header size", FormatBytes(ReadUInt32LittleEndian(header, 0x20)));
            }
            Add(section, "Common location", @"%SystemRoot%\System32\winevt\Logs");
        }

        private static void AddPnfInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Role", "Precompiled driver setup information generated from an INF file.");
            Add(section, "Source INF name", Path.GetFileNameWithoutExtension(path) + ".inf");
            if (header.Length >= 16)
                Add(section, "Header words", "0x" + ReadUInt32LittleEndian(header, 0).ToString("X8", CultureInfo.InvariantCulture) + ", 0x" + ReadUInt32LittleEndian(header, 4).ToString("X8", CultureInfo.InvariantCulture));
            var strings = FindReadableTextLines(header, 4, 20).Where(s => s.IndexOf("\\Windows", StringComparison.OrdinalIgnoreCase) >= 0 || s.EndsWith(".inf", StringComparison.OrdinalIgnoreCase)).Take(8).ToArray();
            if (strings.Length > 0)
                Add(section, "Visible setup strings", string.Join("\r\n", strings));
        }

        private static void AddInfLocInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Role", "Localized strings for a driver INF package.");
            var sourceName = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            if (sourceName.EndsWith(".inf", StringComparison.OrdinalIgnoreCase))
                sourceName = sourceName.Substring(0, sourceName.Length - 4);
            Add(section, "Source INF name", sourceName + ".inf");
            var strings = FindReadableTextLines(header, 3, 20).Take(12).ToArray();
            if (strings.Length > 0)
                Add(section, "Visible strings", string.Join("\r\n", strings));
        }

        private static void AddPrinterDescriptionInfo(ReportSection section, string path, byte[] header)
        {
            var text = DecodeWindowsText(header);
            Add(section, "Role", Path.GetExtension(path).Equals(".gdl", StringComparison.OrdinalIgnoreCase) ? "Generic Description Language printer schema" : "Generic Printer Description file");
            Add(section, "Features/templates", Regex.Matches(text, @"(?im)^\s*\*(Feature|Template)\s*:", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Macros", Regex.Matches(text, @"(?im)^\s*\*Macros\s*:", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Includes", Regex.Matches(text, @"(?im)^\s*\*Include\s*:", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
            AddExampleLines(section, "Example directives", text, @"(?im)^\s*\*[A-Za-z0-9_]+\s*:[^\r\n]*$", 12);
        }

        private static void AddXrmInfo(ReportSection section, byte[] header)
        {
            var text = DecodeWindowsText(header);
            Add(section, "Role", "Software Protection Platform license token / product-key configuration XML.");
            Add(section, "License elements", Regex.Matches(text, "<r:license\\b|<license\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Grant elements", Regex.Matches(text, "<r:grant\\b|<grant\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Product key configs", Regex.Matches(text, "pkey", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
        }

        private static bool LooksLikeSdb(byte[] header)
        {
            return header.Length >= 12 && Encoding.ASCII.GetString(header, 8, 4) == "zdbf";
        }

        private static void AddSdbInfo(ReportSection section, byte[] header)
        {
            Add(section, "Magic", "zdbf");
            Add(section, "Role", "Windows application compatibility / appraiser database.");
            if (header.Length >= 20)
                Add(section, "Header version", ReadUInt32LittleEndian(header, 0).ToString(CultureInfo.InvariantCulture));
            if (header.Length >= 24 && header[16] == 0x78 && header[17] == 0xDA)
                Add(section, "Payload compression hint", "zlib/deflate stream starts near offset 0x10");
        }

        private static bool LooksLikeCatDb(string path, byte[] header)
        {
            var name = Path.GetFileName(path) ?? string.Empty;
            return name.Equals("catdb", StringComparison.OrdinalIgnoreCase) &&
                header.Length >= 12 &&
                (ReadUInt32LittleEndian(header, 0) == 0x98D674C3 ||
                 (header[4] == 0xEF && header[5] == 0xCD && header[6] == 0xAB && header[7] == 0x89));
        }

        private static void AddCatDbInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Role", "Windows catalog database used by catroot/catroot2.");
            Add(section, "Header marker", ReadUInt32LittleEndian(header, 0) == 0x98D674C3 ? "0x98D674C3" : "Extensible Storage Engine database header");
            Add(section, "Catalog folder", Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty));
            if (header.Length >= 20)
                Add(section, "Page/record size-like field", FormatBytes(ReadUInt32LittleEndian(header, 16)));
        }

        private static bool IsAppxLikePackage(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return IsZipHeader(header) && (ext == ".appx" || ext == ".appxbundle" || ext == ".msix" || ext == ".msixbundle");
        }

        private static void AddAppxInfo(ReportSection section, string path)
        {
            Add(section, "Role", "Windows AppX/MSIX ZIP-compatible app package.");
            try
            {
                using (var archive = ZipFile.OpenRead(path))
                {
                    Add(section, "Entries", archive.Entries.Count.ToString(CultureInfo.InvariantCulture));
                    Add(section, "AppxManifest.xml", archive.GetEntry("AppxManifest.xml") != null ? "Present" : "Not found");
                    Add(section, "AppxBlockMap.xml", archive.GetEntry("AppxBlockMap.xml") != null ? "Present" : "Not found");
                    Add(section, "Signature", archive.GetEntry("AppxSignature.p7x") != null ? "Present" : "Not found");
                    var manifest = archive.GetEntry("AppxManifest.xml");
                    if (manifest != null)
                    {
                        var text = ReadZipEntryText(manifest, 128 * 1024);
                        Add(section, "Package identity name", ValueOrNotReported(FirstXmlAttribute(text, "Identity", "Name")));
                        Add(section, "Publisher", ValueOrNotReported(FirstXmlAttribute(text, "Identity", "Publisher")));
                        Add(section, "Package version", ValueOrNotReported(FirstXmlAttribute(text, "Identity", "Version")));
                    }
                }
            }
            catch (Exception ex)
            {
                Add(section, "Package read error", ex.Message);
            }
        }

        private static bool LooksLikeGettextMo(byte[] header)
        {
            return header.Length >= 28 && (ReadUInt32LittleEndian(header, 0) == 0x950412DE || ReadUInt32BigEndian(header, 0) == 0x950412DE);
        }

        private static void AddGettextMoInfo(ReportSection section, byte[] header)
        {
            var little = ReadUInt32LittleEndian(header, 0) == 0x950412DE;
            Add(section, "Role", "GNU gettext compiled translation catalog.");
            Add(section, "Endian", little ? "little-endian" : "big-endian");
            Add(section, "Revision", ReadMoUInt32(header, 4, little).ToString(CultureInfo.InvariantCulture));
            Add(section, "String count", ReadMoUInt32(header, 8, little).ToString(CultureInfo.InvariantCulture));
            Add(section, "Original table offset", "0x" + ReadMoUInt32(header, 12, little).ToString("X", CultureInfo.InvariantCulture));
            Add(section, "Translation table offset", "0x" + ReadMoUInt32(header, 16, little).ToString("X", CultureInfo.InvariantCulture));
        }

        private static bool LooksLikeQtQm(string path, byte[] header)
        {
            return Path.GetExtension(path).Equals(".qm", StringComparison.OrdinalIgnoreCase) || StartsWith(header, new byte[] { 0x3C, 0xB8, 0x64, 0x18 });
        }

        private static void AddQtQmInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Role", "Qt compiled translation catalog.");
            Add(section, "File name locale hint", Path.GetFileNameWithoutExtension(path));
            if (StartsWith(header, new byte[] { 0x3C, 0xB8, 0x64, 0x18 }))
                Add(section, "Magic", "Qt QM binary header");
        }

        private static bool LooksLikeWindowsNetworkConfig(string path, byte[] header)
        {
            var name = Path.GetFileName(path) ?? string.Empty;
            var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var expectedDirectory = Path.Combine(windowsDirectory, "System32", "drivers", "etc");
            if (!string.Equals(Path.GetDirectoryName(path), expectedDirectory, StringComparison.OrdinalIgnoreCase))
                return false;
            return name.Equals("hosts", StringComparison.OrdinalIgnoreCase) || name.Equals("services", StringComparison.OrdinalIgnoreCase) || name.Equals("protocol", StringComparison.OrdinalIgnoreCase) || name.Equals("networks", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddNetworkConfigInfo(ReportSection section, string path, byte[] header)
        {
            var text = DecodeWindowsText(header);
            Add(section, "Role", "Windows TCP/IP configuration text file.");
            Add(section, "Config file", Path.GetFileName(path));
            var dataLines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal))
                .ToArray();
            Add(section, "Non-comment entries", dataLines.Length.ToString(CultureInfo.InvariantCulture));
            if (dataLines.Length > 0)
                Add(section, "Example entries", string.Join("\r\n", dataLines.Take(12).ToArray()));
        }

        private static void AddManifestInfo(ReportSection section, byte[] header)
        {
            var text = DecodeWindowsText(header);
            Add(section, "Role", "Windows side-by-side/application manifest XML.");
            Add(section, "Assembly identity", ValueOrNotReported(FirstXmlAttribute(text, "assemblyIdentity", "name")));
            Add(section, "Version", ValueOrNotReported(FirstXmlAttribute(text, "assemblyIdentity", "version")));
            Add(section, "Requested execution levels", Regex.Matches(text, "<requestedExecutionLevel\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Dependencies", Regex.Matches(text, "<dependentAssembly\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
        }

        private static void AddExampleLines(ReportSection section, string title, string text, string pattern, int max)
        {
            var lines = Regex.Matches(text ?? string.Empty, pattern, RegexOptions.CultureInvariant)
                .Cast<Match>()
                .Select(match => CleanMetadataText(match.Value.Trim()))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(max)
                .ToArray();
            if (lines.Length > 0)
                Add(section, title, string.Join("\r\n", lines));
        }

        private static string DecodeWindowsText(byte[] data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;
            var sample = data.Take(Math.Min(data.Length, 1024 * 1024)).ToArray();
            if (sample.Length >= 2 && sample[0] == 0xFF && sample[1] == 0xFE)
                return Encoding.Unicode.GetString(sample);
            if (sample.Length >= 3 && sample[0] == 0xEF && sample[1] == 0xBB && sample[2] == 0xBF)
                return Encoding.UTF8.GetString(sample);
            return Encoding.UTF8.GetString(sample);
        }

        private static string ReadUInt64LittleEndianText(byte[] data, int offset)
        {
            if (offset + 8 > data.Length)
                return "0";
            ulong value = 0;
            for (var i = 0; i < 8; i++)
                value |= ((ulong)data[offset + i]) << (8 * i);
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static uint ReadMoUInt32(byte[] data, int offset, bool little)
        {
            return little ? ReadUInt32LittleEndian(data, offset) : ReadUInt32BigEndian(data, offset);
        }
    }
}
