using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LibFileDentify;

namespace FileDentify
{
    internal sealed class LibFileDentifyMatch
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Confidence { get; set; }
        public string DetectionBasis { get; set; }
        public string Version { get; set; }
        public bool ShouldSurface { get; set; }
    }

    internal static class LibFileDentifyBridge
    {
        private const string ResourceName = "FileDentify.Embedded.LibFileDentify.dll";
        private static bool initialized;
        private static readonly HashSet<string> CommonIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "windows.pe", "document.pdf", "archive.zip", "archive.rar", "archive.7z", "archive.xz", "archive.gzip",
            "image.png", "image.jpeg", "image.gif", "database.sqlite", "audio.midi", "container.riff", "container.iff",
            "executable.elf", "executable.macho", "windows.lnk"
        };

        public static void Initialize()
        {
            if (initialized) return;
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbeddedAssembly;
            initialized = true;
        }

        public static LibFileDentifyMatch Identify(string fileName, byte[] sample, long fileLength)
        {
            try
            {
                var result = FileTypeDatabase.Identify(fileName, sample, fileLength);
                if (result == null) return null;
                return new LibFileDentifyMatch
                {
                    Id = result.Id,
                    Name = result.Name,
                    Confidence = result.Confidence.ToString(),
                    DetectionBasis = FileTypeDatabase.DetectionBasis(result),
                    Version = FileTypeDatabase.Version,
                    ShouldSurface = result.Confidence != MatchConfidence.ExtensionHint && !CommonIds.Contains(result.Id)
                };
            }
            catch
            {
                // A reusable detector must never make the host application unable to inspect a file.
                return null;
            }
        }

        private static Assembly ResolveEmbeddedAssembly(object sender, ResolveEventArgs args)
        {
            var requested = new AssemblyName(args.Name);
            if (!string.Equals(requested.Name, "LibFileDentify", StringComparison.OrdinalIgnoreCase)) return null;
            var owner = Assembly.GetExecutingAssembly();
            using (var stream = owner.GetManifestResourceStream(ResourceName))
            {
                if (stream == null) return null;
                var bytes = new byte[stream.Length];
                var offset = 0;
                while (offset < bytes.Length)
                {
                    var read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read == 0) break;
                    offset += read;
                }
                if (offset != bytes.Length) Array.Resize(ref bytes, offset);
                return Assembly.Load(bytes);
            }
        }
    }
}
