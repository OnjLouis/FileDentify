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
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("FileDentify")]
[assembly: AssemblyDescription("Accessible file identification utility for Windows")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Andre Louis")]
[assembly: AssemblyProduct("FileDentify")]
[assembly: AssemblyCopyright("Copyright (c) Andre Louis")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]
[assembly: AssemblyInformationalVersion("1.3")]

namespace FileDentify
{
    internal static class Program
    {
        private const string ConsoleStubEnvironmentVariable = "FILEDENTIFY_CONSOLE_STUB";
        private const long LegacyPortableReadmeLength = 6432;
        private static readonly DateTime LegacyPortableReadmeLastWriteUtc = new DateTime(2026, 6, 28, 19, 6, 24, DateTimeKind.Utc);
        public const string Version = "1.3";
        public const string ProjectUrl = "https://github.com/OnjLouis/FileDentify";

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                RemoveStalePortableReadme();
                if (TryHandleCommandLine(args))
                    return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ApplicationExit += delegate { ScreenReaderOutput.Shutdown(); };
                Application.Run(new MainForm(args));
            }
            catch (Exception ex)
            {
                if (IsConsoleCompanionExecutable() || IsLaunchedByConsoleStub() || Console.IsOutputRedirected || Console.IsErrorRedirected)
                {
                    Console.Error.WriteLine("FileDentify error: " + ex.Message);
                    Environment.ExitCode = 1;
                }
                else
                {
                    try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FileDentify-startup-error.txt"), ex.ToString(), Encoding.UTF8); }
                    catch { }
                    MessageBox.Show(ex.Message, "FileDentify startup error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static void RemoveStalePortableReadme()
        {
            try
            {
                var readmePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "README.md");
                var readme = new FileInfo(readmePath);
                if (readme.Exists && readme.Length == LegacyPortableReadmeLength && readme.LastWriteTimeUtc == LegacyPortableReadmeLastWriteUtc)
                    File.Delete(readmePath);
            }
            catch
            {
                // Stale documentation cleanup should never block startup.
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
            var advancedView = false;
            var showHelp = false;
            var terminalMode = false;

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i] ?? string.Empty;
                if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase) || arg.Equals("/?", StringComparison.OrdinalIgnoreCase))
                    showHelp = true;
                else if (IsSwitch(arg, "--version", "-v"))
                {
                    WriteConsoleOrMessage("FileDentify " + Version, "FileDentify");
                    return true;
                }
                else if (IsSwitch(arg, "--install-sendto", "-is"))
                {
                    SendToInstaller.SetInstalled(true);
                    var settings = AppSettings.Load();
                    settings.SendToEnabled = true;
                    settings.Save();
                    return true;
                }
                else if (IsSwitch(arg, "--uninstall-sendto", "-us"))
                {
                    SendToInstaller.SetInstalled(false);
                    var settings = AppSettings.Load();
                    settings.SendToEnabled = false;
                    settings.Save();
                    return true;
                }
                else if (IsSwitch(arg, "--install-desktop", "-id"))
                {
                    DesktopShortcutInstaller.SetInstalled(true);
                    var settings = AppSettings.Load();
                    settings.DesktopShortcutEnabled = true;
                    settings.Save();
                    return true;
                }
                else if (IsSwitch(arg, "--uninstall-desktop", "-ud"))
                {
                    DesktopShortcutInstaller.SetInstalled(false);
                    var settings = AppSettings.Load();
                    settings.DesktopShortcutEnabled = false;
                    settings.Save();
                    return true;
                }
                else if (IsSwitch(arg, "--install-report-association", "-ir"))
                {
                    FileAssociationInstaller.SetInstalled(true);
                    var settings = AppSettings.Load();
                    settings.FileAssociationEnabled = true;
                    settings.Save();
                    return true;
                }
                else if (IsSwitch(arg, "--uninstall-report-association", "-ur"))
                {
                    FileAssociationInstaller.SetInstalled(false);
                    var settings = AppSettings.Load();
                    settings.FileAssociationEnabled = false;
                    settings.Save();
                    return true;
                }
                else if (arg.Equals("--update", StringComparison.OrdinalIgnoreCase) || arg.Equals("-u", StringComparison.OrdinalIgnoreCase))
                {
                    CheckForUpdatesFromCommandLine();
                    return true;
                }
                else if (IsSwitch(arg, "--close", "-c"))
                {
                    CloseOtherInstances();
                    return true;
                }
                else if ((arg.Equals("--report", StringComparison.OrdinalIgnoreCase) || arg.Equals("-r", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                    reportPath = args[++i];
                else if (IsSwitch(arg, "--html-report", "-hr") && i + 1 < args.Length)
                {
                    reportPath = args[++i];
                    forceHtmlReport = true;
                }
                else if (IsSwitch(arg, "--folder-report", "-fr") && i + 1 < args.Length)
                    folderReportPath = args[++i];
                else if (IsSwitch(arg, "--viewer-output", "-vo") && i + 1 < args.Length)
                    viewerOutputPath = args[++i];
                else if (IsSwitch(arg, "--viewer", "-vw"))
                    viewerStdout = true;
                else if (IsSwitch(arg, "--advanced-view", "-av"))
                    advancedView = true;
                else if (IsSwitch(arg, "--viewer-mode", "-vm") && i + 1 < args.Length)
                {
                    AdvancedViewMode parsedMode;
                    if (!AdvancedFileViewRenderer.TryParseMode(args[++i], out parsedMode))
                        throw new InvalidOperationException("Unknown viewer mode. Use readable, hex, binary, or octal.");
                    viewerMode = parsedMode;
                }
                else if (IsSwitch(arg, "--viewer-bytes", "-vb") && i + 1 < args.Length)
                {
                    if (!long.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out viewerBytes) || viewerBytes <= 0)
                        throw new InvalidOperationException("--viewer-bytes must be a positive integer.");
                }
                else if (IsSwitch(arg, "--terminal", "-t"))
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
                !advancedView &&
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

            if (advancedView)
            {
                var existing = files.Where(File.Exists).ToArray();
                if (existing.Length == 0)
                    throw new InvalidOperationException("No readable files were supplied for the advanced viewer.");
                ShowAdvancedViewerDirect(existing[0]);
                Environment.Exit(0);
                return true;
            }

            if (viewerStdout || !string.IsNullOrWhiteSpace(viewerOutputPath))
            {
                var existing = files.Where(File.Exists).ToArray();
                if (existing.Length == 0)
                    throw new InvalidOperationException("No readable files were supplied for the advanced viewer output.");
                EnsureOutputDoesNotOverwriteInput(viewerOutputPath, existing);
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
                var existing = files.Where(path => File.Exists(path) || FileInspector.IsReportableDirectoryPackage(path)).ToArray();
                if (existing.Length == 0)
                    throw new InvalidOperationException("No readable files were supplied for the report.");
                EnsureOutputDoesNotOverwriteInput(reportPath, existing);
                WriteReport(reportPath, existing, forceHtmlReport);
                Environment.Exit(0);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(folderReportPath))
            {
                var targets = CollectFolderReportTargets(files)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (targets.Length == 0)
                    throw new InvalidOperationException("No readable files were found for the folder report.");
                EnsureOutputDoesNotOverwriteInput(folderReportPath, targets);
                WriteReport(folderReportPath, targets, false);
                Environment.Exit(0);
                return true;
            }

            return false;
        }

        private static bool IsSwitch(string value, string longName, string shortName)
        {
            return value.Equals(longName, StringComparison.OrdinalIgnoreCase) ||
                value.Equals(shortName, StringComparison.OrdinalIgnoreCase);
        }

        private static void ShowAdvancedViewerDirect(string path)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var dialog = new AdvancedFileViewerForm(path))
            {
                dialog.ShowInTaskbar = true;
                dialog.ShowDialog();
            }
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

                if (FileInspector.IsReportableDirectoryPackage(input))
                {
                    yield return Path.GetFullPath(input);
                    continue;
                }

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
                    files = Directory.GetFiles(directory).OrderBy(path => path, NaturalPathComparer.Instance).ToArray();
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
                    children = Directory.GetDirectories(directory).OrderBy(path => path, NaturalPathComparer.Instance).ToArray();
                }
                catch
                {
                    children = new string[0];
                }

                foreach (var child in children.Reverse())
                {
                    if (FileInspector.IsReportableDirectoryPackage(child))
                        yield return child;
                    else
                        pending.Push(child);
                }
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
            ReportSectionOrdering.Apply(reports, AppSettings.Load().SectionOrder);

            if (SavedReportStore.IsSavedReportPath(reportPath))
                SavedReportStore.Save(reportPath, reports, stopwatch.Elapsed);
            else
            {
                var content = forceHtml || IsHtmlReportPath(reportPath)
                    ? FileInspector.BuildCombinedHtml(reports, "FileDentify report", stopwatch.Elapsed)
                    : FileInspector.BuildCombinedText(reports, stopwatch.Elapsed);
                File.WriteAllText(reportPath, content, Encoding.UTF8);
            }
        }

        private static void EnsureOutputDoesNotOverwriteInput(string outputPath, IEnumerable<string> inputFiles)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                return;

            var fullOutputPath = Path.GetFullPath(outputPath);
            foreach (var input in inputFiles)
            {
                if (string.IsNullOrWhiteSpace(input))
                    continue;
                if (string.Equals(fullOutputPath, Path.GetFullPath(input), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Refusing to write output over an input file: " + fullOutputPath);
            }
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
                        PostCloseToTopLevelWindows(process.Id);
                        process.WaitForExit(5000);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void PostCloseToTopLevelWindows(int processId)
        {
            try
            {
                EnumWindows(delegate(IntPtr hwnd, IntPtr lParam)
                {
                    int windowProcessId;
                    GetWindowThreadProcessId(hwnd, out windowProcessId);
                    if (windowProcessId == processId)
                        PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    return true;
                }, IntPtr.Zero);
            }
            catch
            {
            }
        }

        private const int WM_CLOSE = 0x0010;

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int processId);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

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
                "  FileDentify.exe --html-report (-hr) report.html [files...]" + Environment.NewLine +
                "  FileDentify.exe --folder-report (-fr) report.txt [folders-or-files...]" + Environment.NewLine +
                "  FileDentify.exe --advanced-view (-av) [file]" + Environment.NewLine +
                "  FileDentify.exe --viewer-output (-vo) output.txt --viewer-mode (-vm) hex [file]" + Environment.NewLine +
                "  FileDentify.exe --viewer (-vw) --viewer-mode (-vm) readable [file]" + Environment.NewLine +
                "  FileDentify.exe --viewer-bytes (-vb) 4194304" + Environment.NewLine +
                "  fd.com [files...]" + Environment.NewLine +
                "  fd.com --terminal (-t)" + Environment.NewLine +
                "  FileDentify.exe --close (-c)" + Environment.NewLine +
                "  FileDentify.exe --update (-u)" + Environment.NewLine +
                "  FileDentify.exe --install-sendto (-is)" + Environment.NewLine +
                "  FileDentify.exe --uninstall-sendto (-us)" + Environment.NewLine +
                "  FileDentify.exe --install-desktop (-id)" + Environment.NewLine +
                "  FileDentify.exe --uninstall-desktop (-ud)" + Environment.NewLine +
                "  FileDentify.exe --install-report-association (-ir)" + Environment.NewLine +
                "  FileDentify.exe --uninstall-report-association (-ur)" + Environment.NewLine +
                "  FileDentify.exe --version (-v)" + Environment.NewLine +
                "  FileDentify.exe --help (-h)" + Environment.NewLine + Environment.NewLine +
                "Opening FileDentify.exe with file paths shows the inspection window. Opening a .fdreport file reloads a saved FileDentify report. --report writes a report without opening the UI; use .fdreport for a reopenable FileDentify report, or .html/--html-report for HTML. --folder-report recursively scans folders into one report. --advanced-view opens the GUI advanced viewer directly. --viewer-output writes advanced viewer output. Use fd.com for interactive terminal paging mode. fd.com and FileDentify.exe must be in the same folder. --close asks other FileDentify windows from the same executable to close.";
        }
    }

}

