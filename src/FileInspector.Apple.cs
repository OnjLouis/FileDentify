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
        private static string AppleFormatTypeName(string path, byte[] header)
        {
            if (IsSparseBundleBandPath(path))
                return "Apple sparse-bundle band file";
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (LooksLikeMachO(header))
                return ext == ".dylib" ? "Mach-O dynamic library" : "Mach-O binary";
            if (Path.GetFileName(path).Equals("Info.plist", StringComparison.OrdinalIgnoreCase) && IsInsideAppleBundle(path))
                return "Apple application or bundle metadata";
            if (ext == ".strings") return "Apple localization strings";
            if (ext == ".car" && StartsWith(header, Encoding.ASCII.GetBytes("BOMStore"))) return "Apple compiled asset catalog";
            if (ext == ".nib") return "Apple Interface Builder nib";
            if (ext == ".metallib") return "Apple Metal shader library";
            if (ext == ".sdef") return "Apple scripting definition";
            if (ext == ".entitlements") return "Apple code-signing entitlements";
            if (ext == ".xcprivacy") return "Apple privacy manifest";
            if (ext == ".mobileconfig") return "Apple configuration profile";
            if (Path.GetFileName(path).Equals(".DS_Store", StringComparison.OrdinalIgnoreCase) && LooksLikeDsStore(header)) return "macOS Finder .DS_Store metadata";
            if (ext == ".ipa" && IsZipHeader(header)) return "iOS application archive";
            if (ext == ".ipsw" && IsZipHeader(header)) return "Apple device firmware restore package";
            if (ext == ".pkg" && StartsWith(header, Encoding.ASCII.GetBytes("xar!"))) return "macOS installer package";
            if (ext == ".crash") return "Apple crash report";
            if (ext == ".ips") return "Apple diagnostic report";
            if (IsAppleMobileBackupStoredFile(path)) return "Apple mobile backup stored file";
            return null;
        }

        private static void AddAppleFormatInfo(List<ReportSection> sections, string path, byte[] header)
        {
            AddAppleBundleInfo(sections, path, header);
            AddAppleResourceInfo(sections, path, header);
            AddAppleZipPackageInfo(sections, path, header);
            AddAppleMobileBackupStoredFileInfo(sections, path, header);
            AddSparseBundleBandFileInfo(sections, path);
        }

        private static bool IsSparseBundleBandPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !string.IsNullOrEmpty(Path.GetExtension(path)))
                return false;
            var parent = Path.GetDirectoryName(path);
            return string.Equals(Path.GetFileName(parent), "bands", StringComparison.OrdinalIgnoreCase) &&
                Regex.IsMatch(Path.GetFileName(path) ?? string.Empty, "^[0-9a-f]{1,16}$", RegexOptions.IgnoreCase);
        }

        private static void AddSparseBundleBandFileInfo(List<ReportSection> sections, string path)
        {
            if (!IsSparseBundleBandPath(path))
                return;
            var file = new FileInfo(path);
            var section = AddSection(sections, "Apple sparse-bundle band");
            Add(section, "Format hint", "Apple sparse-bundle disk-image band file");
            Add(section, "Band index", file.Name);
            Add(section, "Band size", FormatBytes(file.Length));
            var package = Directory.GetParent(file.DirectoryName ?? string.Empty);
            if (package != null && string.Equals(Path.GetExtension(package.FullName), ".sparsebundle", StringComparison.OrdinalIgnoreCase))
            {
                Add(section, "Sparse bundle", package.Name);
                Add(section, "Info.plist", File.Exists(Path.Combine(package.FullName, "Info.plist")) ? "Present" : "Not found");
            }
            Add(section, "Detection basis", "Hexadecimal band filename inside a bands folder; payload bytes may be encrypted or otherwise opaque.");
            Add(section, "Notes", "Sparse-bundle bands are raw chunks of a directory-backed Apple disk image, commonly used by Time Machine. Keep them with the parent .sparsebundle, Info.plist, and other bands; a band is not independently mountable and FileDentify does not decrypt or traverse it.");
        }

        private static void AddAppleMobileBackupStoredFileInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (!IsAppleMobileBackupStoredFile(path))
                return;

            var backupRoot = AppleMobileBackupRootForStoredFile(path);
            var section = AddSection(sections, "Apple mobile backup file");
            Add(section, "Format hint", "Stored file from an Apple iPhone/iPad backup");
            Add(section, "Backup identifier", Path.GetFileName(backupRoot));
            Add(section, "Shard folder", Path.GetFileName(Path.GetDirectoryName(path)));
            Add(section, "File ID", Path.GetFileName(path));
            Add(section, "Manifest.db", File.Exists(Path.Combine(backupRoot, "Manifest.db")) ? "Present in backup root" : "Not found");
            Add(section, "Likely original type", BackupStoredFilePayloadType(header));
            Add(section, "Lookup note", "The original app domain and relative path are stored in Manifest.db. FileDentify reports the hashed backup payload without extracting personal content.");
            Add(section, "Privacy note", "This stored file may contain personal app data from the device backup.");
        }

        private static bool IsAppleMobileBackupStoredFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;
            var fileName = Path.GetFileName(path);
            if (!Regex.IsMatch(fileName ?? string.Empty, "^[0-9a-f]{40}$", RegexOptions.IgnoreCase))
                return false;
            var shard = Path.GetFileName(Path.GetDirectoryName(path));
            if (!Regex.IsMatch(shard ?? string.Empty, "^[0-9a-f]{2}$", RegexOptions.IgnoreCase))
                return false;
            var root = AppleMobileBackupRootForStoredFile(path);
            return IsAppleMobileBackupDirectory(root);
        }

        private static string AppleMobileBackupRootForStoredFile(string path)
        {
            var dir = new FileInfo(path).Directory;
            return dir == null || dir.Parent == null ? string.Empty : dir.Parent.FullName;
        }

        private static string BackupStoredFilePayloadType(byte[] header)
        {
            if (header == null || header.Length == 0)
                return "Empty stored file";
            if (StartsWith(header, Encoding.ASCII.GetBytes("SQLite format 3")))
                return "SQLite database";
            if (StartsWith(header, Encoding.ASCII.GetBytes("bplist00")))
                return "Binary property list";
            if (StartsWith(header, Encoding.ASCII.GetBytes("<?xml")) || LooksLikeText(header))
                return "Text or XML-like data";
            if (header.Length >= 8 && header[0] == 0x89 && header[1] == (byte)'P' && header[2] == (byte)'N' && header[3] == (byte)'G')
                return "PNG image";
            if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                return "JPEG image";
            if (StartsWith(header, Encoding.ASCII.GetBytes("ftyp")))
                return "ISO base media payload";
            if (header.Length >= 12 && header[4] == (byte)'f' && header[5] == (byte)'t' && header[6] == (byte)'y' && header[7] == (byte)'p')
                return "ISO base media payload";
            if (StartsWith(header, Encoding.ASCII.GetBytes("PK\003\004")))
                return "ZIP-compatible payload";
            return "Binary or proprietary payload";
        }

        private static void AddAppleBundleInfo(List<ReportSection> sections, string path, byte[] header)
        {
            var isInfoPlist = Path.GetFileName(path).Equals("Info.plist", StringComparison.OrdinalIgnoreCase) && IsInsideAppleBundle(path);
            if (!isInfoPlist)
                return;

            var bundlePath = FindAppleBundlePath(path);
            var bundleName = Path.GetFileName(bundlePath);
            var section = AddSection(sections, "Apple bundle");
            Add(section, "Bundle kind", AppleBundleKind(bundlePath));
            Add(section, "Bundle name", bundleName);
            Add(section, "Metadata file", "Contents\\Info.plist inside an Apple bundle");

            var text = LooksLikeText(header) ? Encoding.UTF8.GetString(header, 0, Math.Min(header.Length, 65536)) : string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
            {
                AddPlistValue(section, text, "CFBundleDisplayName", "Display name");
                AddPlistValue(section, text, "CFBundleName", "Bundle display name");
                AddPlistValue(section, text, "CFBundleIdentifier", "Bundle identifier");
                AddPlistValue(section, text, "CFBundleExecutable", "Executable");
                AddPlistValue(section, text, "CFBundlePackageType", "Package type");
                AddPlistValue(section, text, "CFBundleShortVersionString", "Short version");
                AddPlistValue(section, text, "CFBundleVersion", "Bundle version");
                AddPlistValue(section, text, "LSMinimumSystemVersion", "Minimum macOS");
                AddPlistValue(section, text, "DTPlatformName", "Built for platform");
                AddPlistValue(section, text, "DTSDKName", "SDK");
                AddPlistValue(section, text, "DTXcode", "Xcode");
                AddPlistValue(section, text, "NSPrincipalClass", "Principal class");
            }
        }

        private static void AddAppleResourceInfo(List<ReportSection> sections, string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (LooksLikeMachO(header))
            {
                AddMachOInfo(sections, path, header);
                return;
            }

            if (Path.GetFileName(path).Equals(".DS_Store", StringComparison.OrdinalIgnoreCase) && LooksLikeDsStore(header))
            {
                var section = AddSection(sections, "Apple Finder metadata");
                Add(section, "Format hint", "macOS Finder .DS_Store metadata");
                Add(section, "Header marker", "Bud1");
                Add(section, "Folder", Path.GetDirectoryName(path) ?? string.Empty);
                if (header.Length >= 32)
                {
                    Add(section, "Store version field", ReadUInt32BigEndian(header, 0).ToString(CultureInfo.InvariantCulture));
                    Add(section, "Block size-like field", FormatBytes(ReadUInt32BigEndian(header, 12)));
                    Add(section, "Root block-like field", "0x" + ReadUInt32BigEndian(header, 16).ToString("X", CultureInfo.InvariantCulture));
                }
                Add(section, "Common use", "Finder stores per-folder view settings, icon positions, comments, and other desktop metadata in .DS_Store files.");
                Add(section, "Notes", "FileDentify reports the .DS_Store container marker and safe header fields only; it does not interpret every Finder record.");
                return;
            }

            if (ext == ".strings")
            {
                var section = AddSection(sections, "Apple localization");
                Add(section, "Format hint", "Apple localized strings file");
                var language = AppleLanguageFromPath(path);
                if (!string.IsNullOrWhiteSpace(language))
                    Add(section, "Language folder", language);
                if (StartsWith(header, Encoding.ASCII.GetBytes("bplist00")))
                    Add(section, "Container", "Binary property list");
                else if (LooksLikeText(header))
                {
                    var lines = FindReadableTextLines(header, 2, 80)
                        .Where(s => s.Contains("=") || s.Contains("\""))
                        .Take(12)
                        .ToArray();
                    if (lines.Length > 0)
                        Add(section, "Visible entries", string.Join(Environment.NewLine, lines));
                }
                return;
            }

            if (ext == ".car" && StartsWith(header, Encoding.ASCII.GetBytes("BOMStore")))
            {
                var section = AddSection(sections, "Apple asset catalog");
                Add(section, "Format hint", "Compiled Apple asset catalog (.car)");
                Add(section, "Header marker", "BOMStore");
                if (header.Length >= 16)
                    Add(section, "Version-like field", ReadUInt32BigEndian(header, 8).ToString(CultureInfo.InvariantCulture));
                Add(section, "Common use", "Compiled images, icons, colors, and other app resources inside macOS or iOS bundles.");
                return;
            }

            if (ext == ".nib")
            {
                var section = AddSection(sections, "Apple interface resource");
                Add(section, "Format hint", "Interface Builder nib resource");
                Add(section, "Common use", "Compiled user-interface resource stored inside an Apple app or framework bundle.");
                return;
            }

            if (ext == ".metallib")
            {
                var section = AddSection(sections, "Apple Metal shader library");
                Add(section, "Format hint", "Compiled Apple Metal shader library");
                Add(section, "File name", Path.GetFileName(path));
                Add(section, "Common use", "Compiled GPU shader code used by macOS or iOS applications.");
                Add(section, "Notes", "FileDentify reports Metal shader libraries as compiled application resources; it does not disassemble shader bytecode.");
                return;
            }

            if (ext == ".sdef")
            {
                var section = AddSection(sections, "Apple scripting definition");
                Add(section, "Format hint", "AppleScript scripting definition");
                Add(section, "File name", Path.GetFileName(path));
                if (LooksLikeText(header))
                {
                    var text = Encoding.UTF8.GetString(header, 0, Math.Min(header.Length, 65536));
                    AddXmlishAttribute(section, text, "suite", "name", "First suite");
                    AddXmlishAttribute(section, text, "command", "name", "First command");
                    Add(section, "Visible command count", Regex.Matches(text, "<command\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Visible class count", Regex.Matches(text, "<class\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
                }
                return;
            }

            if (ext == ".entitlements" || ext == ".xcprivacy")
            {
                var section = AddSection(sections, ext == ".entitlements" ? "Apple entitlements" : "Apple privacy manifest");
                Add(section, "Format hint", ext == ".entitlements" ? "Apple code-signing entitlements" : "Apple privacy manifest");
                Add(section, "File name", Path.GetFileName(path));
                if (LooksLikeText(header))
                {
                    var text = Encoding.UTF8.GetString(header, 0, Math.Min(header.Length, 65536));
                    AddPlistValue(section, text, "com.apple.security.app-sandbox", "App sandbox");
                    AddPlistValue(section, text, "com.apple.security.network.client", "Network client entitlement");
                    AddPlistValue(section, text, "NSPrivacyTracking", "Privacy tracking");
                    AddPlistValue(section, text, "NSPrivacyCollectedDataTypes", "Collected data types");
                    Add(section, "Visible key count", Regex.Matches(text, "<key>", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
                }
                Add(section, "Notes", "FileDentify reports visible plist-style keys only. Entitlements and privacy manifests can describe app capabilities, sandboxing, networking, and data-use declarations.");
                return;
            }

            if (ext == ".mobileconfig")
            {
                var section = AddSection(sections, "Apple configuration profile");
                Add(section, "Format hint", "Apple configuration profile");
                if (LooksLikeText(header))
                {
                    var text = Encoding.UTF8.GetString(header, 0, Math.Min(header.Length, 65536));
                    AddPlistValue(section, text, "PayloadDisplayName", "Payload display name");
                    AddPlistValue(section, text, "PayloadIdentifier", "Payload identifier");
                    AddPlistValue(section, text, "PayloadOrganization", "Organization");
                    AddPlistValue(section, text, "PayloadType", "Payload type");
                    AddPlistValue(section, text, "PayloadUUID", "Payload UUID");
                    AddPlistValue(section, text, "PayloadVersion", "Payload version");
                }
                Add(section, "Privacy note", "Configuration profiles can describe device, network, certificate, and management settings. FileDentify reports visible metadata only.");
                return;
            }

            if (ext == ".pkg" && StartsWith(header, Encoding.ASCII.GetBytes("xar!")))
            {
                var section = AddSection(sections, "macOS installer package");
                Add(section, "Format hint", "XAR-based macOS installer package");
                Add(section, "Header marker", "xar!");
                if (header.Length >= 28)
                {
                    Add(section, "Header size", ReadUInt16BigEndian(header, 4).ToString(CultureInfo.InvariantCulture));
                    Add(section, "Version", ReadUInt16BigEndian(header, 6).ToString(CultureInfo.InvariantCulture));
                    Add(section, "Compressed TOC length", FormatAppleUnsignedBytes(ReadUInt64BigEndian(header, 8)));
                    Add(section, "Uncompressed TOC length", FormatAppleUnsignedBytes(ReadUInt64BigEndian(header, 16)));
                }
                return;
            }
        }

        private static bool LooksLikeMachO(byte[] header)
        {
            if (header == null || header.Length < 4)
                return false;
            var magic = ReadUInt32BigEndian(header, 0);
            return magic == 0xFEEDFACE ||
                magic == 0xFEEDFACF ||
                magic == 0xCEFAEDFE ||
                magic == 0xCFFAEDFE ||
                magic == 0xCAFEBABE ||
                magic == 0xCAFEBABF ||
                magic == 0xBEBAFECA ||
                magic == 0xBFBAFECA;
        }

        private static void AddMachOInfo(List<ReportSection> sections, string path, byte[] header)
        {
            var section = AddSection(sections, "Apple Mach-O binary");
            var ext = Path.GetExtension(path).ToLowerInvariant();
            Add(section, "Format hint", ext == ".dylib" ? "Mach-O dynamic library" : "Mach-O binary");
            Add(section, "Header magic", MachOMagicName(header));
            Add(section, "File role", MachOFileRole(path, header));
            Add(section, "Architectures", MachOArchitectures(header));
            var bundle = FindAppleBundlePath(path);
            if (!string.IsNullOrWhiteSpace(bundle))
                Add(section, "Containing bundle", Path.GetFileName(bundle));
            Add(section, "Notes", "Mach-O is the native executable and library format used by macOS, iOS, and Apple plug-ins. FileDentify reports safe header architecture and role fields only; it does not load or execute the binary.");
        }

        private static string MachOMagicName(byte[] header)
        {
            var magic = ReadUInt32BigEndian(header, 0);
            switch (magic)
            {
                case 0xCAFEBABE: return "CAFEBABE, universal/fat Mach-O";
                case 0xCAFEBABF: return "CAFEBABF, universal/fat Mach-O with 64-bit archive records";
                case 0xBEBAFECA: return "BEBAFECA, byte-swapped universal/fat Mach-O";
                case 0xBFBAFECA: return "BFBAFECA, byte-swapped universal/fat Mach-O with 64-bit archive records";
                case 0xFEEDFACE: return "FEEDFACE, 32-bit Mach-O";
                case 0xFEEDFACF: return "FEEDFACF, 64-bit Mach-O";
                case 0xCEFAEDFE: return "CEFAEDFE, byte-swapped 32-bit Mach-O";
                case 0xCFFAEDFE: return "CFFAEDFE, byte-swapped 64-bit Mach-O";
                default: return "Mach-O";
            }
        }

        private static string MachOFileRole(string path, byte[] header)
        {
            if (Path.GetExtension(path).Equals(".dylib", StringComparison.OrdinalIgnoreCase))
                return "Dynamic library";
            if (header.Length < 16)
                return "Binary";
            var magic = ReadUInt32BigEndian(header, 0);
            if (magic == 0xCAFEBABE || magic == 0xCAFEBABF || magic == 0xBEBAFECA || magic == 0xBFBAFECA)
                return "Universal binary container";
            var littleEndian = magic == 0xCEFAEDFE || magic == 0xCFFAEDFE;
            var fileType = littleEndian ? ReadUInt32LittleEndian(header, 12) : ReadUInt32BigEndian(header, 12);
            switch (fileType)
            {
                case 1: return "Relocatable object";
                case 2: return "Executable";
                case 6: return "Dynamic library";
                case 8: return "Bundle/loadable module";
                case 11: return "Dynamic linker";
                case 10: return "Preloaded executable";
                default: return "Mach-O file type " + fileType.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string MachOArchitectures(byte[] header)
        {
            if (header.Length < 8)
                return "Not enough header data";

            var magic = ReadUInt32BigEndian(header, 0);
            if (magic == 0xCAFEBABE || magic == 0xCAFEBABF)
            {
                var count = ReadUInt32BigEndian(header, 4);
                var entrySize = magic == 0xCAFEBABF ? 32 : 20;
                var entries = new List<string>();
                for (var i = 0; i < Math.Min(count, 8); i++)
                {
                    var offset = 8 + i * entrySize;
                    if (offset + 20 > header.Length)
                        break;
                    var cpu = ReadUInt32BigEndian(header, offset);
                    var size = magic == 0xCAFEBABF && offset + 32 <= header.Length
                        ? ReadUInt64BigEndian(header, offset + 16)
                        : ReadUInt32BigEndian(header, offset + 12);
                    entries.Add(MachOCpuName(cpu) + ", " + FormatUnsignedBytes(size));
                }
                if (entries.Count > 0)
                    return count.ToString(CultureInfo.InvariantCulture) + " architecture(s): " + string.Join(Environment.NewLine, entries.ToArray());
                return count.ToString(CultureInfo.InvariantCulture) + " architecture(s)";
            }

            var littleEndian = magic == 0xCEFAEDFE || magic == 0xCFFAEDFE;
            var cpuType = littleEndian ? ReadUInt32LittleEndian(header, 4) : ReadUInt32BigEndian(header, 4);
            return MachOCpuName(cpuType);
        }

        private static string MachOCpuName(uint cpuType)
        {
            switch (cpuType)
            {
                case 7: return "i386";
                case 0x01000007: return "x86_64";
                case 12: return "ARM";
                case 0x0100000C: return "ARM64";
                case 18: return "PowerPC";
                case 0x01000012: return "PowerPC 64";
                default: return "CPU type 0x" + cpuType.ToString("X", CultureInfo.InvariantCulture);
            }
        }

        private static bool LooksLikeDsStore(byte[] header)
        {
            return header.Length >= 8 &&
                header[0] == 0x00 &&
                header[1] == 0x00 &&
                header[2] == 0x00 &&
                header[3] == 0x01 &&
                header[4] == (byte)'B' &&
                header[5] == (byte)'u' &&
                header[6] == (byte)'d' &&
                header[7] == (byte)'1';
        }

        private static void AddAppleZipPackageInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (!IsZipHeader(header))
                return;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".ipsw" && ext != ".ipa")
                return;

            try
            {
                using (var archive = ZipFile.OpenRead(path))
                {
                    if (ext == ".ipsw")
                        AddIpswInfo(sections, archive);
                    else
                        AddIpaInfo(sections, archive);
                }
            }
            catch (Exception ex)
            {
                Add(AddSection(sections, ext == ".ipsw" ? "Apple firmware package" : "iOS application archive"), "Archive read error", ex.Message);
            }
        }

        private static void AddIpswInfo(List<ReportSection> sections, ZipArchive archive)
        {
            var section = AddSection(sections, "Apple firmware package");
            Add(section, "Format hint", "IPSW restore/update package");
            Add(section, "Container", "ZIP-compatible archive");
            Add(section, "Entry count", archive.Entries.Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Has BuildManifest.plist", (archive.GetEntry("BuildManifest.plist") != null).ToString());
            Add(section, "Has Restore.plist", (archive.GetEntry("Restore.plist") != null).ToString());

            var dmgs = archive.Entries.Where(e => e.FullName.EndsWith(".dmg", StringComparison.OrdinalIgnoreCase)).OrderByDescending(e => e.Length).Take(8).ToArray();
            if (dmgs.Length > 0)
                Add(section, "Largest disk images", string.Join(Environment.NewLine, dmgs.Select(e => e.FullName + " (" + FormatBytes(e.Length) + ")").ToArray()));

            var firmwareEntries = archive.Entries.Where(e =>
                    e.FullName.StartsWith("Firmware/", StringComparison.OrdinalIgnoreCase) ||
                    e.FullName.EndsWith(".im4p", StringComparison.OrdinalIgnoreCase) ||
                    e.FullName.EndsWith(".bbfw", StringComparison.OrdinalIgnoreCase))
                .Take(20)
                .Select(e => e.FullName + " (" + FormatBytes(e.Length) + ")")
                .ToArray();
            if (firmwareEntries.Length > 0)
                Add(section, "Firmware entries", string.Join(Environment.NewLine, firmwareEntries));

            Add(section, "Notes", "IPSW files are Apple restore/update archives. FileDentify lists package structure and manifest presence; it does not flash, decrypt, or modify firmware.");
        }

        private static void AddIpaInfo(List<ReportSection> sections, ZipArchive archive)
        {
            var section = AddSection(sections, "iOS application archive");
            Add(section, "Format hint", "IPA application package");
            Add(section, "Container", "ZIP-compatible archive");
            Add(section, "Entry count", archive.Entries.Count.ToString(CultureInfo.InvariantCulture));
            var appInfo = archive.Entries.FirstOrDefault(e => Regex.IsMatch(e.FullName, @"^Payload/[^/]+\.app/Info\.plist$", RegexOptions.IgnoreCase));
            if (appInfo != null)
                Add(section, "App metadata", appInfo.FullName + " (" + FormatBytes(appInfo.Length) + ")");
            var frameworks = archive.Entries.Count(e => e.FullName.IndexOf(".framework/", StringComparison.OrdinalIgnoreCase) >= 0);
            var plugins = archive.Entries.Count(e => e.FullName.IndexOf(".appex/", StringComparison.OrdinalIgnoreCase) >= 0);
            Add(section, "Framework entries", frameworks.ToString(CultureInfo.InvariantCulture));
            Add(section, "App extension entries", plugins.ToString(CultureInfo.InvariantCulture));
        }

        private static bool IsInsideAppleBundle(string path)
        {
            return !string.IsNullOrWhiteSpace(FindAppleBundlePath(path));
        }

        private static string FindAppleBundlePath(string path)
        {
            var dir = new FileInfo(path).Directory;
            while (dir != null)
            {
                var ext = dir.Extension.ToLowerInvariant();
                if (ext == ".app" || ext == ".framework" || ext == ".bundle" || ext == ".plugin" || ext == ".appex" || ext == ".xpc" || ext == ".driver" || ext == ".kext" || ext == ".prefpane")
                    return dir.FullName;
                dir = dir.Parent;
            }
            return string.Empty;
        }

        private static string AppleBundleKind(string bundlePath)
        {
            switch (Path.GetExtension(bundlePath).ToLowerInvariant())
            {
                case ".app": return "macOS application bundle";
                case ".framework": return "macOS framework bundle";
                case ".bundle": return "macOS loadable bundle";
                case ".plugin": return "macOS plug-in bundle";
                case ".appex": return "Apple app extension bundle";
                case ".xpc": return "Apple XPC service bundle";
                case ".driver": return "macOS audio or hardware driver bundle";
                case ".kext": return "macOS kernel extension bundle";
                case ".prefpane": return "macOS preference pane bundle";
                default: return "Apple bundle";
            }
        }

        private static void AddXmlishAttribute(ReportSection section, string text, string elementName, string attributeName, string label)
        {
            var match = Regex.Match(text ?? string.Empty, "<" + Regex.Escape(elementName) + "\\b[^>]*\\b" + Regex.Escape(attributeName) + "\\s*=\\s*\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var value = match.Groups["value"].Value.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    Add(section, label, value);
            }
        }

        private static string AppleLanguageFromPath(string path)
        {
            foreach (var part in path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                if (part.EndsWith(".lproj", StringComparison.OrdinalIgnoreCase))
                    return part;
            return string.Empty;
        }

        private static string FormatAppleUnsignedBytes(ulong value)
        {
            return value <= long.MaxValue
                ? FormatBytes((long)value)
                : value.ToString(CultureInfo.InvariantCulture) + " bytes";
        }
    }
}
