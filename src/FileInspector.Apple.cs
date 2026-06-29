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
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (Path.GetFileName(path).Equals("Info.plist", StringComparison.OrdinalIgnoreCase) && IsInsideAppleBundle(path))
                return "Apple application or bundle metadata";
            if (ext == ".strings") return "Apple localization strings";
            if (ext == ".car" && StartsWith(header, Encoding.ASCII.GetBytes("BOMStore"))) return "Apple compiled asset catalog";
            if (ext == ".nib") return "Apple Interface Builder nib";
            if (ext == ".mobileconfig") return "Apple configuration profile";
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
                if (ext == ".app" || ext == ".framework" || ext == ".bundle" || ext == ".plugin" || ext == ".appex" || ext == ".kext" || ext == ".prefpane")
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
                case ".kext": return "macOS kernel extension bundle";
                case ".prefpane": return "macOS preference pane bundle";
                default: return "Apple bundle";
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
