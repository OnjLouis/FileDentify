using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace FileDentify
{
    internal static class SavedReportStore
    {
        public const string Extension = ".fdreport";
        private const int FormatVersion = 2;
        private const long MaxLoadBytes = 128L * 1024L * 1024L;

        public static string AutoSavePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FileDentify.fdreport"); }
        }

        public static bool IsSavedReportPath(string path)
        {
            return string.Equals(Path.GetExtension(path), Extension, StringComparison.OrdinalIgnoreCase);
        }

        public static void Save(string path, IEnumerable<FileReport> reports, TimeSpan? elapsed)
        {
            Save(path, reports, elapsed, null);
        }

        public static void Save(string path, IEnumerable<FileReport> reports, TimeSpan? elapsed, SavedReportSelection selection)
        {
            var document = new SavedReportDocument
            {
                Format = "FileDentify report",
                FormatVersion = FormatVersion,
                AppVersion = Program.Version,
                GeneratedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                ElapsedMilliseconds = elapsed.HasValue ? (long?)Math.Round(elapsed.Value.TotalMilliseconds) : null,
                Selection = selection,
                Reports = reports.Select(ToDto).ToList()
            };
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
            File.WriteAllText(path, serializer.Serialize(document), Encoding.UTF8);
        }

        public static SavedReportLoadResult Load(string path)
        {
            var info = new FileInfo(path);
            if (info.Length > MaxLoadBytes)
                throw new InvalidOperationException("This FileDentify report is too large to load safely in this version.");

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
            var document = serializer.Deserialize<SavedReportDocument>(File.ReadAllText(path, Encoding.UTF8));
            if (document == null || document.FormatVersion <= 0 || document.Reports == null)
                throw new InvalidOperationException("This is not a valid FileDentify report file.");

            var reports = document.Reports.Select(FromDto).ToList();
            TimeSpan? elapsed = null;
            if (document.ElapsedMilliseconds.HasValue && document.ElapsedMilliseconds.Value >= 0)
                elapsed = TimeSpan.FromMilliseconds(document.ElapsedMilliseconds.Value);
            return new SavedReportLoadResult { Reports = reports, Elapsed = elapsed, Selection = document.Selection };
        }

        private static SavedFileReport ToDto(FileReport report)
        {
            return new SavedFileReport
            {
                DisplayName = report.DisplayName,
                OriginalPath = report.OriginalPath,
                Sections = report.Sections.Select(section => new SavedReportSection
                {
                    Title = section.Title,
                    Items = section.Items.Select(item => new SavedReportItem { Title = item.Title, Detail = item.Detail }).ToList()
                }).ToList()
            };
        }

        private static FileReport FromDto(SavedFileReport dto)
        {
            var report = new FileReport
            {
                DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? Path.GetFileName(dto.OriginalPath ?? string.Empty) : dto.DisplayName,
                OriginalPath = dto.OriginalPath
            };
            if (dto.Sections != null)
            {
                foreach (var sourceSection in dto.Sections)
                {
                    var section = new ReportSection { Title = sourceSection.Title ?? string.Empty };
                    if (sourceSection.Items != null)
                    {
                        foreach (var item in sourceSection.Items)
                            section.Items.Add(new ReportItem { Title = item.Title ?? string.Empty, Detail = item.Detail ?? string.Empty });
                    }
                    report.Sections.Add(section);
                }
            }
            report.FullText = FileInspector.BuildReportText(report);
            return report;
        }
    }

    internal sealed class SavedReportLoadResult
    {
        public List<FileReport> Reports;
        public TimeSpan? Elapsed;
        public SavedReportSelection Selection;
    }

    internal sealed class SavedReportDocument
    {
        public string Format { get; set; }
        public int FormatVersion { get; set; }
        public string AppVersion { get; set; }
        public string GeneratedUtc { get; set; }
        public long? ElapsedMilliseconds { get; set; }
        public SavedReportSelection Selection { get; set; }
        public List<SavedFileReport> Reports { get; set; }
    }

    internal sealed class SavedReportSelection
    {
        public bool ReportOverview { get; set; }
        public string OriginalPath { get; set; }
        public string DisplayName { get; set; }
        public string SectionTitle { get; set; }
    }

    internal sealed class SavedFileReport
    {
        public string DisplayName { get; set; }
        public string OriginalPath { get; set; }
        public List<SavedReportSection> Sections { get; set; }
    }

    internal sealed class SavedReportSection
    {
        public string Title { get; set; }
        public List<SavedReportItem> Items { get; set; }
    }

    internal sealed class SavedReportItem
    {
        public string Title { get; set; }
        public string Detail { get; set; }
    }
}
