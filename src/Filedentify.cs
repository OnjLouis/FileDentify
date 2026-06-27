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
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("FileDentify")]
[assembly: AssemblyDescription("Accessible file identification utility for Windows")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Andre Louis")]
[assembly: AssemblyProduct("FileDentify")]
[assembly: AssemblyCopyright("Copyright (c) Andre Louis")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0")]

namespace FileDentify
{
    internal static class Program
    {
        public const string Version = "1.0";
        public const string ProjectUrl = "https://github.com/OnjLouis/FileDentify";

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                if (TryHandleCommandLine(args))
                    return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(args));
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FileDentify-startup-error.txt"), ex.ToString(), Encoding.UTF8); }
                catch { }
                MessageBox.Show(ex.Message, "FileDentify startup error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool TryHandleCommandLine(string[] args)
        {
            if (args == null || args.Length == 0)
                return false;

            var files = new List<string>();
            string reportPath = null;
            var showHelp = false;

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i] ?? string.Empty;
                if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase) || arg.Equals("/?", StringComparison.OrdinalIgnoreCase))
                    showHelp = true;
                else if (arg.Equals("--version", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("FileDentify " + Version, "FileDentify", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return true;
                }
                else if (arg.Equals("--install-sendto", StringComparison.OrdinalIgnoreCase))
                {
                    SendToInstaller.SetInstalled(true);
                    var settings = AppSettings.Load();
                    settings.SendToEnabled = true;
                    settings.Save();
                    return true;
                }
                else if (arg.Equals("--uninstall-sendto", StringComparison.OrdinalIgnoreCase))
                {
                    SendToInstaller.SetInstalled(false);
                    var settings = AppSettings.Load();
                    settings.SendToEnabled = false;
                    settings.Save();
                    return true;
                }
                else if ((arg.Equals("--report", StringComparison.OrdinalIgnoreCase) || arg.Equals("-r", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                    reportPath = args[++i];
                else if (arg.StartsWith("-", StringComparison.Ordinal))
                    showHelp = true;
                else
                    files.Add(arg);
            }

            if (showHelp)
            {
                MessageBox.Show(CommandLineHelp(), "FileDentify command line", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(reportPath))
            {
                var existing = files.Where(File.Exists).ToArray();
                if (existing.Length == 0)
                    throw new InvalidOperationException("No readable files were supplied for the report.");
                var builder = new StringBuilder();
                foreach (var file in existing)
                {
                    builder.AppendLine(FileInspector.Inspect(file).FullText);
                    builder.AppendLine();
                }
                File.WriteAllText(reportPath, builder.ToString().TrimEnd(), Encoding.UTF8);
                return true;
            }

            return false;
        }

        private static string CommandLineHelp()
        {
            return "FileDentify " + Version + Environment.NewLine + Environment.NewLine +
                "Usage:" + Environment.NewLine +
                "  FileDentify.exe [files...]" + Environment.NewLine +
                "  FileDentify.exe --report report.txt [files...]" + Environment.NewLine +
                "  FileDentify.exe --install-sendto" + Environment.NewLine +
                "  FileDentify.exe --uninstall-sendto" + Environment.NewLine + Environment.NewLine +
                "Opening with file paths shows the inspection window. --report writes a text report without opening the UI.";
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly TreeView resultsTree;
        private readonly TextBox detailsBox;
        private readonly Button copyButton;
        private readonly Button saveButton;
        private readonly Button openFolderButton;
        private readonly Button addFilesButton;
        private readonly Label statusLabel;
        private readonly System.Windows.Forms.Timer updateCheckTimer;
        private AppSettings settings;
        private readonly List<string> loadedFiles = new List<string>();
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
            fileMenu.DropDownItems.Add("E&xit", null, delegate { Close(); });
            var optionsMenu = new ToolStripMenuItem("&Options");
            optionsMenu.DropDownItems.Add(new ToolStripMenuItem("&Preferences...", null, delegate { ShowPreferences(); }) { ShortcutKeys = Keys.Control | Keys.Oemcomma });
            var helpMenu = new ToolStripMenuItem("&Help");
            helpMenu.DropDownItems.Add(new ToolStripMenuItem("&Help", null, delegate { ShowHelp(); }) { ShortcutKeys = Keys.F1 });
            helpMenu.DropDownItems.Add(new ToolStripMenuItem("&Check for Updates...", null, delegate { CheckForUpdates(true); }) { ShortcutKeys = Keys.Shift | Keys.F1 });
            helpMenu.DropDownItems.Add("&Version History...", null, delegate { ShowVersionHistory(); });
            helpMenu.DropDownItems.Add(new ToolStripMenuItem("&Project page", null, delegate { OpenProjectPage(); }) { ShortcutKeys = Keys.Control | Keys.F1 });
            helpMenu.DropDownItems.Add("Con&tact", null, delegate { OpenContactPage(); });
            helpMenu.DropDownItems.Add("&Donate", null, delegate { OpenDonatePage(); });
            helpMenu.DropDownItems.Add("Third-party &notices", null, delegate { ShowThirdPartyNotices(); });
            helpMenu.DropDownItems.Add(new ToolStripSeparator());
            helpMenu.DropDownItems.Add("&About FileDentify", null, delegate { ShowAbout(); });
            menu.Items.Add(fileMenu);
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
            resultsTree.AfterSelect += delegate { UpdateDetailsFromSelection(); };
            split.Panel1.Controls.Add(resultsTree);

            detailsBox = new TextBox();
            detailsBox.Dock = DockStyle.Fill;
            detailsBox.Multiline = true;
            detailsBox.ScrollBars = ScrollBars.Both;
            detailsBox.ReadOnly = true;
            detailsBox.WordWrap = false;
            detailsBox.AccessibleName = "Selected item details";
            detailsBox.Enter += delegate { ResetTextBoxToTop(detailsBox); };
            split.Panel2.Controls.Add(detailsBox);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.AutoSize = true;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.WrapContents = true;
            buttons.TabStop = false;
            main.Controls.Add(buttons, 0, 1);

            addFilesButton = CreateButton("&Open files...", delegate { AddFiles(); });
            copyButton = CreateButton("&Copy report", delegate { CopyReport(); });
            saveButton = CreateButton("&Save report...", delegate { SaveReport(); });
            openFolderButton = CreateButton("Open containing &folder", delegate { OpenContainingFolder(); });
            buttons.Controls.Add(addFilesButton);
            buttons.Controls.Add(copyButton);
            buttons.Controls.Add(saveButton);
            buttons.Controls.Add(openFolderButton);
            var preferencesButton = CreateButton("&Preferences...", delegate { ShowPreferences(); });
            buttons.Controls.Add(preferencesButton);

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
                resultsTree.Nodes.Add(new TreeNode("No file loaded") { Tag = "Choose Open files to inspect a file. Press F1 for help or Ctrl+comma for Preferences." });
                resultsTree.SelectedNode = resultsTree.Nodes[0];
                addFilesButton.Focus();
            }
            Shown += delegate { ScheduleStartupUpdateCheck(); };
        }

        private Button CreateButton(string text, EventHandler handler)
        {
            var button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.Margin = new Padding(0, 8, 8, 0);
            button.Click += handler;
            return button;
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
            else if (e.KeyCode == Keys.F4)
            {
                ResetTextBoxToTop(detailsBox);
                detailsBox.Focus();
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
            var files = paths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (files.Length == 0)
            {
                statusLabel.Text = "No readable files were selected.";
                return;
            }

            loadedFiles.Clear();
            loadedFiles.AddRange(files);
            resultsTree.Nodes.Clear();
            detailsBox.Clear();
            reportText = string.Empty;
            SetBusy(true, "Inspecting " + files.Length.ToString(CultureInfo.InvariantCulture) + " file(s)...");

            var worker = new Thread(delegate()
            {
                var reports = new List<FileReport>();
                foreach (var file in files)
                    reports.Add(FileInspector.Inspect(file));
                BeginInvoke((MethodInvoker)delegate { ShowReports(reports); });
            });
            worker.IsBackground = true;
            worker.Name = "FileDentify analyzer";
            worker.Start();
        }

        private void ShowReports(List<FileReport> reports)
        {
            resultsTree.BeginUpdate();
            resultsTree.Nodes.Clear();

            var text = new StringBuilder();
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
                text.AppendLine(report.FullText);
                text.AppendLine();
            }

            resultsTree.EndUpdate();
            reportText = EnsureTrailingBlankLine(text.ToString().TrimEnd());
            if (resultsTree.Nodes.Count > 0)
                resultsTree.SelectedNode = resultsTree.Nodes[0];
            SetBusy(false, "Finished. " + reports.Count.ToString(CultureInfo.InvariantCulture) + " file(s) inspected.");
        }

        private void UpdateDetailsFromSelection()
        {
            var node = resultsTree.SelectedNode;
            detailsBox.Text = EnsureTrailingBlankLine(node == null || node.Tag == null ? string.Empty : node.Tag.ToString());
            ResetTextBoxToTop(detailsBox);
        }

        private static string EnsureTrailingBlankLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Environment.NewLine;
            var trimmed = text.TrimEnd('\r', '\n');
            return trimmed + Environment.NewLine + Environment.NewLine;
        }

        private static void ResetTextBoxToTop(TextBox box)
        {
            if (box == null)
                return;
            box.SelectionStart = 0;
            box.SelectionLength = 0;
            box.ScrollToCaret();
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
                dialog.Filter = "Text report (*.txt)|*.txt|All files (*.*)|*.*";
                dialog.FileName = "FileDentify report.txt";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    File.WriteAllText(dialog.FileName, reportText, Encoding.UTF8);
                    statusLabel.Text = "Report saved.";
                }
            }
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
                    dialog.ApplyTo(settings);
                    settings.Save();
                    if (oldSendTo != settings.SendToEnabled)
                    {
                        try { SendToInstaller.SetInstalled(settings.SendToEnabled); }
                        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Send To menu", MessageBoxButtons.OK, MessageBoxIcon.Error); }
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

        private static void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }

        private void ShowHelp()
        {
            ShowTextDialog("FileDentify Help",
                "FileDentify identifies selected files and shows the results in a tree." + Environment.NewLine + Environment.NewLine +
                "Open files: Ctrl+O, Alt+O, or File > Open files." + Environment.NewLine +
                "Save report: Ctrl+S or File > Save report." + Environment.NewLine +
                "Preferences: Alt+P or Ctrl+comma." + Environment.NewLine +
                "Review selected details: F4." + Environment.NewLine +
                "Copy selected details: Ctrl+C from the tree or details box." + Environment.NewLine +
                "Copy full report: Alt+C or the Copy report button." + Environment.NewLine +
                "Check for updates: Shift+F1." + Environment.NewLine +
                "Open the GitHub project page: Ctrl+F1." + Environment.NewLine +
                "Contact: Help > Contact." + Environment.NewLine +
                "Donate: Help > Donate." + Environment.NewLine +
                "Close: Escape." + Environment.NewLine + Environment.NewLine +
                "Command line:" + Environment.NewLine +
                "FileDentify.exe [files...]" + Environment.NewLine +
                "FileDentify.exe --report report.txt [files...]" + Environment.NewLine +
                "FileDentify.exe --install-sendto" + Environment.NewLine +
                "FileDentify.exe --uninstall-sendto");
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
            ShowTextDialog("Third-party notices", LibmagicProbe.NoticeText());
        }

        private void SetBusy(bool busy, string status)
        {
            UseWaitCursor = busy;
            addFilesButton.Enabled = !busy;
            copyButton.Enabled = !busy;
            saveButton.Enabled = !busy;
            openFolderButton.Enabled = !busy;
            statusLabel.Text = status;
        }
    }

    internal sealed class FileReport
    {
        public string DisplayName;
        public string FullText;
        public readonly List<ReportSection> Sections = new List<ReportSection>();
    }

    internal sealed class ReportSection
    {
        public string Title;
        public readonly List<ReportItem> Items = new List<ReportItem>();

        public string DetailText()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Title);
            foreach (var item in Items)
            {
                if (string.Equals((item.Title ?? string.Empty).Trim(), (item.Detail ?? string.Empty).Trim(), StringComparison.Ordinal))
                    sb.AppendLine(item.Title);
                else
                    sb.AppendLine(item.Title + ": " + item.Detail.Replace("\r", "").Replace("\n", " | "));
            }
            return sb.ToString().TrimEnd() + Environment.NewLine + Environment.NewLine;
        }
    }

    internal sealed class ReportItem
    {
        public string Title;
        public string Detail;
    }

    internal sealed class PreferencesForm : Form
    {
        private readonly CheckBox sendToCheckBox;
        private readonly ComboBox updateCheckFrequencyBox;
        private readonly CheckBox installUpdatesQuietlyCheckBox;

        public PreferencesForm(AppSettings settings)
        {
            Text = "Preferences";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(540, 330);
            MinimumSize = new Size(460, 260);
            AccessibleName = "Preferences";

            var tabs = new TabControl { Dock = DockStyle.Fill, AccessibleName = "Preference tabs" };
            Controls.Add(tabs);

            var automationTab = new TabPage("Automation") { AccessibleName = "Automation" };
            tabs.TabPages.Add(automationTab);
            var automationPanel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 1, Padding = new Padding(12) };
            automationTab.Controls.Add(automationPanel);
            sendToCheckBox = new CheckBox
            {
                Text = "Add FileDentify to Windows &Send To menu",
                Checked = settings.SendToEnabled,
                AutoSize = true,
                AccessibleName = "Add FileDentify to Windows Send To menu"
            };
            automationPanel.Controls.Add(sendToCheckBox);

            var updatesTab = new TabPage("Updates") { AccessibleName = "Updates" };
            tabs.TabPages.Add(updatesTab);
            var updatesPanel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 1, Padding = new Padding(12) };
            updatesTab.Controls.Add(updatesPanel);
            updatesPanel.Controls.Add(new Label { Text = "Check GitHub Releases for updates:", AutoSize = true });
            updateCheckFrequencyBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260, AccessibleName = "Check GitHub Releases for updates" };
            updateCheckFrequencyBox.Items.AddRange(new object[] { "At startup", "Every hour", "Every 6 hours", "Every 12 hours", "Daily", "Weekly", "Never" });
            updateCheckFrequencyBox.SelectedIndex = UpdateFrequencyIndex(settings.UpdateCheckFrequency);
            updatesPanel.Controls.Add(updateCheckFrequencyBox);
            installUpdatesQuietlyCheckBox = new CheckBox
            {
                Text = "Download and install updates &quietly when available",
                Checked = settings.InstallUpdatesQuietly,
                AutoSize = true,
                AccessibleName = "Download and install updates quietly when available"
            };
            updatesPanel.Controls.Add(installUpdatesQuietlyCheckBox);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(10) };
            Controls.Add(buttons);
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        public void ApplyTo(AppSettings settings)
        {
            settings.SendToEnabled = sendToCheckBox.Checked;
            settings.UpdateCheckFrequency = UpdateFrequencyFromIndex(updateCheckFrequencyBox.SelectedIndex);
            settings.InstallUpdatesQuietly = installUpdatesQuietlyCheckBox.Checked;
        }

        private static int UpdateFrequencyIndex(string value)
        {
            switch (UpdateService.NormalizeUpdateCheckFrequency(value))
            {
                case "Startup": return 0;
                case "Hourly": return 1;
                case "6Hours": return 2;
                case "12Hours": return 3;
                case "Daily": return 4;
                case "Weekly": return 5;
                case "Never": return 6;
                default: return 0;
            }
        }

        private static string UpdateFrequencyFromIndex(int index)
        {
            switch (index)
            {
                case 1: return "Hourly";
                case 2: return "6Hours";
                case 3: return "12Hours";
                case 4: return "Daily";
                case 5: return "Weekly";
                case 6: return "Never";
                default: return "Startup";
            }
        }
    }

    internal sealed class AppSettings
    {
        public bool SendToEnabled;
        public string UpdateCheckFrequency = "Startup";
        public bool InstallUpdatesQuietly;
        public DateTime? LastAutomaticUpdateCheckUtc;

        public static string SettingsPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FileDentify.ini"); }
        }

        public static AppSettings Load()
        {
            var settings = new AppSettings();
            if (!File.Exists(SettingsPath))
                return settings;
            foreach (var raw in File.ReadAllLines(SettingsPath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("[", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                var split = line.IndexOf('=');
                if (split <= 0)
                    continue;
                var key = line.Substring(0, split).Trim();
                var value = line.Substring(split + 1).Trim();
                if (key.Equals("SendToEnabled", StringComparison.OrdinalIgnoreCase))
                    settings.SendToEnabled = ParseBool(value, settings.SendToEnabled);
                else if (key.Equals("UpdateCheckFrequency", StringComparison.OrdinalIgnoreCase))
                    settings.UpdateCheckFrequency = UpdateService.NormalizeUpdateCheckFrequency(value);
                else if (key.Equals("InstallUpdatesQuietly", StringComparison.OrdinalIgnoreCase))
                    settings.InstallUpdatesQuietly = ParseBool(value, settings.InstallUpdatesQuietly);
                else if (key.Equals("LastAutomaticUpdateCheckUtc", StringComparison.OrdinalIgnoreCase))
                {
                    DateTime parsed;
                    if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
                        settings.LastAutomaticUpdateCheckUtc = parsed;
                }
            }
            return settings;
        }

        public void Save()
        {
            var lines = new List<string>
            {
                "[Settings]",
                "SendToEnabled=" + SendToEnabled,
                "UpdateCheckFrequency=" + UpdateService.NormalizeUpdateCheckFrequency(UpdateCheckFrequency),
                "InstallUpdatesQuietly=" + InstallUpdatesQuietly,
                "LastAutomaticUpdateCheckUtc=" + (LastAutomaticUpdateCheckUtc.HasValue ? LastAutomaticUpdateCheckUtc.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) : string.Empty)
            };
            File.WriteAllLines(SettingsPath, lines.ToArray(), Encoding.ASCII);
        }

        private static bool ParseBool(string value, bool fallback)
        {
            bool parsed;
            return bool.TryParse(value, out parsed) ? parsed : fallback;
        }
    }

    internal static class SendToInstaller
    {
        private const string SendToShortcutName = "File&Dentify.lnk";

        public static void SetInstalled(bool installed)
        {
            var path = GetSendToPath();
            if (installed)
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                    throw new InvalidOperationException("WScript.Shell is not available.");
                var shell = Activator.CreateInstance(shellType);
                var shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                var shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { Application.ExecutablePath });
                shortcutType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { string.Empty });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Application.StartupPath });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static string GetSendToPath()
        {
            var sendToFolder = Environment.GetFolderPath(Environment.SpecialFolder.SendTo);
            if (string.IsNullOrWhiteSpace(sendToFolder))
                throw new InvalidOperationException("The Windows Send To folder could not be found.");
            Directory.CreateDirectory(sendToFolder);
            return Path.Combine(sendToFolder, SendToShortcutName);
        }
    }

    internal static class FileInspector
    {
        private const int HeaderReadSize = 1024 * 1024;
        private const int StringReadSize = 4 * 1024 * 1024;

        public static FileReport Inspect(string path)
        {
            var file = new FileInfo(path);
            var report = new FileReport();
            report.DisplayName = file.Name;

            byte[] header = ReadPrefix(path, HeaderReadSize);
            byte[] stringSample = header.Length >= StringReadSize ? header : ReadPrefix(path, StringReadSize);
            var libmagic = LibmagicProbe.Identify(path);
            var sections = report.Sections;

            var summary = AddSection(sections, "Summary");
            Add(summary, "Likely type", GuessType(path, header, file.Length, libmagic));
            if (libmagic != null && IsUsefulLibmagicDescription(libmagic.Description))
                Add(summary, "Unix file says", libmagic.Description);
            Add(summary, "Path", path);
            Add(summary, "Size", FormatBytes(file.Length) + " (" + file.Length.ToString(CultureInfo.InvariantCulture) + " bytes)");
            Add(summary, "Extension", string.IsNullOrEmpty(file.Extension) ? "(none)" : file.Extension);
            Add(summary, "Modified", file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Add(summary, "Created", file.CreationTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            AddFilesystemInfo(sections, file);
            AddDmgInfo(sections, path, file.Length);

            var signatures = AddSection(sections, "Signature matches");
            foreach (var match in SignatureMatcher.Match(header, path))
                Add(signatures, match.Title, match.Detail);
            if (signatures.Items.Count == 0)
                Add(signatures, "No common signature match", "The first bytes do not match the built-in signature list.");
            AddLibmagicInfo(sections, libmagic);

            var hashes = AddSection(sections, "Hashes");
            AddHashInfo(hashes, path, file.Length);

            var headerSection = AddSection(sections, "Header bytes");
            Add(headerSection, "ASCII preview", AsciiPreview(header, 256));
            Add(headerSection, "Hex preview", HexPreview(header, 256));

            var structure = AddSection(sections, "Structure hints");
            AddStructureHints(structure, path, header);
            AddPeInfo(sections, path, header);
            AddVersionInfo(sections, path);
            AddImageInfo(sections, header);
            AddPdfInfo(sections, header);
            AddFontInfo(sections, header);
            AddOleCompoundInfo(sections, path, header);
            AddCompressedStreamInfo(sections, header);
            AddIso9660Info(sections, path, header);
            AddVirtualDiskInfo(sections, path, header, file.Length);
            AddMozillaLz4Info(sections, header);
            AddUfsInfo(sections, path, header);
            AddPropertyListInfo(sections, header);
            AddSqliteInfo(sections, header);
            AddRarInfo(sections, header);
            AddIsoBmffInfo(sections, header);
            AddRiffInfo(sections, header);
            AddIffInfo(sections, header);
            AddMidiInfo(sections, header);
            AddAudioHeaderInfo(sections, header);

            var foundStrings = FindAsciiStrings(stringSample, 4, 40);
            AddReadableTextInfo(sections, stringSample);

            var strings = AddSection(sections, "Printable strings");
            if (foundStrings.Count == 0)
                Add(strings, "No strings found", "No printable ASCII runs of at least four characters were found in the sampled data.");
            else
                foreach (var s in foundStrings)
                    Add(strings, "0x" + s.Offset.ToString("X8", CultureInfo.InvariantCulture), s.Value);

            var stats = AddSection(sections, "Byte statistics");
            AddByteStats(stats, header, file.Length);
            AddTextInfo(sections, header);

            AddExternalToolInfo(sections, path);
            AddCompanionToolInfo(sections, path);

            report.FullText = BuildReportText(report);
            return report;
        }

        private static void AddStructureHints(ReportSection section, string path, byte[] header)
        {
            if (StartsWith(header, Encoding.ASCII.GetBytes("PK\x03\x04")) || StartsWith(header, Encoding.ASCII.GetBytes("PK\x05\x06")) || StartsWith(header, Encoding.ASCII.GetBytes("PK\x07\x08")))
                AddZipHints(section, path);

            if (header.Length >= 32 && AsciiPreview(header, 32).Contains("Roland SRX"))
                Add(section, "Roland SRX header", "The file starts with a Roland SRX marker. This suggests a synthesizer expansion ROM image rather than a normal archive.");

            if (header.Length > 0)
            {
                var zeroCount = header.Count(b => b == 0);
                Add(section, "Zero bytes in first sample", zeroCount.ToString(CultureInfo.InvariantCulture) + " of " + header.Length.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void AddFilesystemInfo(List<ReportSection> sections, FileInfo file)
        {
            var section = AddSection(sections, "Filesystem");
            Add(section, "Directory", file.DirectoryName ?? string.Empty);
            Add(section, "Base name", Path.GetFileNameWithoutExtension(file.Name));
            Add(section, "File name length", file.Name.Length.ToString(CultureInfo.InvariantCulture) + " characters");
            Add(section, "Attributes", file.Attributes.ToString());
            Add(section, "Modified UTC", file.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Add(section, "Created UTC", file.CreationTimeUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Add(section, "Accessed UTC", file.LastAccessTimeUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }

        private static void AddLibmagicInfo(List<ReportSection> sections, LibmagicResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.Description))
                return;

            var section = AddSection(sections, "Unix file/libmagic");
            Add(section, "Description", result.Description);
            if (!string.IsNullOrWhiteSpace(result.Mime))
                Add(section, "MIME", result.Mime);
            Add(section, "Engine", result.Engine);
        }

        private static void AddDmgInfo(List<ReportSection> sections, string path, long length)
        {
            if (!string.Equals(Path.GetExtension(path), ".dmg", StringComparison.OrdinalIgnoreCase))
                return;

            var section = AddSection(sections, "Apple disk image");
            Add(section, "Extension hint", "DMG / Apple disk image");
            var suffix = ReadSuffix(path, 8192);
            var offset = IndexOfAscii(suffix, "koly");
            if (offset < 0)
            {
                Add(section, "UDIF trailer", "Not found in the final 8192 bytes.");
                return;
            }

            Add(section, "UDIF trailer", "Found");
            Add(section, "Trailer offset from file end", (suffix.Length - offset).ToString(CultureInfo.InvariantCulture) + " bytes before end");
            if (offset + 512 <= suffix.Length)
            {
                Add(section, "UDIF version", ReadUInt32BigEndian(suffix, offset + 4).ToString(CultureInfo.InvariantCulture));
                Add(section, "UDIF header size", ReadUInt32BigEndian(suffix, offset + 8).ToString(CultureInfo.InvariantCulture) + " bytes");
                Add(section, "Trailer preview", HexPreview(suffix.Skip(offset).Take(64).ToArray(), 64));
            }
        }

        private static void AddVersionInfo(List<ReportSection> sections, string path)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                var items = new[]
                {
                    new { Name = "Description", Value = info.FileDescription },
                    new { Name = "Product", Value = info.ProductName },
                    new { Name = "Company", Value = info.CompanyName },
                    new { Name = "File version", Value = info.FileVersion },
                    new { Name = "Product version", Value = info.ProductVersion },
                    new { Name = "Copyright", Value = info.LegalCopyright },
                    new { Name = "Original filename", Value = info.OriginalFilename },
                    new { Name = "Internal name", Value = info.InternalName },
                    new { Name = "Language", Value = info.Language }
                }.Where(i => !string.IsNullOrWhiteSpace(i.Value)).ToArray();

                if (items.Length == 0)
                    return;

                var section = AddSection(sections, "Version information");
                foreach (var item in items)
                    Add(section, item.Name, item.Value);
            }
            catch
            {
            }
        }

        private static void AddImageInfo(List<ReportSection> sections, byte[] header)
        {
            try
            {
                if (header.Length >= 24 && StartsWith(header, new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0d, 0x0a, 0x1a, 0x0a }))
                {
                    var section = AddSection(sections, "Image");
                    Add(section, "Format", "PNG");
                    Add(section, "Dimensions", ReadUInt32BigEndian(header, 16) + " x " + ReadUInt32BigEndian(header, 20));
                }
                else if (header.Length >= 10 && (StartsWith(header, Encoding.ASCII.GetBytes("GIF87a")) || StartsWith(header, Encoding.ASCII.GetBytes("GIF89a"))))
                {
                    var section = AddSection(sections, "Image");
                    Add(section, "Format", "GIF");
                    Add(section, "Dimensions", BitConverter.ToUInt16(header, 6) + " x " + BitConverter.ToUInt16(header, 8));
                }
                else if (header.Length >= 26 && header[0] == 'B' && header[1] == 'M')
                {
                    var section = AddSection(sections, "Image");
                    Add(section, "Format", "BMP");
                    Add(section, "Dimensions", BitConverter.ToInt32(header, 18) + " x " + Math.Abs(BitConverter.ToInt32(header, 22)));
                    Add(section, "Bits per pixel", BitConverter.ToUInt16(header, 28).ToString(CultureInfo.InvariantCulture));
                }
                else if (header.Length >= 4 && header[0] == 0xff && header[1] == 0xd8)
                {
                    var dimensions = TryReadJpegDimensions(header);
                    if (dimensions != null)
                    {
                        var section = AddSection(sections, "Image");
                        Add(section, "Format", "JPEG");
                        Add(section, "Dimensions", dimensions);
                    }
                }
            }
            catch
            {
            }
        }

        private static uint ReadUInt32BigEndian(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];
        }

        private static ulong ReadUInt64BigEndian(byte[] data, int offset)
        {
            return ((ulong)data[offset] << 56) | ((ulong)data[offset + 1] << 48) | ((ulong)data[offset + 2] << 40) | ((ulong)data[offset + 3] << 32) |
                ((ulong)data[offset + 4] << 24) | ((ulong)data[offset + 5] << 16) | ((ulong)data[offset + 6] << 8) | data[offset + 7];
        }

        private static ushort ReadUInt16BigEndian(byte[] data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        private static ushort ReadUInt16LittleEndian(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static uint ReadUInt32LittleEndian(byte[] data, int offset)
        {
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

        private static double ReadIeeeExtended80(byte[] data, int offset)
        {
            if (offset + 10 > data.Length)
                return 0;
            var exponent = ((data[offset] & 0x7f) << 8) | data[offset + 1];
            var sign = (data[offset] & 0x80) == 0 ? 1 : -1;
            if (exponent == 0)
                return 0;
            var mantissa = ReadUInt64BigEndian(data, offset + 2);
            return sign * (double)mantissa * Math.Pow(2, exponent - 16383 - 63);
        }

        private static string TryReadJpegDimensions(byte[] data)
        {
            var i = 2;
            while (i + 9 < data.Length)
            {
                if (data[i] != 0xff)
                {
                    i++;
                    continue;
                }
                while (i < data.Length && data[i] == 0xff)
                    i++;
                if (i >= data.Length)
                    return null;
                var marker = data[i++];
                if (marker == 0xd8 || marker == 0xd9 || marker == 0x01)
                    continue;
                if (i + 1 >= data.Length)
                    return null;
                var length = ReadUInt16BigEndian(data, i);
                if (length < 2 || i + length > data.Length)
                    return null;
                if ((marker >= 0xc0 && marker <= 0xc3) || (marker >= 0xc5 && marker <= 0xc7) || (marker >= 0xc9 && marker <= 0xcb) || (marker >= 0xcd && marker <= 0xcf))
                {
                    var height = ReadUInt16BigEndian(data, i + 3);
                    var width = ReadUInt16BigEndian(data, i + 5);
                    return width + " x " + height;
                }
                i += length;
            }
            return null;
        }

        private static void AddPdfInfo(List<ReportSection> sections, byte[] header)
        {
            if (header.Length < 5 || !StartsWith(header, Encoding.ASCII.GetBytes("%PDF-")))
                return;
            var section = AddSection(sections, "PDF");
            var firstLineEnd = Array.IndexOf(header, (byte)0x0a);
            if (firstLineEnd < 0 || firstLineEnd > 32)
                firstLineEnd = Math.Min(header.Length, 32);
            Add(section, "Header", Encoding.ASCII.GetString(header, 0, firstLineEnd).Trim());
            var text = Encoding.ASCII.GetString(header.Take(Math.Min(header.Length, 65536)).ToArray());
            Add(section, "Linearized hint", text.IndexOf("/Linearized", StringComparison.OrdinalIgnoreCase) >= 0 ? "Present" : "Not found in first sample");
            Add(section, "Encryption hint", text.IndexOf("/Encrypt", StringComparison.OrdinalIgnoreCase) >= 0 ? "Present" : "Not found in first sample");
        }

        private static void AddFontInfo(List<ReportSection> sections, byte[] header)
        {
            var format = FontFormatName(header);
            if (format == null)
                return;

            var section = AddSection(sections, "Font");
            Add(section, "Format", format);
            if (header.Length >= 12)
            {
                if (StartsWith(header, Encoding.ASCII.GetBytes("wOFF")) || StartsWith(header, Encoding.ASCII.GetBytes("wOF2")))
                {
                    Add(section, "Flavor", FontFlavorName(Encoding.ASCII.GetString(header, 4, 4)));
                    Add(section, "Declared length", FormatBytes(ReadUInt32BigEndian(header, 8)));
                    Add(section, "Table count", ReadUInt16BigEndian(header, 12).ToString(CultureInfo.InvariantCulture));
                    if (header.Length >= 20)
                        Add(section, "Uncompressed sfnt size", FormatBytes(ReadUInt32BigEndian(header, 16)));
                }
                else
                {
                    Add(section, "Table count", ReadUInt16BigEndian(header, 4).ToString(CultureInfo.InvariantCulture));
                    var tables = new List<string>();
                    for (var offset = 12; offset + 16 <= header.Length && tables.Count < 20; offset += 16)
                    {
                        var tag = Encoding.ASCII.GetString(header, offset, 4);
                        if (!IsPrintableAscii(tag))
                            break;
                        tables.Add(tag + " at 0x" + ReadUInt32BigEndian(header, offset + 8).ToString("X", CultureInfo.InvariantCulture) + ", " + FormatBytes(ReadUInt32BigEndian(header, offset + 12)));
                    }
                    if (tables.Count > 0)
                        Add(section, "Table directory", string.Join("\r\n", tables.ToArray()));
                }
            }
        }

        private static void AddOleCompoundInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (!IsOleCompoundFile(header))
                return;

            var section = AddSection(sections, "OLE compound document");
            Add(section, "Container", "Microsoft Compound File Binary Format");
            var ext = Path.GetExtension(path);
            if (string.Equals(ext, ".msi", StringComparison.OrdinalIgnoreCase))
                Add(section, "Extension hint", "Windows Installer package");
            else if (string.Equals(ext, ".doc", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".xls", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".ppt", StringComparison.OrdinalIgnoreCase))
                Add(section, "Extension hint", "Legacy Microsoft Office document");
            if (header.Length >= 30)
            {
                Add(section, "Sector shift", ReadUInt16LittleEndian(header, 30).ToString(CultureInfo.InvariantCulture));
                Add(section, "Mini-sector shift", ReadUInt16LittleEndian(header, 32).ToString(CultureInfo.InvariantCulture));
            }
            if (header.Length >= 56)
                Add(section, "Directory sector count", ReadUInt32LittleEndian(header, 40).ToString(CultureInfo.InvariantCulture));
        }

        private static void AddCompressedStreamInfo(List<ReportSection> sections, byte[] header)
        {
            var kind = CompressionFormatName(header);
            if (kind == null)
                return;

            var section = AddSection(sections, "Compressed stream");
            Add(section, "Format", kind);
            if (StartsWith(header, new byte[] { 0xFD, (byte)'7', (byte)'z', (byte)'X', (byte)'Z', 0x00 }) && header.Length >= 12)
                Add(section, "Header flags", "0x" + header[6].ToString("X2", CultureInfo.InvariantCulture) + " 0x" + header[7].ToString("X2", CultureInfo.InvariantCulture));
        }

        private static void AddIso9660Info(List<ReportSection> sections, string path, byte[] header)
        {
            if (!HasIso9660Descriptor(header))
                return;

            var section = AddSection(sections, "ISO 9660 volume");
            Add(section, "Descriptor marker", "CD001 at offset 0x8001");
            if (header.Length >= 0x8050)
            {
                var systemId = Encoding.ASCII.GetString(header, 0x8008, 32).Trim();
                var volumeId = Encoding.ASCII.GetString(header, 0x8028, 32).Trim();
                if (!string.IsNullOrWhiteSpace(systemId))
                    Add(section, "System id", systemId);
                if (!string.IsNullOrWhiteSpace(volumeId))
                    Add(section, "Volume id", volumeId);
            }
            if (string.Equals(Path.GetExtension(path), ".iso", StringComparison.OrdinalIgnoreCase))
                Add(section, "Extension hint", "Optical disc image");
        }

        private static void AddVirtualDiskInfo(List<ReportSection> sections, string path, byte[] header, long length)
        {
            var ext = Path.GetExtension(path);
            var kind = VirtualDiskFormatName(path, header);
            if (kind == null)
                return;

            var section = AddSection(sections, "Virtual disk");
            Add(section, "Format", kind);
            if (string.Equals(ext, ".vmdk", StringComparison.OrdinalIgnoreCase) && LooksLikeText(header))
            {
                var text = Encoding.ASCII.GetString(header.Take(Math.Min(header.Length, 8192)).ToArray());
                AddKeyValueLine(section, text, "createType");
                AddKeyValueLine(section, text, "CID");
                AddKeyValueLine(section, text, "parentCID");
                AddKeyValueLine(section, text, "ddb.virtualHWVersion");
                AddKeyValueLine(section, text, "ddb.adapterType");
            }
            if (string.Equals(ext, ".vhd", StringComparison.OrdinalIgnoreCase))
                Add(section, "VHD footer", length >= 512 ? "Expected in final 512 bytes" : "File is smaller than a normal VHD footer");
        }

        private static void AddMozillaLz4Info(List<ReportSection> sections, byte[] header)
        {
            if (!StartsWith(header, Encoding.ASCII.GetBytes("mozLz40\0")))
                return;

            var section = AddSection(sections, "Mozilla LZ4 JSON");
            Add(section, "Marker", "mozLz40");
            Add(section, "Common use", "Firefox/Thunderbird profile data compressed with Mozilla's LZ4 wrapper");
            var preview = AsciiPreview(header.Skip(8).Take(160).ToArray(), 160);
            if (!string.IsNullOrWhiteSpace(preview))
                Add(section, "Compressed payload preview", preview);
        }

        private static void AddUfsInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (header.Length < 4 || !StartsWith(header, Encoding.ASCII.GetBytes("UFS2")))
                return;

            var section = AddSection(sections, "UFS sample library container");
            Add(section, "Format marker", "UFS2");
            Add(section, "Container family", "UVI/Falcon soundbank or sample-library container");

            var embeddedName = ReadNullTerminatedAscii(header, 0x30, 96);
            if (!string.IsNullOrWhiteSpace(embeddedName))
                Add(section, "Embedded name", embeddedName);

            Add(section, "Extension", Path.GetExtension(path));
        }

        private static string ReadNullTerminatedAscii(byte[] data, int offset, int maxLength)
        {
            if (data == null || offset < 0 || offset >= data.Length)
                return string.Empty;
            var count = 0;
            while (offset + count < data.Length && count < maxLength)
            {
                var b = data[offset + count];
                if (b == 0)
                    break;
                if (b < 32 || b >= 127)
                    return string.Empty;
                count++;
            }
            return count == 0 ? string.Empty : Encoding.ASCII.GetString(data, offset, count);
        }

        private static void AddRiffInfo(List<ReportSection> sections, byte[] header)
        {
            if (header.Length < 12 || !StartsWith(header, Encoding.ASCII.GetBytes("RIFF")))
                return;
            var section = AddSection(sections, "RIFF");
            var riffSize = BitConverter.ToUInt32(header, 4);
            var form = Encoding.ASCII.GetString(header, 8, 4);
            Add(section, "Form type", form);
            Add(section, "Declared RIFF payload size", FormatBytes(riffSize) + " (" + riffSize.ToString(CultureInfo.InvariantCulture) + " bytes)");

            var offset = 12;
            var chunks = new List<string>();
            while (offset + 8 <= header.Length && chunks.Count < 20)
            {
                var id = Encoding.ASCII.GetString(header, offset, 4);
                var size = BitConverter.ToUInt32(header, offset + 4);
                chunks.Add(id + " (" + FormatBytes(size) + ")");
                if (id == "fmt " && size >= 16 && offset + 24 <= header.Length)
                {
                    var format = BitConverter.ToUInt16(header, offset + 8);
                    var channels = BitConverter.ToUInt16(header, offset + 10);
                    var sampleRate = BitConverter.ToUInt32(header, offset + 12);
                    var byteRate = BitConverter.ToUInt32(header, offset + 16);
                    var blockAlign = BitConverter.ToUInt16(header, offset + 20);
                    var bits = BitConverter.ToUInt16(header, offset + 22);
                    Add(section, "Audio format", WaveFormatName(format) + " (0x" + format.ToString("X4", CultureInfo.InvariantCulture) + ")");
                    Add(section, "Channels", channels.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Sample rate", sampleRate.ToString(CultureInfo.InvariantCulture) + " Hz");
                    Add(section, "Bits per sample", bits.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Byte rate", byteRate.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Block align", blockAlign.ToString(CultureInfo.InvariantCulture));
                }
                var next = offset + 8L + size + (size % 2);
                if (next <= offset || next > int.MaxValue)
                    break;
                offset = (int)next;
            }
            Add(section, "First chunks", string.Join("\r\n", chunks.ToArray()));
        }

        private static void AddIffInfo(List<ReportSection> sections, byte[] header)
        {
            if (header.Length < 12 || !StartsWith(header, Encoding.ASCII.GetBytes("FORM")))
                return;

            var section = AddSection(sections, "AIFF/IFF");
            var declaredSize = ReadUInt32BigEndian(header, 4);
            var formType = Encoding.ASCII.GetString(header, 8, 4);
            Add(section, "Form type", formType);
            Add(section, "Declared FORM payload size", FormatBytes(declaredSize) + " (" + declaredSize.ToString(CultureInfo.InvariantCulture) + " bytes)");

            var offset = 12;
            var chunks = new List<string>();
            while (offset + 8 <= header.Length && chunks.Count < 24)
            {
                var id = Encoding.ASCII.GetString(header, offset, 4);
                var size = ReadUInt32BigEndian(header, offset + 4);
                chunks.Add(id + " (" + FormatBytes(size) + ")");

                if (id == "COMM" && size >= 18 && offset + 26 <= header.Length)
                {
                    var channels = ReadUInt16BigEndian(header, offset + 8);
                    var frames = ReadUInt32BigEndian(header, offset + 10);
                    var bits = ReadUInt16BigEndian(header, offset + 14);
                    var sampleRate = ReadIeeeExtended80(header, offset + 16);
                    Add(section, "Channels", channels.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Sample frames", frames.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Bits per sample", bits.ToString(CultureInfo.InvariantCulture));
                    if (sampleRate > 0)
                        Add(section, "Sample rate", sampleRate.ToString("0.###", CultureInfo.InvariantCulture) + " Hz");
                    if (formType == "AIFC" && size >= 22 && offset + 30 <= header.Length)
                        Add(section, "Compression type", Encoding.ASCII.GetString(header, offset + 26, 4));
                }

                var next = offset + 8L + size + (size % 2);
                if (next <= offset || next > int.MaxValue)
                    break;
                offset = (int)next;
            }

            if (chunks.Count > 0)
                Add(section, "First chunks", string.Join("\r\n", chunks.ToArray()));
        }

        private static void AddMidiInfo(List<ReportSection> sections, byte[] header)
        {
            if (header.Length < 14 || !StartsWith(header, Encoding.ASCII.GetBytes("MThd")))
                return;

            var section = AddSection(sections, "MIDI");
            var headerLength = ReadUInt32BigEndian(header, 4);
            var format = ReadUInt16BigEndian(header, 8);
            var tracks = ReadUInt16BigEndian(header, 10);
            var division = ReadUInt16BigEndian(header, 12);
            Add(section, "Header length", headerLength.ToString(CultureInfo.InvariantCulture));
            Add(section, "Format", MidiFormatName(format) + " (" + format.ToString(CultureInfo.InvariantCulture) + ")");
            Add(section, "Declared track count", tracks.ToString(CultureInfo.InvariantCulture));
            Add(section, "Timing division", MidiDivisionDescription(division));

            var offset = 8 + (int)Math.Min(headerLength, int.MaxValue - 8);
            var chunks = new List<string>();
            while (offset + 8 <= header.Length && chunks.Count < 20)
            {
                var id = Encoding.ASCII.GetString(header, offset, 4);
                var size = ReadUInt32BigEndian(header, offset + 4);
                chunks.Add(id + " (" + FormatBytes(size) + ")");
                var next = offset + 8L + size;
                if (next <= offset || next > int.MaxValue)
                    break;
                offset = (int)next;
            }

            if (chunks.Count > 0)
                Add(section, "First chunks", string.Join("\r\n", chunks.ToArray()));
        }

        private static void AddPropertyListInfo(List<ReportSection> sections, byte[] header)
        {
            if (header.Length >= 8 && StartsWith(header, Encoding.ASCII.GetBytes("bplist00")))
            {
                var section = AddSection(sections, "Property list");
                Add(section, "Format", "Binary property list");
                Add(section, "Version marker", Encoding.ASCII.GetString(header, 6, 2));
                return;
            }

            if (!LooksLikeText(header))
                return;
            var text = Encoding.UTF8.GetString(header.Take(Math.Min(header.Length, 8192)).ToArray());
            if (text.IndexOf("<plist", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("DOCTYPE plist", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var section = AddSection(sections, "Property list");
                Add(section, "Format", "XML property list");
            }
        }

        private static void AddSqliteInfo(List<ReportSection> sections, byte[] header)
        {
            if (header.Length < 100 || !StartsWith(header, Encoding.ASCII.GetBytes("SQLite format 3\0")))
                return;

            var section = AddSection(sections, "SQLite database");
            var pageSize = ReadUInt16BigEndian(header, 16);
            Add(section, "Page size", (pageSize == 1 ? 65536 : pageSize).ToString(CultureInfo.InvariantCulture) + " bytes");
            Add(section, "Write version", SqliteJournalMode(header[18]));
            Add(section, "Read version", SqliteJournalMode(header[19]));
            Add(section, "Page count", ReadUInt32BigEndian(header, 28).ToString(CultureInfo.InvariantCulture));
            Add(section, "Schema cookie", ReadUInt32BigEndian(header, 40).ToString(CultureInfo.InvariantCulture));
            Add(section, "User version", ReadUInt32BigEndian(header, 60).ToString(CultureInfo.InvariantCulture));
            Add(section, "Application id", "0x" + ReadUInt32BigEndian(header, 68).ToString("X8", CultureInfo.InvariantCulture));
        }

        private static void AddRarInfo(List<ReportSection> sections, byte[] header)
        {
            var rar4 = Encoding.GetEncoding(28591).GetBytes("Rar!\x1A\x07\x00");
            var rar5 = Encoding.GetEncoding(28591).GetBytes("Rar!\x1A\x07\x01\x00");
            if (!StartsWith(header, rar4) && !StartsWith(header, rar5))
                return;

            var section = AddSection(sections, "RAR archive");
            Add(section, "Format version", StartsWith(header, rar5) ? "RAR 5" : "RAR 4 or earlier");
            Add(section, "Marker", HexPreview(header, StartsWith(header, rar5) ? 8 : 7));
        }

        private static void AddIsoBmffInfo(List<ReportSection> sections, byte[] header)
        {
            if (header.Length < 12 || Encoding.ASCII.GetString(header, 4, 4) != "ftyp")
                return;
            var section = AddSection(sections, "ISO base media");
            var boxSize = ReadUInt32BigEndian(header, 0);
            var major = Encoding.ASCII.GetString(header, 8, 4).TrimEnd('\0', ' ');
            var minor = ReadUInt32BigEndian(header, 12);
            Add(section, "Major brand", major);
            Add(section, "Minor version", minor.ToString(CultureInfo.InvariantCulture));
            Add(section, "File type box size", FormatBytes(boxSize));
            var brands = new List<string>();
            for (var i = 16; i + 4 <= header.Length && i < boxSize; i += 4)
            {
                var brand = Encoding.ASCII.GetString(header, i, 4).TrimEnd('\0', ' ');
                if (!string.IsNullOrWhiteSpace(brand))
                    brands.Add(brand);
                if (brands.Count >= 24)
                    break;
            }
            if (brands.Count > 0)
                Add(section, "Compatible brands", string.Join(", ", brands.ToArray()));
            Add(section, "Family hint", IsoBrandHint(major, brands));
        }

        private static string IsoBrandHint(string major, List<string> brands)
        {
            var all = new List<string>(brands ?? new List<string>());
            if (!string.IsNullOrWhiteSpace(major))
                all.Insert(0, major);
            if (all.Any(b => b.StartsWith("qt", StringComparison.OrdinalIgnoreCase))) return "QuickTime/MOV";
            if (all.Any(b => b.StartsWith("M4A", StringComparison.OrdinalIgnoreCase) || b.StartsWith("M4B", StringComparison.OrdinalIgnoreCase))) return "MPEG-4 audio";
            if (all.Any(b => b.StartsWith("isom", StringComparison.OrdinalIgnoreCase) || b.StartsWith("mp4", StringComparison.OrdinalIgnoreCase))) return "MP4/ISO media";
            if (all.Any(b => b.StartsWith("3g", StringComparison.OrdinalIgnoreCase))) return "3GPP media";
            if (all.Any(b => b.StartsWith("avif", StringComparison.OrdinalIgnoreCase))) return "AVIF image";
            if (all.Any(b => b.StartsWith("heic", StringComparison.OrdinalIgnoreCase) || b.StartsWith("heix", StringComparison.OrdinalIgnoreCase))) return "HEIF/HEIC image";
            return "ISO base media family";
        }

        private static string WaveFormatName(ushort format)
        {
            switch (format)
            {
                case 0x0001: return "PCM";
                case 0x0003: return "IEEE float";
                case 0x0006: return "A-law";
                case 0x0007: return "mu-law";
                case 0x0011: return "IMA ADPCM";
                case 0x0055: return "MP3";
                case 0xfffe: return "Extensible";
                default: return "Unknown";
            }
        }

        private static string MidiFormatName(ushort format)
        {
            switch (format)
            {
                case 0: return "Single track";
                case 1: return "Multiple synchronous tracks";
                case 2: return "Multiple asynchronous sequences";
                default: return "Unknown";
            }
        }

        private static string MidiDivisionDescription(ushort division)
        {
            if ((division & 0x8000) == 0)
                return (division & 0x7fff).ToString(CultureInfo.InvariantCulture) + " ticks per quarter note";

            var fpsByte = (sbyte)((division >> 8) & 0xff);
            var ticks = division & 0xff;
            return Math.Abs(fpsByte).ToString(CultureInfo.InvariantCulture) + " SMPTE frames per second, " + ticks.ToString(CultureInfo.InvariantCulture) + " ticks per frame";
        }

        private static string SqliteJournalMode(byte value)
        {
            switch (value)
            {
                case 1: return "Legacy rollback journal";
                case 2: return "WAL-capable";
                default: return "Unknown (" + value.ToString(CultureInfo.InvariantCulture) + ")";
            }
        }

        private static void AddAudioHeaderInfo(List<ReportSection> sections, byte[] header)
        {
            if (header.Length >= 4 && StartsWith(header, Encoding.ASCII.GetBytes("fLaC")))
                AddFlacHeaderInfo(sections, header);
            if (header.Length >= 4 && StartsWith(header, Encoding.ASCII.GetBytes("OggS")))
                AddOggHeaderInfo(sections, header);
            if (header.Length >= 10 && StartsWith(header, Encoding.ASCII.GetBytes("ID3")))
                AddId3HeaderInfo(sections, header);
        }

        private static void AddFlacHeaderInfo(List<ReportSection> sections, byte[] header)
        {
            var section = AddSection(sections, "FLAC");
            var offset = 4;
            var blocks = new List<string>();
            while (offset + 4 <= header.Length && blocks.Count < 12)
            {
                var blockHeader = header[offset];
                var last = (blockHeader & 0x80) != 0;
                var type = blockHeader & 0x7f;
                var length = (header[offset + 1] << 16) | (header[offset + 2] << 8) | header[offset + 3];
                blocks.Add(FlacBlockName(type) + " (" + length.ToString(CultureInfo.InvariantCulture) + " bytes)" + (last ? ", last" : ""));
                if (type == 0 && length >= 34 && offset + 4 + 18 <= header.Length)
                {
                    var p = offset + 4 + 10;
                    var sampleRate = (header[p] << 12) | (header[p + 1] << 4) | ((header[p + 2] & 0xf0) >> 4);
                    var channels = ((header[p + 2] & 0x0e) >> 1) + 1;
                    var bits = (((header[p + 2] & 0x01) << 4) | ((header[p + 3] & 0xf0) >> 4)) + 1;
                    Add(section, "Sample rate", sampleRate.ToString(CultureInfo.InvariantCulture) + " Hz");
                    Add(section, "Channels", channels.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Bits per sample", bits.ToString(CultureInfo.InvariantCulture));
                }
                offset += 4 + length;
                if (last)
                    break;
            }
            Add(section, "Metadata blocks", string.Join("\r\n", blocks.ToArray()));
        }

        private static string FlacBlockName(int type)
        {
            switch (type)
            {
                case 0: return "STREAMINFO";
                case 1: return "PADDING";
                case 2: return "APPLICATION";
                case 3: return "SEEKTABLE";
                case 4: return "VORBIS_COMMENT";
                case 5: return "CUESHEET";
                case 6: return "PICTURE";
                default: return "Block type " + type;
            }
        }

        private static void AddOggHeaderInfo(List<ReportSection> sections, byte[] header)
        {
            if (header.Length < 27)
                return;
            var section = AddSection(sections, "Ogg");
            Add(section, "Stream structure version", header[4].ToString(CultureInfo.InvariantCulture));
            Add(section, "Header type flags", "0x" + header[5].ToString("X2", CultureInfo.InvariantCulture));
            Add(section, "Serial number", BitConverter.ToUInt32(header, 14).ToString(CultureInfo.InvariantCulture));
            Add(section, "Page sequence", BitConverter.ToUInt32(header, 18).ToString(CultureInfo.InvariantCulture));
            Add(section, "Page segments", header[26].ToString(CultureInfo.InvariantCulture));
            var sample = Encoding.ASCII.GetString(header.Take(Math.Min(header.Length, 4096)).ToArray());
            if (sample.Contains("OpusHead")) Add(section, "Codec hint", "Opus");
            else if (sample.Contains("vorbis")) Add(section, "Codec hint", "Vorbis");
            else if (sample.Contains("Speex")) Add(section, "Codec hint", "Speex");
        }

        private static void AddId3HeaderInfo(List<ReportSection> sections, byte[] header)
        {
            var section = AddSection(sections, "ID3");
            Add(section, "Version", "ID3v2." + header[3].ToString(CultureInfo.InvariantCulture) + "." + header[4].ToString(CultureInfo.InvariantCulture));
            Add(section, "Flags", "0x" + header[5].ToString("X2", CultureInfo.InvariantCulture));
            var size = ((header[6] & 0x7f) << 21) | ((header[7] & 0x7f) << 14) | ((header[8] & 0x7f) << 7) | (header[9] & 0x7f);
            Add(section, "Tag size", FormatBytes(size) + " (" + size.ToString(CultureInfo.InvariantCulture) + " bytes)");
        }

        private static void AddPeInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (header.Length < 0x40 || header[0] != 'M' || header[1] != 'Z')
                return;

            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var br = new BinaryReader(fs))
                {
                    fs.Position = 0x3c;
                    var peOffset = br.ReadInt32();
                    if (peOffset <= 0 || peOffset > fs.Length - 256)
                        return;

                    fs.Position = peOffset;
                    if (br.ReadUInt32() != 0x00004550)
                        return;

                    var machine = br.ReadUInt16();
                    var sectionCount = br.ReadUInt16();
                    var timestamp = br.ReadUInt32();
                    br.ReadUInt32();
                    br.ReadUInt32();
                    var optionalHeaderSize = br.ReadUInt16();
                    var characteristics = br.ReadUInt16();
                    var optionalStart = fs.Position;
                    var magic = br.ReadUInt16();
                    var isPe32Plus = magic == 0x20b;
                    var isPe32 = magic == 0x10b;
                    if (!isPe32 && !isPe32Plus)
                        return;

                    fs.Position = optionalStart + 2;
                    var linkerMajor = br.ReadByte();
                    var linkerMinor = br.ReadByte();
                    fs.Position = optionalStart + 16;
                    var entryPointRva = br.ReadUInt32();
                    fs.Position = optionalStart + (isPe32Plus ? 24 : 28);
                    var imageBaseText = isPe32Plus ? "0x" + br.ReadUInt64().ToString("X", CultureInfo.InvariantCulture) : "0x" + br.ReadUInt32().ToString("X", CultureInfo.InvariantCulture);
                    fs.Position = optionalStart + 40;
                    var osMajor = br.ReadUInt16();
                    var osMinor = br.ReadUInt16();
                    var imageMajor = br.ReadUInt16();
                    var imageMinor = br.ReadUInt16();
                    var subsystemMajor = br.ReadUInt16();
                    var subsystemMinor = br.ReadUInt16();
                    fs.Position = optionalStart + 68;
                    var subsystem = br.ReadUInt16();
                    var dllCharacteristics = br.ReadUInt16();
                    fs.Position = optionalStart + (isPe32Plus ? 108 : 92);
                    var dataDirectoryCount = br.ReadUInt32();
                    var clrRva = 0u;
                    var clrSize = 0u;
                    if (dataDirectoryCount > 14)
                    {
                        fs.Position = optionalStart + (isPe32Plus ? 112 : 96) + (14 * 8);
                        clrRva = br.ReadUInt32();
                        clrSize = br.ReadUInt32();
                    }

                    var section = AddSection(sections, "Windows executable");
                    Add(section, "PE format", isPe32Plus ? "PE32+ (usually 64-bit)" : "PE32 (usually 32-bit)");
                    Add(section, "Machine", MachineName(machine) + " (0x" + machine.ToString("X4", CultureInfo.InvariantCulture) + ")");
                    Add(section, "Subsystem", SubsystemName(subsystem) + " (0x" + subsystem.ToString("X4", CultureInfo.InvariantCulture) + ")");
                    Add(section, "Sections", sectionCount.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Entry point RVA", "0x" + entryPointRva.ToString("X", CultureInfo.InvariantCulture));
                    Add(section, "Image base", imageBaseText);
                    Add(section, "Linker version", linkerMajor.ToString(CultureInfo.InvariantCulture) + "." + linkerMinor.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Required OS version", osMajor + "." + osMinor);
                    Add(section, "Image version", imageMajor + "." + imageMinor);
                    Add(section, "Subsystem version", subsystemMajor + "." + subsystemMinor);
                    Add(section, "Characteristics", "0x" + characteristics.ToString("X4", CultureInfo.InvariantCulture));
                    Add(section, "DLL characteristics", DllCharacteristicsText(dllCharacteristics));
                    if (timestamp != 0)
                    {
                        var dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timestamp).ToLocalTime();
                        Add(section, "PE timestamp", dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                    }
                    Add(section, ".NET CLR header", clrRva != 0 && clrSize != 0 ? "Present" : "Not present");
                    fs.Position = optionalStart + optionalHeaderSize;
                    var sectionRows = new List<string>();
                    for (var i = 0; i < sectionCount && i < 32 && fs.Position + 40 <= fs.Length; i++)
                    {
                        var nameBytes = br.ReadBytes(8);
                        var name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0', ' ');
                        var virtualSize = br.ReadUInt32();
                        var virtualAddress = br.ReadUInt32();
                        var rawSize = br.ReadUInt32();
                        var rawPointer = br.ReadUInt32();
                        fs.Position += 16;
                        sectionRows.Add(name + "  RVA 0x" + virtualAddress.ToString("X", CultureInfo.InvariantCulture) + ", virtual " + FormatBytes(virtualSize) + ", raw " + FormatBytes(rawSize) + " at 0x" + rawPointer.ToString("X", CultureInfo.InvariantCulture));
                    }
                    if (sectionRows.Count > 0)
                        Add(section, "Section table", string.Join("\r\n", sectionRows.ToArray()));
                }
            }
            catch (Exception ex)
            {
                var section = AddSection(sections, "Windows executable");
                Add(section, "PE parse error", ex.Message);
            }
        }

        private static string MachineName(ushort machine)
        {
            switch (machine)
            {
                case 0x014c: return "Intel 386";
                case 0x8664: return "x64";
                case 0x01c0: return "ARM";
                case 0x01c4: return "ARMv7";
                case 0xaa64: return "ARM64";
                default: return "Unknown";
            }
        }

        private static string SubsystemName(ushort subsystem)
        {
            switch (subsystem)
            {
                case 1: return "Native";
                case 2: return "Windows GUI";
                case 3: return "Windows console";
                case 7: return "POSIX console";
                case 9: return "Windows CE GUI";
                case 10: return "EFI application";
                case 11: return "EFI boot service driver";
                case 12: return "EFI runtime driver";
                case 14: return "Xbox";
                case 16: return "Windows boot application";
                default: return "Unknown";
            }
        }

        private static string DllCharacteristicsText(ushort value)
        {
            var flags = new List<string>();
            if ((value & 0x0020) != 0) flags.Add("High entropy VA");
            if ((value & 0x0040) != 0) flags.Add("Dynamic base / ASLR");
            if ((value & 0x0080) != 0) flags.Add("Force integrity");
            if ((value & 0x0100) != 0) flags.Add("NX compatible");
            if ((value & 0x0200) != 0) flags.Add("No isolation");
            if ((value & 0x0400) != 0) flags.Add("No SEH");
            if ((value & 0x0800) != 0) flags.Add("No bind");
            if ((value & 0x1000) != 0) flags.Add("AppContainer");
            if ((value & 0x4000) != 0) flags.Add("Control Flow Guard");
            if ((value & 0x8000) != 0) flags.Add("Terminal Server aware");
            return "0x" + value.ToString("X4", CultureInfo.InvariantCulture) + (flags.Count == 0 ? "" : " (" + string.Join(", ", flags.ToArray()) + ")");
        }

        private static void AddZipHints(ReportSection section, string path)
        {
            try
            {
                using (var archive = ZipFile.OpenRead(path))
                {
                    var hasAbletonBundleInfo = archive.GetEntry("BundleInfo.json") != null;
                    var hasAbletonSong = archive.GetEntry("Song.abl") != null;
                    Add(section, "ZIP entries", archive.Entries.Count.ToString(CultureInfo.InvariantCulture));
                    var dirs = archive.Entries.Count(e => e.FullName.EndsWith("/", StringComparison.Ordinal));
                    var compressed = archive.Entries.Sum(e => e.CompressedLength);
                    var uncompressed = archive.Entries.Sum(e => e.Length);
                    Add(section, "Directory entries", dirs.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Compressed payload total", FormatBytes(compressed));
                    Add(section, "Uncompressed payload total", FormatBytes(uncompressed));
                    if (compressed > 0)
                        Add(section, "Overall expansion ratio", ((double)uncompressed / compressed).ToString("0.00", CultureInfo.InvariantCulture) + "x");
                    var names = archive.Entries.Take(30).Select(e => e.FullName + (e.FullName.EndsWith("/", StringComparison.Ordinal) ? "" : " (" + FormatBytes(e.Length) + ")")).ToArray();
                    Add(section, "First entries", names.Length == 0 ? "(empty archive)" : string.Join("\r\n", names));
                    if (archive.GetEntry("[Content_Types].xml") != null)
                        Add(section, "Office Open XML hint", "The archive contains [Content_Types].xml, common in docx/xlsx/pptx packages.");
                    if (archive.GetEntry("META-INF/MANIFEST.MF") != null)
                        Add(section, "Java/JAR hint", "The archive contains META-INF/MANIFEST.MF.");
                    if (archive.Entries.Any(e => e.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)))
                        Add(section, "Open Packaging Convention hint", "The archive contains .rels relationship files.");
                    if (string.Equals(Path.GetExtension(path), ".ablbundle", StringComparison.OrdinalIgnoreCase) || (hasAbletonBundleInfo && hasAbletonSong))
                        Add(section, "Ableton bundle hint", "The archive looks like an Ableton Move/Live bundle, with project metadata and embedded samples.");
                }
            }
            catch (Exception ex)
            {
                Add(section, "ZIP read error", ex.Message);
            }
        }

        private static void AddExternalToolInfo(List<ReportSection> sections, string path)
        {
            if (!ShouldRunFfprobe(path))
                return;
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var ffprobe = Path.Combine(appDir, "ffprobe.exe");
            if (!File.Exists(ffprobe))
                return;

            var output = RunTool(ffprobe, "-hide_banner -v error -show_format -show_streams \"" + path + "\"", 8000);
            if (string.IsNullOrWhiteSpace(output))
                return;
            if (output.IndexOf("Invalid data found", StringComparison.OrdinalIgnoreCase) >= 0)
                return;
            var media = AddSection(sections, "ffprobe");
            Add(media, "Output", output.Trim());
        }

        private static void AddCompanionToolInfo(List<ReportSection> sections, string path)
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();

            if (ext == "flac")
            {
                var metaflac = Path.Combine(appDir, "metaflac.exe");
                if (File.Exists(metaflac))
                {
                    var output = RunTool(metaflac, "--list --block-number=0 \"" + path + "\"", 8000);
                    if (!string.IsNullOrWhiteSpace(output))
                        Add(AddSection(sections, "metaflac"), "STREAMINFO", TrimToolOutput(output, 12000));
                }
            }

            if (ext == "opus" || ext == "ogg" || ext == "oga")
            {
                var opusinfo = Path.Combine(appDir, "opusinfo.exe");
                if (File.Exists(opusinfo))
                {
                    var output = RunTool(opusinfo, "\"" + path + "\"", 8000);
                    if (!string.IsNullOrWhiteSpace(output) && output.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) < 0)
                        Add(AddSection(sections, "opusinfo"), "Output", TrimToolOutput(output, 12000));
                }
            }

            var vgmstream = Path.Combine(appDir, "vgmstream-cli.exe");
            if (File.Exists(vgmstream) && IsLikelyGameAudioExtension(ext))
            {
                var output = RunTool(vgmstream, "-m \"" + path + "\"", 8000);
                if (!string.IsNullOrWhiteSpace(output) && output.IndexOf("failed", StringComparison.OrdinalIgnoreCase) < 0 && output.IndexOf("unsupported", StringComparison.OrdinalIgnoreCase) < 0)
                    Add(AddSection(sections, "vgmstream"), "Metadata", TrimToolOutput(output, 12000));
            }
        }

        private static bool IsLikelyGameAudioExtension(string ext)
        {
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "adx", "brstm", "dsp", "fsb", "hca", "msf", "nus3audio", "rsd", "str", "vgmstream", "wem", "xma", "xvag"
            };
            return exts.Contains(ext);
        }

        private static string TrimToolOutput(string output, int maxChars)
        {
            output = (output ?? string.Empty).Trim();
            if (output.Length <= maxChars)
                return output;
            return output.Substring(0, maxChars) + Environment.NewLine + "[Output truncated]";
        }

        private static bool ShouldRunFfprobe(string path)
        {
            var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            var mediaExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "aac", "aif", "aiff", "alac", "ape", "avi", "flac", "flv", "m4a", "m4v", "mkv", "mov", "mp3", "mp4", "mpeg", "mpg", "oga", "ogg", "opus", "ts", "wav", "webm", "wma", "wmv"
            };
            return mediaExtensions.Contains(ext);
        }

        private static string RunTool(string exe, string args, int timeoutMs)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (var process = Process.Start(psi))
                {
                    if (process == null)
                        return string.Empty;
                    if (!process.WaitForExit(timeoutMs))
                    {
                        try { process.Kill(); } catch { }
                        return "Tool timed out.";
                    }
                    return process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private static ReportSection AddSection(List<ReportSection> sections, string title)
        {
            var section = new ReportSection { Title = title };
            sections.Add(section);
            return section;
        }

        private static void Add(ReportSection section, string title, string detail)
        {
            section.Items.Add(new ReportItem { Title = title, Detail = detail ?? string.Empty });
        }

        private static byte[] ReadPrefix(string path, int maxBytes)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var count = (int)Math.Min(maxBytes, fs.Length);
                var data = new byte[count];
                var offset = 0;
                while (offset < count)
                {
                    var read = fs.Read(data, offset, count - offset);
                    if (read == 0)
                        break;
                    offset += read;
                }
                if (offset == count)
                    return data;
                Array.Resize(ref data, offset);
                return data;
            }
        }

        private static byte[] ReadSuffix(string path, int maxBytes)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var count = (int)Math.Min(maxBytes, fs.Length);
                var data = new byte[count];
                fs.Position = fs.Length - count;
                var offset = 0;
                while (offset < count)
                {
                    var read = fs.Read(data, offset, count - offset);
                    if (read == 0)
                        break;
                    offset += read;
                }
                if (offset == count)
                    return data;
                Array.Resize(ref data, offset);
                return data;
            }
        }

        private static string GuessType(string path, byte[] header, long length, LibmagicResult libmagic)
        {
            var matches = SignatureMatcher.Match(header, path).ToArray();
            if (header.Length >= 32 && AsciiPreview(header, 32).Contains("Roland SRX"))
                return "Roland SRX expansion ROM image";
            if (header.Length >= 4 && StartsWith(header, Encoding.ASCII.GetBytes("UFS2")))
                return "UVI/Falcon UFS sample library container";
            if (IsZipHeader(header) && string.Equals(Path.GetExtension(path), ".ablbundle", StringComparison.OrdinalIgnoreCase))
                return "Ableton Move/Live bundle (ZIP-compatible container)";
            var font = FontFormatName(header);
            if (font != null)
                return font;
            var compression = CompressionFormatName(header);
            if (compression != null)
                return compression;
            if (IsOleCompoundFile(header))
            {
                if (string.Equals(Path.GetExtension(path), ".msi", StringComparison.OrdinalIgnoreCase))
                    return "Windows Installer package";
                return "OLE compound document";
            }
            var virtualDisk = VirtualDiskFormatName(path, header);
            if (virtualDisk != null)
                return virtualDisk;
            if (HasIso9660Descriptor(header))
                return "ISO 9660 optical disc image";
            var riff = RiffTypeName(header);
            if (riff != null)
                return riff;
            var isoBrand = IsoBmffTypeName(header);
            if (isoBrand != null)
                return isoBrand;
            if (StartsWith(header, Encoding.ASCII.GetBytes("mozLz40\0")))
                return "Mozilla LZ4-compressed JSON/profile data";
            if (matches.Length > 0)
                return matches[0].Detail;
            if (string.Equals(Path.GetExtension(path), ".dmg", StringComparison.OrdinalIgnoreCase))
            {
                var suffix = ReadSuffix(path, 8192);
                if (IndexOfAscii(suffix, "koly") >= 0)
                    return "Apple UDIF disk image";
                return "Possible Apple disk image";
            }
            if (libmagic != null && IsUsefulLibmagicDescription(libmagic.Description))
                return libmagic.Description + " (from Unix file/libmagic)";
            if (LooksLikeText(header))
                return "Plain text or text-like data";
            return "Unknown binary data";
        }

        private static bool IsUsefulLibmagicDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return false;
            var value = description.Trim();
            return !value.Equals("data", StringComparison.OrdinalIgnoreCase) &&
                !value.Equals("empty", StringComparison.OrdinalIgnoreCase) &&
                !value.StartsWith("application/octet-stream", StringComparison.OrdinalIgnoreCase);
        }

        private static string RiffTypeName(byte[] header)
        {
            if (header.Length < 12 || !StartsWith(header, Encoding.ASCII.GetBytes("RIFF")))
                return null;
            var form = Encoding.ASCII.GetString(header, 8, 4);
            switch (form)
            {
                case "WAVE": return "WAV audio";
                case "WEBP": return "WebP image";
                case "AVI ": return "AVI video";
                case "sfbk": return "SoundFont 2 sound bank";
                case "ACON": return "Animated cursor";
                default: return null;
            }
        }

        private static string IsoBmffTypeName(byte[] header)
        {
            if (header.Length < 16 || Encoding.ASCII.GetString(header, 4, 4) != "ftyp")
                return null;
            var boxSize = ReadUInt32BigEndian(header, 0);
            var major = Encoding.ASCII.GetString(header, 8, 4).TrimEnd('\0', ' ');
            var brands = new List<string>();
            for (var i = 16; i + 4 <= header.Length && i < boxSize && brands.Count < 24; i += 4)
                brands.Add(Encoding.ASCII.GetString(header, i, 4).TrimEnd('\0', ' '));
            var hint = IsoBrandHint(major, brands);
            return hint == "ISO base media family" ? "ISO base media file" : hint;
        }

        private static string FontFormatName(byte[] header)
        {
            if (header.Length < 4)
                return null;
            if (StartsWith(header, Encoding.ASCII.GetBytes("OTTO"))) return "OpenType CFF font";
            if (StartsWith(header, Encoding.ASCII.GetBytes("true"))) return "Classic TrueType font";
            if (StartsWith(header, Encoding.ASCII.GetBytes("typ1"))) return "PostScript Type 1 font";
            if (StartsWith(header, Encoding.ASCII.GetBytes("wOFF"))) return "WOFF web font";
            if (StartsWith(header, Encoding.ASCII.GetBytes("wOF2"))) return "WOFF2 web font";
            if (header[0] == 0x00 && header[1] == 0x01 && header[2] == 0x00 && header[3] == 0x00) return "TrueType font";
            return null;
        }

        private static string FontFlavorName(string flavor)
        {
            switch (flavor)
            {
                case "\0\x01\0\0": return "TrueType outlines";
                case "OTTO": return "OpenType CFF outlines";
                case "true": return "Classic TrueType outlines";
                default: return flavor;
            }
        }

        private static string CompressionFormatName(byte[] header)
        {
            if (StartsWith(header, new byte[] { 0xFD, (byte)'7', (byte)'z', (byte)'X', (byte)'Z', 0x00 })) return "XZ compressed data";
            if (StartsWith(header, new byte[] { 0x04, 0x22, 0x4D, 0x18 })) return "LZ4 frame compressed data";
            if (StartsWith(header, new byte[] { 0x28, 0xB5, 0x2F, 0xFD })) return "Zstandard compressed data";
            return null;
        }

        private static bool IsZipHeader(byte[] header)
        {
            return StartsWith(header, Encoding.ASCII.GetBytes("PK\x03\x04")) ||
                StartsWith(header, Encoding.ASCII.GetBytes("PK\x05\x06")) ||
                StartsWith(header, Encoding.ASCII.GetBytes("PK\x07\x08"));
        }

        private static bool IsOleCompoundFile(byte[] header)
        {
            return StartsWith(header, new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 });
        }

        private static bool HasIso9660Descriptor(byte[] header)
        {
            return header.Length >= 0x8006 &&
                header[0x8001] == (byte)'C' &&
                header[0x8002] == (byte)'D' &&
                header[0x8003] == (byte)'0' &&
                header[0x8004] == (byte)'0' &&
                header[0x8005] == (byte)'1';
        }

        private static string VirtualDiskFormatName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path);
            if (StartsWith(header, Encoding.ASCII.GetBytes("KDMV"))) return "VMware sparse virtual disk";
            if (StartsWith(header, Encoding.ASCII.GetBytes("vhdxfile"))) return "Hyper-V VHDX virtual disk";
            if (StartsWith(header, Encoding.GetEncoding(28591).GetBytes("QFI\xFB"))) return "QEMU QCOW2 virtual disk";
            if (header.Length >= 0x44 && header[0x40] == 0x7F && header[0x41] == 0x10 && header[0x42] == 0xDA && header[0x43] == 0xBE) return "VirtualBox VDI virtual disk";
            if (LooksLikeText(header))
            {
                var text = Encoding.ASCII.GetString(header.Take(Math.Min(header.Length, 4096)).ToArray());
                if (text.IndexOf("# Disk DescriptorFile", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("createType=", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "VMware VMDK descriptor";
                if (text.IndexOf("<Envelope", StringComparison.OrdinalIgnoreCase) >= 0 && string.Equals(ext, ".ovf", StringComparison.OrdinalIgnoreCase))
                    return "Open Virtualization Format descriptor";
            }
            if (string.Equals(ext, ".vhd", StringComparison.OrdinalIgnoreCase)) return "Hyper-V VHD virtual disk";
            if (string.Equals(ext, ".vhdx", StringComparison.OrdinalIgnoreCase)) return "Hyper-V VHDX virtual disk";
            return null;
        }

        private static void AddKeyValueLine(ReportSection section, string text, string key)
        {
            var value = FindDescriptorValue(text, key);
            if (!string.IsNullOrWhiteSpace(value))
                Add(section, key, value);
        }

        private static string FindDescriptorValue(string text, string key)
        {
            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                    continue;
                return trimmed.Substring(key.Length + 1).Trim().Trim('"');
            }
            return string.Empty;
        }

        private static bool IsPrintableAscii(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            foreach (var ch in value)
                if (ch < 32 || ch >= 127)
                    return false;
            return true;
        }

        private static bool LooksLikeText(byte[] data)
        {
            if (data.Length == 0)
                return false;
            var sample = data.Take(Math.Min(data.Length, 4096)).ToArray();
            var bad = sample.Count(b => b < 9 || (b > 13 && b < 32));
            return bad <= Math.Max(1, sample.Length / 50);
        }

        private static void AddHashInfo(ReportSection section, string path, long length)
        {
            const long fullHashLimit = 64L * 1024L * 1024L;
            if (length <= fullHashLimit)
            {
                Add(section, "SHA-256", Sha256(path, length));
            }
            else
            {
                Add(section, "SHA-256 first 64 MiB", Sha256(path, fullHashLimit));
                Add(section, "Full SHA-256", "Skipped by default for large files so inspection stays responsive.");
            }
        }

        private static string Sha256(string path, long maxBytes)
        {
            using (var sha = SHA256.Create())
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var buffer = new byte[1024 * 1024];
                var remaining = Math.Min(maxBytes, fs.Length);
                while (remaining > 0)
                {
                    var read = fs.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                    if (read == 0)
                        break;
                    remaining -= read;
                    sha.TransformBlock(buffer, 0, read, null, 0);
                }
                sha.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "bytes", "KiB", "MiB", "GiB", "TiB" };
            double value = bytes;
            var unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return value.ToString(unit == 0 ? "0" : "0.##", CultureInfo.InvariantCulture) + " " + units[unit];
        }

        private static string FormatUnsignedBytes(ulong bytes)
        {
            if (bytes <= long.MaxValue)
                return FormatBytes((long)bytes);
            return bytes.ToString(CultureInfo.InvariantCulture) + " bytes";
        }

        private static string AsciiPreview(byte[] data, int count)
        {
            var sb = new StringBuilder();
            foreach (var b in data.Take(count))
                sb.Append(b >= 32 && b < 127 ? (char)b : '.');
            return sb.ToString();
        }

        private static int IndexOfAscii(byte[] data, string text)
        {
            if (data == null || string.IsNullOrEmpty(text))
                return -1;
            var needle = Encoding.ASCII.GetBytes(text);
            for (var i = 0; i <= data.Length - needle.Length; i++)
            {
                var found = true;
                for (var j = 0; j < needle.Length; j++)
                {
                    if (data[i + j] != needle[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return i;
            }
            return -1;
        }

        private static string HexPreview(byte[] data, int count)
        {
            var sb = new StringBuilder();
            var limit = Math.Min(data.Length, count);
            for (var i = 0; i < limit; i += 16)
            {
                sb.Append(i.ToString("X8", CultureInfo.InvariantCulture));
                sb.Append("  ");
                for (var j = 0; j < 16 && i + j < limit; j++)
                    sb.Append(data[i + j].ToString("X2", CultureInfo.InvariantCulture)).Append(' ');
                sb.Append(" ");
                for (var j = 0; j < 16 && i + j < limit; j++)
                {
                    var b = data[i + j];
                    sb.Append(b >= 32 && b < 127 ? (char)b : '.');
                }
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        private static void AddByteStats(ReportSection section, byte[] data, long fileLength)
        {
            if (data.Length == 0)
            {
                Add(section, "Empty file", "The file has no bytes.");
                return;
            }
            var unique = data.Distinct().Count();
            Add(section, "Sample size", FormatBytes(data.Length) + " from " + FormatBytes(fileLength));
            Add(section, "Unique byte values in sample", unique.ToString(CultureInfo.InvariantCulture) + " of 256");
            Add(section, "Entropy estimate", ShannonEntropy(data).ToString("0.000", CultureInfo.InvariantCulture) + " bits per byte");
            var top = data.GroupBy(b => b).Select(g => new { Byte = g.Key, Count = g.Count() }).OrderByDescending(g => g.Count).Take(16);
            var lines = top.Select(g => "0x" + g.Byte.ToString("X2", CultureInfo.InvariantCulture) + "  " + g.Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Most common bytes", string.Join("\r\n", lines.ToArray()));
        }

        private static double ShannonEntropy(byte[] data)
        {
            if (data.Length == 0)
                return 0;
            var counts = new int[256];
            foreach (var b in data)
                counts[b]++;
            double entropy = 0;
            foreach (var count in counts)
            {
                if (count == 0)
                    continue;
                var p = (double)count / data.Length;
                entropy -= p * (Math.Log(p) / Math.Log(2));
            }
            return entropy;
        }

        private static void AddTextInfo(List<ReportSection> sections, byte[] data)
        {
            if (!LooksLikeText(data))
                return;
            var section = AddSection(sections, "Text hints");
            var encoding = "No BOM detected";
            if (StartsWith(data, new byte[] { 0xef, 0xbb, 0xbf })) encoding = "UTF-8 with BOM";
            else if (StartsWith(data, new byte[] { 0xff, 0xfe, 0x00, 0x00 })) encoding = "UTF-32 little-endian BOM";
            else if (StartsWith(data, new byte[] { 0x00, 0x00, 0xfe, 0xff })) encoding = "UTF-32 big-endian BOM";
            else if (StartsWith(data, new byte[] { 0xff, 0xfe })) encoding = "UTF-16 little-endian BOM";
            else if (StartsWith(data, new byte[] { 0xfe, 0xff })) encoding = "UTF-16 big-endian BOM";
            Add(section, "Encoding marker", encoding);
            var crlf = CountSequence(data, new byte[] { 0x0d, 0x0a });
            var lf = data.Count(b => b == 0x0a) - crlf;
            var cr = data.Count(b => b == 0x0d) - crlf;
            Add(section, "Line endings in sample", "CRLF: " + crlf + ", LF: " + lf + ", CR: " + cr);
        }

        private static int CountSequence(byte[] data, byte[] sequence)
        {
            var count = 0;
            for (var i = 0; i <= data.Length - sequence.Length; i++)
            {
                var match = true;
                for (var j = 0; j < sequence.Length; j++)
                    if (data[i + j] != sequence[j])
                    {
                        match = false;
                        break;
                    }
                if (match)
                    count++;
            }
            return count;
        }

        private static List<FoundString> FindAsciiStrings(byte[] data, int minLength, int maxResults)
        {
            var results = new List<FoundString>();
            var i = 0;
            while (i < data.Length && results.Count < maxResults)
            {
                if (data[i] >= 32 && data[i] < 127)
                {
                    var start = i;
                    while (i < data.Length && data[i] >= 32 && data[i] < 127)
                        i++;
                    if (i - start >= minLength)
                    {
                        var value = Encoding.ASCII.GetString(data, start, Math.Min(i - start, 160));
                        results.Add(new FoundString { Offset = start, Value = value });
                    }
                }
                else
                {
                    i++;
                }
            }
            return results;
        }

        private static void AddReadableTextInfo(List<ReportSection> sections, byte[] data)
        {
            const int maxLines = 200;
            var lines = FindReadableTextLines(data, 4, maxLines);
            var section = AddSection(sections, "Readable text");
            if (lines.Count == 0)
            {
                Add(section, "No readable text found", "No ASCII or UTF-16 text runs of at least four characters were found in the sampled data.");
                return;
            }

            foreach (var line in lines)
                Add(section, line, line);
            Add(section, "Scan note", "Plain text view of strings found in the first " + FormatBytes(data.Length) + ". Offsets are kept in the separate Printable strings section.");
            if (lines.Count >= maxLines)
                Add(section, "Limit", "Showing the first " + maxLines.ToString(CultureInfo.InvariantCulture) + " readable text runs.");
        }

        private static List<string> FindReadableTextLines(byte[] data, int minLength, int maxResults)
        {
            var lines = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            AddReadableLines(lines, seen, FindAsciiStrings(data, minLength, maxResults * 2).Select(s => s.Value), maxResults);
            AddReadableLines(lines, seen, FindUtf16Strings(data, minLength, maxResults, true).Select(s => s.Value), maxResults);
            AddReadableLines(lines, seen, FindUtf16Strings(data, minLength, maxResults, false).Select(s => s.Value), maxResults);
            return lines;
        }

        private static void AddReadableLines(List<string> lines, HashSet<string> seen, IEnumerable<string> values, int maxResults)
        {
            foreach (var value in values)
            {
                var cleaned = CleanReadableText(value);
                if (!IsUsefulReadableLine(cleaned) || seen.Contains(cleaned))
                    continue;
                seen.Add(cleaned);
                lines.Add(cleaned);
                if (lines.Count >= maxResults)
                    break;
            }
        }

        private static string CleanReadableText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var sb = new StringBuilder();
            var lastWasSpace = false;
            foreach (var ch in value.Trim())
            {
                if (char.IsControl(ch))
                    continue;
                if (char.IsWhiteSpace(ch))
                {
                    if (!lastWasSpace)
                        sb.Append(' ');
                    lastWasSpace = true;
                }
                else
                {
                    sb.Append(ch);
                    lastWasSpace = false;
                }
                if (sb.Length >= 240)
                    break;
            }
            return sb.ToString().Trim();
        }

        private static bool IsUsefulReadableLine(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length < 6)
                return false;

            if (value.IndexOf("ABCDEFGHIJKLMNOPQRSTUVWXYZ", StringComparison.Ordinal) >= 0)
                return false;

            var lettersOrDigits = value.Count(char.IsLetterOrDigit);
            if (lettersOrDigits < 3)
                return false;

            var symbols = value.Count(ch => !char.IsLetterOrDigit(ch) && !char.IsWhiteSpace(ch));
            if (symbols > lettersOrDigits)
                return false;

            if (value.Length > 30)
            {
                var dominant = value.Where(char.IsLetterOrDigit).GroupBy(ch => ch).Select(g => g.Count()).DefaultIfEmpty(0).Max();
                if (dominant > lettersOrDigits / 3)
                    return false;
            }

            var naturalWords = NaturalWordCount(value);
            if (naturalWords == 0)
                return false;

            if (value.Any(char.IsWhiteSpace) && value.Length >= 8 && naturalWords >= 2)
                return true;

            if (value.Length >= 8 && naturalWords >= 1 && value.Any(ch => ch == ':' || ch == '\\' || ch == '/' || ch == '.' || ch == '-' || ch == '_'))
                return true;

            var vowels = value.Count(ch => "aeiouAEIOU".IndexOf(ch) >= 0);
            if (value.Length >= 12 && vowels >= 2 && symbols <= lettersOrDigits / 4)
                return true;

            return false;
        }

        private static int NaturalWordCount(string value)
        {
            var words = 0;
            var lettersInRun = 0;
            foreach (var ch in value)
            {
                if (char.IsLetter(ch))
                {
                    lettersInRun++;
                    continue;
                }
                if (lettersInRun >= 2)
                    words++;
                lettersInRun = 0;
            }
            if (lettersInRun >= 2)
                words++;
            return words;
        }

        private static List<FoundString> FindUtf16Strings(byte[] data, int minLength, int maxResults, bool littleEndian)
        {
            var results = new List<FoundString>();
            for (var alignment = 0; alignment < 2 && results.Count < maxResults; alignment++)
            {
                var i = alignment;
                while (i + 1 < data.Length && results.Count < maxResults)
                {
                    var ch = ReadUtf16Char(data, i, littleEndian);
                    if (IsReadableUtf16TextChar(ch))
                    {
                        var start = i;
                        var chars = new List<char>();
                        while (i + 1 < data.Length)
                        {
                            ch = ReadUtf16Char(data, i, littleEndian);
                            if (!IsReadableUtf16TextChar(ch))
                                break;
                            chars.Add(ch);
                            i += 2;
                            if (chars.Count >= 160)
                                break;
                        }
                        if (chars.Count >= minLength)
                            results.Add(new FoundString { Offset = start, Value = new string(chars.ToArray()) });
                    }
                    else
                    {
                        i += 2;
                    }
                }
            }
            return results;
        }

        private static char ReadUtf16Char(byte[] data, int offset, bool littleEndian)
        {
            return littleEndian
                ? (char)(data[offset] | (data[offset + 1] << 8))
                : (char)((data[offset] << 8) | data[offset + 1]);
        }

        private static bool IsReadableTextChar(char ch)
        {
            if (ch == '\t' || ch == '\r' || ch == '\n')
                return true;
            return ch >= 32 && ch != 0x7f && !char.IsSurrogate(ch);
        }

        private static bool IsReadableUtf16TextChar(char ch)
        {
            if (ch == '\t' || ch == '\r' || ch == '\n')
                return true;
            return ch >= 32 && ch < 256 && ch != 0x7f;
        }

        private static bool StartsWith(byte[] data, byte[] signature)
        {
            if (data.Length < signature.Length)
                return false;
            for (var i = 0; i < signature.Length; i++)
                if (data[i] != signature[i])
                    return false;
            return true;
        }

        private static string BuildReportText(FileReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine(report.DisplayName);
            sb.AppendLine(new string('=', report.DisplayName.Length));
            foreach (var section in report.Sections)
            {
                sb.AppendLine();
                sb.AppendLine(section.Title);
                sb.AppendLine(new string('-', section.Title.Length));
                foreach (var item in section.Items)
                {
                    if (string.Equals((item.Title ?? string.Empty).Trim(), (item.Detail ?? string.Empty).Trim(), StringComparison.Ordinal))
                    {
                        sb.AppendLine(item.Title);
                    }
                    else
                    {
                        sb.AppendLine(item.Title + ":");
                        sb.AppendLine(item.Detail);
                    }
                }
            }
            return sb.ToString().TrimEnd();
        }
    }

    internal sealed class FoundString
    {
        public int Offset;
        public string Value;
    }

    internal sealed class LibmagicResult
    {
        public string Description;
        public string Mime;
        public string Engine;
    }

    internal static class LibmagicProbe
    {
        private static readonly object Sync = new object();
        private static string extractedDirectory;
        private static bool cleanupRegistered;

        public static LibmagicResult Identify(string path)
        {
            try
            {
                var dir = EnsureExtracted();
                if (string.IsNullOrEmpty(dir))
                    return null;

                var exe = Path.Combine(dir, "file.exe");
                var magic = Path.Combine(dir, "magic.mgc");
                var description = RunFile(exe, dir, "-b -m " + Quote(magic) + " " + Quote(path), 6000);
                if (string.IsNullOrWhiteSpace(description))
                    return null;

                var mime = RunFile(exe, dir, "-b --mime -m " + Quote(magic) + " " + Quote(path), 6000);
                return new LibmagicResult
                {
                    Description = description.Trim(),
                    Mime = string.IsNullOrWhiteSpace(mime) ? string.Empty : mime.Trim(),
                    Engine = "Embedded file/libmagic 5.30 Windows build, using embedded magic.mgc"
                };
            }
            catch
            {
                return null;
            }
        }

        public static string NoticeText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("FileDentify embeds the Unix file/libmagic engine for broad file identification.");
            sb.AppendLine();
            sb.AppendLine("file/libmagic notice");
            sb.AppendLine("--------------------");
            sb.AppendLine(ReadResourceText("FileDentify.Embedded.COPYING.file"));
            sb.AppendLine();
            sb.AppendLine("libgnurx notice");
            sb.AppendLine("---------------");
            sb.AppendLine(ReadResourceText("FileDentify.Embedded.COPYING.libgnurx"));
            return sb.ToString().TrimEnd();
        }

        private static string EnsureExtracted()
        {
            lock (Sync)
            {
                if (!string.IsNullOrEmpty(extractedDirectory) && Directory.Exists(extractedDirectory))
                    return extractedDirectory;

                var dir = Path.Combine(Path.GetTempPath(), "FileDentify-libmagic-" + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));
                Directory.CreateDirectory(dir);
                ExtractResource("FileDentify.Embedded.file.exe", Path.Combine(dir, "file.exe"));
                ExtractResource("FileDentify.Embedded.libmagic-1.dll", Path.Combine(dir, "libmagic-1.dll"));
                ExtractResource("FileDentify.Embedded.libgnurx-0.dll", Path.Combine(dir, "libgnurx-0.dll"));
                ExtractResource("FileDentify.Embedded.magic.mgc", Path.Combine(dir, "magic.mgc"));
                extractedDirectory = dir;

                if (!cleanupRegistered)
                {
                    AppDomain.CurrentDomain.ProcessExit += delegate { Cleanup(); };
                    cleanupRegistered = true;
                }

                return extractedDirectory;
            }
        }

        private static void ExtractResource(string name, string path)
        {
            if (File.Exists(path))
                return;

            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (input == null)
                    throw new InvalidOperationException("Missing embedded resource: " + name);
                using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                    input.CopyTo(output);
            }
        }

        private static string ReadResourceText(string name)
        {
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (input == null)
                    return "Missing embedded notice: " + name;
                using (var reader = new StreamReader(input, Encoding.UTF8, true))
                    return reader.ReadToEnd().TrimEnd();
            }
        }

        private static string RunFile(string exe, string workingDirectory, string arguments, int timeoutMs)
        {
            var psi = new ProcessStartInfo(exe, arguments);
            psi.WorkingDirectory = workingDirectory;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.EnvironmentVariables["MAGIC"] = Path.Combine(workingDirectory, "magic.mgc");
            psi.EnvironmentVariables["PATH"] = workingDirectory + ";" + psi.EnvironmentVariables["PATH"];

            using (var process = Process.Start(psi))
            {
                if (process == null)
                    return string.Empty;
                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(); } catch { }
                    return string.Empty;
                }
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(output))
                    return output;
                return error;
            }
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static void Cleanup()
        {
            try
            {
                if (!string.IsNullOrEmpty(extractedDirectory) && Directory.Exists(extractedDirectory))
                    Directory.Delete(extractedDirectory, true);
            }
            catch
            {
            }
        }
    }

    internal static class SignatureMatcher
    {
        public static IEnumerable<ReportItem> Match(byte[] data, string path)
        {
            foreach (var sig in Signatures)
                if (MatchesAt(data, sig.Bytes, sig.Offset))
                    yield return new ReportItem { Title = sig.Name, Detail = sig.Description };

            if (data.Length >= 12 && StartsWith(data, Encoding.ASCII.GetBytes("RIFF")))
            {
                var fourcc = Encoding.ASCII.GetString(data, 8, 4);
                yield return new ReportItem { Title = "RIFF form", Detail = fourcc };
            }

            if (data.Length >= 32 && Ascii(data, 0, Math.Min(64, data.Length)).Contains("Roland SRX"))
                yield return new ReportItem { Title = "Roland SRX", Detail = "Roland SRX expansion ROM marker found at file start." };
        }

        private static readonly Signature[] Signatures =
        {
            new Signature("%PDF-", "PDF header", "PDF document"),
            new Signature("PK\x03\x04", "ZIP local file header", "ZIP-compatible container"),
            new Signature("PK\x05\x06", "ZIP end-of-central-directory header", "Empty ZIP-compatible container"),
            new Signature("Rar!\x1A\x07\x01\x00", "RAR 5 header", "RAR 5 archive"),
            new Signature("Rar!\x1A\x07\x00", "RAR 4 header", "RAR archive"),
            new Signature("7z\xBC\xAF\x27\x1C", "7-Zip header", "7-Zip archive"),
            new Signature("\x1F\x8B", "gzip header", "gzip compressed data"),
            new Signature("\x89PNG\r\n\x1A\n", "PNG header", "PNG image"),
            new Signature("\xFF\xD8\xFF", "JPEG header", "JPEG image"),
            new Signature("GIF87a", "GIF87a header", "GIF image"),
            new Signature("GIF89a", "GIF89a header", "GIF image"),
            new Signature("RIFF", "RIFF header", "RIFF container"),
            new Signature("OggS", "OggS header", "Ogg media container"),
            new Signature("fLaC", "FLAC marker", "FLAC audio"),
            new Signature("ID3", "ID3 tag", "MP3 audio with ID3 tag"),
            new Signature("MThd", "MIDI header", "Standard MIDI file"),
            new Signature("MZ", "MZ executable header", "Windows executable or DLL"),
            new Signature("\x7FELF", "ELF header", "ELF executable"),
            new Signature("SQLite format 3\0", "SQLite header", "SQLite database"),
            new Signature("FORM", "FORM header", "IFF/AIFF-style container"),
            new Signature("BM", "BMP header", "BMP image"),
            new Signature("BZh", "bzip2 header", "bzip2 compressed data"),
            new Signature("\xFD" + "7zXZ\0", "XZ header", "XZ compressed data"),
            new Signature("\x04\x22\x4D\x18", "LZ4 frame header", "LZ4 frame compressed data"),
            new Signature("\x28\xB5\x2F\xFD", "Zstandard header", "Zstandard compressed data"),
            new Signature("mozLz40\0", "Mozilla LZ4 header", "Firefox/Thunderbird LZ4-compressed profile data"),
            new Signature("xar!", "XAR header", "XAR archive, often used by Apple installer packages"),
            new Signature("ustar", "TAR ustar marker", "TAR archive", 257),
            new Signature("Cr24", "Chrome extension header", "Chrome extension package"),
            new Signature("MSCF", "CAB header", "Microsoft Cabinet archive"),
            new Signature("ITSF", "CHM header", "Compiled HTML help file"),
            new Signature("\xD0\xCF\x11\xE0\xA1\xB1\x1A\xE1", "OLE compound header", "OLE compound document / Microsoft structured storage"),
            new Signature("\0\x01\0\0", "TrueType sfnt header", "TrueType font"),
            new Signature("OTTO", "OpenType CFF header", "OpenType CFF font"),
            new Signature("wOFF", "WOFF header", "WOFF web font"),
            new Signature("wOF2", "WOFF2 header", "WOFF2 web font"),
            new Signature("KDMV", "VMware sparse disk header", "VMware sparse virtual disk"),
            new Signature("vhdxfile", "VHDX header", "Hyper-V VHDX virtual disk"),
            new Signature("QFI\xFB", "QCOW2 header", "QEMU QCOW2 virtual disk"),
            new Signature("CD001", "ISO 9660 descriptor", "ISO 9660 optical disc image", 0x8001),
            new Signature("FLV", "FLV header", "Flash video"),
            new Signature("DICM", "DICOM marker", "DICOM medical image", 128),
            new Signature("NES\x1A", "iNES header", "Nintendo NES ROM image"),
            new Signature("ftyp", "ISO BMFF ftyp box", "MP4/QuickTime/ISO base media file", 4)
        };

        private static bool StartsWith(byte[] data, byte[] signature)
        {
            if (data.Length < signature.Length)
                return false;
            for (var i = 0; i < signature.Length; i++)
                if (data[i] != signature[i])
                    return false;
            return true;
        }

        private static bool MatchesAt(byte[] data, byte[] signature, int offset)
        {
            if (offset < 0 || data.Length < offset + signature.Length)
                return false;
            for (var i = 0; i < signature.Length; i++)
                if (data[offset + i] != signature[i])
                    return false;
            return true;
        }

        private static string Ascii(byte[] data, int offset, int count)
        {
            var sb = new StringBuilder();
            for (var i = offset; i < offset + count && i < data.Length; i++)
                sb.Append(data[i] >= 32 && data[i] < 127 ? (char)data[i] : '.');
            return sb.ToString();
        }

        private sealed class Signature
        {
            public readonly byte[] Bytes;
            public readonly string Name;
            public readonly string Description;
            public readonly int Offset;

            public Signature(string bytes, string name, string description, int offset = 0)
            {
                Bytes = Encoding.GetEncoding(28591).GetBytes(bytes);
                Name = name;
                Description = description;
                Offset = offset;
            }
        }
    }

    internal sealed class GitHubReleaseInfo
    {
        public string tag_name { get; set; }
        public string html_url { get; set; }
        public string body { get; set; }
        public List<GitHubReleaseAsset> assets { get; set; }
    }

    internal sealed class GitHubReleaseAsset
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
    }

    internal static class UpdateService
    {
        public static GitHubReleaseInfo FetchLatestRelease(string projectUrl, string version)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            using (var client = CreateGitHubClient(version))
                return new JavaScriptSerializer().Deserialize<GitHubReleaseInfo>(client.DownloadString(ApiUrl(projectUrl) + "/releases/latest"));
        }

        public static List<GitHubReleaseInfo> FetchReleases(string projectUrl, string version)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            using (var client = CreateGitHubClient(version))
                return new JavaScriptSerializer().Deserialize<List<GitHubReleaseInfo>>(client.DownloadString(ApiUrl(projectUrl) + "/releases?per_page=100")) ?? new List<GitHubReleaseInfo>();
        }

        public static GitHubReleaseInfo LatestVersionedRelease(IEnumerable<GitHubReleaseInfo> releases)
        {
            return (releases ?? new List<GitHubReleaseInfo>())
                .Select(r => new { Release = r, Version = ReleaseVersion(r) })
                .Where(i => i.Version != null)
                .OrderByDescending(i => i.Version)
                .Select(i => i.Release)
                .FirstOrDefault();
        }

        public static GitHubReleaseAsset FindPortableZipAsset(GitHubReleaseInfo release)
        {
            if (release == null)
                return null;
            return (release.assets ?? new List<GitHubReleaseAsset>())
                .Where(a => a != null && !string.IsNullOrWhiteSpace(a.browser_download_url) && !string.IsNullOrWhiteSpace(a.name))
                .Where(a => a.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(a => a.name.IndexOf("portable", StringComparison.OrdinalIgnoreCase) >= 0)
                .ThenByDescending(a => a.name.IndexOf("FileDentify", StringComparison.OrdinalIgnoreCase) >= 0)
                .FirstOrDefault();
        }

        public static string BuildReleaseNotes(IEnumerable<GitHubReleaseInfo> releases, Version current, Version latest, string currentVersion)
        {
            var newer = (releases ?? new List<GitHubReleaseInfo>())
                .Select(r => new { Release = r, Version = ReleaseVersion(r) })
                .Where(i => i.Version != null && i.Version > current && i.Version <= latest)
                .OrderByDescending(i => i.Version)
                .ToList();
            var builder = new StringBuilder();
            builder.AppendLine("Your version: " + currentVersion);
            builder.AppendLine("New version: " + latest);
            builder.AppendLine();
            builder.AppendLine("Changes between " + currentVersion + " and " + latest);
            builder.AppendLine();
            if (newer.Count == 0)
            {
                builder.AppendLine("No release notes were provided for this update.");
                return builder.ToString();
            }
            foreach (var item in newer)
            {
                builder.AppendLine(item.Release.tag_name);
                builder.AppendLine(FormatReleaseNotesForDialog(item.Release.body, "No release notes were provided for this update."));
                builder.AppendLine();
            }
            return builder.ToString();
        }

        public static string FormatReleaseNotesForDialog(string text, string emptyText)
        {
            if (string.IsNullOrWhiteSpace(text))
                return emptyText;
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine).Trim();
        }

        public static string NormalizeUpdateCheckFrequency(string value)
        {
            if (string.Equals(value, "Hourly", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "Hour", StringComparison.OrdinalIgnoreCase)) return "Hourly";
            if (string.Equals(value, "6Hours", StringComparison.OrdinalIgnoreCase)) return "6Hours";
            if (string.Equals(value, "12Hours", StringComparison.OrdinalIgnoreCase)) return "12Hours";
            if (string.Equals(value, "Daily", StringComparison.OrdinalIgnoreCase)) return "Daily";
            if (string.Equals(value, "Weekly", StringComparison.OrdinalIgnoreCase)) return "Weekly";
            if (string.Equals(value, "Never", StringComparison.OrdinalIgnoreCase)) return "Never";
            return "Startup";
        }

        public static TimeSpan? AutomaticUpdateInterval(string frequency)
        {
            switch (NormalizeUpdateCheckFrequency(frequency))
            {
                case "Hourly": return TimeSpan.FromHours(1);
                case "6Hours": return TimeSpan.FromHours(6);
                case "12Hours": return TimeSpan.FromHours(12);
                case "Daily": return TimeSpan.FromDays(1);
                case "Weekly": return TimeSpan.FromDays(7);
                default: return null;
            }
        }

        public static string GetUpdaterTempDirectory(string appDir)
        {
            var candidates = new List<string>();
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
                candidates.Add(Path.Combine(localAppData, "Temp"));
            candidates.Add(Path.GetTempPath());
            candidates.Add(Path.Combine(appDir, "Update Temp"));
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;
                try
                {
                    var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(candidate));
                    Directory.CreateDirectory(fullPath);
                    return fullPath;
                }
                catch { }
            }
            throw new InvalidOperationException("Could not create a temporary folder for the updater.");
        }

        public static string BuildUpdaterScript(string zipUrl, string targetDir, string exePath, string tempDir, int processId, string version)
        {
            return
                "$ErrorActionPreference = 'Stop'\r\n" +
                "Add-Type -AssemblyName System.Windows.Forms\r\n" +
                "$zipUrl = " + PowerShellQuote(zipUrl) + "\r\n" +
                "$userAgent = " + PowerShellQuote("FileDentify " + version) + "\r\n" +
                "$target = " + PowerShellQuote(targetDir) + "\r\n" +
                "$exe = " + PowerShellQuote(exePath) + "\r\n" +
                "$tempBase = " + PowerShellQuote(tempDir) + "\r\n" +
                "$pidToWait = " + processId.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                "try {\r\n" +
                "  [System.IO.Directory]::CreateDirectory($tempBase) | Out-Null\r\n" +
                "  $root = Join-Path $tempBase ('FileDentifyUpdate_' + [guid]::NewGuid().ToString('N'))\r\n" +
                "  $zip = Join-Path $root 'update.zip'\r\n" +
                "  $stage = Join-Path $root 'stage'\r\n" +
                "  [System.IO.Directory]::CreateDirectory($root) | Out-Null\r\n" +
                "  [System.IO.Directory]::CreateDirectory($stage) | Out-Null\r\n" +
                "  Invoke-WebRequest -Uri $zipUrl -OutFile $zip -UseBasicParsing -UserAgent $userAgent\r\n" +
                "  Expand-Archive -LiteralPath $zip -DestinationPath $stage -Force\r\n" +
                "  $source = $stage\r\n" +
                "  if (-not (Test-Path -LiteralPath (Join-Path $source 'FileDentify.exe'))) {\r\n" +
                "    $candidate = Get-ChildItem -LiteralPath $stage -Recurse -Filter 'FileDentify.exe' -File | Select-Object -First 1\r\n" +
                "    if ($candidate) { $source = $candidate.DirectoryName }\r\n" +
                "  }\r\n" +
                "  if (-not (Test-Path -LiteralPath (Join-Path $source 'FileDentify.exe'))) { throw 'The downloaded ZIP does not contain FileDentify.exe.' }\r\n" +
                "  Get-Process -Id $pidToWait -ErrorAction SilentlyContinue | Wait-Process\r\n" +
                "  Get-ChildItem -LiteralPath $source -Force | ForEach-Object {\r\n" +
                "    if ($_.name -ieq 'FileDentify.ini' -or $_.name -ieq 'Update Temp') { return }\r\n" +
                "    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $target $_.name) -Recurse -Force\r\n" +
                "  }\r\n" +
                "  Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue\r\n" +
                "  Start-Process -FilePath $exe\r\n" +
                "} catch {\r\n" +
                "  [System.Windows.Forms.MessageBox]::Show('FileDentify update failed:' + [Environment]::NewLine + [Environment]::NewLine + $_.Exception.Message, 'FileDentify updater', 'OK', 'Error') | Out-Null\r\n" +
                "}\r\n" +
                "Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue\r\n";
        }

        private static WebClient CreateGitHubClient(string version)
        {
            var client = new WebClient();
            client.Headers.Add("User-Agent", "FileDentify " + version);
            return client;
        }

        private static string ApiUrl(string projectUrl)
        {
            return projectUrl.Replace("https://github.com/", "https://api.github.com/repos/");
        }

        private static Version ReleaseVersion(GitHubReleaseInfo release)
        {
            if (release == null || string.IsNullOrWhiteSpace(release.tag_name))
                return null;
            Version version;
            return Version.TryParse(release.tag_name.Trim().TrimStart('v', 'V'), out version) ? version : null;
        }

        private static string PowerShellQuote(string value)
        {
            return "'" + (value ?? string.Empty).Replace("'", "''") + "'";
        }
    }
}

