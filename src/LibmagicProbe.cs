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
{    internal sealed class FoundString
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

    internal sealed class ThirdPartyNotice
    {
        public ThirdPartyNotice(string title, string text)
        {
            Title = title;
            Text = text;
        }

        public string Title { get; private set; }
        public string Text { get; private set; }

        public override string ToString()
        {
            return Title;
        }
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
                    Engine = "Embedded file/libmagic 5.48 MSYS2 MinGW-w64 build, using embedded magic.mgc"
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
            sb.AppendLine("Embedded runtime: MSYS2 MinGW-w64 file/libmagic 5.48 package and required runtime DLLs.");
            foreach (var notice in NoticeEntries())
            {
                sb.AppendLine();
                sb.AppendLine(notice.Title);
                sb.AppendLine(new string('-', notice.Title.Length));
                sb.AppendLine(notice.Text);
            }
            return sb.ToString().TrimEnd();
        }

        public static ThirdPartyNotice[] NoticeEntries()
        {
            return new[]
            {
                new ThirdPartyNotice("Overview", "FileDentify embeds the Unix file/libmagic engine for broad file identification." + Environment.NewLine + "Embedded runtime: MSYS2 MinGW-w64 file/libmagic 5.48 package and required runtime DLLs."),
                new ThirdPartyNotice("file/libmagic notice", ReadResourceText("FileDentify.Embedded.COPYING.file")),
                new ThirdPartyNotice("libsystre notice", ReadResourceText("FileDentify.Embedded.COPYING.libsystre")),
                new ThirdPartyNotice("libtre notice", ReadResourceText("FileDentify.Embedded.COPYING.libtre")),
                new ThirdPartyNotice("gettext runtime notice", ReadResourceText("FileDentify.Embedded.COPYING.gettext-runtime")),
                new ThirdPartyNotice("libintl notice", ReadResourceText("FileDentify.Embedded.COPYING.libintl")),
                new ThirdPartyNotice("libiconv GPL notice", ReadResourceText("FileDentify.Embedded.COPYING.libiconv-gpl")),
                new ThirdPartyNotice("libiconv LGPL notice", ReadResourceText("FileDentify.Embedded.COPYING.libiconv-lgpl")),
                ScreenReaderOutput.NoticeEntry()
            };
        }

        private static string EnsureExtracted()
        {
            lock (Sync)
            {
                if (!string.IsNullOrEmpty(extractedDirectory) && Directory.Exists(extractedDirectory))
                    return extractedDirectory;

                var dir = TemporaryFileService.GetProcessDirectory("libmagic");
                Directory.CreateDirectory(dir);
                ExtractResource("FileDentify.Embedded.file.exe", Path.Combine(dir, "file.exe"));
                ExtractResource("FileDentify.Embedded.libmagic-1.dll", Path.Combine(dir, "libmagic-1.dll"));
                ExtractResource("FileDentify.Embedded.libsystre-0.dll", Path.Combine(dir, "libsystre-0.dll"));
                ExtractResource("FileDentify.Embedded.libtre-5.dll", Path.Combine(dir, "libtre-5.dll"));
                ExtractResource("FileDentify.Embedded.libintl-8.dll", Path.Combine(dir, "libintl-8.dll"));
                ExtractResource("FileDentify.Embedded.libiconv-2.dll", Path.Combine(dir, "libiconv-2.dll"));
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

}

