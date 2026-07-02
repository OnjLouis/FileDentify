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
        private static string SteinbergCubaseTypeName(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".all": return "Classic Steinberg Cubase song/project file";
                case ".arr": return "Classic Steinberg Cubase arrangement file";
                case ".cpr": return "Steinberg Cubase project file";
                case ".npr": return "Steinberg Nuendo project file";
                case ".prt": return "Steinberg Cubase part file";
                case ".fxb": return "VST effect/instrument bank";
                case ".fxp": return "VST effect/instrument preset";
                case ".vstpreset": return "Steinberg VST preset";
                case ".drm": return "Cubase drum map";
                case ".srf": return "Steinberg resource or settings file";
                default: return null;
            }
        }

        private static void AddSteinbergCubaseInfo(List<ReportSection> sections, string path, byte[] data)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".wrk" || ext == ".cwp" || ext == ".rpp" || ext == ".rpp-bak")
                return;
            if (QwsTypeName(path, data) != null)
                return;

            var type = SteinbergCubaseTypeName(path);
            var strings = FindReadableTextLines(data, 3, 500);
            if (type == null && BackupConfigTypeName(path, data) != null)
                return;
            if (strings.Any(s =>
                s.IndexOf("CAKEWALK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Cakewalk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("SONAR", StringComparison.OrdinalIgnoreCase) >= 0))
                return;

            var hasSteinbergText = strings.Any(s =>
                s.IndexOf("Cubase", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Nuendo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Steinberg", StringComparison.OrdinalIgnoreCase) >= 0);

            if (type == null && !hasSteinbergText)
                return;

            var isKnownProjectOrPreset = type != null;
            var isVstDll = type == null && ext == ".dll" && strings.Any(s => s.IndexOf("VST", StringComparison.OrdinalIgnoreCase) >= 0);
            var section = AddSection(sections, isVstDll ? "Steinberg/VST" : "Steinberg Cubase");
            Add(section, "Format hint", type ?? (isVstDll ? "Windows VST plug-in or Steinberg SDK marker" : "Steinberg/Cubase-related readable markers found"));
            Add(section, "Detection basis", type != null ? "Known Steinberg/Cubase-related extension, plus sampled readable strings where available." : (isVstDll ? "VST/Steinberg SDK readable strings found in sampled data." : "Steinberg/Cubase-related readable strings found in sampled data."));
            if (ext == ".all" || ext == ".arr")
                Add(section, "Compatibility note", ".all and .arr are classic Cubase song/arrangement formats. They usually need an old Cubase conversion path before modern Cubase project formats can use them.");

            AddVstPresetHeaderInfo(section, path, data);

            AddCategory(section, "Driver or audio system strings", strings, "ASIO", "DirectX", "MME", "MIDI", "VST");
            AddCategory(section, isVstDll ? "VST or Steinberg markers" : "Cubase or Steinberg markers", strings, "Cubase", "Nuendo", "Steinberg", "VST", "ASIO");
            if (isKnownProjectOrPreset)
            {
                AddCategory(section, "MIDI edit commands", strings, "Delete Notes", "DelShrtNotes", "Random Notes", "Fix Velocity", "Random Velo", "FadeOutVelo", "Push Forward", "Push Back", "Double Tempo", "Half Tempo");
                AddCategory(section, "Drum map names", strings, "BassDrum", "Bass Drum", "Snare", "HiHat", "Tom", "Conga", "Bongo", "Agogo", "Cuica", "Guiro", "Cymbl", "Ride", "Hand Clap", "Side Stick");
                AddCategory(section, "Effect and room preset names", strings, "Chorus", "Flanger", "Leslie", "Hall", "Room", "Delay", "Reverb", "Flimmer", "12-String");
                AddCategory(section, "Groove or quantize names", strings, "Groove", "Tuplet", "Shuf", "Randm", "Tlet");
                AddCategory(section, "Font or UI strings", strings, "Courier", "Times New Roman", "Arial", "Font");
                AddCategory(section, "Possible track or part names", strings, "Melodie", "Cue", "Master", "VOL", "pan");
            }

            var markers = strings.Where(IsCubaseMarker).Distinct(StringComparer.OrdinalIgnoreCase).Take(40).ToArray();
            if (markers.Length > 0)
                Add(section, "Internal marker preview", string.Join("\r\n", markers));

            Add(section, "Notes", isKnownProjectOrPreset
                ? "Steinberg project and preset formats are proprietary. FileDentify reports extension-level identity and readable project or preset clues from sampled strings, not a full project parse."
                : "Many Windows audio plug-ins contain Steinberg VST SDK strings. FileDentify reports those visible markers only; it does not load the plug-in or treat the DLL as a Cubase project.");
        }

        private static void AddVstPresetHeaderInfo(ReportSection section, string path, byte[] data)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".fxp" && ext != ".fxb")
                return;
            if (data.Length < 60 || Encoding.ASCII.GetString(data, 0, 4) != "CcnK")
                return;

            Add(section, "VST chunk marker", "CcnK");
            var chunkSize = ReadUInt32BigEndian(data, 4);
            if (chunkSize > 0)
                Add(section, "VST chunk size", FormatBytes(chunkSize));
            Add(section, "VST preset kind", Encoding.ASCII.GetString(data, 8, 4));
            Add(section, "VST version", ReadUInt32BigEndian(data, 12).ToString(CultureInfo.InvariantCulture));
            Add(section, "Plugin id", Encoding.ASCII.GetString(data, 16, 4));
            Add(section, "Plugin version", ReadUInt32BigEndian(data, 20).ToString(CultureInfo.InvariantCulture));
            var programName = ReadFixedAscii(data, 28, 28);
            if (!string.IsNullOrWhiteSpace(programName))
                Add(section, "Program name", programName);
        }

        private static void AddCategory(ReportSection section, string title, List<string> strings, params string[] needles)
        {
            var matches = strings
                .Where(s => needles.Any(n => MatchesCubaseNeedle(s, n)))
                .Where(s => s.Length <= 120)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(30)
                .ToArray();
            if (matches.Length > 0)
                Add(section, title, string.Join("\r\n", matches));
        }

        private static bool MatchesCubaseNeedle(string value, string needle)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(needle))
                return false;
            if (needle.Length <= 4 && needle.All(char.IsLetterOrDigit))
                return Regex.IsMatch(value, @"(?<![A-Za-z0-9])" + Regex.Escape(needle) + @"(?![A-Za-z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsCubaseMarker(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            if (Regex.IsMatch(value, @"^[A-Z]\d{3}$", RegexOptions.CultureInvariant))
                return true;
            if (Regex.IsMatch(value, @"^VOL ?\d{1,2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                return true;
            if (Regex.IsMatch(value, @"^pan ?\d{1,2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                return true;
            return false;
        }
    }
}
