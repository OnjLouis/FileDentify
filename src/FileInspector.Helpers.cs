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
    {        private static ReportSection AddSection(List<ReportSection> sections, string title)
        {
            var section = new ReportSection { Title = title };
            sections.Add(section);
            return section;
        }

        private static void Add(ReportSection section, string title, string detail)
        {
            if (section != null && IsMergeableReportNoteTitle(title))
            {
                var existing = section.Items.FirstOrDefault(item => IsMergeableReportNoteTitle(item.Title));
                var newDetail = FormatMergedReportNote(title, detail);
                if (existing != null)
                {
                    if (string.IsNullOrWhiteSpace(newDetail))
                        return;
                    existing.Title = "Notes";
                    var oldDetail = existing.Detail ?? string.Empty;
                    if (oldDetail.IndexOf(newDetail, StringComparison.OrdinalIgnoreCase) >= 0)
                        return;
                    existing.Detail = string.IsNullOrWhiteSpace(oldDetail)
                        ? newDetail
                        : oldDetail.TrimEnd() + Environment.NewLine + Environment.NewLine + newDetail;
                    return;
                }
                section.Items.Add(new ReportItem { Title = "Notes", Detail = newDetail });
                return;
            }
            section.Items.Add(new ReportItem { Title = title, Detail = detail ?? string.Empty });
        }

        private static bool IsMergeableReportNoteTitle(string title)
        {
            var text = (title ?? string.Empty).Trim();
            return string.Equals(text, "Notes", StringComparison.OrdinalIgnoreCase) ||
                text.EndsWith(" note", StringComparison.OrdinalIgnoreCase) ||
                text.EndsWith(" notes", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatMergedReportNote(string title, string detail)
        {
            var text = detail ?? string.Empty;
            var label = (title ?? string.Empty).Trim();
            if (string.Equals(label, "Notes", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(label))
                return text;
            return label + ": " + text;
        }

        private static void AddFileDentifyDatabaseInfo(List<ReportSection> sections, string path, byte[] header, long length)
        {
            AddFileDentifyDatabaseInfo(sections, path, header, length, FileDentifyDatabaseTypeName(path, header, length));
        }

        private static void AddFileDentifyDatabaseInfo(List<ReportSection> sections, string path, byte[] header, long length, string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return;

            var section = AddSection(sections, "FileDentify database");
            Add(section, "Best match", typeName);
            Add(section, "Source", "FileDentify built-in file-type database");
            Add(section, "Detection basis", FileDentifyDatabaseDetectionBasis(path, header));
            if (string.IsNullOrWhiteSpace(Path.GetExtension(path)))
                Add(section, "Extensionless file", "The filename has no extension, so FileDentify relied on header, filename, path, or structure clues.");
        }

        private static string FileDentifyDatabaseTypeName(string path, byte[] header, long length)
        {
            if (IsWindowsShortcut(header)) return "Windows shortcut (.lnk)";
            if (IsInternetShortcut(path, header)) return "Internet shortcut or web favorite (.url)";
            var savedReportType = SavedReportTypeName(path, header);
            if (savedReportType != null) return savedReportType;
            var developerType = DeveloperFormatTypeName(path, header);
            if (developerType != null) return developerType;
            var developerAppResourceType = DeveloperAppResourceTypeName(path, header);
            if (developerAppResourceType != null) return developerAppResourceType;
            var backupConfigType = BackupConfigTypeName(path, header);
            if (backupConfigType != null) return backupConfigType;
            var legacySoundBankType = LegacySoundBankTypeName(path, header, length);
            if (legacySoundBankType != null) return legacySoundBankType;
            var ensoniqType = EnsoniqTypeName(path, header);
            if (ensoniqType != null) return ensoniqType;
            var qwsType = QwsTypeName(path, header);
            if (qwsType != null) return qwsType;
            var nvdaAddonType = NvdaAddonTypeName(path, header);
            if (nvdaAddonType != null) return nvdaAddonType;
            var gameType = GameFileTypeName(path, header);
            if (gameType != null) return gameType;
            var legacyMusicType = LegacyMusicTypeName(path, header);
            if (legacyMusicType != null) return legacyMusicType;
            if (header.Length >= 32 && AsciiPreview(header, 32).Contains("Roland SRX"))
                return "Roland SRX expansion ROM image";
            var mobileToneType = MobilePhoneToneTypeName(path, header);
            if (mobileToneType != null) return mobileToneType;
            var sampleLibraryType = SampleLibraryTypeName(path, header);
            if (sampleLibraryType != null) return sampleLibraryType;
            if (header.Length >= 4 && StartsWith(header, Encoding.ASCII.GetBytes("UFS2")))
                return "UVI/Falcon UFS sample library container";
            if (string.Equals(Path.GetExtension(path), ".ufs", StringComparison.OrdinalIgnoreCase))
                return "UFS sample-library container";
            if (string.Equals(Path.GetExtension(path), ".blob", StringComparison.OrdinalIgnoreCase))
                return "Binary blob asset or metadata container";
            var clipmanType = ClipmanTypeName(path, header);
            if (clipmanType != null) return clipmanType;
            var symbianAppType = SymbianAppResourceTypeName(path, header);
            if (symbianAppType != null) return symbianAppType;
            var midletType = JavaMidletTypeName(path, header);
            if (midletType != null) return midletType;
            var nativeInstrumentsType = NativeInstrumentsTypeName(path);
            if (nativeInstrumentsType != null) return nativeInstrumentsType;
            var steinbergType = SteinbergCubaseTypeName(path);
            if (steinbergType != null) return steinbergType;
            var rolandCloudType = RolandCloudTypeName(path, header);
            if (rolandCloudType != null) return rolandCloudType;
            var musicProjectType = MusicProjectFormatTypeName(path, header);
            if (musicProjectType != null) return musicProjectType;
            var projectSidecarType = ProjectSidecarTypeName(path, header);
            if (projectSidecarType != null) return projectSidecarType;
            var audioSupportType = AudioSupportTypeName(path, header);
            if (audioSupportType != null) return audioSupportType;
            var macAudioPluginType = MacAudioPluginTypeName(path, header);
            if (macAudioPluginType != null) return macAudioPluginType;
            var appleType = AppleFormatTypeName(path, header);
            if (appleType != null) return appleType;
            var firmwareType = FirmwareTypeName(path, header);
            if (firmwareType != null) return firmwareType;
            var hardwareIdType = HardwareIdDatabaseTypeName(path, header);
            if (hardwareIdType != null) return hardwareIdType;
            var accessibilityDataType = AccessibilityDataTypeName(path, header);
            if (accessibilityDataType != null) return accessibilityDataType;
            var legacyAppResourceType = LegacyAppResourceTypeName(path, header);
            if (legacyAppResourceType != null) return legacyAppResourceType;
            var personalDataType = PersonalDataTypeName(path, header);
            if (personalDataType != null) return personalDataType;
            var windowsSystemType = WindowsSystemTypeName(path, header);
            if (windowsSystemType != null) return windowsSystemType;
            var commonDataType = CommonDataTypeName(path, header);
            if (commonDataType != null) return commonDataType;
            var installerDataType = InstallerDataTypeName(path, header);
            if (installerDataType != null) return installerDataType;
            var virtualMachineMetadataType = VirtualMachineMetadataTypeName(path, header);
            if (virtualMachineMetadataType != null) return virtualMachineMetadataType;
            if (StartsWith(header, Encoding.ASCII.GetBytes("MThd")))
                return "Standard MIDI file";
            return null;
        }

        private static string FileDentifyDatabaseDetectionBasis(string path, byte[] header)
        {
            var parts = new List<string>();
            var ext = Path.GetExtension(path);
            if (string.IsNullOrWhiteSpace(ext))
                parts.Add("no extension");
            else
                parts.Add("extension " + ext);
            if (header.Length >= 4)
                parts.Add("sampled header");
            parts.Add("FileDentify rules");
            return string.Join(", ", parts.ToArray());
        }

        private static byte[] ReadPrefix(string path, int maxBytes)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var count = (int)Math.Min(maxBytes, fs.Length);
                var data = new byte[count];
                var offset = 0;
                while (offset < count)
                {
                    var read = fs.Read(data, offset, count - offset);
                    if (read == 0)
                        break;
                    offset += read;
                }
                if (offset == count)
                    return data;
                Array.Resize(ref data, offset);
                return data;
            }
        }

        private static byte[] ReadSuffix(string path, int maxBytes)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var count = (int)Math.Min(maxBytes, fs.Length);
                var data = new byte[count];
                fs.Position = fs.Length - count;
                var offset = 0;
                while (offset < count)
                {
                    var read = fs.Read(data, offset, count - offset);
                    if (read == 0)
                        break;
                    offset += read;
                }
                if (offset == count)
                    return data;
                Array.Resize(ref data, offset);
                return data;
            }
        }

        private static string GuessType(string path, byte[] header, long length, LibmagicResult libmagic)
        {
            var matches = SignatureMatcher.Match(header, path).ToArray();
            if (IsWindowsShortcut(header))
                return "Windows shortcut (.lnk)";
            if (IsInternetShortcut(path, header))
                return "Internet shortcut or web favorite (.url)";
            var savedReportType = SavedReportTypeName(path, header);
            if (savedReportType != null)
                return savedReportType;
            var developerType = DeveloperFormatTypeName(path, header);
            if (developerType != null)
                return developerType;
            var developerAppResourceType = DeveloperAppResourceTypeName(path, header);
            if (developerAppResourceType != null)
                return developerAppResourceType;
            var backupConfigType = BackupConfigTypeName(path, header);
            if (backupConfigType != null)
                return backupConfigType;
            var legacySoundBankType = LegacySoundBankTypeName(path, header, length);
            if (legacySoundBankType != null)
                return legacySoundBankType;
            var ensoniqType = EnsoniqTypeName(path, header);
            if (ensoniqType != null)
                return ensoniqType;
            var qwsType = QwsTypeName(path, header);
            if (qwsType != null)
                return qwsType;
            var nvdaAddonType = NvdaAddonTypeName(path, header);
            if (nvdaAddonType != null)
                return nvdaAddonType;
            var gameType = GameFileTypeName(path, header);
            if (gameType != null)
                return gameType;
            var legacyMusicType = LegacyMusicTypeName(path, header);
            if (legacyMusicType != null)
                return legacyMusicType;
            if (header.Length >= 32 && AsciiPreview(header, 32).Contains("Roland SRX"))
                return "Roland SRX expansion ROM image";
            var mobileToneType = MobilePhoneToneTypeName(path, header);
            if (mobileToneType != null)
                return mobileToneType;
            var sampleLibraryType = SampleLibraryTypeName(path, header);
            if (sampleLibraryType != null)
                return sampleLibraryType;
            if (header.Length >= 4 && StartsWith(header, Encoding.ASCII.GetBytes("UFS2")))
                return "UVI/Falcon UFS sample library container";
            if (string.Equals(Path.GetExtension(path), ".ufs", StringComparison.OrdinalIgnoreCase))
                return "UFS sample-library container";
            if (string.Equals(Path.GetExtension(path), ".blob", StringComparison.OrdinalIgnoreCase))
                return "Binary blob asset or metadata container";
            var clipmanType = ClipmanTypeName(path, header);
            if (clipmanType != null)
                return clipmanType;
            var symbianAppType = SymbianAppResourceTypeName(path, header);
            if (symbianAppType != null)
                return symbianAppType;
            var midletType = JavaMidletTypeName(path, header);
            if (midletType != null)
                return midletType;
            var nativeInstrumentsType = NativeInstrumentsTypeName(path);
            if (nativeInstrumentsType != null)
                return nativeInstrumentsType;
            var steinbergType = SteinbergCubaseTypeName(path);
            if (steinbergType != null)
                return steinbergType;
            var rolandCloudType = RolandCloudTypeName(path, header);
            if (rolandCloudType != null)
                return rolandCloudType;
            var musicProjectType = MusicProjectFormatTypeName(path, header);
            if (musicProjectType != null)
                return musicProjectType;
            var projectSidecarType = ProjectSidecarTypeName(path, header);
            if (projectSidecarType != null)
                return projectSidecarType;
            var audioSupportType = AudioSupportTypeName(path, header);
            if (audioSupportType != null)
                return audioSupportType;
            var macAudioPluginType = MacAudioPluginTypeName(path, header);
            if (macAudioPluginType != null)
                return macAudioPluginType;
            var appleType = AppleFormatTypeName(path, header);
            if (appleType != null)
                return appleType;
            var firmwareType = FirmwareTypeName(path, header);
            if (firmwareType != null)
                return firmwareType;
            var hardwareIdType = HardwareIdDatabaseTypeName(path, header);
            if (hardwareIdType != null)
                return hardwareIdType;
            var accessibilityDataType = AccessibilityDataTypeName(path, header);
            if (accessibilityDataType != null)
                return accessibilityDataType;
            var legacyAppResourceType = LegacyAppResourceTypeName(path, header);
            if (legacyAppResourceType != null)
                return legacyAppResourceType;
            var personalDataType = PersonalDataTypeName(path, header);
            if (personalDataType != null)
                return personalDataType;
            var windowsSystemType = WindowsSystemTypeName(path, header);
            if (windowsSystemType != null)
                return windowsSystemType;
            var commonDataType = CommonDataTypeName(path, header);
            if (commonDataType != null)
                return commonDataType;
            var installerDataType = InstallerDataTypeName(path, header);
            if (installerDataType != null)
                return installerDataType;
            var virtualMachineMetadataType = VirtualMachineMetadataTypeName(path, header);
            if (virtualMachineMetadataType != null)
                return virtualMachineMetadataType;
            var symbianType = SymbianPackageTypeName(path, header);
            if (symbianType != null)
                return symbianType;
            var cabinetType = CabinetTypeName(path, header);
            if (cabinetType != null)
                return cabinetType;
            var windowsImageType = WindowsImageTypeName(path, header);
            if (windowsImageType != null)
                return windowsImageType;
            var auType = SunAuTypeName(path, header);
            if (auType != null)
                return auType;
            var transportStreamType = MpegTransportStreamTypeName(path, header);
            if (transportStreamType != null)
                return transportStreamType;
            var oggAudioType = OggAudioTypeName(header);
            if (oggAudioType != null)
                return oggAudioType;
            var asfMediaType = AsfMediaTypeName(header);
            if (asfMediaType != null)
                return asfMediaType;
            if (string.Equals(Path.GetExtension(path), ".nrg", StringComparison.OrdinalIgnoreCase))
                return "Nero Burning ROM disc image";
            if (IsZipHeader(header) && string.Equals(Path.GetExtension(path), ".ablbundle", StringComparison.OrdinalIgnoreCase))
                return "Ableton Move/Live bundle (ZIP-compatible container)";
            var zipDocumentType = ZipDocumentTypeName(path, header);
            if (zipDocumentType != null)
                return zipDocumentType;
            var ebookType = EbookTypeName(path, header);
            if (ebookType != null)
                return ebookType;
            var font = FontFormatName(header);
            if (font != null)
                return font;
            var compression = CompressionFormatName(header);
            if (compression != null)
                return compression;
            if (IsOleCompoundFile(header))
            {
                if (string.Equals(Path.GetExtension(path), ".msi", StringComparison.OrdinalIgnoreCase))
                    return "Windows Installer package";
                return "OLE compound document";
            }
            var virtualDisk = VirtualDiskFormatName(path, header);
            if (virtualDisk != null)
                return virtualDisk;
            if (HasIso9660Descriptor(header))
                return "ISO 9660 optical disc image";
            var riff = RiffTypeName(header);
            if (riff != null)
                return riff;
            var isoBrand = IsoBmffTypeName(header);
            if (isoBrand != null)
                return isoBrand;
            if (StartsWith(header, Encoding.ASCII.GetBytes("mozLz40\0")))
                return "Mozilla LZ4-compressed JSON/profile data";
            if (matches.Length > 0)
                return matches[0].Detail;
            if (string.Equals(Path.GetExtension(path), ".dmg", StringComparison.OrdinalIgnoreCase))
            {
                var suffix = ReadSuffix(path, 8192);
                if (IndexOfAscii(suffix, "koly") >= 0)
                    return "Apple UDIF disk image";
                return "Possible Apple disk image";
            }
            if (libmagic != null && ShouldShowLibmagicInSummary(libmagic.Description))
                return libmagic.Description + " (from Unix file/libmagic)";
            if (LooksLikeText(header))
                return "Plain text or text-like data";
            return "Unknown binary data";
        }

        private static bool IsUsefulLibmagicDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return false;
            var value = description.Trim();
            return !value.Equals("data", StringComparison.OrdinalIgnoreCase) &&
                !value.Equals("empty", StringComparison.OrdinalIgnoreCase) &&
                !value.StartsWith("application/octet-stream", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldShowLibmagicInSummary(string description)
        {
            if (!IsUsefulLibmagicDescription(description))
                return false;
            var value = description.Trim();
            if (value.Length > 180)
                return false;
            return value.IndexOf("MS Windows shortcut", StringComparison.OrdinalIgnoreCase) < 0 &&
                value.IndexOf("Internet shortcut", StringComparison.OrdinalIgnoreCase) < 0 &&
                value.IndexOf("Generic INItialization configuration", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static string RiffTypeName(byte[] header)
        {
            if (header.Length < 12 || !StartsWith(header, Encoding.ASCII.GetBytes("RIFF")))
                return null;
            var form = Encoding.ASCII.GetString(header, 8, 4);
            switch (form)
            {
                case "WAVE": return "WAV audio";
                case "WEBP": return "WebP image";
                case "AVI ": return "AVI video";
                case "sfbk": return "SoundFont/SBK sound bank";
                case "DLS ": return "DLS instrument bank";
                case "RMID": return "RIFF MIDI container";
                case "ACON": return "Animated cursor";
                default: return null;
            }
        }

        private static string IsoBmffTypeName(byte[] header)
        {
            if (header.Length < 16 || Encoding.ASCII.GetString(header, 4, 4) != "ftyp")
                return null;
            var boxSize = ReadUInt32BigEndian(header, 0);
            var major = Encoding.ASCII.GetString(header, 8, 4).TrimEnd('\0', ' ');
            var brands = new List<string>();
            for (var i = 16; i + 4 <= header.Length && i < boxSize && brands.Count < 24; i += 4)
                brands.Add(Encoding.ASCII.GetString(header, i, 4).TrimEnd('\0', ' '));
            var hint = IsoBrandHint(major, brands);
            return hint == "ISO base media family" ? "ISO base media file" : hint;
        }

        private static string FontFormatName(byte[] header)
        {
            if (header.Length < 4)
                return null;
            if (StartsWith(header, Encoding.ASCII.GetBytes("OTTO"))) return "OpenType CFF font";
            if (StartsWith(header, Encoding.ASCII.GetBytes("true"))) return "Classic TrueType font";
            if (StartsWith(header, Encoding.ASCII.GetBytes("typ1"))) return "PostScript Type 1 font";
            if (StartsWith(header, Encoding.ASCII.GetBytes("wOFF"))) return "WOFF web font";
            if (StartsWith(header, Encoding.ASCII.GetBytes("wOF2"))) return "WOFF2 web font";
            if (header[0] == 0x00 && header[1] == 0x01 && header[2] == 0x00 && header[3] == 0x00) return "TrueType font";
            return null;
        }

        private static string FontFlavorName(string flavor)
        {
            switch (flavor)
            {
                case "\0\x01\0\0": return "TrueType outlines";
                case "OTTO": return "OpenType CFF outlines";
                case "true": return "Classic TrueType outlines";
                default: return flavor;
            }
        }

        private static string CompressionFormatName(byte[] header)
        {
            if (StartsWith(header, new byte[] { 0xFD, (byte)'7', (byte)'z', (byte)'X', (byte)'Z', 0x00 })) return "XZ compressed data";
            if (StartsWith(header, new byte[] { 0x04, 0x22, 0x4D, 0x18 })) return "LZ4 frame compressed data";
            if (StartsWith(header, new byte[] { 0x28, 0xB5, 0x2F, 0xFD })) return "Zstandard compressed data";
            return null;
        }

        private static bool IsZipHeader(byte[] header)
        {
            return StartsWith(header, Encoding.ASCII.GetBytes("PK\x03\x04")) ||
                StartsWith(header, Encoding.ASCII.GetBytes("PK\x05\x06")) ||
                StartsWith(header, Encoding.ASCII.GetBytes("PK\x07\x08"));
        }

        private static bool IsOleCompoundFile(byte[] header)
        {
            return StartsWith(header, new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 });
        }

        private static bool HasIso9660Descriptor(byte[] header)
        {
            return header.Length >= 0x8006 &&
                header[0x8001] == (byte)'C' &&
                header[0x8002] == (byte)'D' &&
                header[0x8003] == (byte)'0' &&
                header[0x8004] == (byte)'0' &&
                header[0x8005] == (byte)'1';
        }

        private static string VirtualDiskFormatName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path);
            if (StartsWith(header, Encoding.ASCII.GetBytes("KDMV"))) return "VMware sparse virtual disk";
            if (StartsWith(header, Encoding.ASCII.GetBytes("vhdxfile"))) return "Hyper-V VHDX virtual disk";
            if (StartsWith(header, Encoding.GetEncoding(28591).GetBytes("QFI\xFB"))) return "QEMU QCOW2 virtual disk";
            if (header.Length >= 0x44 && header[0x40] == 0x7F && header[0x41] == 0x10 && header[0x42] == 0xDA && header[0x43] == 0xBE) return "VirtualBox VDI virtual disk";
            if (LooksLikeText(header))
            {
                var text = Encoding.ASCII.GetString(header.Take(Math.Min(header.Length, 4096)).ToArray());
                if (text.IndexOf("# Disk DescriptorFile", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("createType=", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "VMware VMDK descriptor";
                if (text.IndexOf("<Envelope", StringComparison.OrdinalIgnoreCase) >= 0 && string.Equals(ext, ".ovf", StringComparison.OrdinalIgnoreCase))
                    return "Open Virtualization Format descriptor";
            }
            if (string.Equals(ext, ".vhd", StringComparison.OrdinalIgnoreCase)) return "Hyper-V VHD virtual disk";
            if (string.Equals(ext, ".vhdx", StringComparison.OrdinalIgnoreCase)) return "Hyper-V VHDX virtual disk";
            return null;
        }

        private static void AddKeyValueLine(ReportSection section, string text, string key)
        {
            var value = FindDescriptorValue(text, key);
            if (!string.IsNullOrWhiteSpace(value))
                Add(section, key, value);
        }

        private static string FindDescriptorValue(string text, string key)
        {
            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                    continue;
                return trimmed.Substring(key.Length + 1).Trim().Trim('"');
            }
            return string.Empty;
        }

        private static bool IsPrintableAscii(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            foreach (var ch in value)
                if (ch < 32 || ch >= 127)
                    return false;
            return true;
        }

        private static bool LooksLikeText(byte[] data)
        {
            if (data.Length == 0)
                return false;
            var sample = data.Take(Math.Min(data.Length, 4096)).ToArray();
            var bad = sample.Count(b => b < 9 || (b > 13 && b < 32));
            return bad <= Math.Max(1, sample.Length / 50);
        }

        private static void AddHashInfo(ReportSection section, string path, long length)
        {
            const long fullHashLimit = 64L * 1024L * 1024L;
            if (length <= fullHashLimit)
            {
                Add(section, "SHA-256", Sha256(path, length));
            }
            else
            {
                Add(section, "SHA-256 first 64 MiB", Sha256(path, fullHashLimit));
                Add(section, "Full SHA-256", "Skipped by default for large files so inspection stays responsive.");
            }
        }

        private static string Sha256(string path, long maxBytes)
        {
            using (var sha = SHA256.Create())
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var buffer = new byte[1024 * 1024];
                var remaining = Math.Min(maxBytes, fs.Length);
                while (remaining > 0)
                {
                    var read = fs.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                    if (read == 0)
                        break;
                    remaining -= read;
                    sha.TransformBlock(buffer, 0, read, null, 0);
                }
                sha.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "bytes", "KiB", "MiB", "GiB", "TiB" };
            double value = bytes;
            var unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return value.ToString(unit == 0 ? "0" : "0.##", CultureInfo.InvariantCulture) + " " + units[unit];
        }

        private static string FormatUnsignedBytes(ulong bytes)
        {
            if (bytes <= long.MaxValue)
                return FormatBytes((long)bytes);
            return bytes.ToString(CultureInfo.InvariantCulture) + " bytes";
        }

        private static string AsciiPreview(byte[] data, int count)
        {
            var sb = new StringBuilder();
            foreach (var b in data.Take(count))
                sb.Append(b >= 32 && b < 127 ? (char)b : '.');
            return sb.ToString();
        }

        private static int IndexOfAscii(byte[] data, string text)
        {
            if (data == null || string.IsNullOrEmpty(text))
                return -1;
            var needle = Encoding.ASCII.GetBytes(text);
            for (var i = 0; i <= data.Length - needle.Length; i++)
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

        private static string HexPreview(byte[] data, int count)
        {
            var sb = new StringBuilder();
            var limit = Math.Min(data.Length, count);
            for (var i = 0; i < limit; i += 16)
            {
                sb.Append(i.ToString("X8", CultureInfo.InvariantCulture));
                sb.Append("  ");
                for (var j = 0; j < 16 && i + j < limit; j++)
                    sb.Append(data[i + j].ToString("X2", CultureInfo.InvariantCulture)).Append(' ');
                sb.Append(" ");
                for (var j = 0; j < 16 && i + j < limit; j++)
                {
                    var b = data[i + j];
                    sb.Append(b >= 32 && b < 127 ? (char)b : '.');
                }
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        private static void AddByteStats(ReportSection section, byte[] data, long fileLength)
        {
            if (data.Length == 0)
            {
                Add(section, "Empty file", "The file has no bytes.");
                return;
            }
            var unique = data.Distinct().Count();
            Add(section, "Sample size", FormatBytes(data.Length) + " from " + FormatBytes(fileLength));
            Add(section, "Unique byte values in sample", unique.ToString(CultureInfo.InvariantCulture) + " of 256");
            Add(section, "Entropy estimate", ShannonEntropy(data).ToString("0.000", CultureInfo.InvariantCulture) + " bits per byte");
            var top = data.GroupBy(b => b).Select(g => new { Byte = g.Key, Count = g.Count() }).OrderByDescending(g => g.Count).Take(16);
            var lines = top.Select(g => "0x" + g.Byte.ToString("X2", CultureInfo.InvariantCulture) + "  " + g.Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Most common bytes", string.Join("\r\n", lines.ToArray()));
        }

        private static double ShannonEntropy(byte[] data)
        {
            if (data.Length == 0)
                return 0;
            var counts = new int[256];
            foreach (var b in data)
                counts[b]++;
            double entropy = 0;
            foreach (var count in counts)
            {
                if (count == 0)
                    continue;
                var p = (double)count / data.Length;
                entropy -= p * (Math.Log(p) / Math.Log(2));
            }
            return entropy;
        }
    }
}

