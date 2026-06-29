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
            "Speech voice",
            "Native Instruments",
            "Korg",
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
            "Nintendo Switch content",
            "Game/ROM data",
            "Mobile phone tone",
            "Symbian app/resource",
            "Java MIDlet",
            "Symbian package",
            "Firmware / device image",
            "Virtual disk",
            "Windows shortcut",
            "Internet shortcut",
            "Windows executable",
            "PDF",
            "Office document metadata",
            "OpenDocument metadata",
            "Image",
            "Audio metadata",
            "QuickTime metadata",
            "MPEG transport stream",
            "Readable text"
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

            foreach (var title in DefaultPrioritySections)
            {
                foreach (var section in list.Where(item => string.Equals(item.Title, title, StringComparison.OrdinalIgnoreCase) && !IsPinnedSection(item.Title)))
                {
                    if (configuredOrder.Contains(section.Title, StringComparer.OrdinalIgnoreCase))
                        continue;
                    if (yielded.Add(section))
                        yield return section;
                }
            }

            foreach (var title in configuredOrder)
            {
                var section = list.FirstOrDefault(item => string.Equals(item.Title, title, StringComparison.OrdinalIgnoreCase) && !IsPinnedSection(item.Title));
                if (section != null && yielded.Add(section))
                    yield return section;
            }

            foreach (var section in list.Where(section => !IsPinnedSection(section.Title) && !configuredOrder.Contains(section.Title, StringComparer.OrdinalIgnoreCase)))
            {
                if (yielded.Add(section))
                    yield return section;
            }
        }
    }
}
