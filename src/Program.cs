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

[assembly: AssemblyTitle("FileDentify")]
[assembly: AssemblyDescription("Accessible file identification utility for Windows")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Andre Louis")]
[assembly: AssemblyProduct("FileDentify")]
[assembly: AssemblyCopyright("Copyright (c) Andre Louis")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]
[assembly: AssemblyInformationalVersion("1.1")]

namespace FileDentify
{
    internal static class Program
    {
        private const string ConsoleStubEnvironmentVariable = "FILEDENTIFY_CONSOLE_STUB";
        public const string Version = "1.1";
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
            var isConsoleCompanion = IsConsoleCompanionExecutable();
            var launchedByConsoleStub = IsLaunchedByConsoleStub();

            if (args == null || args.Length == 0)
            {
                if (isConsoleCompanion || launchedByConsoleStub)
                {
                    Console.WriteLine(CommandLineHelp());
                    return true;
                }
                return false;
            }

            var files = new List<string>();
            string reportPath = null;
            string folderReportPath = null;
            string viewerOutputPath = null;
            var viewerStdout = false;
            var viewerMode = AdvancedViewMode.ReadableText;
            long viewerBytes = 4 * 1024 * 1024;
            var forceHtmlReport = false;
            var showHelp = false;
            var terminalMode = false;

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i] ?? string.Empty;
                if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase) || arg.Equals("/?", StringComparison.OrdinalIgnoreCase))
                    showHelp = true;
                else if (arg.Equals("--version", StringComparison.OrdinalIgnoreCase))
                {
                    WriteConsoleOrMessage("FileDentify " + Version, "FileDentify");
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
                else if (arg.Equals("--install-desktop", StringComparison.OrdinalIgnoreCase))
                {
                    DesktopShortcutInstaller.SetInstalled(true);
                    var settings = AppSettings.Load();
                    settings.DesktopShortcutEnabled = true;
                    settings.Save();
                    return true;
                }
                else if (arg.Equals("--uninstall-desktop", StringComparison.OrdinalIgnoreCase))
                {
                    DesktopShortcutInstaller.SetInstalled(false);
                    var settings = AppSettings.Load();
                    settings.DesktopShortcutEnabled = false;
                    settings.Save();
                    return true;
                }
                else if (arg.Equals("--update", StringComparison.OrdinalIgnoreCase) || arg.Equals("-u", StringComparison.OrdinalIgnoreCase))
                {
                    CheckForUpdatesFromCommandLine();
                    return true;
                }
                else if (arg.Equals("--close", StringComparison.OrdinalIgnoreCase))
                {
                    CloseOtherInstances();
                    return true;
                }
                else if ((arg.Equals("--report", StringComparison.OrdinalIgnoreCase) || arg.Equals("-r", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                    reportPath = args[++i];
                else if (arg.Equals("--html-report", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    reportPath = args[++i];
                    forceHtmlReport = true;
                }
                else if (arg.Equals("--folder-report", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    folderReportPath = args[++i];
                else if (arg.Equals("--viewer-output", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    viewerOutputPath = args[++i];
                else if (arg.Equals("--viewer", StringComparison.OrdinalIgnoreCase))
                    viewerStdout = true;
                else if (arg.Equals("--viewer-mode", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    AdvancedViewMode parsedMode;
                    if (!AdvancedFileViewRenderer.TryParseMode(args[++i], out parsedMode))
                        throw new InvalidOperationException("Unknown viewer mode. Use readable, hex, binary, or octal.");
                    viewerMode = parsedMode;
                }
                else if (arg.Equals("--viewer-bytes", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    if (!long.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out viewerBytes) || viewerBytes <= 0)
                        throw new InvalidOperationException("--viewer-bytes must be a positive integer.");
                }
                else if (arg.Equals("--terminal", StringComparison.OrdinalIgnoreCase))
                {
                    if (isConsoleCompanion || launchedByConsoleStub)
                        terminalMode = true;
                    else
                        showHelp = true;
                }
                else if (arg.StartsWith("-", StringComparison.Ordinal))
                    showHelp = true;
                else
                    files.Add(arg);
            }

            if (isConsoleCompanion &&
                !terminalMode &&
                string.IsNullOrWhiteSpace(reportPath) &&
                string.IsNullOrWhiteSpace(folderReportPath) &&
                string.IsNullOrWhiteSpace(viewerOutputPath) &&
                !viewerStdout &&
                files.Count > 0)
                terminalMode = true;

            if (showHelp)
            {
                WriteConsoleOrMessage(CommandLineHelp(), "FileDentify command line");
                return true;
            }

            if (terminalMode)
            {
                TerminalMode.Show(files);
                Environment.Exit(0);
                return true;
            }

            if (viewerStdout || !string.IsNullOrWhiteSpace(viewerOutputPath))
            {
                var existing = files.Where(File.Exists).ToArray();
                if (existing.Length == 0)
                    throw new InvalidOperationException("No readable files were supplied for the advanced viewer output.");
                var output = BuildAdvancedViewerOutput(existing, viewerMode, viewerBytes);
                if (viewerStdout)
                    Console.WriteLine(output);
                else
                    File.WriteAllText(viewerOutputPath, output, Encoding.UTF8);
                Environment.Exit(0);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(reportPath))
            {
                var existing = files.Where(File.Exists).ToArray();
                if (existing.Length == 0)
                    throw new InvalidOperationException("No readable files were supplied for the report.");
                WriteReport(reportPath, existing, forceHtmlReport);
                Environment.Exit(0);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(folderReportPath))
            {
                var targets = CollectFolderReportTargets(files)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, NaturalPathComparer.Instance)
                    .ToArray();
                if (targets.Length == 0)
                    throw new InvalidOperationException("No readable files were found for the folder report.");
                WriteReport(folderReportPath, targets, false);
                Environment.Exit(0);
                return true;
            }

            return false;
        }

        private static IEnumerable<string> CollectFolderReportTargets(IEnumerable<string> inputs)
        {
            foreach (var input in inputs)
            {
                if (File.Exists(input))
                {
                    yield return Path.GetFullPath(input);
                    continue;
                }

                if (!Directory.Exists(input))
                    continue;

                foreach (var file in SafeEnumerateFiles(input))
                    yield return file;
            }
        }

        private static bool IsConsoleCompanionExecutable()
        {
            var fileName = Path.GetFileName(Application.ExecutablePath);
            return string.Equals(fileName, "fd.com", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLaunchedByConsoleStub()
        {
            return string.Equals(Environment.GetEnvironmentVariable(ConsoleStubEnvironmentVariable), "1", StringComparison.Ordinal);
        }

        private static void WriteConsoleOrMessage(string text, string title)
        {
            if (IsConsoleCompanionExecutable() || IsLaunchedByConsoleStub() || Console.IsOutputRedirected)
                Console.WriteLine(text);
            else
                MessageBox.Show(text, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static IEnumerable<string> SafeEnumerateFiles(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                var directory = pending.Pop();
                string[] files;
                try
                {
                    files = Directory.GetFiles(directory);
                }
                catch
                {
                    files = new string[0];
                }

                foreach (var file in files)
                    yield return file;

                string[] children;
                try
                {
                    children = Directory.GetDirectories(directory);
                }
                catch
                {
                    children = new string[0];
                }

                foreach (var child in children)
                    pending.Push(child);
            }
        }

        private static void WriteReport(string reportPath, IEnumerable<string> files, bool forceHtml)
        {
            var stopwatch = Stopwatch.StartNew();
            var reports = new List<FileReport>();
            foreach (var file in files)
            {
                try
                {
                    reports.Add(FileInspector.Inspect(file));
                }
                catch (Exception ex)
                {
                    reports.Add(FileInspector.BuildErrorReport(file, ex));
                }
            }
            stopwatch.Stop();

            var content = forceHtml || IsHtmlReportPath(reportPath)
                ? FileInspector.BuildCombinedHtml(reports, "FileDentify report", stopwatch.Elapsed)
                : FileInspector.BuildCombinedText(reports, stopwatch.Elapsed);
            File.WriteAllText(reportPath, content, Encoding.UTF8);
        }

        private static string BuildAdvancedViewerOutput(IEnumerable<string> files, AdvancedViewMode mode, long maxBytes)
        {
            var builder = new StringBuilder();
            foreach (var file in files)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine();
                }
                builder.AppendLine(AdvancedFileViewRenderer.RenderFile(file, mode, maxBytes));
            }
            return builder.ToString().TrimEnd();
        }

        private static bool IsHtmlReportPath(string path)
        {
            var extension = Path.GetExtension(path);
            return string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".htm", StringComparison.OrdinalIgnoreCase);
        }

        private static void CheckForUpdatesFromCommandLine()
        {
            try
            {
                var releases = UpdateService.FetchReleases(ProjectUrl, Version);
                var release = UpdateService.LatestVersionedRelease(releases) ?? UpdateService.FetchLatestRelease(ProjectUrl, Version);
                if (release == null)
                {
                    WriteConsoleOrMessage("No FileDentify release was found.", "Check for Updates");
                    return;
                }

                System.Version current;
                System.Version remote;
                if (!System.Version.TryParse(Version, out current))
                    current = new System.Version(0, 0);
                if (!TryParseReleaseVersion(release.tag_name, out remote))
                {
                    WriteConsoleOrMessage("The latest FileDentify release could not be versioned: " + (release.tag_name ?? string.Empty), "Check for Updates");
                    return;
                }

                if (remote <= current)
                {
                    WriteConsoleOrMessage("FileDentify is up to date. Current version: " + Version + ".", "Check for Updates");
                    return;
                }

                var message = "FileDentify " + remote + " is available. Current version: " + Version + "." +
                    Environment.NewLine + Environment.NewLine +
                    (string.IsNullOrWhiteSpace(release.html_url) ? ProjectUrl + "/releases" : release.html_url);
                WriteConsoleOrMessage(message, "Check for Updates");
            }
            catch (Exception ex)
            {
                WriteConsoleOrMessage("Could not check for updates. GitHub releases may not exist yet, or the network request failed." + Environment.NewLine + Environment.NewLine + ex.Message, "Check for Updates");
            }
        }

        private static bool TryParseReleaseVersion(string tag, out System.Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(tag))
                return false;
            return System.Version.TryParse(tag.Trim().TrimStart('v', 'V'), out version);
        }

        private static void CloseOtherInstances()
        {
            var current = Process.GetCurrentProcess();
            var currentPath = SafeFullPath(Application.ExecutablePath);
            var processName = Path.GetFileNameWithoutExtension(Application.ExecutablePath);

            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    try
                    {
                        if (process.Id == current.Id)
                            continue;

                        var otherPath = SafeProcessPath(process);
                        if (!string.IsNullOrWhiteSpace(currentPath) &&
                            !string.IsNullOrWhiteSpace(otherPath) &&
                            !string.Equals(currentPath, otherPath, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (process.MainWindowHandle != IntPtr.Zero)
                            process.CloseMainWindow();
                        process.WaitForExit(5000);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static string SafeProcessPath(Process process)
        {
            try
            {
                return SafeFullPath(process.MainModule == null ? string.Empty : process.MainModule.FileName);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string SafeFullPath(string path)
        {
            try
            {
                return string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string CommandLineHelp()
        {
            return "FileDentify " + Version + Environment.NewLine + Environment.NewLine +
                "Usage:" + Environment.NewLine +
                "  FileDentify.exe [files...]" + Environment.NewLine +
                "  FileDentify.exe --report report.txt [files...]" + Environment.NewLine +
                "  FileDentify.exe --html-report report.html [files...]" + Environment.NewLine +
                "  FileDentify.exe --folder-report report.txt [folders-or-files...]" + Environment.NewLine +
                "  FileDentify.exe --viewer-output output.txt --viewer-mode hex [file]" + Environment.NewLine +
                "  FileDentify.exe --viewer --viewer-mode readable [file]" + Environment.NewLine +
                "  fd.com [files...]" + Environment.NewLine +
                "  fd.com -u" + Environment.NewLine +
                "  FileDentify.exe --close" + Environment.NewLine +
                "  FileDentify.exe -u" + Environment.NewLine +
                "  FileDentify.exe --update" + Environment.NewLine +
                "  FileDentify.exe --install-sendto" + Environment.NewLine +
                "  FileDentify.exe --uninstall-sendto" + Environment.NewLine +
                "  FileDentify.exe --install-desktop" + Environment.NewLine +
                "  FileDentify.exe --uninstall-desktop" + Environment.NewLine + Environment.NewLine +
                "Opening FileDentify.exe with file paths shows the inspection window. --report writes a report without opening the UI. Use .html or --html-report for HTML. --folder-report recursively scans folders into one report. --viewer-output writes advanced viewer output. Use fd.com for interactive terminal paging mode. fd.com and FileDentify.exe must be in the same folder. --close asks other FileDentify windows from the same executable to close.";
        }
    }

}

