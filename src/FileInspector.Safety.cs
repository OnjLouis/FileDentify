using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static void AddSafetyHintInfo(List<ReportSection> sections, string path, byte[] header, LibFileDentifyMatch libraryMatch)
        {
            if (AddLibraryMatchSafetyHint(sections, path, libraryMatch))
                return;

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
            Add(section, "Mismatch", "Detected content: " + expectation.Description + ". Filename extension: " + (string.IsNullOrEmpty(ext) ? "none" : ext) + ". Expected: " + string.Join(", ", expectation.AcceptedExtensions.OrderBy(value => value).ToArray()) + ".");
            if (IsExecutableLookingExtension(ext))
                Add(section, "Risk", "The filename extension can launch code or a command on Windows, while the detected content belongs to a different file family.");
            AddNote(section, "Advice", "FileDentify identifies file structure, not whether a file is harmless. Because the filename does not match the content, verify the source and scan it with trusted security software before opening it.");
        }

        private static bool AddLibraryMatchSafetyHint(List<ReportSection> sections, string path, LibFileDentifyMatch match)
        {
            if (match == null || !string.Equals(match.Confidence, "High", StringComparison.OrdinalIgnoreCase))
                return false;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            var expected = new HashSet<string>(match.ExpectedExtensions ?? new string[0], StringComparer.OrdinalIgnoreCase);
            var hasExpectedExtensions = expected.Count > 0;
            var extensionMatches = hasExpectedExtensions && expected.Contains(ext);
            var extensionMissing = string.IsNullOrEmpty(ext);
            if (extensionMatches || extensionMissing && !hasExpectedExtensions)
                return false;

            var section = AddSection(sections, "Safety hints");
            var expectedText = hasExpectedExtensions ? string.Join(", ", expected.OrderBy(value => value).ToArray()) : "not specified";
            Add(section, "Mismatch", "Detected content: " + match.Name + ". Filename extension: " + (extensionMissing ? "none" : ext) + ". Expected: " + expectedText + ". The content match does not depend on the filename.");
            if (IsExecutableOrActiveContent(match))
                Add(section, "Risk", "The detected content may contain executable code, scripts, shortcuts, firmware, or security-sensitive material. Do not run or install it merely because its filename appears harmless.");
            else if (IsExecutableLookingExtension(ext))
                Add(section, "Risk", "The filename extension can launch code or a command on Windows, while the detected content belongs to a different file family.");
            AddNote(section, "Advice", "FileDentify identifies file structure, not whether a file is harmless. Because the filename does not match the content, verify the source and scan it with trusted security software before opening, running, importing, or installing it.");
            return true;
        }

        private static bool IsExecutableOrActiveContent(LibFileDentifyMatch match)
        {
            if (match == null)
                return false;
            var category = match.Category ?? string.Empty;
            return category.IndexOf("Executable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                category.IndexOf("Automation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                category.IndexOf("Firmware", StringComparison.OrdinalIgnoreCase) >= 0 ||
                category.IndexOf("Security", StringComparison.OrdinalIgnoreCase) >= 0 ||
                string.Equals(match.Id, "windows.lnk", StringComparison.OrdinalIgnoreCase);
        }

        private static HeaderExpectation DetectHeaderExpectation(byte[] header)
        {
            if (StartsWith(header, Encoding.ASCII.GetBytes("%PDF-")))
                return HeaderExpectation.For("a PDF document", ".pdf");
            if (StartsWith(header, Encoding.ASCII.GetBytes("MZ")))
                return HeaderExpectation.For("a Windows executable or DLL", ".exe", ".dll", ".scr", ".sys", ".ocx", ".cpl", ".drv", ".efi", ".mui", ".mun", ".acm", ".ax", ".pyd", ".node", ".w5s", ".rock");
            if (StartsWith(header, Encoding.ASCII.GetBytes("PK\x03\x04")) || StartsWith(header, Encoding.ASCII.GetBytes("PK\x05\x06")) || StartsWith(header, Encoding.ASCII.GetBytes("PK\x07\x08")))
                return HeaderExpectation.For("a ZIP-compatible container", ".zip", ".docx", ".xlsx", ".pptx", ".jar", ".apk", ".ipa", ".ipsw", ".epub", ".ablbundle", ".nvda-addon", ".nupkg", ".npz", ".ckpt", ".pt", ".pth", ".appx", ".appxbundle", ".msix", ".msixbundle");
            if (StartsWith(header, Encoding.ASCII.GetBytes("Rar!\x1A\x07\x00")) || StartsWith(header, Encoding.ASCII.GetBytes("Rar!\x1A\x07\x01\x00")))
                return HeaderExpectation.For("a RAR archive", ".rar");
            if (StartsWith(header, Encoding.GetEncoding(28591).GetBytes("7z\xBC\xAF\x27\x1C")))
                return HeaderExpectation.For("a 7-Zip archive", ".7z");
            if (StartsWith(header, Encoding.GetEncoding(28591).GetBytes("\x89PNG\r\n\x1A\n")))
                return HeaderExpectation.For("a PNG image", ".png");
            if (StartsWith(header, new byte[] { 0xFF, 0xD8, 0xFF }))
                return HeaderExpectation.For("a JPEG image", ".jpg", ".jpeg", ".jpe", ".thm");
            if (StartsWith(header, Encoding.ASCII.GetBytes("GIF87a")) || StartsWith(header, Encoding.ASCII.GetBytes("GIF89a")))
                return HeaderExpectation.For("a GIF image", ".gif");
            if (StartsWith(header, Encoding.ASCII.GetBytes("SQLite format 3\0")))
                return HeaderExpectation.For("a SQLite database", ".sqlite", ".sqlite3", ".db", ".db3");
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
