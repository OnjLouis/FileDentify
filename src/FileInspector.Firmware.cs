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
        private static string FirmwareTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (LooksLikeZoomAccessibilityData(header))
                return "ZOOM H1essential accessibility guide-sound data";
            var zoomRecorder = ZoomRecorderFirmwareDetails(header);
            if (zoomRecorder != null)
                return "ZOOM " + zoomRecorder[0] + " recorder firmware image";
            if (LooksLikeOrbitReader20PlusFirmware(header))
                return "Orbit Reader 20 Plus firmware image";
            if (LooksLikeAndroidBootImage(header))
                return "Android boot image";
            if (LooksLikeUbootLegacyImage(header))
                return "U-Boot legacy uImage";
            if (LooksLikeUf2Firmware(header))
                return "UF2 microcontroller firmware";
            if (LooksLikeTrxFirmware(header))
                return "Broadcom/OpenWrt TRX firmware image";
            if (LooksLikeFlattenedDeviceTree(header))
                return "Flattened Device Tree blob";
            if (LooksLikeIntelHex(path, header))
                return "Intel HEX firmware image";
            if (LooksLikeMotorolaSRecord(path, header))
                return "Motorola S-record firmware image";
            if (LooksLikeSynologyPat(path, header))
                return "Synology DSM/SRM update PAT archive";
            if (StartsWith(header, Encoding.ASCII.GetBytes("_PT_")))
                return "PC BIOS/UEFI firmware image";
            if (StartsWith(header, Encoding.ASCII.GetBytes("Roland SRX")))
                return "Roland SRX expansion ROM image";
            if (StartsWith(header, Encoding.ASCII.GetBytes("XMVh")))
                return "Roland FA screen saver/movie data";
            if (ext == ".bin" && PathLooksRolandExpansion(path))
                return "Roland FA/SRX expansion image";
            if (ext == ".1q8" || Regex.IsMatch(ext, @"^\.[0-9a-z]{3}$", RegexOptions.IgnoreCase))
            {
                var name = Path.GetFileName(path) ?? string.Empty;
                if (name.StartsWith("E7A32", StringComparison.OrdinalIgnoreCase) || StartsWith(header, Encoding.ASCII.GetBytes("_PT_")))
                    return "MSI motherboard BIOS image";
            }
            return null;
        }

        private static void AddFirmwareInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var type = FirmwareTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Firmware / device image");
            Add(section, "Format hint", type);
            Add(section, "File size", FormatBytes(fileLength) + " (" + fileLength.ToString(CultureInfo.InvariantCulture) + " bytes)");

            var zoomRecorder = ZoomRecorderFirmwareDetails(header);
            if (LooksLikeZoomAccessibilityData(header))
                AddZoomAccessibilityDataInfo(section, path, fileLength);
            else if (zoomRecorder != null)
                AddZoomRecorderFirmwareInfo(section, path, header, fileLength, zoomRecorder);
            else if (LooksLikeOrbitReader20PlusFirmware(header))
                AddOrbitReader20PlusFirmwareInfo(section, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("_PT_")))
                AddPcFirmwareInfo(section, path, header);
            else if (LooksLikeAndroidBootImage(header))
                AddAndroidBootImageInfo(section, header);
            else if (LooksLikeUbootLegacyImage(header))
                AddUbootLegacyImageInfo(section, header);
            else if (LooksLikeUf2Firmware(header))
                AddUf2Info(section, header);
            else if (LooksLikeTrxFirmware(header))
                AddTrxInfo(section, header);
            else if (LooksLikeFlattenedDeviceTree(header))
                AddFlattenedDeviceTreeInfo(section, header);
            else if (LooksLikeIntelHex(path, header))
                AddIntelHexInfo(section, header);
            else if (LooksLikeMotorolaSRecord(path, header))
                AddMotorolaSRecordInfo(section, header);
            else if (LooksLikeSynologyPat(path, header))
                AddSynologyPatInfo(section, path, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("Roland SRX")) || PathLooksRolandExpansion(path))
                AddRolandExpansionInfo(section, path, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("XMVh")))
                AddRolandMovieInfo(section, header);

            Add(section, "Notes", "Firmware and device images are reported from headers, filenames, and visible strings only. FileDentify does not flash, unpack, or modify them.");
        }

        private static bool LooksLikeZoomAccessibilityData(byte[] header)
        {
            if (header == null || header.Length < 80)
                return false;
            var prefix = Encoding.ASCII.GetString(header, 0, Math.Min(header.Length, 128));
            return prefix.StartsWith("H1essential Accessibility Data", StringComparison.Ordinal) &&
                prefix.IndexOf("ZOOM Corporation", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddZoomAccessibilityDataInfo(ReportSection section, string path, long fileLength)
        {
            Add(section, "Manufacturer", "ZOOM Corporation");
            Add(section, "Recorder", "H1essential");
            Add(section, "Role", "accessibility guide-sound data");
            if (Sha256(path, fileLength) == "49a628ce6bc498987b2728ce97346477146ae752836e8180eb1a71df368e5987")
                Add(section, "Official package version", "H1essential guide sound version 2.00, English");
            Add(section, "Detection basis", "H1essential Accessibility Data header and ZOOM copyright marker; filename and extension are not required.");
            Add(section, "Compatibility note", "This companion image is distinct from system firmware. Follow ZOOM's accessibility-file installation guide when updating guide sounds.");
        }

        private static string[] ZoomRecorderFirmwareDetails(byte[] header)
        {
            if (header == null || header.Length < 80)
                return null;
            var prefix = Encoding.ASCII.GetString(header, 0, Math.Min(header.Length, 128));
            string model = null;
            string family = null;
            if (prefix.StartsWith("H1essential System Data", StringComparison.Ordinal)) { model = "H1essential"; family = "H series handy recorder"; }
            else if (prefix.StartsWith("H6essential System Data", StringComparison.Ordinal)) { model = "H6essential"; family = "H series handy recorder"; }
            else if (prefix.StartsWith("H6 System Data", StringComparison.Ordinal)) { model = "H6"; family = "H series handy recorder"; }
            else if (prefix.StartsWith("F8nPro System Data", StringComparison.Ordinal)) { model = "F8n Pro"; family = "F series field recorder"; }
            else if (prefix.StartsWith("F8n System Data", StringComparison.Ordinal)) { model = "F8n"; family = "F series field recorder"; }
            else if (prefix.StartsWith("F8 System Data", StringComparison.Ordinal)) { model = "F8"; family = "F series field recorder"; }
            else if (prefix.StartsWith("F6 System Data", StringComparison.Ordinal)) { model = "F6"; family = "F series field recorder"; }
            else if (prefix.StartsWith("F3 System Data", StringComparison.Ordinal)) { model = "F3"; family = "F series field recorder"; }
            return model != null && prefix.IndexOf("ZOOM Corporation", StringComparison.OrdinalIgnoreCase) >= 0
                ? new[] { model, family }
                : null;
        }

        private static void AddZoomRecorderFirmwareInfo(ReportSection section, string path, byte[] header, long fileLength, string[] details)
        {
            Add(section, "Manufacturer", "ZOOM Corporation");
            Add(section, "Recorder", details[0]);
            Add(section, "Product family", details[1]);
            Add(section, "Header identity", ReadNullTerminated(header, 0, Math.Min(96, header.Length)).Trim());
            var version = KnownZoomRecorderFirmwareVersion(path, fileLength);
            if (version != null)
                Add(section, "Official package version", version);
            Add(section, "Detection basis", "Model-specific System Data header and ZOOM copyright marker; filename and extension are not required.");
            Add(section, "Compatibility note", "Recorder firmware is model-specific. Confirm the recorder model and follow ZOOM's official update guide before using this file.");
        }

        private static string KnownZoomRecorderFirmwareVersion(string path, long fileLength)
        {
            if (fileLength <= 0 || fileLength > 32L * 1024 * 1024)
                return null;
            var hash = Sha256(path, fileLength);
            switch (hash)
            {
                case "56524ffafb21bacd15953eda16b473fb80bf1a69115a2770fca0a5c2357b4f26": return "H1essential system version 2.20";
                case "7e8018e8695a1c71aaf1716b7472f2c2a1ed510029845d01e3eaf4f8fecfe14e": return "H6 system version 2.50";
                case "a294f3782554ee88d6a949b1acf8fadadbeedc8f15ce75516c7585237f032884": return "H6essential system version 3.30";
                case "c16f3307ffc3c85b881566dd9b779b5ecbf03131c2ecad4d4f06b370f1cd814d": return "F3 system version 2.20";
                case "7f14f62186aaa66fdf1478f726026555a864373d73f63909970751e0d908cd19": return "F6 system version 2.20";
                case "bdc275371b9accc063ef13a655be03a4e361fd68a557eaf677eb3230adae2885": return "F8 system version 6.40";
                case "b03f0fec4006f669be118e170a57275f328076b3daa58b897b7affc849d02135": return "F8n system version 2.50";
                case "229f1ca767f80bb1d706de1b650a536964d18b1ee8a7bfd82534f5eb8ba2b7df": return "F8n Pro system version 1.30";
                default: return null;
            }
        }

        private static bool LooksLikeOrbitReader20PlusFirmware(byte[] header)
        {
            if (header == null || header.Length < 64)
                return false;

            var marker = ReadNullTerminated(header, 0x20, 8);
            var family = ReadNullTerminated(header, 0x28, 8);
            var version = ReadNullTerminated(header, 0x30, 16);
            return marker.Equals("OR-20-02", StringComparison.Ordinal) &&
                Regex.IsMatch(family, @"^[A-Z][0-9]\.[0-9]{2}\.[0-9]{2}$", RegexOptions.CultureInvariant) &&
                Regex.IsMatch(version, @"^[A-Z][0-9]\.[0-9]{2}\.[0-9]{2}\.[0-9]{2}[rb][0-9]{2}$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase) &&
                version.StartsWith(family + ".", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddOrbitReader20PlusFirmwareInfo(ReportSection section, byte[] header)
        {
            Add(section, "Manufacturer", "Orbit Research");
            Add(section, "Device", "Orbit Reader 20 Plus braille display and notetaker");
            Add(section, "Header marker", ReadNullTerminated(header, 0x20, 8));
            Add(section, "Compatible firmware family", ReadNullTerminated(header, 0x28, 8));
            Add(section, "Firmware version", ReadNullTerminated(header, 0x30, 16));
            Add(section, "Installation", "Orbit Research supplies these target-software .bin files for installation with its Windows upgrade utility or from an SD card.");
            Add(section, "Compatibility note", "Orbit Reader 20 Plus hardware variations use different firmware families. Confirm that the device's current version begins with the compatible family shown above before installing an update.");
        }

        private static void AddPcFirmwareInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Header marker", StartsWith(header, Encoding.ASCII.GetBytes("_PT_")) ? "_PT_" : "Not found in first sample");
            var strings = FindAsciiStrings(header, 4, 80).Select(s => s.Value).Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToArray();
            if (strings.Length > 0)
                Add(section, "Visible firmware strings", string.Join("\r\n", strings));
            var name = Path.GetFileName(path) ?? string.Empty;
            if (Regex.IsMatch(name, @"^E[0-9A-Z]{5,}\.", RegexOptions.IgnoreCase))
                Add(section, "Filename hint", "MSI-style motherboard BIOS filename");
        }

        private static void AddRolandExpansionInfo(ReportSection section, string path, byte[] header)
        {
            if (StartsWith(header, Encoding.ASCII.GetBytes("Roland SRX")))
                Add(section, "Header marker", "Roland SRX");
            var title = ReadFixedAscii(header, 16, 32);
            if (!string.IsNullOrWhiteSpace(title))
                Add(section, "Visible title", title);
            var parent = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(parent))
                Add(section, "Folder hint", parent);
            Add(section, "Common use", "Roland FA/Jupiter/Integra-style expansion image or SRX-derived sound expansion data.");
        }

        private static void AddRolandMovieInfo(ReportSection section, byte[] header)
        {
            Add(section, "Header marker", "XMVh");
            if (header.Length >= 16)
            {
                Add(section, "Width-like field", ReadUInt16BigEndian(header, 4).ToString(CultureInfo.InvariantCulture));
                Add(section, "Height-like field", ReadUInt16BigEndian(header, 6).ToString(CultureInfo.InvariantCulture));
                Add(section, "Frame/count-like field", ReadUInt32BigEndian(header, 8).ToString(CultureInfo.InvariantCulture));
            }
            if (IndexOfAscii(header, "XMVf") >= 0)
                Add(section, "Frame marker", "XMVf found in header sample");
        }

        private static bool LooksLikeSynologyPat(string path, byte[] header)
        {
            if (!string.Equals(Path.GetExtension(path), ".pat", StringComparison.OrdinalIgnoreCase))
                return false;
            return LooksLikeSynologyTarPat(header) || LooksLikeSynologyEncryptedArchive(header);
        }

        private static bool LooksLikeSynologyTarPat(byte[] header)
        {
            if (header.Length < 265)
                return false;
            var name = ReadNullTerminated(header, 0, Math.Min(100, header.Length));
            return (name.Equals("VERSION", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("GRUB_VER", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)) &&
                Encoding.ASCII.GetString(header, 257, Math.Min(8, header.Length - 257)).StartsWith("ustar", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeSynologyEncryptedArchive(byte[] header)
        {
            return header.Length >= 4 &&
                ((header[1] == 0xAD && header[2] == 0xBE && header[3] == 0xEF) ||
                 (header[1] == 0xBF && header[2] == 0xBA && header[3] == 0xAD) ||
                 (header[0] == 0xAD && header[1] == 0xBE && header[2] == 0xEF) ||
                 (header[0] == 0xBF && header[1] == 0xBA && header[2] == 0xAD));
        }

        private static void AddSynologyPatInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Common use", "Synology DiskStation/Router Manager operating-system install or update package");
            var name = Path.GetFileName(path) ?? string.Empty;
            var match = Regex.Match(name, @"^(DSM|SRM|BSM)_([^_]+)_([0-9]+)(?:\.pat)?$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                Add(section, "Product family", match.Groups[1].Value.ToUpperInvariant());
                Add(section, "Model/platform", match.Groups[2].Value);
                Add(section, "Build", match.Groups[3].Value);
            }
            if (LooksLikeSynologyTarPat(header))
            {
                Add(section, "Container style", "tar-style Synology PAT bundle");
                Add(section, "First archive entry", ReadNullTerminated(header, 0, Math.Min(100, header.Length)));
            }
            else if (LooksLikeSynologyEncryptedArchive(header))
            {
                Add(section, "Container style", "Synology encrypted archive wrapper");
                Add(section, "Magic bytes", BitConverter.ToString(header, 0, Math.Min(4, header.Length)).Replace("-", " "));
            }
            Add(section, "Notes", "Synology .pat files share their extension with unrelated pattern and instrument formats. FileDentify only reports Synology PAT when the update-package wrapper is visible; it does not decrypt, unpack, or install firmware.");
        }

        private static bool PathLooksRolandExpansion(string path)
        {
            var value = path ?? string.Empty;
            return value.IndexOf("RolandFA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("FA_EXP-", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("EXP", StringComparison.OrdinalIgnoreCase) >= 0 && value.IndexOf("Roland", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeAndroidBootImage(byte[] header)
        {
            return StartsWith(header, Encoding.ASCII.GetBytes("ANDROID!")) && header.Length >= 64;
        }

        private static void AddAndroidBootImageInfo(ReportSection section, byte[] header)
        {
            Add(section, "Magic", "ANDROID!");
            Add(section, "Kernel size", FormatBytes(ReadUInt32LittleEndian(header, 8)));
            Add(section, "Kernel load address", "0x" + ReadUInt32LittleEndian(header, 12).ToString("X8", CultureInfo.InvariantCulture));
            Add(section, "Ramdisk size", FormatBytes(ReadUInt32LittleEndian(header, 16)));
            Add(section, "Ramdisk load address", "0x" + ReadUInt32LittleEndian(header, 20).ToString("X8", CultureInfo.InvariantCulture));
            Add(section, "Second-stage size", FormatBytes(ReadUInt32LittleEndian(header, 24)));
            Add(section, "Page size", FormatBytes(ReadUInt32LittleEndian(header, 36)));
            Add(section, "Header version", ReadUInt32LittleEndian(header, 40).ToString(CultureInfo.InvariantCulture));
            var product = ReadNullTerminated(header, 48, 16);
            if (!string.IsNullOrWhiteSpace(product))
                Add(section, "Product/name field", product);
            var commandLine = ReadNullTerminated(header, 64, Math.Min(512, Math.Max(0, header.Length - 64)));
            if (!string.IsNullOrWhiteSpace(commandLine))
                Add(section, "Kernel command line preview", TrimForFirmware(commandLine, 400));
        }

        private static bool LooksLikeUbootLegacyImage(byte[] header)
        {
            return header.Length >= 64 && ReadUInt32BigEndian(header, 0) == 0x27051956;
        }

        private static void AddUbootLegacyImageInfo(ReportSection section, byte[] header)
        {
            Add(section, "Magic", "0x27051956");
            Add(section, "Header CRC", "0x" + ReadUInt32BigEndian(header, 4).ToString("X8", CultureInfo.InvariantCulture));
            var timestamp = ReadUInt32BigEndian(header, 8);
            if (timestamp > 0)
                Add(section, "Timestamp", UnixTimeText(timestamp));
            Add(section, "Image data size", FormatBytes(ReadUInt32BigEndian(header, 12)));
            Add(section, "Load address", "0x" + ReadUInt32BigEndian(header, 16).ToString("X8", CultureInfo.InvariantCulture));
            Add(section, "Entry point", "0x" + ReadUInt32BigEndian(header, 20).ToString("X8", CultureInfo.InvariantCulture));
            Add(section, "Data CRC", "0x" + ReadUInt32BigEndian(header, 24).ToString("X8", CultureInfo.InvariantCulture));
            Add(section, "OS", UbootOsName(header[28]));
            Add(section, "CPU architecture", UbootArchName(header[29]));
            Add(section, "Image type", UbootImageTypeName(header[30]));
            Add(section, "Compression", UbootCompressionName(header[31]));
            var name = ReadNullTerminated(header, 32, 32);
            if (!string.IsNullOrWhiteSpace(name))
                Add(section, "Image name", name);
        }

        private static bool LooksLikeUf2Firmware(byte[] header)
        {
            return header.Length >= 512 &&
                ReadUInt32LittleEndian(header, 0) == 0x0A324655 &&
                ReadUInt32LittleEndian(header, 4) == 0x9E5D5157 &&
                ReadUInt32LittleEndian(header, 508) == 0x0AB16F30;
        }

        private static void AddUf2Info(ReportSection section, byte[] header)
        {
            var flags = ReadUInt32LittleEndian(header, 8);
            Add(section, "Magic", "UF2 block");
            Add(section, "Flags", "0x" + flags.ToString("X8", CultureInfo.InvariantCulture));
            Add(section, "Target address", "0x" + ReadUInt32LittleEndian(header, 12).ToString("X8", CultureInfo.InvariantCulture));
            Add(section, "Payload size", FormatBytes(ReadUInt32LittleEndian(header, 16)));
            Add(section, "Block number", ReadUInt32LittleEndian(header, 20).ToString(CultureInfo.InvariantCulture));
            Add(section, "Total blocks", ReadUInt32LittleEndian(header, 24).ToString(CultureInfo.InvariantCulture));
            var familyOrSize = ReadUInt32LittleEndian(header, 28);
            Add(section, (flags & 0x2000) != 0 ? "Family ID" : "File size/family field", "0x" + familyOrSize.ToString("X8", CultureInfo.InvariantCulture));
        }

        private static bool LooksLikeTrxFirmware(byte[] header)
        {
            return StartsWith(header, Encoding.ASCII.GetBytes("HDR0")) && header.Length >= 28;
        }

        private static void AddTrxInfo(ReportSection section, byte[] header)
        {
            Add(section, "Magic", "HDR0");
            Add(section, "Declared length", FormatBytes(ReadUInt32LittleEndian(header, 4)));
            Add(section, "CRC32", "0x" + ReadUInt32LittleEndian(header, 8).ToString("X8", CultureInfo.InvariantCulture));
            Add(section, "Flags", "0x" + ReadUInt16LittleEndian(header, 12).ToString("X4", CultureInfo.InvariantCulture));
            Add(section, "Version", ReadUInt16LittleEndian(header, 14).ToString(CultureInfo.InvariantCulture));
            var offsets = new List<string>();
            for (var i = 0; i < 4 && 16 + i * 4 + 4 <= header.Length; i++)
            {
                var value = ReadUInt32LittleEndian(header, 16 + i * 4);
                if (value != 0)
                    offsets.Add("offset[" + i.ToString(CultureInfo.InvariantCulture) + "] 0x" + value.ToString("X", CultureInfo.InvariantCulture));
            }
            if (offsets.Count > 0)
                Add(section, "Partition offsets", string.Join("\r\n", offsets));
        }

        private static bool LooksLikeFlattenedDeviceTree(byte[] header)
        {
            return header.Length >= 40 && ReadUInt32BigEndian(header, 0) == 0xD00DFEED;
        }

        private static void AddFlattenedDeviceTreeInfo(ReportSection section, byte[] header)
        {
            Add(section, "Magic", "0xD00DFEED");
            Add(section, "Total size", FormatBytes(ReadUInt32BigEndian(header, 4)));
            Add(section, "Structure block offset", "0x" + ReadUInt32BigEndian(header, 8).ToString("X", CultureInfo.InvariantCulture));
            Add(section, "Strings block offset", "0x" + ReadUInt32BigEndian(header, 12).ToString("X", CultureInfo.InvariantCulture));
            Add(section, "Memory reserve map offset", "0x" + ReadUInt32BigEndian(header, 16).ToString("X", CultureInfo.InvariantCulture));
            Add(section, "Version", ReadUInt32BigEndian(header, 20).ToString(CultureInfo.InvariantCulture));
            Add(section, "Last compatible version", ReadUInt32BigEndian(header, 24).ToString(CultureInfo.InvariantCulture));
            Add(section, "Strings block size", FormatBytes(ReadUInt32BigEndian(header, 32)));
            Add(section, "Structure block size", FormatBytes(ReadUInt32BigEndian(header, 36)));
        }

        private static bool LooksLikeIntelHex(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".hex" && ext != ".ihex" && ext != ".ihx")
                return false;
            var text = SampleText(header);
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Take(20).ToArray();
            return lines.Length > 0 && lines.All(line => Regex.IsMatch(line.Trim(), @"^:[0-9A-Fa-f]{10,}$"));
        }

        private static void AddIntelHexInfo(ReportSection section, byte[] header)
        {
            var lines = SampleText(header).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => Regex.IsMatch(line, @"^:[0-9A-Fa-f]{10,}$"))
                .Take(500)
                .ToArray();
            Add(section, "Sampled records", lines.Length.ToString(CultureInfo.InvariantCulture));
            Add(section, "Record types", RecordTypeSummary(lines.Select(line => line.Substring(7, 2)), IntelHexRecordTypeName));
            var dataBytes = lines.Sum(line => ParseHexByte(line, 1));
            Add(section, "Data bytes in sample", FormatBytes(dataBytes));
            if (lines.Any(line => line.StartsWith(":00000001", StringComparison.OrdinalIgnoreCase)))
                Add(section, "EOF record", "Present in sample");
        }

        private static bool LooksLikeMotorolaSRecord(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var knownExt = ext == ".s19" || ext == ".s28" || ext == ".s37" || ext == ".srec" || ext == ".mot" || ext == ".s";
            if (!knownExt)
                return false;
            var lines = SampleText(header).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Take(20).ToArray();
            return lines.Length > 0 && lines.All(line => Regex.IsMatch(line.Trim(), @"^S[0-9][0-9A-Fa-f]{4,}$"));
        }

        private static void AddMotorolaSRecordInfo(ReportSection section, byte[] header)
        {
            var lines = SampleText(header).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => Regex.IsMatch(line, @"^S[0-9][0-9A-Fa-f]{4,}$"))
                .Take(500)
                .ToArray();
            Add(section, "Sampled records", lines.Length.ToString(CultureInfo.InvariantCulture));
            Add(section, "Record types", RecordTypeSummary(lines.Select(line => line.Substring(0, 2)), SRecordTypeName));
            var dataBytes = lines.Sum(line => (long)SRecordDataByteCount(line));
            Add(section, "Data bytes in sample", FormatBytes(dataBytes));
            var headerRecord = lines.FirstOrDefault(line => line.StartsWith("S0", StringComparison.OrdinalIgnoreCase));
            if (headerRecord != null)
            {
                var name = DecodeSRecordHeaderText(headerRecord);
                if (!string.IsNullOrWhiteSpace(name))
                    Add(section, "Header text", name);
            }
        }

        private static string RecordTypeSummary(IEnumerable<string> types, Func<string, string> describe)
        {
            var rows = types
                .GroupBy(type => type.ToUpperInvariant())
                .OrderBy(group => group.Key)
                .Select(group => group.Key + " - " + describe(group.Key) + ": " + group.Count().ToString(CultureInfo.InvariantCulture))
                .ToArray();
            return rows.Length == 0 ? "(none in sample)" : string.Join("\r\n", rows);
        }

        private static string IntelHexRecordTypeName(string type)
        {
            switch (type)
            {
                case "00": return "data";
                case "01": return "end of file";
                case "02": return "extended segment address";
                case "03": return "start segment address";
                case "04": return "extended linear address";
                case "05": return "start linear address";
                default: return "record type";
            }
        }

        private static string SRecordTypeName(string type)
        {
            switch (type.ToUpperInvariant())
            {
                case "S0": return "header";
                case "S1": return "data, 16-bit address";
                case "S2": return "data, 24-bit address";
                case "S3": return "data, 32-bit address";
                case "S5": return "record count";
                case "S7": return "termination, 32-bit start address";
                case "S8": return "termination, 24-bit start address";
                case "S9": return "termination, 16-bit start address";
                default: return "record type";
            }
        }

        private static int SRecordDataByteCount(string line)
        {
            if (line.Length < 4)
                return 0;
            var count = ParseHexByte(line, 2);
            var addressBytes = line.StartsWith("S1", StringComparison.OrdinalIgnoreCase) || line.StartsWith("S5", StringComparison.OrdinalIgnoreCase) || line.StartsWith("S9", StringComparison.OrdinalIgnoreCase) ? 2 :
                line.StartsWith("S2", StringComparison.OrdinalIgnoreCase) || line.StartsWith("S6", StringComparison.OrdinalIgnoreCase) || line.StartsWith("S8", StringComparison.OrdinalIgnoreCase) ? 3 :
                line.StartsWith("S3", StringComparison.OrdinalIgnoreCase) || line.StartsWith("S7", StringComparison.OrdinalIgnoreCase) ? 4 : 2;
            return Math.Max(0, count - addressBytes - 1);
        }

        private static string DecodeSRecordHeaderText(string line)
        {
            var count = ParseHexByte(line, 2);
            var dataChars = Math.Max(0, count - 3) * 2;
            if (line.Length < 8 + dataChars)
                return string.Empty;
            var bytes = new List<byte>();
            for (var i = 8; i + 1 < 8 + dataChars; i += 2)
                bytes.Add((byte)ParseHexByte(line, i));
            return CleanMetadataText(Encoding.ASCII.GetString(bytes.ToArray()).TrimEnd('\0'));
        }

        private static int ParseHexByte(string value, int offset)
        {
            if (string.IsNullOrEmpty(value) || offset + 2 > value.Length)
                return 0;
            int parsed;
            return int.TryParse(value.Substring(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
        }

        private static string ReadNullTerminated(byte[] data, int offset, int length)
        {
            if (offset >= data.Length || length <= 0)
                return string.Empty;
            var count = Math.Min(length, data.Length - offset);
            var end = offset;
            while (end < offset + count && data[end] != 0)
                end++;
            return CleanMetadataText(Encoding.ASCII.GetString(data, offset, end - offset));
        }

        private static string SampleText(byte[] data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;
            return Encoding.ASCII.GetString(data.Take(Math.Min(data.Length, 1024 * 1024)).ToArray());
        }

        private static string UnixTimeText(uint timestamp)
        {
            try
            {
                var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                return epoch.AddSeconds(timestamp).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";
            }
            catch
            {
                return timestamp.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string TrimForFirmware(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            value = CleanMetadataText(value);
            return value.Length <= max ? value : value.Substring(0, max) + "...";
        }

        private static string UbootOsName(byte value)
        {
            switch (value)
            {
                case 5: return "Linux";
                case 9: return "VxWorks";
                case 14: return "RTEMS";
                case 17: return "Integrity";
                default: return "code " + value.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string UbootArchName(byte value)
        {
            switch (value)
            {
                case 2: return "ARM";
                case 3: return "Intel x86";
                case 5: return "MIPS";
                case 7: return "PowerPC";
                case 8: return "IBM S/390";
                case 22: return "AArch64";
                case 23: return "RISC-V";
                default: return "code " + value.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string UbootImageTypeName(byte value)
        {
            switch (value)
            {
                case 2: return "kernel";
                case 3: return "ramdisk";
                case 4: return "multi-file image";
                case 5: return "firmware";
                case 6: return "script";
                case 7: return "filesystem";
                case 8: return "flat device tree";
                default: return "code " + value.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string UbootCompressionName(byte value)
        {
            switch (value)
            {
                case 0: return "none";
                case 1: return "gzip";
                case 2: return "bzip2";
                case 3: return "LZMA";
                case 5: return "LZO";
                case 6: return "LZ4";
                case 7: return "Zstandard";
                default: return "code " + value.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
