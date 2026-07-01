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
    internal sealed partial class MainForm
    {

        private void AddFiles(bool append)
        {
            if (!append && !ConfirmReplaceCurrentReport())
                return;

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = append ? "Append files to FileDentify report" : "Open files to identify";
                dialog.Multiselect = true;
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    LoadFiles(dialog.FileNames, append);
            }
        }


        private void OpenFolder(bool append)
        {
            if (!append && !ConfirmReplaceCurrentReport())
                return;

            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = append ? "Choose a folder to append to the FileDentify report" : "Choose a folder to inspect with FileDentify";
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                    LoadFiles(new[] { dialog.SelectedPath }, append);
            }
        }


        private void NewReport()
        {
            if (!ConfirmReplaceCurrentReport("New report", "Starting a new report will clear the current report. Save the current report first?"))
                return;

            loadedFiles.Clear();
            currentReports.Clear();
            currentReportElapsed = null;
            currentSavedReportPath = string.Empty;
            reportText = string.Empty;
            resultsTree.Nodes.Clear();
            detailsBox.Clear();
            detailsBrowser.DocumentText = string.Empty;
            PruneAdvancedViewerStates(new string[0]);
            ShowEmptyState("New report started.", "Choose Open files to inspect a file, or use Send To to append files while FileDentify is open.");
            addFilesButton.Focus();
        }


        private void AppendFilesFromAnotherInstance(string[] paths)
        {
            if (paths == null || paths.Length == 0)
                return;
            LoadFiles(paths, currentReports.Count > 0);
        }


        private void OpenSavedReport()
        {
            if (!ConfirmReplaceCurrentReport())
                return;

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Open FileDentify report";
                dialog.Filter = "FileDentify report (*.fdreport)|*.fdreport|All files (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    LoadSavedReport(dialog.FileName);
            }
        }


        private void LoadSavedReport(string path)
        {
            try
            {
                var loaded = SavedReportStore.Load(path);
                loadedFiles.Clear();
                loadedFiles.AddRange(loaded.Reports
                    .Select(report => report.OriginalPath)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase));
                currentSavedReportPath = Path.GetFullPath(path);
                ShowReports(loaded.Reports, loaded.Elapsed ?? TimeSpan.Zero, null, loaded.Selection);
                AddRecentReport(currentSavedReportPath);
                statusLabel.Text = "Opened saved FileDentify report.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Open FileDentify report", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void PopulateRecentReportsMenu()
        {
            recentReportsMenu.DropDownItems.Clear();
            var recent = (settings.RecentReports ?? new List<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToArray();
            if (recent.Length == 0)
            {
                recentReportsMenu.DropDownItems.Add(new ToolStripMenuItem("No recent reports") { Enabled = false });
                return;
            }

            foreach (var path in recent)
            {
                var label = RecentReportLabel(path);
                recentReportsMenu.DropDownItems.Add(new ToolStripMenuItem(label, null, delegate { OpenRecentReport(path); }));
            }
        }


        private void OpenLastReport()
        {
            var path = SavedReportStore.AutoSavePath;
            if (!File.Exists(path))
            {
                statusLabel.Text = "No automatically saved last report is available.";
                return;
            }
            if (!ConfirmReplaceCurrentReport())
                return;
            LoadSavedReport(path);
        }


        private void OpenRecentReport(string path)
        {
            if (!File.Exists(path))
            {
                statusLabel.Text = "Recent report not found: " + path;
                RemoveRecentReport(path);
                return;
            }
            if (!ConfirmReplaceCurrentReport())
                return;
            LoadSavedReport(path);
        }


        private string RecentReportLabel(string path)
        {
            if (string.Equals(path, SavedReportStore.AutoSavePath, StringComparison.OrdinalIgnoreCase))
            {
                var reports = currentReports.Count > 0 ? currentReports : TryReadReportsForLabel(path);
                if (reports.Count == 1 && !string.IsNullOrWhiteSpace(reports[0].OriginalPath))
                    return "Last report: " + reports[0].OriginalPath;
                if (reports.Count == 2)
                    return "Last report: " + reports[0].OriginalPath + "; " + reports[1].OriginalPath;
                if (reports.Count > 2)
                    return "Last report: " + reports.Count.ToString(CultureInfo.InvariantCulture) + " files";
                return "Last automatically saved report";
            }
            return path;
        }


        private static List<FileReport> TryReadReportsForLabel(string path)
        {
            try { return SavedReportStore.Load(path).Reports ?? new List<FileReport>(); }
            catch { return new List<FileReport>(); }
        }


        private void AddRecentReport(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            var full = Path.GetFullPath(path);
            var recent = new List<string> { full };
            recent.AddRange((settings.RecentReports ?? new List<string>())
                .Where(item => !string.Equals(item, full, StringComparison.OrdinalIgnoreCase))
                .Take(9));
            settings.RecentReports = recent;
            settings.Save();
        }


        private void RemoveRecentReport(string path)
        {
            settings.RecentReports = (settings.RecentReports ?? new List<string>())
                .Where(item => !string.Equals(item, path, StringComparison.OrdinalIgnoreCase))
                .ToList();
            settings.Save();
        }


        private void RefreshOriginalFiles()
        {
            var paths = currentReports
                .Select(report => report.OriginalPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (paths.Length == 0)
            {
                statusLabel.Text = "This report has no original file paths to refresh.";
                return;
            }

            var existing = paths.Where(File.Exists).ToArray();
            var missing = paths.Where(path => !File.Exists(path)).ToArray();
            if (existing.Length == 0)
            {
                statusLabel.Text = "Cannot refresh. None of the original files are available on this machine.";
                MessageBox.Show(this, "None of the original files in this report are available on this machine, so FileDentify cannot refresh it.", "Refresh original files", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (missing.Length > 0)
            {
                MessageBox.Show(this,
                    missing.Length.ToString(CultureInfo.InvariantCulture) + " original file(s) are not available on this machine. FileDentify will refresh the " + existing.Length.ToString(CultureInfo.InvariantCulture) + " file(s) it can still read.",
                    "Refresh original files",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            var restoreSelection = CurrentSavedReportSelection();
            var restoreDetailsFocus = DetailsPaneContainsFocus();
            LoadFiles(existing, false, restoreSelection, restoreDetailsFocus);
        }


        private bool ConfirmReplaceCurrentReport()
        {
            return ConfirmReplaceCurrentReport("Open files", "Opening new files will replace the current report. Save the current report first?");
        }


        private bool ConfirmReplaceCurrentReport(string title, string message)
        {
            if (currentReports.Count == 0)
                return true;

            var result = MessageBox.Show(
                this,
                message,
                title,
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);
            if (result == DialogResult.Cancel)
                return false;
            if (result == DialogResult.Yes)
                return SaveReport();
            return true;
        }


        private void LoadFiles(IEnumerable<string> paths, bool append)
        {
            LoadFiles(paths, append, null, false);
        }


        private void LoadFiles(IEnumerable<string> paths, bool append, SavedReportSelection restoreSelection, bool restoreDetailsFocus)
        {
            var inputs = paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (inputs.Length == 0)
            {
                if (append && currentReports.Count > 0)
                    statusLabel.Text = "No readable files were selected.";
                else
                    ShowEmptyState("No readable files were selected.", "Choose Open files to inspect a file. Press F1 for help or Ctrl+comma for Preferences.");
                return;
            }

            var existingReports = append ? currentReports.ToList() : new List<FileReport>();
            var existingFiles = append ? loadedFiles.ToList() : new List<string>();
            var existingElapsed = append ? currentReportElapsed : null;
            var restoreFileName = append ? (TopLevelNode(resultsTree.SelectedNode) == null ? null : TopLevelNode(resultsTree.SelectedNode).Text) : null;
            var restoreSectionName = append ? CurrentFileSectionName() : null;
            if (!append)
            {
                currentReports.Clear();
                currentReportElapsed = null;
                currentSavedReportPath = string.Empty;
                resultsTree.Nodes.Clear();
                detailsBox.Clear();
                reportText = string.Empty;
            }
            SetBusy(true, "Finding files...");
            ShowProgressState("Finding files", "Scanning selected files and folders. Large folders can take a moment before inspection starts.");

            var worker = new Thread(delegate()
            {
                var stopwatch = Stopwatch.StartNew();
                var files = ExpandInputPaths(inputs)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (append)
                {
                    var alreadyLoaded = new HashSet<string>(existingFiles, StringComparer.OrdinalIgnoreCase);
                    files = files.Where(path => !alreadyLoaded.Contains(path)).ToArray();
                }
                if (files.Length == 0)
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (append && existingReports.Count > 0)
                        {
                            loadedFiles.Clear();
                            loadedFiles.AddRange(existingFiles);
                            RebuildReportTree(restoreFileName, restoreSectionName);
                            SetBusy(false, "No new readable files were found.");
                            statusLabel.Text = "No new readable files were found.";
                        }
                        else
                        {
                            loadedFiles.Clear();
                            SetBusy(false, "No readable files were found.");
                            ShowEmptyState("No readable files were found.", "The selected input did not contain readable files. Choose Open files to inspect a file, or send a folder that contains files.");
                        }
                    });
                    return;
                }

                BeginInvoke((MethodInvoker)delegate
                {
                    loadedFiles.Clear();
                    loadedFiles.AddRange(append ? existingFiles.Concat(files) : files);
                    if (!append)
                        PruneAdvancedViewerStates(files);
                    SetBusy(true, (append ? "Appending " : "Inspecting ") + files.Length.ToString(CultureInfo.InvariantCulture) + " file(s)...");
                });

                var reports = new List<FileReport>();
                for (var i = 0; i < files.Length; i++)
                {
                    var index = i;
                    if (index == 0 || index % 25 == 0)
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            ShowProgressState("Generating report", "Inspecting file " + (index + 1).ToString(CultureInfo.InvariantCulture) + " of " + files.Length.ToString(CultureInfo.InvariantCulture) + ": " + Path.GetFileName(files[index]));
                            statusLabel.Text = "Generating report. " + (index + 1).ToString(CultureInfo.InvariantCulture) + " of " + files.Length.ToString(CultureInfo.InvariantCulture) + " file(s)...";
                        });
                    }
                    try
                    {
                        reports.Add(FileInspector.Inspect(files[i]));
                    }
                    catch (Exception ex)
                    {
                        reports.Add(FileInspector.BuildErrorReport(files[i], ex));
                    }
                }
                stopwatch.Stop();
                BeginInvoke((MethodInvoker)delegate
                {
                    var combinedReports = append ? existingReports.Concat(reports).ToList() : reports;
                    var combinedElapsed = append && existingElapsed.HasValue ? existingElapsed.Value + stopwatch.Elapsed : stopwatch.Elapsed;
                    ShowReports(combinedReports, combinedElapsed, append ? Path.GetFileName(files[0]) : null, append ? null : restoreSelection);
                    if (restoreDetailsFocus)
                        FocusDetailsPane();
                });
            });
            worker.IsBackground = true;
            worker.Name = "FileDentify analyzer";
            worker.Start();
        }


        private static IEnumerable<string> ExpandInputPaths(IEnumerable<string> inputs)
        {
            foreach (var input in inputs)
            {
                string path;
                try { path = Path.GetFullPath(input); }
                catch { continue; }

                if (File.Exists(path))
                {
                    yield return path;
                    continue;
                }

                if (!Directory.Exists(path))
                    continue;

                if (FileInspector.IsReportableDirectoryPackage(path))
                {
                    yield return path;
                    continue;
                }

                foreach (var file in SafeEnumerateFiles(path))
                    yield return file;
            }
        }


        private static IEnumerable<string> SafeEnumerateFiles(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var directory = pending.Pop();
                string[] files;
                try { files = Directory.GetFiles(directory).OrderBy(path => path, NaturalPathComparer.Instance).ToArray(); }
                catch { files = new string[0]; }
                foreach (var file in files)
                    yield return file;

                string[] directories;
                try { directories = Directory.GetDirectories(directory).OrderBy(path => path, NaturalPathComparer.Instance).ToArray(); }
                catch { directories = new string[0]; }
                foreach (var child in directories.Reverse())
                {
                    if (FileInspector.IsReportableDirectoryPackage(child))
                        yield return child;
                    else
                        pending.Push(child);
                }
            }
        }


        private void ShowEmptyState(string status, string detail)
        {
            resultsTree.BeginUpdate();
            resultsTree.Nodes.Clear();
            var node = new TreeNode("No files loaded") { Tag = detail };
            resultsTree.Nodes.Add(node);
            resultsTree.SelectedNode = node;
            resultsTree.EndUpdate();
            detailsBox.Text = EnsureTrailingBlankLine(detail);
            UpdateDetailsBrowser("No files loaded", TextDetailsToHtml("No files loaded", detail));
            ResetTextBoxToTop(detailsBox);
            statusLabel.Text = status;
        }


        private void ShowReports(List<FileReport> reports, TimeSpan elapsed)
        {
            ShowReports(reports, elapsed, null, null);
        }


        private void ShowReports(List<FileReport> reports, TimeSpan elapsed, string preferredFileName)
        {
            ShowReports(reports, elapsed, preferredFileName, null);
        }


        private void ShowReports(List<FileReport> reports, TimeSpan elapsed, string preferredFileName, SavedReportSelection selection)
        {
            currentReports.Clear();
            currentReports.AddRange(reports);
            currentReportElapsed = elapsed;
            ReportSectionOrdering.Apply(currentReports, settings.SectionOrder);
            RebuildReportTree(preferredFileName, null, selection);
            AutoSaveCurrentReport();
            SetBusy(false, "Finished. " + currentReports.Count.ToString(CultureInfo.InvariantCulture) + " file(s) inspected in " + FormatElapsed(elapsed) + ".");
        }


        private void RebuildReportTree(string preferredFileName, string preferredSectionTitle)
        {
            RebuildReportTree(preferredFileName, preferredSectionTitle, null);
        }


        private void RebuildReportTree(string preferredFileName, string preferredSectionTitle, SavedReportSelection selection)
        {
            resultsTree.BeginUpdate();
            resultsTree.Nodes.Clear();

            TreeNode preferredNode = null;
            if (currentReports.Count > 1)
            {
                var overviewText = AddCurrentReportPathToOverview(FileInspector.BuildOverviewText(currentReports, currentReportElapsed));
                var overviewNode = new TreeNode("Report overview");
                overviewNode.Tag = overviewText;
                resultsTree.Nodes.Add(overviewNode);
                if (selection != null && selection.ReportOverview)
                    preferredNode = overviewNode;
            }

            foreach (var report in currentReports)
            {
                var root = new TreeNode(report.DisplayName);
                root.Name = report.OriginalPath ?? report.DisplayName;
                root.Tag = report.FullText;
                if (ReportMatchesSelection(report, selection) && string.IsNullOrWhiteSpace(selection.SectionTitle))
                    preferredNode = root;
                if (string.Equals(report.DisplayName, preferredFileName, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(preferredSectionTitle))
                    preferredNode = root;
                foreach (var section in report.Sections)
                {
                    var sectionNode = new TreeNode(section.Title);
                    sectionNode.Tag = section.DetailText();
                    if (ReportMatchesSelection(report, selection) &&
                        string.Equals(section.Title, selection.SectionTitle, StringComparison.OrdinalIgnoreCase) &&
                        string.IsNullOrWhiteSpace(selection.ItemTitle))
                        preferredNode = sectionNode;
                    if (string.Equals(report.DisplayName, preferredFileName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(section.Title, preferredSectionTitle, StringComparison.OrdinalIgnoreCase))
                        preferredNode = sectionNode;
                    foreach (var item in section.Items)
                    {
                        if (IsPlainReadableTextTreeItem(section, item))
                            continue;

                        var itemNode = new TreeNode(item.Title);
                        itemNode.Tag = item.Detail;
                        if (ReportMatchesSelection(report, selection) &&
                            string.Equals(section.Title, selection.SectionTitle, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(item.Title, selection.ItemTitle, StringComparison.OrdinalIgnoreCase))
                            preferredNode = itemNode;
                        sectionNode.Nodes.Add(itemNode);
                    }
                    root.Nodes.Add(sectionNode);
                }
                resultsTree.Nodes.Add(root);
                root.Expand();
            }

            resultsTree.EndUpdate();
            reportText = EnsureTrailingBlankLine(FileInspector.BuildCombinedText(currentReports, currentReportElapsed));
            if (preferredNode != null)
                resultsTree.SelectedNode = preferredNode;
            else if (resultsTree.Nodes.Count > 0)
                resultsTree.SelectedNode = resultsTree.Nodes[0];
        }


        private static bool IsPlainReadableTextTreeItem(ReportSection section, ReportItem item)
        {
            if (section == null || item == null)
                return false;
            if (!string.Equals(section.Title, "Readable text", StringComparison.OrdinalIgnoreCase))
                return false;

            var title = (item.Title ?? string.Empty).Trim();
            if (string.Equals(title, "No readable text found", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(title, "Limit", StringComparison.OrdinalIgnoreCase) ||
                IsHtmlNavigationHeadingItem(title))
                return false;

            var detail = ReportSection.NormalizeDetailText(item.Detail).Trim();
            return string.Equals(title, detail, StringComparison.Ordinal);
        }


        private string AddCurrentReportPathToOverview(string overviewText)
        {
            if (string.IsNullOrWhiteSpace(overviewText))
                return overviewText;

            var path = !string.IsNullOrWhiteSpace(currentSavedReportPath) ? currentSavedReportPath : SavedReportStore.AutoSavePath;
            if (string.IsNullOrWhiteSpace(path))
                return overviewText;

            var normalized = overviewText.TrimEnd();
            var heading = "Report overview" + Environment.NewLine + "===============";
            if (normalized.StartsWith(heading, StringComparison.Ordinal))
            {
                return heading +
                    Environment.NewLine +
                    "Current FileDentify report path:" +
                    Environment.NewLine +
                    path +
                    normalized.Substring(heading.Length);
            }

            return normalized +
                Environment.NewLine +
                Environment.NewLine +
                "Current FileDentify report path" +
                Environment.NewLine +
                "-------------------------------" +
                Environment.NewLine +
                path;
        }


        private static bool ReportMatchesSelection(FileReport report, SavedReportSelection selection)
        {
            if (report == null || selection == null || selection.ReportOverview)
                return false;
            if (!string.IsNullOrWhiteSpace(selection.OriginalPath) &&
                string.Equals(report.OriginalPath, selection.OriginalPath, StringComparison.OrdinalIgnoreCase))
                return true;
            return !string.IsNullOrWhiteSpace(selection.DisplayName) &&
                string.Equals(report.DisplayName, selection.DisplayName, StringComparison.OrdinalIgnoreCase);
        }



        private void ShowProgressState(string title, string detail)
        {
            if (WindowState == FormWindowState.Minimized || !ContainsFocus)
                htmlDetailsWantsFocus = false;
            resultsTree.BeginUpdate();
            resultsTree.Nodes.Clear();
            var node = new TreeNode(title) { Tag = detail };
            resultsTree.Nodes.Add(node);
            resultsTree.SelectedNode = node;
            resultsTree.EndUpdate();
            detailsBox.Text = EnsureTrailingBlankLine(detail);
            UpdateDetailsBrowser(title, TextDetailsToHtml(title, detail));
            ResetTextBoxToTop(detailsBox);
        }
    }
}

