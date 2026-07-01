using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static void AddSafetyHintInfo(List<ReportSection> sections, string path, byte[] header)
        {
            var expectation = DetectHeaderExpectation(header);
            if (expectation == null)
                return;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (expectation.AcceptedExtensions.Contains(ext))
                return;
            if (ext == ".fon" && WindowsSystemTypeName(path, header) == "Windows bitmap font library")
                return;
            if (SpeechVoiceTypeName(path, header) != null)
                return;

            var section = AddSection(sections, "Safety hints");
            Add(section, "Header and extension mismatch", "The first bytes look like " + expectation.Description + ", but the filename extension is " + (string.IsNullOrEmpty(ext) ? "(none)" : ext) + ".");
            Add(section, "Expected extension(s)", string.Join(", ", expectation.AcceptedExtensions.ToArray()));
            if (IsExecutableLookingExtension(ext))
                Add(section, "Executable-looking extension", "This extension can be used to run code or launch a command on Windows, but the header looks like a different file family.");
            Add(section, "Recommendation", "Treat unexpected mismatches cautiously. Scan the file with trusted security tools and confirm the source before opening it directly.");
        }

        private static HeaderExpectation DetectHeaderExpectation(byte[] header)
        {
            if (StartsWith(header, Encoding.ASCII.GetBytes("%PDF-")))
                return HeaderExpectation.For("a PDF document", ".pdf");
            if (StartsWith(header, Encoding.ASCII.GetBytes("MZ")))
                return HeaderExpectation.For("a Windows executable or DLL", ".exe", ".dll", ".scr", ".sys", ".ocx", ".cpl", ".drv");
            if (StartsWith(header, Encoding.ASCII.GetBytes("PK\x03\x04")) || StartsWith(header, Encoding.ASCII.GetBytes("PK\x05\x06")) || StartsWith(header, Encoding.ASCII.GetBytes("PK\x07\x08")))
                return HeaderExpectation.For("a ZIP-compatible container", ".zip", ".docx", ".xlsx", ".pptx", ".jar", ".apk", ".ipa", ".ipsw", ".epub", ".ablbundle", ".nvda-addon", ".nupkg", ".ckpt", ".pt", ".pth", ".appx", ".appxbundle", ".msix", ".msixbundle");
            if (StartsWith(header, Encoding.ASCII.GetBytes("Rar!\x1A\x07\x00")) || StartsWith(header, Encoding.ASCII.GetBytes("Rar!\x1A\x07\x01\x00")))
                return HeaderExpectation.For("a RAR archive", ".rar");
            if (StartsWith(header, Encoding.GetEncoding(28591).GetBytes("7z\xBC\xAF\x27\x1C")))
                return HeaderExpectation.For("a 7-Zip archive", ".7z");
            if (StartsWith(header, Encoding.GetEncoding(28591).GetBytes("\x89PNG\r\n\x1A\n")))
                return HeaderExpectation.For("a PNG image", ".png");
            if (StartsWith(header, new byte[] { 0xFF, 0xD8, 0xFF }))
                return HeaderExpectation.For("a JPEG image", ".jpg", ".jpeg", ".jpe");
            if (StartsWith(header, Encoding.ASCII.GetBytes("GIF87a")) || StartsWith(header, Encoding.ASCII.GetBytes("GIF89a")))
                return HeaderExpectation.For("a GIF image", ".gif");
            if (StartsWith(header, Encoding.ASCII.GetBytes("SQLite format 3\0")))
                return HeaderExpectation.For("a SQLite database", ".sqlite", ".sqlite3", ".db");
            if (IsOleCompoundFile(header))
                return HeaderExpectation.For("an OLE compound document", ".doc", ".xls", ".ppt", ".msi", ".msg", ".vsd");
            if (IsWindowsShortcut(header))
                return HeaderExpectation.For("a Windows shortcut", ".lnk");
            return null;
        }

        private static bool IsExecutableLookingExtension(string ext)
        {
            switch (ext)
            {
                case ".exe":
                case ".scr":
                case ".com":
                case ".bat":
                case ".cmd":
                case ".ps1":
                case ".js":
                case ".jse":
                case ".vbs":
                case ".vbe":
                case ".msi":
                case ".lnk":
                    return true;
                default:
                    return false;
            }
        }

        private sealed class HeaderExpectation
        {
            public string Description { get; private set; }
            public HashSet<string> AcceptedExtensions { get; private set; }

            public static HeaderExpectation For(string description, params string[] extensions)
            {
                return new HeaderExpectation
                {
                    Description = description,
                    AcceptedExtensions = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase)
                };
            }
        }
    }
}
