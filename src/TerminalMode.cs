using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace FileDentify
{
    internal static class TerminalMode
    {
        private const int AdvancedChunkSize = 4 * 1024 * 1024;
        private const long AdvancedLoadAllLimit = 128L * 1024L * 1024L;
        private const int AttachParentProcess = -1;
        private static readonly IntPtr StdInputHandle = new IntPtr(-10);
        private static readonly IntPtr StdOutputHandle = new IntPtr(-11);
        private const int FileTypeDisk = 0x0001;
        private const int FileTypeChar = 0x0002;
        private const int FileTypePipe = 0x0003;

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(IntPtr handle);

        [DllImport("kernel32.dll")]
        private static extern int GetFileType(IntPtr handle);

        public static void Show(IEnumerable<string> files)
        {
            Show(files, false);
        }

        public static void Show(IEnumerable<string> files, bool forceOwnConsole)
        {
            var interactive = EnsureConsole(forceOwnConsole);
            var existing = (files ?? new string[0]).Where(File.Exists).ToArray();
            if (existing.Length == 0)
            {
                Console.Error.WriteLine("No readable files were supplied.");
                return;
            }

            var builder = new StringBuilder();
            var starts = new List<int>();
            var fileList = existing.ToList();
            foreach (var file in existing)
            {
                starts.Add(builder.ToString().Replace("\r\n", "\n").Split('\n').Length - 1);
                builder.AppendLine(FileInspector.Inspect(file).FullText);
                builder.AppendLine();
            }
            var report = builder.ToString().TrimEnd();
            if (!interactive)
            {
                Console.WriteLine(report);
                return;
            }
            PageText(report, fileList, starts);
        }

        private static bool EnsureConsole(bool forceOwnConsole)
        {
            var outputType = GetFileType(GetStdHandle(StdOutputHandle));
            var inputType = GetFileType(GetStdHandle(StdInputHandle));
            var redirected = outputType == FileTypeDisk || outputType == FileTypePipe || inputType == FileTypeDisk || inputType == FileTypePipe;
            if (redirected && !forceOwnConsole)
            {
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
                Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
                return false;
            }

            if (forceOwnConsole)
                AllocConsole();
            else if (!AttachConsole(AttachParentProcess))
                AllocConsole();
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
            Console.SetIn(new StreamReader(Console.OpenStandardInput()));
            return outputType == FileTypeChar || !Console.IsOutputRedirected;
        }

        private static void PageText(string text, List<string> files, List<int> starts)
        {
            var lines = (text ?? string.Empty).Replace("\r\n", "\n").Split('\n');
            var top = 0;
            var exitRequested = false;
            var previousTreatControlCAsInput = false;
            try
            {
                previousTreatControlCAsInput = Console.TreatControlCAsInput;
                Console.TreatControlCAsInput = true;
            }
            catch
            {
            }

            try
            {
                while (!exitRequested)
                {
                    var pageSize = DrawConsolePage(lines, top, "Report mode. F4 advanced view. Up/PgUp previous. Down/PgDn next. Home/End. Q/Esc/Ctrl+C exits. Line " +
                        Math.Min(top + 1, lines.Length).ToString(CultureInfo.InvariantCulture) + " of " + lines.Length.ToString(CultureInfo.InvariantCulture) + ".");

                    var key = Console.ReadKey(true);
                    if (IsQuitKey(key))
                        exitRequested = true;
                    else if (key.Key == ConsoleKey.DownArrow || key.Key == ConsoleKey.PageDown || key.Key == ConsoleKey.Spacebar)
                        top = Math.Min(Math.Max(0, lines.Length - 1), top + pageSize);
                    else if (key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.PageUp)
                        top = Math.Max(0, top - pageSize);
                    else if (key.Key == ConsoleKey.Home)
                        top = 0;
                    else if (key.Key == ConsoleKey.End)
                        top = Math.Max(0, lines.Length - pageSize);
                    else if (key.Key == ConsoleKey.F4)
                        OpenAdvancedViewerForCurrentFile(files, starts, top);
                }
            }
            finally
            {
                try { Console.TreatControlCAsInput = previousTreatControlCAsInput; }
                catch { }
                FreeConsole();
            }
        }

        private static void OpenAdvancedViewerForCurrentFile(List<string> files, List<int> starts, int topLine)
        {
            if (files.Count == 0)
                return;
            var index = 0;
            for (var i = 0; i < starts.Count; i++)
            {
                if (starts[i] <= topLine)
                    index = i;
                else
                    break;
            }

            PageAdvancedFile(files[Math.Min(index, files.Count - 1)]);
        }

        private static void PageAdvancedFile(string path)
        {
            var file = new FileInfo(path);
            var mode = AdvancedViewMode.ReadableText;
            var loadedBytes = Math.Min(file.Length, AdvancedChunkSize);
            var top = 0;

            while (true)
            {
                var document = BuildAdvancedDocument(path, mode, loadedBytes);
                var lines = document.Replace("\r\n", "\n").Split('\n');
                var status = "Advanced " + AdvancedFileViewRenderer.ModeName(mode) +
                    ". Alt+T text, Alt+X hex, Alt+B binary, Alt+O octal. L/Ctrl+L load more. Shift+L load all. Esc/Q/Backspace returns. Line " +
                    Math.Min(top + 1, lines.Length).ToString(CultureInfo.InvariantCulture) + " of " + lines.Length.ToString(CultureInfo.InvariantCulture) +
                    ". Loaded " + FormatBytes(loadedBytes) + " of " + FormatBytes(file.Length) + ".";
                var pageSize = DrawConsolePage(lines, top, status);

                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.Q || key.Key == ConsoleKey.Backspace ||
                    (key.Key == ConsoleKey.C && (key.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control))
                    return;
                if (key.Key == ConsoleKey.DownArrow || key.Key == ConsoleKey.PageDown || key.Key == ConsoleKey.Spacebar)
                    top = Math.Min(Math.Max(0, lines.Length - 1), top + pageSize);
                else if (key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.PageUp)
                    top = Math.Max(0, top - pageSize);
                else if (key.Key == ConsoleKey.Home)
                    top = 0;
                else if (key.Key == ConsoleKey.End)
                    top = Math.Max(0, lines.Length - pageSize);
                else if (IsModeKey(key, ConsoleKey.T))
                    ChangeAdvancedMode(ref mode, AdvancedViewMode.ReadableText, ref loadedBytes, ref top, file.Length);
                else if (IsModeKey(key, ConsoleKey.X))
                    ChangeAdvancedMode(ref mode, AdvancedViewMode.Hex, ref loadedBytes, ref top, file.Length);
                else if (IsModeKey(key, ConsoleKey.B))
                    ChangeAdvancedMode(ref mode, AdvancedViewMode.Binary, ref loadedBytes, ref top, file.Length);
                else if (IsModeKey(key, ConsoleKey.O))
                    ChangeAdvancedMode(ref mode, AdvancedViewMode.Octal, ref loadedBytes, ref top, file.Length);
                else if (key.Key == ConsoleKey.L && (key.Modifiers & ConsoleModifiers.Shift) == ConsoleModifiers.Shift)
                {
                    loadedBytes = Math.Min(file.Length, AdvancedLoadAllLimit);
                    top = 0;
                }
                else if (key.Key == ConsoleKey.L)
                {
                    loadedBytes = Math.Min(file.Length, loadedBytes + AdvancedChunkSize);
                    top = Math.Min(top, Math.Max(0, lines.Length - pageSize));
                }
            }
        }

        private static void ChangeAdvancedMode(ref AdvancedViewMode mode, AdvancedViewMode newMode, ref long loadedBytes, ref int top, long fileLength)
        {
            mode = newMode;
            loadedBytes = Math.Min(fileLength, Math.Max(loadedBytes, Math.Min(fileLength, AdvancedChunkSize)));
            top = 0;
        }

        private static bool IsModeKey(ConsoleKeyInfo key, ConsoleKey expected)
        {
            return key.Key == expected && ((key.Modifiers & ConsoleModifiers.Alt) == ConsoleModifiers.Alt || key.Modifiers == 0);
        }

        private static string BuildAdvancedDocument(string path, AdvancedViewMode mode, long loadedBytes)
        {
            var file = new FileInfo(path);
            var count = (int)Math.Min(Math.Min(file.Length, loadedBytes), int.MaxValue);
            var data = new byte[count];
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var offset = 0;
                while (offset < count)
                {
                    var read = fs.Read(data, offset, count - offset);
                    if (read == 0)
                        break;
                    offset += read;
                }
                if (offset != count)
                    Array.Resize(ref data, offset);
            }

            var sb = new StringBuilder();
            sb.AppendLine("FileDentify terminal advanced view");
            sb.AppendLine("==================================");
            sb.AppendLine("Path:");
            sb.AppendLine(path);
            sb.AppendLine("Mode:");
            sb.AppendLine(AdvancedFileViewRenderer.ModeName(mode));
            sb.AppendLine("Bytes rendered:");
            sb.AppendLine(FormatBytes(data.LongLength) + " of " + FormatBytes(file.Length));
            sb.AppendLine();
            sb.AppendLine(AdvancedFileViewRenderer.RenderChunk(data, 0, mode));
            return sb.ToString().TrimEnd();
        }

        private static int DrawConsolePage(string[] lines, int top, string status)
        {
            var height = Math.Max(5, Console.WindowHeight);
            var width = Math.Max(20, Console.WindowWidth);
            var pageSize = Math.Max(1, height - 1);
            Console.Clear();
            for (var i = 0; i < pageSize; i++)
            {
                if (top + i < lines.Length)
                    WriteClippedLine(lines[top + i], width);
                else
                    Console.WriteLine();
            }
            WriteStatus(status, width);
            return pageSize;
        }

        private static void WriteClippedLine(string line, int width)
        {
            line = line ?? string.Empty;
            if (line.Length > width - 1)
                line = line.Substring(0, width - 1);
            Console.WriteLine(line);
        }

        private static void WriteStatus(string status, int width)
        {
            status = status ?? string.Empty;
            if (status.Length > width - 1)
                status = status.Substring(0, width - 1);
            Console.Write(status.PadRight(Math.Max(0, width - 1)));
        }

        private static bool IsQuitKey(ConsoleKeyInfo key)
        {
            return key.Key == ConsoleKey.Escape ||
                key.Key == ConsoleKey.Q ||
                (key.Key == ConsoleKey.C && (key.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control);
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
            return unit == 0 ? bytes.ToString(CultureInfo.InvariantCulture) + " bytes" : value.ToString("0.##", CultureInfo.InvariantCulture) + " " + units[unit];
        }
    }
}
