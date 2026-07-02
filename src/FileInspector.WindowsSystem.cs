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
            if (LooksLikeWindowsMinidump(path, header)) return "Windows minidump crash dump";
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
            if (ext == ".fon" && LooksLikeWindowsFon(header)) return "Windows bitmap font library";
            if (ext == ".mui") return "Windows MUI language resource";
            if (ext == ".mun") return "Windows MUN system resource";
            if (ext == ".etl") return "Windows Event Trace Log";
            if (ext == ".wer") return "Windows Error Reporting report";
            if (ext == ".mof") return "WMI Managed Object Format";
            if (ext == ".mfl") return "WMI MOF localization";
            if (ext == ".cdxml") return "PowerShell CDXML cmdlet definition";
            if (ext == ".psd1") return "PowerShell module manifest/data file";
            if (ext == ".psm1") return "PowerShell script module";
            if (ext == ".ps1xml") return "PowerShell type/format XML";
            if (ext == ".msc") return "Microsoft Management Console snap-in";
            if (ext == ".winmd") return "Windows Runtime metadata";
            if (ext == ".xbf") return "compiled XAML binary file";
            if (ext == ".pri") return "Windows package resource index";
            if (ext == ".nls") return "Windows National Language Support data";
            if (ext == ".ttc" && StartsWith(header, Encoding.ASCII.GetBytes("ttcf"))) return "TrueType font collection";
            if (ext == ".acm") return "Windows Audio Compression Manager codec";
            if (ext == ".ax") return "DirectShow filter module";
            if (ext == ".grxml") return "Speech Recognition Grammar XML";
            if (ext == ".wprp") return "Windows Performance Recorder profile";
            if (ext == ".ppkg") return "Windows provisioning package";
            if (ext == ".provxml") return "Windows provisioning XML";
            if (ext == ".devicemetadata-ms") return "Windows device metadata package";
            if (ext == ".cip") return "Windows Code Integrity policy";
            if (ext == ".ppd") return "PostScript printer description";
            if (ext == ".regtrans-ms") return "Windows registry transaction log";
            if (ext == ".blf") return "Windows Common Log File System base log";
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
            else if (LooksLikeWindowsMinidump(path, header))
                AddWindowsMinidumpInfo(section, path, header, fileLength);
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
            else if (Path.GetExtension(path).Equals(".fon", StringComparison.OrdinalIgnoreCase))
                AddWindowsFonInfo(section, path, header);
            else
                AddModernWindowsSystemInfo(section, path, header, fileLength);

            Add(section, "Notes", "Windows system files are reported from headers, filenames, and safe text/XML structure only. FileDentify does not install, import, execute, or modify them.");
        }

        private static void AddModernWindowsSystemInfo(ReportSection section, string path, byte[] header, long fileLength)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".mui":
                case ".mun":
                    AddWindowsResourceModuleInfo(section, path, header, ext == ".mui" ? "localized user-interface strings and resources" : "Windows system resources split from executable modules");
                    break;
                case ".etl":
                    AddWindowsTraceInfo(section, path, fileLength);
                    break;
                case ".wer":
                    AddWerInfo(section, header);
                    break;
                case ".mof":
                case ".mfl":
                    AddMofInfo(section, path, header);
                    break;
                case ".cdxml":
                    AddCdxmlInfo(section, header);
                    break;
                case ".psd1":
                    AddPowerShellDataFileInfo(section, header);
                    break;
                case ".psm1":
                    AddPowerShellModuleInfo(section, header);
                    break;
                case ".ps1xml":
                    AddPowerShellXmlInfo(section, path, header);
                    break;
                case ".msc":
                    AddMmcInfo(section, header);
                    break;
                case ".winmd":
                    AddWinMdInfo(section, path, header);
                    break;
                case ".xbf":
                    Add(section, "Role", "Compiled XAML binary produced for Windows UI resources.");
                    Add(section, "Resource", Path.GetFileName(path));
                    break;
                case ".pri":
                    AddPriInfo(section, path, header);
                    break;
                case ".nls":
                    AddNlsInfo(section, path);
                    break;
                case ".ttc":
                    AddTrueTypeCollectionInfo(section, path, header);
                    break;
                case ".acm":
                    AddWindowsResourceModuleInfo(section, path, header, "Audio Compression Manager codec module");
                    break;
                case ".ax":
                    AddWindowsResourceModuleInfo(section, path, header, "DirectShow filter module");
                    break;
                case ".grxml":
                    AddSpeechGrammarInfo(section, header);
                    break;
                case ".wprp":
                    AddWprProfileInfo(section, header);
                    break;
                case ".ppkg":
                    AddProvisioningPackageInfo(section, path, header);
                    break;
                case ".provxml":
                    AddProvisioningXmlInfo(section, header);
                    break;
                case ".devicemetadata-ms":
                    AddDeviceMetadataInfo(section, path, header);
                    break;
                case ".cip":
                    Add(section, "Role", "Windows Code Integrity policy data.");
                    Add(section, "Policy file", Path.GetFileName(path));
                    Add(section, "Safety note", "FileDentify does not validate or apply code-integrity policies.");
                    break;
                case ".ppd":
                    AddPostScriptPrinterInfo(section, header);
                    break;
                case ".regtrans-ms":
                    Add(section, "Role", "Windows registry transaction log sidecar.");
                    Add(section, "Common companion", Path.GetFileNameWithoutExtension(path));
                    break;
                case ".blf":
                    Add(section, "Role", "Common Log File System base log used by Windows transaction logs.");
                    Add(section, "Log file", Path.GetFileName(path));
                    break;
            }
        }

        private static void AddWindowsResourceModuleInfo(ReportSection section, string path, byte[] header, string role)
        {
            Add(section, "Role", role + ".");
            Add(section, "Module", Path.GetFileName(path));
            if (StartsWith(header, Encoding.ASCII.GetBytes("MZ")))
                Add(section, "Container", "Windows PE/resource module");
            var strings = FindReadableTextLines(header, 4, 80)
                .Where(line => line.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("Windows", StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToArray();
            if (strings.Length > 0)
                Add(section, "Visible resource strings", string.Join("\r\n", strings));
        }

        private static void AddWindowsTraceInfo(ReportSection section, string path, long fileLength)
        {
            Add(section, "Role", "Event Trace Log captured by Event Tracing for Windows.");
            Add(section, "Trace file", Path.GetFileName(path));
            Add(section, "Trace size", FormatBytes(fileLength));
            Add(section, "Common producers", "Windows Performance Recorder, boot tracing, update diagnostics, setup, and application diagnostics");
        }

        private static bool LooksLikeWindowsMinidump(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return (ext == ".dmp" || ext == ".mdmp") && StartsWith(header, Encoding.ASCII.GetBytes("MDMP"));
        }

        private static void AddWindowsMinidumpInfo(ReportSection section, string path, byte[] header, long fileLength)
        {
            Add(section, "Role", "Windows minidump crash dump.");
            Add(section, "Dump file", Path.GetFileName(path));
            Add(section, "Dump size", FormatBytes(fileLength));
            Add(section, "Header marker", "MDMP");
            if (header.Length >= 16)
            {
                Add(section, "Stream count", ReadUInt32LittleEndian(header, 8).ToString(CultureInfo.InvariantCulture));
                Add(section, "Stream directory RVA", "0x" + ReadUInt32LittleEndian(header, 12).ToString("X", CultureInfo.InvariantCulture));
            }
            Add(section, "Privacy note", "Crash dumps can contain local paths, module names, command lines, and fragments of process memory. Share minidump reports carefully.");
        }

        private static void AddWerInfo(ReportSection section, byte[] header)
        {
            var text = DecodeWindowsText(header);
            Add(section, "Role", "Windows Error Reporting text report.");
            Add(section, "Event type", ValueOrNotReported(FirstKeyValue(text, "EventType")));
            Add(section, "Application", ValueOrNotReported(FirstKeyValue(text, "AppName")));
            Add(section, "Friendly event name", ValueOrNotReported(FirstKeyValue(text, "FriendlyEventName")));
            Add(section, "Signature fields", Regex.Matches(text, @"(?im)^Sig\[\d+\]\.", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
            AddExampleLines(section, "Useful report lines", text, @"(?im)^(EventType|AppName|FriendlyEventName|LoadedModule\[\d+\]|OsInfo\[\d+\]\.(Name|Value))=.*$", 12);
            Add(section, "Privacy note", "WER reports may contain local paths, module names, bucket identifiers, and diagnostic metadata.");
        }

        private static void AddMofInfo(ReportSection section, string path, byte[] header)
        {
            var text = DecodeWindowsText(header);
            Add(section, "Role", Path.GetExtension(path).Equals(".mfl", StringComparison.OrdinalIgnoreCase) ? "Localized WMI MOF resource." : "WMI Managed Object Format schema/source file.");
            Add(section, "Class declarations", Regex.Matches(text, @"(?im)^\s*class\s+\w+", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Instance declarations", Regex.Matches(text, @"(?im)^\s*instance\s+of\s+\w+", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Pragmas", Regex.Matches(text, @"(?im)^\s*#pragma\b", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
            AddExampleLines(section, "Example declarations", text, @"(?im)^\s*(class\s+\w+|instance\s+of\s+\w+|#pragma\s+\w+)[^\r\n]*$", 10);
        }

        private static void AddCdxmlInfo(ReportSection section, byte[] header)
        {
            var text = DecodeWindowsText(header);
            Add(section, "Role", "PowerShell cmdlet definition over CIM/WMI.");
            Add(section, "Cmdlet elements", Regex.Matches(text, "<Cmdlet\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Class name", ValueOrNotReported(FirstXmlAttribute(text, "Class", "ClassName")));
            Add(section, "Cmdlet adapter", ValueOrNotReported(FirstXmlAttribute(text, "PowerShellMetadata", "CmdletAdapter")));
            AddExampleLines(section, "Example cmdlets", text, @"(?im)<Cmdlet\b[^>]*>", 10);
        }

        private static void AddPowerShellDataFileInfo(ReportSection section, byte[] header)
        {
            var text = DecodeWindowsText(header);
            Add(section, "Role", "PowerShell data file or module manifest.");
            Add(section, "Module version", ValueOrNotReported(FirstPowerShellAssignment(text, "ModuleVersion")));
            Add(section, "Root module", ValueOrNotReported(FirstPowerShellAssignment(text, "RootModule")));
            Add(section, "GUID", ValueOrNotReported(FirstPowerShellAssignment(text, "GUID")));
            Add(section, "Assignments", Regex.Matches(text, @"(?im)^\s*[A-Za-z_][A-Za-z0-9_]*\s*=", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
        }

        private static void AddPowerShellModuleInfo(ReportSection section, byte[] header)
        {
            var text = DecodeWindowsText(header);
            Add(section, "Role", "PowerShell script module.");
            Add(section, "Functions", Regex.Matches(text, @"(?im)^\s*function\s+[\w:-]+", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Classes", Regex.Matches(text, @"(?im)^\s*class\s+\w+", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Workflows", Regex.Matches(text, @"(?im)^\s*workflow\s+[\w:-]+", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
            AddExampleLines(section, "Example declarations", text, @"(?im)^\s*(function|class|workflow)\s+[\w:-]+", 12);
            Add(section, "Safety note", "PowerShell code is summarized as text only. FileDentify does not run scripts or load modules.");
        }

        private static void AddPowerShellXmlInfo(ReportSection section, string path, byte[] header)
        {
            var text = DecodeWindowsText(header);
            Add(section, "Role", (Path.GetFileName(path) ?? string.Empty).IndexOf("format", StringComparison.OrdinalIgnoreCase) >= 0 ? "PowerShell formatting XML." : "PowerShell type/extended data XML.");
            Add(section, "View elements", Regex.Matches(text, "<View\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Type elements", Regex.Matches(text, "<Type\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Selection sets", Regex.Matches(text, "<SelectionSet\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
        }

        private static void AddMmcInfo(ReportSection section, byte[] header)
        {
            var text = DecodeWindowsText(header);
            Add(section, "Role", "Microsoft Management Console saved console/snap-in file.");
            Add(section, "Snap-in markers", Regex.Matches(text, "SnapIn|Snap-in|Snapin", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Console file marker", text.IndexOf("MMC_ConsoleFile", StringComparison.OrdinalIgnoreCase) >= 0 ? "Present" : "Not seen in sample");
        }

        private static void AddWinMdInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Role", "Windows Runtime metadata assembly.");
            Add(section, "Metadata file", Path.GetFileName(path));
            if (StartsWith(header, Encoding.ASCII.GetBytes("MZ")))
                Add(section, "Container", "PE/CLR metadata file");
            Add(section, "Notes", "WinMD files describe Windows Runtime APIs. FileDentify identifies the metadata container and safe visible strings only; it does not load the assembly.");
        }

        private static void AddPriInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Role", "Windows packaged resource index used by UWP/MSIX resources.");
            Add(section, "Resource index", Path.GetFileName(path));
            var visible = FindReadableTextLines(header, 4, 60)
                .Where(line => line.IndexOf("mrm", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("resource", StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToArray();
            if (visible.Length > 0)
                Add(section, "Visible markers", string.Join("\r\n", visible));
        }

        private static void AddNlsInfo(ReportSection section, string path)
        {
            Add(section, "Role", "Windows National Language Support data table.");
            Add(section, "Data file", Path.GetFileName(path));
            var name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            var match = Regex.Match(name, @"^c_(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
                Add(section, "Code page hint", match.Groups[1].Value);
        }

        private static void AddTrueTypeCollectionInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Role", "TrueType/OpenType font collection containing multiple font faces.");
            Add(section, "Font collection", Path.GetFileName(path));
            if (header.Length >= 12)
            {
                Add(section, "TTC version", ReadUInt16BigEndian(header, 4).ToString(CultureInfo.InvariantCulture) + "." + ReadUInt16BigEndian(header, 6).ToString(CultureInfo.InvariantCulture));
                Add(section, "Font count", ReadUInt32BigEndian(header, 8).ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void AddSpeechGrammarInfo(ReportSection section, byte[] header)
        {
            var text = DecodeWindowsText(header);
            Add(section, "Role", "Speech Recognition Grammar Specification XML.");
            Add(section, "Rules", Regex.Matches(text, "<rule\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Items", Regex.Matches(text, "<item\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Choices", Regex.Matches(text, "<one-of\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
        }

        private static void AddWprProfileInfo(ReportSection section, byte[] header)
        {
            var text = DecodeWindowsText(header);
            Add(section, "Role", "Windows Performance Recorder profile XML.");
            Add(section, "Profiles", Regex.Matches(text, "<Profile\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Collectors", Regex.Matches(text, "<SystemCollector\\b|<EventCollector\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Providers", Regex.Matches(text, "<EventProvider\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
        }

        private static void AddProvisioningPackageInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Role", "Windows provisioning package for applying device/account/settings configuration.");
            Add(section, "Package", Path.GetFileName(path));
            if (IsZipHeader(header))
                Add(section, "Container", "ZIP-compatible package");
            Add(section, "Safety note", "Provisioning packages can change Windows settings when applied. FileDentify only identifies the package.");
        }

        private static void AddProvisioningXmlInfo(ReportSection section, byte[] header)
        {
            var text = DecodeWindowsText(header);
            Add(section, "Role", "Windows provisioning XML configuration.");
            Add(section, "Characteristic elements", Regex.Matches(text, "<characteristic\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Parm elements", Regex.Matches(text, "<parm\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
        }

        private static void AddDeviceMetadataInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Role", "Windows device metadata package.");
            Add(section, "Package", Path.GetFileName(path));
            if (IsZipHeader(header))
                Add(section, "Container", "ZIP-compatible metadata package");
            Add(section, "Common contents", "DeviceInformation.xml, PackageInfo.xml, icons, and device experience metadata");
        }

        private static void AddPostScriptPrinterInfo(ReportSection section, byte[] header)
        {
            var text = DecodeWindowsText(header);
            Add(section, "Role", "PostScript Printer Description text file.");
            Add(section, "Product", ValueOrNotReported(FirstPpdValue(text, "Product")));
            Add(section, "Language level", ValueOrNotReported(FirstPpdValue(text, "LanguageLevel")));
            Add(section, "Nick name", ValueOrNotReported(FirstPpdValue(text, "NickName")));
        }

        private static string FirstKeyValue(string text, string key)
        {
            var match = Regex.Match(text ?? string.Empty, @"(?im)^" + Regex.Escape(key) + @"=(.*)$", RegexOptions.CultureInvariant);
            return match.Success ? CleanMetadataText(match.Groups[1].Value.Trim()) : string.Empty;
        }

        private static string FirstPowerShellAssignment(string text, string key)
        {
            var match = Regex.Match(text ?? string.Empty, @"(?im)^\s*" + Regex.Escape(key) + @"\s*=\s*['""]?([^'"",\r\n#]+)", RegexOptions.CultureInvariant);
            return match.Success ? CleanMetadataText(match.Groups[1].Value.Trim()) : string.Empty;
        }

        private static string FirstPpdValue(string text, string key)
        {
            var match = Regex.Match(text ?? string.Empty, @"(?im)^\*" + Regex.Escape(key) + @":\s*(.*)$", RegexOptions.CultureInvariant);
            return match.Success ? CleanMetadataText(match.Groups[1].Value.Trim().Trim('"')) : string.Empty;
        }

        private static bool LooksLikeWindowsFon(byte[] header)
        {
            if (header.Length < 0x40 || !StartsWith(header, Encoding.ASCII.GetBytes("MZ")))
                return false;
            var neOffset = (int)ReadUInt32LittleEndian(header, 0x3C);
            return neOffset >= 0 && neOffset + 2 <= header.Length &&
                header[neOffset] == (byte)'N' &&
                header[neOffset + 1] == (byte)'E';
        }

        private static void AddWindowsFonInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Role", "Legacy Windows bitmap font resource library.");
            Add(section, "Font file", Path.GetFileName(path));
            var neOffset = (int)ReadUInt32LittleEndian(header, 0x3C);
            Add(section, "Executable wrapper", "MZ with NE header at 0x" + neOffset.ToString("X", CultureInfo.InvariantCulture));
            if (neOffset + 0x40 <= header.Length)
            {
                Add(section, "NE linker version", header[neOffset + 2].ToString(CultureInfo.InvariantCulture) + "." + header[neOffset + 3].ToString(CultureInfo.InvariantCulture));
                Add(section, "Entry table offset", "0x" + ReadUInt16LittleEndian(header, neOffset + 4).ToString("X", CultureInfo.InvariantCulture));
                Add(section, "Resource table offset", "0x" + ReadUInt16LittleEndian(header, neOffset + 0x24).ToString("X", CultureInfo.InvariantCulture) + " from NE header");
            }
            Add(section, "Common location", @"%SystemRoot%\Fonts");
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
