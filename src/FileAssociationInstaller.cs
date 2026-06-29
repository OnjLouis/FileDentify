using System;
using Microsoft.Win32;
using System.Windows.Forms;

namespace FileDentify
{
    internal static class FileAssociationInstaller
    {
        private const string ProgId = "FileDentify.Report";

        public static void SetInstalled(bool installed)
        {
            if (installed)
                Install();
            else
                Uninstall();
        }

        private static void Install()
        {
            using (var extension = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + SavedReportStore.Extension))
            {
                extension.SetValue(string.Empty, ProgId);
                extension.SetValue("Content Type", "application/vnd.filedentify.report");
                extension.SetValue("PerceivedType", "document");
            }

            using (var progId = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId))
            {
                progId.SetValue(string.Empty, "FileDentify report");
            }

            using (var icon = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId + @"\DefaultIcon"))
                icon.SetValue(string.Empty, "\"" + Application.ExecutablePath + "\",0");

            using (var command = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId + @"\shell\open\command"))
                command.SetValue(string.Empty, "\"" + Application.ExecutablePath + "\" \"%1\"");
        }

        private static void Uninstall()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + SavedReportStore.Extension, false); }
            catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + ProgId, false); }
            catch { }
        }
    }
}
