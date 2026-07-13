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
        private static string Vst3VendorResourceTypeName(string path, byte[] header)
        {
            if (!IsInsideMacAudioPluginBundle(path))
                return null;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if ((ext == ".winfo" || ext == ".dsig") && IsPaceEdenResource(path, header))
                return "PACE Eden plug-in wrapper metadata";
            if (ext == ".ivg" && IsSonicChargeResource(path, header))
                return "Sonic Charge IVG vector UI resource";
            if (ext == ".ivgfont" && IsSonicChargeResource(path, header))
                return "Sonic Charge IVG font resource";
            if ((ext == ".cushy" || ext == ".makaron" || ext == ".zlim") && IsSonicChargeResource(path, header))
                return "Sonic Charge plug-in UI/resource data";
            if (ext == ".tagset" && IsUniversalAudioVst3Resource(path))
                return "Universal Audio plug-in tag set";
            if ((ext == ".resembed" || ext == ".path" || ext == ".bt" || ext == ".config") && IsIzotopeVst3Resource(path, header))
                return "iZotope plug-in resource data";
            if ((ext == ".vstxml" || ext == ".gdr") && IsWaldorfVst3Resource(path))
                return ext == ".vstxml" ? "Waldorf VST parameter metadata" : "Waldorf plug-in glyph/resource map";
            if (ext == ".nrc" && StartsWith(header, Encoding.ASCII.GetBytes("#NI#RsrcContnr#")))
                return "Native Instruments resource container";
            if (ext == ".lua" && IsNativeInstrumentsVst3Script(path))
                return "Native Instruments controller script";
            if (ext == ".syx" && StartsWith(header, new byte[] { 0xF0 }))
                return "MIDI System Exclusive bank bundled with a plug-in";
            return null;
        }

        private static void AddVst3VendorResourceInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = Vst3VendorResourceTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, Vst3VendorResourceSectionName(path, header));
            Add(section, "Format hint", type);
            Add(section, "Bundle", Vst3BundleName(path));
            Add(section, "Resource file", Path.GetFileName(path));
            Add(section, "Role", Vst3VendorResourceRole(path, header));
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Detection basis", "VST3 bundle path, extension " + Path.GetExtension(path) + ", and sampled header or visible strings.");

            if (ShouldShowVst3VendorResourceStrings(path, header))
            {
                var visible = FindReadableTextLines(sample, 4, 120)
                    .Where(IsUsefulVst3VendorResourceString)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(18)
                    .ToArray();
                if (visible.Length > 0)
                    Add(section, "Visible resource strings", string.Join(Environment.NewLine, visible));
            }

            Add(section, "Notes", Vst3VendorResourceNotes(section.Title));
        }

        private static string Vst3VendorResourceSectionName(string path, byte[] header)
        {
            if (IsPaceEdenResource(path, header)) return "PACE Eden";
            if (IsSonicChargeResource(path, header)) return "Sonic Charge";
            if (IsUniversalAudioVst3Resource(path)) return "Universal Audio";
            if (IsIzotopeVst3Resource(path, header)) return "iZotope";
            if (IsWaldorfVst3Resource(path)) return "Waldorf";
            if (Path.GetExtension(path).Equals(".nrc", StringComparison.OrdinalIgnoreCase) || IsNativeInstrumentsVst3Script(path)) return "Native Instruments VST3 resource";
            return "VST3 vendor resource";
        }

        private static string Vst3VendorResourceRole(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".winfo") return "PACE wrapper information";
            if (ext == ".dsig") return "PACE code-signature metadata";
            if (ext == ".ivg") return "vector UI drawing resource";
            if (ext == ".ivgfont") return "font resource converted for the plug-in UI";
            if (ext == ".cushy") return "Sonic Charge UI/layout script";
            if (ext == ".makaron") return "Sonic Charge macro/config resource";
            if (ext == ".zlim") return "compressed Sonic Charge resource payload";
            if (ext == ".tagset") return "plug-in category/tag metadata";
            if (ext == ".resembed") return "embedded iZotope resource index or bundle";
            if (ext == ".path") return "iZotope internal bundle pointer";
            if (ext == ".bt") return "iZotope preset/tree data";
            if (ext == ".config") return "plug-in configuration resource";
            if (ext == ".vstxml") return "VST parameter structure metadata";
            if (ext == ".gdr") return "glyph or bitmap-region map";
            if (ext == ".nrc") return "Native Instruments resource container";
            if (ext == ".lua") return "hardware/controller integration script";
            if (ext == ".syx") return "factory MIDI SysEx data";
            return "VST3 bundle resource";
        }

        private static string Vst3VendorResourceNotes(string sectionTitle)
        {
            switch (sectionTitle)
            {
                case "PACE Eden":
                    return "PACE Eden files are copy-protection/wrapper metadata found inside some commercial plug-ins. FileDentify reports visible publisher/product strings only; it does not validate signatures or bypass licensing.";
                case "Sonic Charge":
                    return "Sonic Charge plug-ins bundle UI descriptions, vector drawing resources, fonts, macro files, and compressed payloads. FileDentify reports file role and visible labels only.";
                case "Universal Audio":
                    return "Universal Audio VST3 tag sets are plug-in metadata used by hosts for category and feature browsing. FileDentify reports visible plist/XML strings only.";
                case "iZotope":
                    return "iZotope VST3 resources can include embedded UI layouts, DSP resource lists, bundle pointers, and preset trees. FileDentify reports visible paths and labels without loading the plug-in.";
                case "Waldorf":
                    return "Waldorf VST3 resources include parameter metadata and UI glyph maps for classic Waldorf instruments and effects. FileDentify reports visible structure only.";
                case "Native Instruments VST3 resource":
                    return "Native Instruments VST3 resources can include resource containers and hardware/controller scripts. FileDentify reports container/script identity without running scripts or loading the plug-in.";
                default:
                    return "This is an internal resource from a VST3 bundle. FileDentify reports context and visible metadata only; it does not load executable plug-in code.";
            }
        }

        private static bool IsPaceEdenResource(string path, byte[] header)
        {
            return path.IndexOf("__Pace_Eden", StringComparison.OrdinalIgnoreCase) >= 0 ||
                AsciiPreview(header, Math.Min(header.Length, 128)).IndexOf("TChk", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsSonicChargeResource(string path, byte[] header)
        {
            return path.IndexOf("Sonic Charge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                AsciiPreview(header, Math.Min(header.Length, 128)).IndexOf("IVG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                AsciiPreview(header, Math.Min(header.Length, 128)).IndexOf("makaron", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsUniversalAudioVst3Resource(string path)
        {
            return path.IndexOf("\\uaudio_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/uaudio_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("Universal Audio", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsIzotopeVst3Resource(string path, byte[] header)
        {
            return path.IndexOf("iZotope", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\iZ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/iZ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                StartsWith(header, Encoding.ASCII.GetBytes("ESAS")) ||
                AsciiPreview(header, Math.Min(header.Length, 128)).IndexOf("DSP/AutomatableParamList", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsWaldorfVst3Resource(string path)
        {
            return path.IndexOf("\\Attack.vst3\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/Attack.vst3/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\PPG Wave", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/PPG Wave", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\D-Pole.vst3\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/D-Pole.vst3/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsNativeInstrumentsVst3Script(string path)
        {
            return Path.GetExtension(path).Equals(".lua", StringComparison.OrdinalIgnoreCase) &&
                (path.IndexOf("\\Komplete Kontrol.vst3\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 path.IndexOf("/Komplete Kontrol.vst3/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 path.IndexOf("\\Maschine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 path.IndexOf("/Maschine", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string Vst3BundleName(string path)
        {
            var bundle = FindMacAudioPluginBundlePath(path);
            return string.IsNullOrWhiteSpace(bundle) ? string.Empty : Path.GetFileName(bundle);
        }

        private static bool IsUsefulVst3VendorResourceString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            var text = value.Trim();
            if (text.Length < 4 || text.Length > 160)
                return false;
            if (Regex.IsMatch(text, @"^[0-9A-Fa-f\-\{\}]{24,}$"))
                return false;
            return text.Any(char.IsLetter);
        }

        private static bool ShouldShowVst3VendorResourceStrings(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".nrc" || ext == ".zlim" || ext == ".syx")
                return false;
            if (ext == ".rom" || ext == ".bin")
                return false;
            return LooksLikeText(header) ||
                ext == ".winfo" ||
                ext == ".dsig" ||
                ext == ".resembed" ||
                ext == ".bt";
        }
    }
}
