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
{    internal sealed class MainForm : Form
    {
        private readonly TreeView resultsTree;
        private readonly TextBox detailsBox;
        private readonly Button copyButton;
        private readonly Button saveButton;
        private readonly Button viewHtmlButton;
        private readonly Button openFolderButton;
        private readonly Button addFilesButton;
        private readonly Button closeButton;
        private readonly Label statusLabel;
        private readonly System.Windows.Forms.Timer updateCheckTimer;
        private AppSettings settings;
        private readonly List<string> loadedFiles = new List<string>();
        private readonly List<FileReport> currentReports = new List<FileReport>();
        private readonly List<string> temporaryHtmlReports = new List<string>();
        private TimeSpan? currentReportElapsed;
        private volatile string reportText = string.Empty;

        public MainForm(string[] args)
        {
            settings = AppSettings.Load();
            Text = "FileDentify " + Program.Version;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(820, 560);
            Width = 980;
            Height = 700;
            KeyPreview = true;

            var menu = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("&File");
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("&Open files...", null, delegate { AddFiles(); }) { ShortcutKeys = Keys.Control | Keys.O });
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("&Save report...", null, delegate { SaveReport(); }) { ShortcutKeys = Keys.Control | Keys.S });
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("&View HTML report", null, delegate { ViewHtmlReport(); }) { ShortcutKeyDisplayString = "Alt+V" });
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("&Advanced file viewer", null, delegate { OpenAdvancedFileViewer(); }) { ShortcutKeys = Keys.F4 });
            fileMenu.DropDownItems.Add("E&xit", null, delegate { Close(); });
            var editMenu = new ToolStripMenuItem("&Edit");
            editMenu.DropDownItems.Add(new ToolStripMenuItem("&Copy selected details", null, delegate { CopyCurrentSelection(); }) { ShortcutKeys = Keys.Control | Keys.C });
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Copy &full report", null, delegate { CopyReport(); }));
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Collapse all", null, delegate { CollapseAllResults(); }) { ShortcutKeyDisplayString = "Ctrl+Shift+Left" });
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Expand all", null, delegate { ExpandAllResults(); }) { ShortcutKeyDisplayString = "Ctrl+Shift+Right" });
            var optionsMenu = new ToolStripMenuItem("&Options");
            optionsMenu.DropDownItems.Add(new ToolStripMenuItem("&Preferences...", null, delegate { ShowPreferences(); }) { ShortcutKeys = Keys.Control | Keys.Oemcomma });
            var helpMenu = new ToolStripMenuItem("&Help");
            helpMenu.DropDownItems.Add(new ToolStripMenuItem("&Help", null, delegate { ShowHelp(); }) { ShortcutKeys = Keys.F1 });
            helpMenu.DropDownItems.Add(new ToolStripMenuItem("&Check for Updates...", null, delegate { CheckForUpdates(true); }) { ShortcutKeys = Keys.Shift | Keys.F1 });
            helpMenu.DropDownItems.Add("&Version History...", null, delegate { ShowVersionHistory(); });
            helpMenu.DropDownItems.Add(new ToolStripMenuItem("&Project page", null, delegate { OpenProjectPage(); }) { ShortcutKeys = Keys.Control | Keys.F1 });
            helpMenu.DropDownItems.Add("Con&tact", null, delegate { OpenContactPage(); });
            helpMenu.DropDownItems.Add("&Donate", null, delegate { OpenDonatePage(); });
            helpMenu.DropDownItems.Add("Other &software", null, delegate { OpenOtherSoftwarePage(); });
            helpMenu.DropDownItems.Add("Third-party &notices", null, delegate { ShowThirdPartyNotices(); });
            helpMenu.DropDownItems.Add(new ToolStripSeparator());
            helpMenu.DropDownItems.Add("&About FileDentify", null, delegate { ShowAbout(); });
            menu.Items.Add(fileMenu);
            menu.Items.Add(editMenu);
            menu.Items.Add(optionsMenu);
            menu.Items.Add(helpMenu);
            MainMenuStrip = menu;
            Controls.Add(menu);

            var main = new TableLayoutPanel();
            main.Dock = DockStyle.Fill;
            main.ColumnCount = 1;
            main.RowCount = 3;
            main.Padding = new Padding(10, 6, 10, 10);
            main.TabStop = false;
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(main);
            main.BringToFront();

            var split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.TabStop = false;
            split.Panel1.TabStop = false;
            split.Panel2.TabStop = false;
            main.Controls.Add(split, 0, 0);

            resultsTree = new TreeView();
            resultsTree.Dock = DockStyle.Fill;
            resultsTree.HideSelection = false;
            resultsTree.AccessibleName = "Identification tree";
            resultsTree.ContextMenuStrip = CreateTreeContextMenu();
            resultsTree.AfterSelect += delegate { UpdateDetailsFromSelection(); };
            resultsTree.KeyDown += ResultsTree_KeyDown;
            resultsTree.NodeMouseClick += ResultsTree_NodeMouseClick;
            split.Panel1.Controls.Add(resultsTree);

            detailsBox = new TextBox();
            detailsBox.Dock = DockStyle.Fill;
            detailsBox.Multiline = true;
            detailsBox.ScrollBars = ScrollBars.Both;
            detailsBox.ReadOnly = true;
            detailsBox.WordWrap = false;
            detailsBox.AccessibleName = "Selected item details";
            detailsBox.Enter += delegate { ResetTextBoxToTop(detailsBox); };
            detailsBox.KeyDown += TextBoxSelectAll_KeyDown;
            split.Panel2.Controls.Add(detailsBox);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.AutoSize = true;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.WrapContents = true;
            buttons.TabStop = false;
            main.Controls.Add(buttons, 0, 1);

            addFilesButton = CreateButton("Open files...", delegate { AddFiles(); }, "Open files, Ctrl+O", "Choose one or more files to inspect. Shortcut Control O.");
            copyButton = CreateButton("&Copy report", delegate { CopyReport(); }, "Copy report, Alt+C", "Copy the current report text. Shortcut Alt C.");
            saveButton = CreateButton("Save report...", delegate { SaveReport(); }, "Save report, Ctrl+S", "Save the current report as text or HTML. Shortcut Control S.");
            viewHtmlButton = CreateButton("&View HTML report", delegate { ViewHtmlReport(); }, "View HTML report, Alt+V", "Open a temporary HTML version of the current report in the default browser. Shortcut Alt V.");
            openFolderButton = CreateButton("Open containing fo&lder", delegate { OpenContainingFolder(); }, "Open containing folder, Alt+L", "Open the selected file's folder in File Explorer. Shortcut Alt L.");
            buttons.Controls.Add(addFilesButton);
            buttons.Controls.Add(copyButton);
            buttons.Controls.Add(saveButton);
            buttons.Controls.Add(viewHtmlButton);
            buttons.Controls.Add(openFolderButton);
            var preferencesButton = CreateButton("Preferences...", delegate { ShowPreferences(); }, "Preferences, Ctrl+comma", "Open FileDentify preferences. Shortcut Control comma.");
            buttons.Controls.Add(preferencesButton);
            var helpButton = CreateButton("Help", delegate { ShowHelp(); }, "Help, F1", "Open the FileDentify manual. Shortcut F1.");
            buttons.Controls.Add(helpButton);
            closeButton = CreateButton("Close", delegate { Close(); }, "Close, Escape", "Close FileDentify. Shortcut Escape.");
            buttons.Controls.Add(closeButton);

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.AutoSize = true;
            statusLabel.AccessibleName = "Status";
            statusLabel.Text = "Ready.";
            main.Controls.Add(statusLabel, 0, 2);

            KeyDown += MainForm_KeyDown;
            updateCheckTimer = new System.Windows.Forms.Timer();
            updateCheckTimer.Interval = 60 * 60 * 1000;
            updateCheckTimer.Tick += delegate { CheckAutomaticUpdateSchedule(); };
            ConfigureUpdateTimer();

            var initial = args.Where(a => !string.IsNullOrWhiteSpace(a)).Select(Path.GetFullPath).ToArray();
            if (initial.Length > 0)
                LoadFiles(initial);
            else
            {
                statusLabel.Text = "Choose one or more files to inspect.";
                resultsTree.Nodes.Add(new TreeNode("No file loaded") { Tag = "Choose Open files to inspect a file. FileDentify combines file/libmagic-style identification with Windows metadata, readable strings, hashes, advanced viewing, and accessible reports. Press F1 for help or Ctrl+comma for Preferences." });
                resultsTree.SelectedNode = resultsTree.Nodes[0];
                addFilesButton.Focus();
            }
            Shown += delegate { ScheduleStartupUpdateCheck(); };
            FormClosed += delegate { DeleteTemporaryHtmlReports(); };
        }

        private Button CreateButton(string text, EventHandler handler, string accessibleName, string accessibleDescription)
        {
            var button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.Margin = new Padding(0, 8, 8, 0);
            button.AccessibleRole = AccessibleRole.PushButton;
            button.AccessibleName = accessibleName;
            button.AccessibleDescription = accessibleDescription;
            button.Click += handler;
            return button;
        }

        private ContextMenuStrip CreateTreeContextMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripMenuItem("&Copy selected details", null, delegate { CopyCurrentSelection(); }) { ShortcutKeyDisplayString = "Ctrl+C" });
            menu.Items.Add(new ToolStripMenuItem("Copy &full report", null, delegate { CopyReport(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Co&llapse all", null, delegate { CollapseAllResults(); }) { ShortcutKeyDisplayString = "Ctrl+Shift+Left" });
            menu.Items.Add(new ToolStripMenuItem("&Expand all", null, delegate { ExpandAllResults(); }) { ShortcutKeyDisplayString = "Ctrl+Shift+Right" });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("&View HTML report", null, delegate { ViewHtmlReport(); }) { ShortcutKeyDisplayString = "Alt+V" });
            menu.Items.Add(new ToolStripMenuItem("&Save report...", null, delegate { SaveReport(); }) { ShortcutKeyDisplayString = "Ctrl+S" });
            menu.Items.Add(new ToolStripMenuItem("&Advanced file viewer", null, delegate { OpenAdvancedFileViewer(); }) { ShortcutKeyDisplayString = "F4" });
            return menu;
        }

        private void ResultsTree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right && e.Node != null)
                resultsTree.SelectedNode = e.Node;
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F1)
            {
                ShowHelp();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.O)
            {
                AddFiles();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.S)
            {
                SaveReport();
                e.Handled = true;
            }
            else if (!e.Alt && e.KeyCode == Keys.F4)
            {
                OpenAdvancedFileViewer();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                CopyCurrentSelection();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Oemcomma)
            {
                ShowPreferences();
                e.Handled = true;
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.Left)
            {
                CollapseAllResults();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.Right)
            {
                ExpandAllResults();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.Shift && e.KeyCode == Keys.F1)
            {
                CheckForUpdates(true);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.F1)
            {
                OpenProjectPage();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Shift | Keys.Left))
            {
                CollapseAllResults();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Shift | Keys.Right))
            {
                ExpandAllResults();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ResultsTree_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.Shift && e.KeyCode == Keys.Left)
            {
                CollapseAllResults();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.Right)
            {
                ExpandAllResults();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
        }

        private void CollapseAllResults()
        {
            if (resultsTree.Nodes.Count == 0)
                return;
            var selected = TopLevelNode(resultsTree.SelectedNode) ?? resultsTree.Nodes[0];
            resultsTree.BeginUpdate();
            resultsTree.CollapseAll();
            resultsTree.SelectedNode = selected;
            resultsTree.EndUpdate();
            selected.EnsureVisible();
            statusLabel.Text = "Report tree collapsed.";
        }

        private void ExpandAllResults()
        {
            if (resultsTree.Nodes.Count == 0)
                return;
            var selected = resultsTree.SelectedNode;
            resultsTree.BeginUpdate();
            resultsTree.ExpandAll();
            resultsTree.EndUpdate();
            if (selected != null)
            {
                resultsTree.SelectedNode = selected;
                selected.EnsureVisible();
            }
            statusLabel.Text = "Report tree expanded.";
        }

        private static TreeNode TopLevelNode(TreeNode node)
        {
            if (node == null)
                return null;
            while (node.Parent != null)
                node = node.Parent;
            return node;
        }

        private void AddFiles()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Open files to identify";
                dialog.Multiselect = true;
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    LoadFiles(dialog.FileNames);
            }
        }

        private void LoadFiles(IEnumerable<string> paths)
        {
            var inputs = paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (inputs.Length == 0)
            {
                ShowEmptyState("No readable files were selected.", "Choose Open files to inspect a file. Press F1 for help or Ctrl+comma for Preferences.");
                return;
            }

            currentReports.Clear();
            currentReportElapsed = null;
            resultsTree.Nodes.Clear();
            detailsBox.Clear();
            reportText = string.Empty;
            SetBusy(true, "Finding files...");
            ShowProgressState("Finding files", "Scanning selected files and folders. Large folders can take a moment before inspection starts.");

            var worker = new Thread(delegate()
            {
                var stopwatch = Stopwatch.StartNew();
                var files = ExpandInputPaths(inputs)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, NaturalPathComparer.Instance)
                    .ToArray();
                if (files.Length == 0)
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        loadedFiles.Clear();
                        SetBusy(false, "No readable files were found.");
                        ShowEmptyState("No readable files were found.", "The selected input did not contain readable files. Choose Open files to inspect a file, or send a folder that contains files.");
                    });
                    return;
                }

                BeginInvoke((MethodInvoker)delegate
                {
                    loadedFiles.Clear();
                    loadedFiles.AddRange(files);
                    SetBusy(true, "Inspecting " + files.Length.ToString(CultureInfo.InvariantCulture) + " file(s)...");
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
                    reports.Add(FileInspector.Inspect(files[i]));
                }
                stopwatch.Stop();
                BeginInvoke((MethodInvoker)delegate { ShowReports(reports, stopwatch.Elapsed); });
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
                try { files = Directory.GetFiles(directory); }
                catch { files = new string[0]; }
                foreach (var file in files)
                    yield return file;

                string[] directories;
                try { directories = Directory.GetDirectories(directory); }
                catch { directories = new string[0]; }
                foreach (var child in directories)
                    pending.Push(child);
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
            ResetTextBoxToTop(detailsBox);
            statusLabel.Text = status;
        }

        private void ShowReports(List<FileReport> reports, TimeSpan elapsed)
        {
            resultsTree.BeginUpdate();
            resultsTree.Nodes.Clear();
            currentReports.Clear();
            currentReports.AddRange(reports);
            currentReportElapsed = elapsed;

            if (reports.Count > 1)
            {
                var overviewText = FileInspector.BuildOverviewText(reports, elapsed);
                var overviewNode = new TreeNode("Report overview");
                overviewNode.Tag = overviewText;
                resultsTree.Nodes.Add(overviewNode);
            }

            foreach (var report in reports)
            {
                var root = new TreeNode(report.DisplayName);
                root.Tag = report.FullText;
                foreach (var section in report.Sections)
                {
                    var sectionNode = new TreeNode(section.Title);
                    sectionNode.Tag = section.DetailText();
                    foreach (var item in section.Items)
                    {
                        var itemNode = new TreeNode(item.Title);
                        itemNode.Tag = item.Detail;
                        sectionNode.Nodes.Add(itemNode);
                    }
                    root.Nodes.Add(sectionNode);
                }
                resultsTree.Nodes.Add(root);
                root.Expand();
            }

            resultsTree.EndUpdate();
            reportText = EnsureTrailingBlankLine(FileInspector.BuildCombinedText(reports, elapsed));
            if (resultsTree.Nodes.Count > 0)
                resultsTree.SelectedNode = resultsTree.Nodes[0];
            SetBusy(false, "Finished. " + reports.Count.ToString(CultureInfo.InvariantCulture) + " file(s) inspected in " + FormatElapsed(elapsed) + ".");
        }

        private void ShowProgressState(string title, string detail)
        {
            resultsTree.BeginUpdate();
            resultsTree.Nodes.Clear();
            var node = new TreeNode(title) { Tag = detail };
            resultsTree.Nodes.Add(node);
            resultsTree.SelectedNode = node;
            resultsTree.EndUpdate();
            detailsBox.Text = EnsureTrailingBlankLine(detail);
            ResetTextBoxToTop(detailsBox);
        }

        private void UpdateDetailsFromSelection()
        {
            var node = resultsTree.SelectedNode;
            detailsBox.Text = EnsureTrailingBlankLine(node == null || node.Tag == null ? string.Empty : node.Tag.ToString());
            ResetTextBoxToTop(detailsBox);
        }

        private void OpenAdvancedFileViewer()
        {
            var path = SelectedFilePath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                statusLabel.Text = "Choose a file in the tree before opening the advanced viewer.";
                return;
            }

            using (var dialog = new AdvancedFileViewerForm(path))
                dialog.ShowDialog(this);
        }

        private string SelectedFilePath()
        {
            var node = TopLevelNode(resultsTree.SelectedNode);
            if (node == null)
                return loadedFiles.Count == 1 ? loadedFiles[0] : string.Empty;
            return loadedFiles.FirstOrDefault(path => string.Equals(Path.GetFileName(path), node.Text, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        }

        private static string EnsureTrailingBlankLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Environment.NewLine;
            var trimmed = NormalizeLineEndings(text).TrimEnd('\r', '\n');
            return trimmed + Environment.NewLine + Environment.NewLine;
        }

        private static string NormalizeLineEndings(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
        }

        private static void ResetTextBoxToTop(TextBox box)
        {
            if (box == null)
                return;
            box.SelectionStart = 0;
            box.SelectionLength = 0;
            box.ScrollToCaret();
        }

        private static void TextBoxSelectAll_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                var box = sender as TextBox;
                if (box != null)
                    box.SelectAll();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
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

            if (string.IsNullOrEmpty(text))
            {
                statusLabel.Text = "No selected details to copy.";
                return;
            }

            Clipboard.SetText(text);
            statusLabel.Text = "Selected details copied to the clipboard.";
        }

        private void SaveReport()
        {
            if (string.IsNullOrEmpty(reportText))
            {
                statusLabel.Text = "No report is available to save.";
                return;
            }
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Save FileDentify report";
                dialog.Filter = "Text report (*.txt)|*.txt|HTML report (*.html)|*.html;*.htm|All files (*.*)|*.*";
                dialog.FileName = "FileDentify report.txt";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    if (IsHtmlReportPath(dialog.FileName))
                        File.WriteAllText(dialog.FileName, FileInspector.BuildCombinedHtml(currentReports, "FileDentify report", currentReportElapsed), Encoding.UTF8);
                    else
                        File.WriteAllText(dialog.FileName, reportText, Encoding.UTF8);
                    statusLabel.Text = "Report saved.";
                }
            }
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
            if (loadedFiles.Count == 0)
                return;
            var path = loadedFiles[0];
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
                    ConfigureUpdateTimer();
                    statusLabel.Text = "Preferences saved.";
                }
            }
        }

        private void ConfigureUpdateTimer()
        {
            if (updateCheckTimer == null)
                return;
            updateCheckTimer.Stop();
            if (UpdateService.AutomaticUpdateInterval(settings.UpdateCheckFrequency).HasValue)
                updateCheckTimer.Start();
        }

        private void CheckAutomaticUpdateSchedule()
        {
            var normalized = UpdateService.NormalizeUpdateCheckFrequency(settings.UpdateCheckFrequency);
            if (normalized.Equals("Never", StringComparison.OrdinalIgnoreCase))
                return;
            var interval = normalized.Equals("Startup", StringComparison.OrdinalIgnoreCase) ? TimeSpan.Zero : UpdateService.AutomaticUpdateInterval(normalized);
            if (interval.HasValue && settings.LastAutomaticUpdateCheckUtc.HasValue && DateTime.UtcNow - settings.LastAutomaticUpdateCheckUtc.Value < interval.Value)
                return;
            settings.LastAutomaticUpdateCheckUtc = DateTime.UtcNow;
            settings.Save();
            CheckForUpdates(false);
        }

        private void ScheduleStartupUpdateCheck()
        {
            if (!UpdateService.NormalizeUpdateCheckFrequency(settings.UpdateCheckFrequency).Equals("Startup", StringComparison.OrdinalIgnoreCase))
                return;

            var timer = new System.Windows.Forms.Timer();
            timer.Interval = 5000;
            timer.Tick += delegate
            {
                timer.Stop();
                timer.Dispose();
                CheckAutomaticUpdateSchedule();
            };
            timer.Start();
        }

        private void CheckForUpdates(bool showCurrent)
        {
            try
            {
                var releases = UpdateService.FetchReleases(Program.ProjectUrl, Program.Version);
                var release = UpdateService.LatestVersionedRelease(releases) ?? UpdateService.FetchLatestRelease(Program.ProjectUrl, Program.Version);
                var latest = release == null ? string.Empty : (release.tag_name ?? string.Empty).Trim();
                var current = new Version(Program.Version);
                Version remote;
                if (!Version.TryParse(latest.TrimStart('v', 'V'), out remote) || remote <= current)
                {
                    if (showCurrent)
                        MessageBox.Show(this, "FileDentify is up to date.", "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (settings.InstallUpdatesQuietly && TryStartUpdate(release, false))
                    return;
                ShowUpdateAvailableDialog(release, latest, UpdateService.BuildReleaseNotes(releases, current, remote, Program.Version));
            }
            catch (Exception ex)
            {
                if (showCurrent)
                    MessageBox.Show(this, "Could not check for updates. GitHub releases may not exist yet, or the network request failed." + Environment.NewLine + Environment.NewLine + ex.Message, "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ShowUpdateAvailableDialog(GitHubReleaseInfo release, string latest, string releaseNotes)
        {
            var releaseUrl = release == null || string.IsNullOrWhiteSpace(release.html_url) ? Program.ProjectUrl + "/releases" : release.html_url;
            var zipAsset = UpdateService.FindPortableZipAsset(release);
            using (var dialog = new Form())
            {
                dialog.Text = "FileDentify update available";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Size = new Size(620, 430);
                dialog.MinimumSize = new Size(520, 340);
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                dialog.Controls.Add(layout);
                layout.Controls.Add(new Label { Text = "FileDentify " + latest + " is available.", AutoSize = true }, 0, 0);
                var notesBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Text = EnsureTrailingBlankLine(releaseNotes), AccessibleName = "Release notes" };
                notesBox.Enter += delegate { ResetTextBoxToTop(notesBox); };
                notesBox.KeyDown += TextBoxSelectAll_KeyDown;
                layout.Controls.Add(notesBox, 0, 1);
                var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
                var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };
                buttons.Controls.Add(close);
                var releaseButton = new Button { Text = "Open &release page", AutoSize = true };
                releaseButton.Click += delegate { Process.Start(new ProcessStartInfo { FileName = releaseUrl, UseShellExecute = true }); };
                buttons.Controls.Add(releaseButton);
                if (zipAsset != null)
                {
                    var install = new Button { Text = "&Download and install", AutoSize = true };
                    install.Click += delegate { if (TryStartUpdate(release, true)) dialog.Close(); };
                    buttons.Controls.Add(install);
                }
                layout.Controls.Add(buttons, 0, 2);
                dialog.CancelButton = close;
                dialog.Shown += delegate { ResetTextBoxToTop(notesBox); };
                dialog.ShowDialog(this);
            }
        }

        private void ShowVersionHistory()
        {
            try
            {
                var releases = UpdateService.FetchReleases(Program.ProjectUrl, Program.Version);
                var release = UpdateService.LatestVersionedRelease(releases) ?? UpdateService.FetchLatestRelease(Program.ProjectUrl, Program.Version);
                var version = release == null ? Program.Version : (release.tag_name ?? Program.Version).Trim().TrimStart('v', 'V');
                var notes = UpdateService.FormatReleaseNotesForDialog(release == null ? string.Empty : release.body, "No release notes were provided.");
                ShowTextDialog("Version History - " + version, "Latest release: " + version + Environment.NewLine + Environment.NewLine + notes);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not check updates. GitHub releases may not exist yet, or the network request failed." + Environment.NewLine + Environment.NewLine + ex.Message, "Version History", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ShowTextDialog(string title, string text)
        {
            using (var dialog = new Form())
            {
                dialog.Text = title;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Size = new Size(620, 430);
                var box = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Text = EnsureTrailingBlankLine(text) };
                box.Enter += delegate { ResetTextBoxToTop(box); };
                box.KeyDown += TextBoxSelectAll_KeyDown;
                dialog.Controls.Add(box);
                var close = new Button { Text = "Close", DialogResult = DialogResult.Cancel };
                close.SetBounds(-100, -100, 1, 1);
                dialog.Controls.Add(close);
                dialog.CancelButton = close;
                dialog.Shown += delegate
                {
                    box.Focus();
                    BeginInvoke((MethodInvoker)delegate { ResetTextBoxToTop(box); });
                };
                dialog.ShowDialog(this);
            }
        }

        private bool TryStartUpdate(GitHubReleaseInfo release, bool showErrors)
        {
            var zipAsset = UpdateService.FindPortableZipAsset(release);
            if (zipAsset == null)
            {
                if (showErrors)
                    MessageBox.Show(this, "This GitHub release does not include a downloadable ZIP package. Please open the release page instead.", "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            try
            {
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var exePath = Application.ExecutablePath;
                var updaterTempDir = UpdateService.GetUpdaterTempDirectory(appDir);
                var scriptPath = Path.Combine(updaterTempDir, "FileDentifyUpdater-" + Guid.NewGuid().ToString("N") + ".ps1");
                File.WriteAllText(scriptPath, UpdateService.BuildUpdaterScript(zipAsset.browser_download_url, appDir, exePath, updaterTempDir, Process.GetCurrentProcess().Id, Program.Version), Encoding.UTF8);
                Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\"") { UseShellExecute = false, CreateNoWindow = true });
                Close();
                return true;
            }
            catch (Exception ex)
            {
                if (showErrors)
                    MessageBox.Show(this, ex.Message, "Could not start updater", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void OpenProjectPage()
        {
            OpenUrl(Program.ProjectUrl);
        }

        private void OpenContactPage()
        {
            OpenUrl("https://onj.me/contact");
        }

        private void OpenDonatePage()
        {
            OpenUrl("https://onj.me/donate");
        }

        private void OpenOtherSoftwarePage()
        {
            OpenUrl("https://onj.me/software");
        }

        private static void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }

        private void ShowHelp()
        {
            ManualService.OpenManual(this);
        }

        private void ShowAbout()
        {
            MessageBox.Show(this,
                "FileDentify " + Program.Version + " inspects selected files using bounded local reads, common binary signatures, text/header checks, ZIP container hints, embedded Unix file/libmagic, and bundled tools such as ffprobe when available.\r\n\r\nIt is designed as a SendTo-friendly, keyboard-first file identification utility.",
                "About FileDentify",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ShowThirdPartyNotices()
        {
            ShowThirdPartyNoticesDialog();
        }

        private void ShowThirdPartyNoticesDialog()
        {
            using (var dialog = new Form())
            {
                dialog.Text = "Third-party notices";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Size = new Size(760, 560);
                dialog.MinimumSize = new Size(520, 360);
                dialog.KeyPreview = true;

                var layout = new TableLayoutPanel();
                layout.Dock = DockStyle.Fill;
                layout.ColumnCount = 1;
                layout.RowCount = 3;
                layout.Padding = new Padding(8);
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                dialog.Controls.Add(layout);

                var notices = LibmagicProbe.NoticeEntries();
                var selector = new ComboBox();
                selector.DropDownStyle = ComboBoxStyle.DropDownList;
                selector.Dock = DockStyle.Top;
                selector.AccessibleName = "Notice";
                selector.AccessibleDescription = "Choose a third-party notice to review.";
                selector.Margin = new Padding(0, 0, 0, 8);
                selector.Items.AddRange(notices);
                layout.Controls.Add(selector, 0, 0);

                var noticeBox = new TextBox();
                noticeBox.Dock = DockStyle.Fill;
                noticeBox.Multiline = true;
                noticeBox.ReadOnly = true;
                noticeBox.ScrollBars = ScrollBars.Both;
                noticeBox.WordWrap = false;
                noticeBox.AccessibleName = "Notice text";
                noticeBox.AccessibleDescription = "Read-only text for the selected third-party notice.";
                noticeBox.KeyDown += TextBoxSelectAll_KeyDown;
                layout.Controls.Add(noticeBox, 0, 1);

                var close = new Button();
                close.Text = "Close";
                close.AutoSize = true;
                close.Anchor = AnchorStyles.Right;
                close.DialogResult = DialogResult.Cancel;
                layout.Controls.Add(close, 0, 2);
                dialog.CancelButton = close;

                EventHandler updateNotice = delegate
                {
                    var selected = selector.SelectedItem as ThirdPartyNotice;
                    noticeBox.Text = EnsureTrailingBlankLine(selected == null ? string.Empty : selected.Text);
                    ResetTextBoxToTop(noticeBox);
                };
                selector.SelectedIndexChanged += updateNotice;

                dialog.KeyDown += delegate(object sender, KeyEventArgs e)
                {
                    if (e.KeyCode == Keys.Escape)
                    {
                        dialog.DialogResult = DialogResult.Cancel;
                        dialog.Close();
                        e.Handled = true;
                    }
                };

                dialog.Shown += delegate
                {
                    if (selector.Items.Count > 0)
                        selector.SelectedIndex = 0;
                    selector.Focus();
                };

                dialog.ShowDialog(this);
            }
        }

        private void SetBusy(bool busy, string status)
        {
            UseWaitCursor = busy;
            addFilesButton.Enabled = !busy;
            copyButton.Enabled = !busy;
            saveButton.Enabled = !busy;
            viewHtmlButton.Enabled = !busy;
            openFolderButton.Enabled = !busy;
            closeButton.Enabled = true;
            statusLabel.Text = status;
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            return elapsed.TotalSeconds < 60
                ? elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " seconds"
                : elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        }
    }

}

