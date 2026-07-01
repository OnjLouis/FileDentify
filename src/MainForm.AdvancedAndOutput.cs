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

        private void OpenAdvancedFileViewer()
        {
            var path = SelectedFilePath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                statusLabel.Text = "Choose a file in the tree before opening the advanced viewer.";
                return;
            }

            AdvancedFileViewerState state;
            if (!advancedViewerStates.TryGetValue(path, out state))
            {
                state = new AdvancedFileViewerState();
                advancedViewerStates[path] = state;
            }

            using (var dialog = new AdvancedFileViewerForm(path, state))
                dialog.ShowDialog(this);
        }


        private void PruneAdvancedViewerStates(IEnumerable<string> inputs)
        {
            var keep = new HashSet<string>(
                inputs.Select(p => Path.GetFullPath(p)),
                StringComparer.OrdinalIgnoreCase);
            var remove = advancedViewerStates.Keys.Where(path => !keep.Contains(path)).ToArray();
            foreach (var path in remove)
                advancedViewerStates.Remove(path);
        }


        private string SelectedFilePath()
        {
            var node = TopLevelNode(resultsTree.SelectedNode);
            if (node == null)
                return loadedFiles.Count == 1 ? loadedFiles[0] : string.Empty;
            if (!string.IsNullOrWhiteSpace(node.Name) && File.Exists(node.Name))
                return node.Name;
            return loadedFiles.FirstOrDefault(path => string.Equals(Path.GetFileName(path), node.Text, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        }


        private void CopyReport()
        {
            if (!string.IsNullOrEmpty(reportText))
            {
                Clipboard.SetText(reportText);
                statusLabel.Text = "Report copied to the clipboard.";
            }
        }


        private void CopyCurrentSelection()
        {
            var text = string.Empty;
            if (detailsBox.Focused && detailsBox.SelectionLength > 0)
                text = detailsBox.SelectedText;
            else if (resultsTree.Focused)
            {
                var node = resultsTree.SelectedNode;
                if (node != null && node.Tag != null)
                    text = node.Tag.ToString();
            }
            else if (detailsBox.Focused)
                text = detailsBox.Text.TrimEnd('\r', '\n');
            else if (detailsBrowser.ContainsFocus)
            {
                var node = resultsTree.SelectedNode;
                if (node != null && node.Tag != null)
                    text = node.Tag.ToString();
            }

            if (string.IsNullOrEmpty(text))
            {
                statusLabel.Text = "No selected details to copy.";
                return;
            }

            Clipboard.SetText(text);
            statusLabel.Text = "Selected details copied to the clipboard.";
        }


        private bool SaveReport()
        {
            if (string.IsNullOrEmpty(reportText))
            {
                statusLabel.Text = "No report is available to save.";
                return false;
            }
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Save FileDentify report";
                dialog.Filter = "FileDentify report (*.fdreport)|*.fdreport|Text report (*.txt)|*.txt|HTML report (*.html)|*.html;*.htm|All files (*.*)|*.*";
                dialog.FileName = "FileDentify report.fdreport";
                dialog.DefaultExt = "fdreport";
                dialog.AddExtension = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        if (SavedReportStore.IsSavedReportPath(dialog.FileName))
                        {
                            SavedReportStore.Save(dialog.FileName, currentReports, currentReportElapsed, CurrentSavedReportSelection());
                            currentSavedReportPath = Path.GetFullPath(dialog.FileName);
                            AddRecentReport(currentSavedReportPath);
                        }
                        else if (IsHtmlReportPath(dialog.FileName))
                            File.WriteAllText(dialog.FileName, FileInspector.BuildCombinedHtml(currentReports, "FileDentify report", currentReportElapsed), Encoding.UTF8);
                        else
                            File.WriteAllText(dialog.FileName, reportText, Encoding.UTF8);
                        statusLabel.Text = "Report saved.";
                        return true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, ex.Message, "Save report", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
            }
            return false;
        }


        private void AutoSaveCurrentReport()
        {
            if (!settings.AutoSaveLastReport || currentReports.Count == 0)
                return;
            try
            {
                SavedReportStore.Save(SavedReportStore.AutoSavePath, currentReports, currentReportElapsed, CurrentSavedReportSelection());
                AddRecentReport(SavedReportStore.AutoSavePath);
            }
            catch
            {
            }
        }


        private SavedReportSelection CurrentSavedReportSelection()
        {
            var selected = resultsTree.SelectedNode;
            if (IsReportOverviewNode(selected))
                return new SavedReportSelection { ReportOverview = true };

            var fileNode = TopLevelNode(selected);
            if (fileNode == null || fileNode.Nodes.Count == 0)
                return null;

            var report = currentReports.FirstOrDefault(item =>
                string.Equals(item.OriginalPath, fileNode.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.DisplayName, fileNode.Text, StringComparison.OrdinalIgnoreCase));
            return new SavedReportSelection
            {
                OriginalPath = report == null ? fileNode.Name : report.OriginalPath,
                DisplayName = report == null ? fileNode.Text : report.DisplayName,
                SectionTitle = CurrentFileSectionName(),
                ItemTitle = CurrentFileItemName()
            };
        }


        private void ViewHtmlReport()
        {
            if (currentReports.Count == 0)
            {
                statusLabel.Text = "No report is available to view.";
                return;
            }

            try
            {
                var dir = Path.Combine(Path.GetTempPath(), "FileDentify-html-reports");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "FileDentify-report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".html");
                File.WriteAllText(path, FileInspector.BuildCombinedHtml(currentReports, "FileDentify report", currentReportElapsed), Encoding.UTF8);
                temporaryHtmlReports.Add(path);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                statusLabel.Text = "HTML report opened in the default browser.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "View HTML report", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void DeleteTemporaryHtmlReports()
        {
            foreach (var path in temporaryHtmlReports.ToArray())
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch
                {
                }
            }
        }


        private static bool IsHtmlReportPath(string path)
        {
            var extension = Path.GetExtension(path);
            return string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".htm", StringComparison.OrdinalIgnoreCase);
        }


        private void OpenContainingFolder()
        {
            var path = SelectedFilePath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                statusLabel.Text = "The selected file is not available on this machine.";
                return;
            }
            Process.Start("explorer.exe", "/select,\"" + path + "\"");
        }


        private void ShowPreferences()
        {
            using (var dialog = new PreferencesForm(settings))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    var oldSendTo = settings.SendToEnabled;
                    var oldDesktopShortcut = settings.DesktopShortcutEnabled;
                    var oldFileAssociation = settings.FileAssociationEnabled;
                    dialog.ApplyTo(settings);
                    settings.Save();
                    if (oldSendTo != settings.SendToEnabled)
                    {
                        try { SendToInstaller.SetInstalled(settings.SendToEnabled); }
                        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Send To menu", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    }
                    if (oldDesktopShortcut != settings.DesktopShortcutEnabled)
                    {
                        try { DesktopShortcutInstaller.SetInstalled(settings.DesktopShortcutEnabled); }
                        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Desktop shortcut", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    }
                    if (oldFileAssociation != settings.FileAssociationEnabled)
                    {
                        try { FileAssociationInstaller.SetInstalled(settings.FileAssociationEnabled); }
                        catch (Exception ex) { MessageBox.Show(this, ex.Message, "FileDentify report association", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    }
                    if (settings.AutoSaveLastReport)
                        AutoSaveCurrentReport();
                    ConfigureUpdateTimer();
                    statusLabel.Text = "Preferences saved.";
                }
            }
        }
    }
}

