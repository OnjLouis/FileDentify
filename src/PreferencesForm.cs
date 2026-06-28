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
{    internal sealed class PreferencesForm : Form
    {
        private readonly CheckBox sendToCheckBox;
        private readonly CheckBox desktopShortcutCheckBox;
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
            desktopShortcutCheckBox = new CheckBox
            {
                Text = "Create a &desktop shortcut for FileDentify",
                Checked = settings.DesktopShortcutEnabled,
                AutoSize = true,
                AccessibleName = "Create a desktop shortcut for FileDentify"
            };
            automationPanel.Controls.Add(desktopShortcutCheckBox);

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
            settings.DesktopShortcutEnabled = desktopShortcutCheckBox.Checked;
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

}

