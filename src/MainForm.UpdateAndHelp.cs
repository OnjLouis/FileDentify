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
                var close = new ShortcutButton { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel, AccessibleName = "Close", AccessibleDescription = "Close this dialog.", ShortcutText = "ESC" };
                toolTip.SetToolTip(close, "ESC");
                buttons.Controls.Add(close);
                var releaseButton = new ShortcutButton { Text = "Open &release page", AutoSize = true, AccessibleName = "Open release page", AccessibleDescription = "Open the release page in the default browser.", ShortcutText = "Alt+R" };
                toolTip.SetToolTip(releaseButton, "Alt+R");
                releaseButton.Click += delegate { Process.Start(new ProcessStartInfo { FileName = releaseUrl, UseShellExecute = true }); };
                buttons.Controls.Add(releaseButton);
                if (zipAsset != null)
                {
                    var install = new ShortcutButton { Text = "&Download and install", AutoSize = true, AccessibleName = "Download and install", AccessibleDescription = "Download and install this update.", ShortcutText = "Alt+D" };
                    toolTip.SetToolTip(install, "Alt+D");
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
                var close = new ShortcutButton { Text = "Close", DialogResult = DialogResult.Cancel, AccessibleName = "Close", AccessibleDescription = "Close this dialog.", ShortcutText = "ESC" };
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

                var close = new ShortcutButton();
                close.Text = "Close";
                close.AutoSize = true;
                close.Anchor = AnchorStyles.Right;
                close.DialogResult = DialogResult.Cancel;
                close.AccessibleName = "Close";
                close.AccessibleDescription = "Close this dialog.";
                close.ShortcutText = "ESC";
                toolTip.SetToolTip(close, "ESC");
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

