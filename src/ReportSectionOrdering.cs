using System;
using System.Collections.Generic;
using System.Linq;

namespace FileDentify
{
    internal static class ReportSectionOrdering
    {
        private static readonly string[] DefaultPrioritySections =
        {
            "Safety hints",
            "FileDentify saved report",
            "Clipman",
            "Backup/config data",
            "Legacy sound bank",
            "Ensoniq sampler",
            "QWS sequencer",
            "NVDA add-on",
            "Accessibility data",
            "Speech voice",
            "Roland sample data",
            "Roland sequencer song",
            "Roland sound data",
            "Native Instruments",
            "Korg",
            "iKaossilator",
            "GForce M-Tron",
            "Toontrack",
            "Decent Sampler",
            "Universal Audio LUNA",
            "AIR Music Technology",
            "Maize Sampler",
            "Applied Acoustics Systems",
            "Audio Modeling",
            "UJAM",
            "UJAM-style blob",
            "Valhalla DSP",
            "Modartt Pianoteq",
            "AI model / Ollama",
            "XLN Audio",
            "Spectrasonics",
            "Spitfire Audio",
            "Roland Cloud",
            "Mac audio plug-in",
            "Logic Pro project",
            "GarageBand project",
            "Logic Pro",
            "Pro Tools",
            "Apple sparse bundle",
            "Apple mobile backup",
            "Apple mobile backup file",
            "Apple bundle",
            "Apple firmware package",
            "iOS application archive",
            "Ableton",
            "Steinberg Cubase",
            "REAPER project",
            "Cakewalk project",
            "Sampler instrument",
            "Audio sample resource",
            "Nintendo Switch content",
            "Game/ROM data",
            "Legacy music/game audio",
            "MoonShell/R4",
            "Mobile phone tone",
            "Symbian app/resource",
            "Java MIDlet",
            "Symbian package",
            "Firmware / device image",
            "Hardware ID database",
            "Message/contact data",
            "Windows/system data",
            "Installer data",
            "Virtual machine metadata",
            "Developer/app resources",
            "Legacy app/plugin resource",
            "Virtual disk",
            "Windows shortcut",
            "Internet shortcut",
            "Windows executable",
            "PDF",
            "Ebook / help file",
            "Office document metadata",
            "OpenDocument metadata",
            "Image",
            "Windows property metadata",
            "Audio metadata",
            "QuickTime metadata",
            "ISO base media",
            "Media details",
            "ffprobe",
            "MPEG transport stream",
            "Readable text"
        };

        private static readonly string[] GenericEvidenceSections =
        {
            "Filesystem",
            "Signature matches",
            "Readable text",
            "Unix file/libmagic",
            "Hashes",
            "Header bytes",
            "Structure hints",
            "Printable strings",
            "Byte statistics",
            "Text hints",
            "Companion tools",
            "External tools"
        };

        public static void Apply(IEnumerable<FileReport> reports, IEnumerable<string> configuredOrder)
        {
            if (reports == null)
                return;

            var order = (configuredOrder ?? new List<string>())
                .Where(title => !IsPinnedSection(title))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var report in reports)
            {
                if (report == null)
                    continue;
                var ordered = OrderedSections(report.Sections, order).ToList();
                report.Sections.Clear();
                report.Sections.AddRange(ordered);
                report.FullText = FileInspector.BuildReportText(report);
            }
        }

        public static bool IsPinnedSection(string title)
        {
            return string.Equals(title, "Summary", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<ReportSection> OrderedSections(IEnumerable<ReportSection> sections, List<string> configuredOrder)
        {
            var list = sections == null ? new List<ReportSection>() : sections.ToList();
            var yielded = new HashSet<ReportSection>();
            foreach (var section in list.Where(section => IsPinnedSection(section.Title)))
            {
                yielded.Add(section);
                    yield return section;
            }

            foreach (var section in SectionsFromConfiguredOrder(list, configuredOrder).Where(IsSpecificSection))
            {
                if (yielded.Add(section))
                    yield return section;
            }

            foreach (var title in DefaultPrioritySections.Where(title => !IsGenericEvidenceSection(title)))
            {
                foreach (var section in list.Where(item => string.Equals(item.Title, title, StringComparison.OrdinalIgnoreCase) && !IsPinnedSection(item.Title)))
                {
                    if (yielded.Add(section))
                        yield return section;
                }
            }

            foreach (var section in list.Where(section => !IsPinnedSection(section.Title) && IsSpecificSection(section)))
            {
                if (yielded.Add(section))
                    yield return section;
            }

            foreach (var section in SectionsFromConfiguredOrder(list, configuredOrder).Where(IsGenericOrUnclassifiedSection))
            {
                if (yielded.Add(section))
                    yield return section;
            }

            foreach (var section in list.Where(section => !IsPinnedSection(section.Title)))
            {
                if (yielded.Add(section))
                    yield return section;
            }
        }

        public static bool IsGenericEvidenceSection(string title)
        {
            return GenericEvidenceSections.Contains(title ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsSpecificSectionForOrdering(string title)
        {
            return !IsPinnedSection(title) && !IsGenericEvidenceSection(title);
        }

        private static IEnumerable<ReportSection> SectionsFromConfiguredOrder(List<ReportSection> list, List<string> configuredOrder)
        {
            foreach (var title in configuredOrder)
            {
                var section = list.FirstOrDefault(item => string.Equals(item.Title, title, StringComparison.OrdinalIgnoreCase) && !IsPinnedSection(item.Title));
                if (section != null)
                    yield return section;
            }
        }

        private static bool IsSpecificSection(ReportSection section)
        {
            return section != null && IsSpecificSectionForOrdering(section.Title);
        }

        private static bool IsGenericOrUnclassifiedSection(ReportSection section)
        {
            return section != null && !IsPinnedSection(section.Title) && !IsSpecificSection(section);
        }
    }
}
