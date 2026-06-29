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
    {        private const int HeaderReadSize = 1024 * 1024;
        private const int StringReadSize = 4 * 1024 * 1024;

        public static FileReport Inspect(string path)
        {
            if (Directory.Exists(path) && IsReportableDirectoryPackage(path))
                return InspectDirectoryPackage(path);

            var file = new FileInfo(path);
            var report = new FileReport();
            report.DisplayName = file.Name;
            report.OriginalPath = file.FullName;

            byte[] header = ReadPrefix(path, HeaderReadSize);
            byte[] stringSample = header.Length >= StringReadSize ? header : ReadPrefix(path, StringReadSize);
            byte[] displayHeader = RedactSensitiveClipmanSample(path, header);
            byte[] displayStringSample = ReferenceEquals(stringSample, header) ? displayHeader : RedactSensitiveClipmanSample(path, stringSample);
            var libmagic = LibmagicProbe.Identify(path);
            var sections = report.Sections;

            var summary = AddSection(sections, "Summary");
            Add(summary, "Likely type", GuessType(path, header, file.Length, libmagic));
            if (libmagic != null && ShouldShowLibmagicInSummary(libmagic.Description))
                Add(summary, "Unix file says", libmagic.Description);
            Add(summary, "Path", path);
            Add(summary, "Size", FormatBytes(file.Length) + " (" + file.Length.ToString(CultureInfo.InvariantCulture) + " bytes)");
            Add(summary, "Extension", string.IsNullOrEmpty(file.Extension) ? "(none)" : file.Extension);
            Add(summary, "Modified", file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Add(summary, "Created", file.CreationTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            AddReadableTextInfo(sections, displayStringSample);
            AddFilesystemInfo(sections, file);
            AddWindowsPropertyMetadata(sections, path);
            AddDmgInfo(sections, path, file.Length);

            var signatures = AddSection(sections, "Signature matches");
            foreach (var match in SignatureMatcher.Match(header, path))
                Add(signatures, match.Title, match.Detail);
            if (signatures.Items.Count == 0)
                Add(signatures, "No common signature match", "The first bytes do not match the built-in signature list.");
            AddLibmagicInfo(sections, libmagic);
            AddSafetyHintInfo(sections, path, header);

            var hashes = AddSection(sections, "Hashes");
            AddHashInfo(hashes, path, file.Length);

            var headerSection = AddSection(sections, "Header bytes");
            Add(headerSection, "ASCII preview", AsciiPreview(displayHeader, 256));
            Add(headerSection, "Hex preview", HexPreview(displayHeader, 256));

            var structure = AddSection(sections, "Structure hints");
            AddStructureHints(structure, path, header);
            AddPeInfo(sections, path, header);
            AddVersionInfo(sections, path);
            AddWindowsShortcutInfo(sections, header);
            AddInternetShortcutInfo(sections, path, header);
            AddImageInfo(sections, header);
            AddPdfInfo(sections, path, header, file.Length);
            AddZipDocumentMetadata(sections, path, header);
            AddFontInfo(sections, header);
            AddOleCompoundInfo(sections, path, header);
            AddCompressedStreamInfo(sections, header);
            AddCabinetInfo(sections, path, header);
            AddWindowsImageInfo(sections, path, header);
            AddIso9660Info(sections, path, header);
            AddNeroImageInfo(sections, path, file.Length);
            AddVirtualDiskInfo(sections, path, header, file.Length);
            AddMozillaLz4Info(sections, header);
            AddUfsInfo(sections, path, header);
            AddBlobInfo(sections, path, header);
            AddClipmanInfo(sections, path, stringSample, file.Length);
            AddSavedReportInfo(sections, path, header);
            AddDeveloperFormatInfo(sections, path, header);
            AddBackupConfigInfo(sections, path, header, stringSample, file.Length);
            AddLegacySoundBankInfo(sections, path, header, stringSample, file.Length);
            AddSymbianPackageInfo(sections, path, header);
            AddSymbianAppResourceInfo(sections, path, header, stringSample, file.Length);
            AddJavaMidletInfo(sections, path, header, file.Length);
            AddFirmwareInfo(sections, path, header, file.Length);
            AddNativeInstrumentsInfo(sections, path, stringSample);
            AddSteinbergCubaseInfo(sections, path, stringSample);
            AddAppleFormatInfo(sections, path, header);
            AddMacAudioPluginInfo(sections, path, header);
            AddRolandCloudInfo(sections, path, header, stringSample, file.Length);
            AddSampleLibraryInfo(sections, path, header, stringSample, file.Length);
            AddMusicProjectFormatInfo(sections, path, header, stringSample, file.Length);
            AddGameFileInfo(sections, path, header);
            AddPropertyListInfo(sections, header);
            AddSqliteInfo(sections, header);
            AddRarInfo(sections, header);
            AddIsoBmffInfo(sections, header);
            AddRiffInfo(sections, path, header, file.Length);
            AddIffInfo(sections, header);
            AddMidiInfo(sections, header);
            AddAudioHeaderInfo(sections, path, header, file.Length);
            AddMobilePhoneToneInfo(sections, path, header);

            var foundStrings = FindAsciiStrings(displayStringSample, 4, 40);

            var strings = AddSection(sections, "Printable strings");
            if (foundStrings.Count == 0)
                Add(strings, "No strings found", "No printable ASCII runs of at least four characters were found in the sampled data.");
            else
                foreach (var s in foundStrings)
                    Add(strings, "0x" + s.Offset.ToString("X8", CultureInfo.InvariantCulture), s.Value);

            var stats = AddSection(sections, "Byte statistics");
            AddByteStats(stats, header, file.Length);
            AddTextInfo(sections, displayHeader);

            AddExternalToolInfo(sections, path);
            AddCompanionToolInfo(sections, path);

            report.FullText = BuildReportText(report);
            return report;
        }

        private static void AddStructureHints(ReportSection section, string path, byte[] header)
        {
            if (StartsWith(header, Encoding.ASCII.GetBytes("PK\x03\x04")) || StartsWith(header, Encoding.ASCII.GetBytes("PK\x05\x06")) || StartsWith(header, Encoding.ASCII.GetBytes("PK\x07\x08")))
                AddZipHints(section, path);

            if (header.Length >= 32 && AsciiPreview(header, 32).Contains("Roland SRX"))
                Add(section, "Roland SRX header", "The file starts with a Roland SRX marker. This suggests a synthesizer expansion ROM image rather than a normal archive.");

            if (header.Length > 0)
            {
                var zeroCount = header.Count(b => b == 0);
                Add(section, "Zero bytes in first sample", zeroCount.ToString(CultureInfo.InvariantCulture) + " of " + header.Length.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void AddFilesystemInfo(List<ReportSection> sections, FileInfo file)
        {
            var section = AddSection(sections, "Filesystem");
            Add(section, "Directory", file.DirectoryName ?? string.Empty);
            Add(section, "Base name", Path.GetFileNameWithoutExtension(file.Name));
            Add(section, "File name length", file.Name.Length.ToString(CultureInfo.InvariantCulture) + " characters");
            Add(section, "Attributes", file.Attributes.ToString());
            Add(section, "Modified UTC", file.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Add(section, "Created UTC", file.CreationTimeUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Add(section, "Accessed UTC", file.LastAccessTimeUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }

        private static void AddLibmagicInfo(List<ReportSection> sections, LibmagicResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.Description))
                return;

            var section = AddSection(sections, "Unix file/libmagic");
            Add(section, "Description", result.Description);
            if (!string.IsNullOrWhiteSpace(result.Mime))
                Add(section, "MIME", result.Mime);
            Add(section, "Engine", result.Engine);
        }

    }
}

