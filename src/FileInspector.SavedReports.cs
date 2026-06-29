using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static string SavedReportTypeName(string path, byte[] header)
        {
            if (SavedReportStore.IsSavedReportPath(path) || LooksLikeSavedReport(header))
                return "FileDentify saved report";
            return null;
        }

        private static void AddSavedReportInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (!SavedReportStore.IsSavedReportPath(path) && !LooksLikeSavedReport(header))
                return;

            var section = AddSection(sections, "FileDentify saved report");
            Add(section, "Format hint", "Native FileDentify .fdreport");
            try
            {
                var loaded = SavedReportStore.Load(path);
                Add(section, "Stored files", loaded.Reports.Count.ToString(CultureInfo.InvariantCulture));
                if (loaded.Elapsed.HasValue)
                    Add(section, "Original generation time", FormatElapsed(loaded.Elapsed.Value));
                var paths = new List<string>();
                foreach (var report in loaded.Reports)
                {
                    if (!string.IsNullOrWhiteSpace(report.OriginalPath))
                        paths.Add(report.OriginalPath);
                }
                if (paths.Count > 0)
                    Add(section, "Original paths", string.Join("\r\n", paths.GetRange(0, Math.Min(paths.Count, 25)).ToArray()));
                Add(section, "Open behavior", "Open this file with FileDentify to reload the saved report tree. Inspecting it as a file reports the report container itself.");
            }
            catch (Exception ex)
            {
                Add(section, "Parse note", "The file has a FileDentify report extension or marker, but could not be loaded as a saved report: " + ex.Message);
            }
        }

        private static bool LooksLikeSavedReport(byte[] header)
        {
            if (header == null || header.Length == 0)
                return false;
            var text = Encoding.UTF8.GetString(header, 0, Math.Min(header.Length, 4096));
            return text.IndexOf("\"Format\":\"FileDentify report\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("\"Format\" : \"FileDentify report\"", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
