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
{    internal static class SignatureMatcher
    {
        public static IEnumerable<ReportItem> Match(byte[] data, string path)
        {
            foreach (var sig in Signatures)
                if (MatchesAt(data, sig.Bytes, sig.Offset))
                    yield return new ReportItem { Title = sig.Name, Detail = sig.Description };

            if (data.Length >= 12 && StartsWith(data, Encoding.ASCII.GetBytes("RIFF")))
            {
                var fourcc = Encoding.ASCII.GetString(data, 8, 4);
                yield return new ReportItem { Title = "RIFF form", Detail = fourcc };
            }

            if (data.Length >= 32 && Ascii(data, 0, Math.Min(64, data.Length)).Contains("Roland SRX"))
                yield return new ReportItem { Title = "Roland SRX", Detail = "Roland SRX expansion ROM marker found at file start." };
            if (StartsWith(data, Encoding.ASCII.GetBytes("NESM\x1A")))
                yield return new ReportItem { Title = "NES Sound Format", Detail = "Nintendo Entertainment System music file" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("NES\x1A")))
                yield return new ReportItem { Title = "iNES ROM", Detail = "Nintendo Entertainment System ROM image" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("PACK")))
                yield return new ReportItem { Title = "Quake PACK", Detail = "Quake-style game package archive" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("IWAD")))
                yield return new ReportItem { Title = "Doom IWAD", Detail = "Doom internal WAD game data" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("PWAD")))
                yield return new ReportItem { Title = "Doom PWAD", Detail = "Doom patch WAD game data" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("MComprHD")))
                yield return new ReportItem { Title = "CHD disk image", Detail = "MAME Compressed Hunks of Data disk image" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("BKHD")))
                yield return new ReportItem { Title = "Wwise sound bank", Detail = "Audiokinetic Wwise BNK sound bank" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("RPA-")))
                yield return new ReportItem { Title = "Ren'Py archive", Detail = "Ren'Py game resource archive" };
            if (data.Length >= 0x104 && Ascii(data, 0x100, 4) == "SEGA")
                yield return new ReportItem { Title = "Sega ROM", Detail = "Sega Mega Drive/Genesis-style ROM header" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("BEGIN:IMELODY")))
                yield return new ReportItem { Title = "iMelody", Detail = "iMelody mobile ringtone text" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("MMMD")))
                yield return new ReportItem { Title = "SMAF/MMF", Detail = "Yamaha SMAF mobile audio/ringtone container" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("cmid")))
                yield return new ReportItem { Title = "CMX/PMD", Detail = "Qualcomm CMX/PMD mobile audio container" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("#!AMR")))
                yield return new ReportItem { Title = "AMR", Detail = "Adaptive Multi-Rate speech/audio" };

            var headText = Ascii(data, 0, Math.Min(512, data.Length)).TrimStart('\uFEFF', '.', ' ', '\t', '\r', '\n');
            if (data.Length >= 18 && headText.StartsWith("[InternetShortcut]", StringComparison.OrdinalIgnoreCase))
                yield return new ReportItem { Title = "Internet Shortcut", Detail = "Windows Internet shortcut (.url)" };
            else if (string.Equals(Path.GetExtension(path), ".url", StringComparison.OrdinalIgnoreCase) &&
                (headText.IndexOf("URL=", StringComparison.OrdinalIgnoreCase) >= 0 ||
                headText.IndexOf("BASEURL=", StringComparison.OrdinalIgnoreCase) >= 0 ||
                headText.IndexOf("ORIGURL=", StringComparison.OrdinalIgnoreCase) >= 0))
                yield return new ReportItem { Title = "Internet Shortcut", Detail = "Windows Internet shortcut or saved web favorite (.url)" };

            if (string.Equals(Path.GetExtension(path), ".ufs", StringComparison.OrdinalIgnoreCase))
                yield return new ReportItem { Title = "UFS extension", Detail = "Sample-library container using .ufs extension" };
            if (string.Equals(Path.GetExtension(path), ".blob", StringComparison.OrdinalIgnoreCase))
                yield return new ReportItem { Title = "Blob extension", Detail = "Binary blob asset or metadata container" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("CLIPDB1")))
                yield return new ReportItem { Title = "Clipman database", Detail = "Clipman CLIPDB1 compressed history database" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("CLIPDB2")))
                yield return new ReportItem { Title = "Clipman database", Detail = "Clipman CLIPDB2 encrypted history database" };
            if (string.Equals(Path.GetExtension(path), ".clipdb", StringComparison.OrdinalIgnoreCase))
                yield return new ReportItem { Title = "Clipman extension", Detail = "Clipman history database" };

            var mobileTone = MobileToneExtensionDescription(path);
            if (mobileTone != null)
                yield return new ReportItem { Title = "Mobile tone extension", Detail = mobileTone };

            var game = GameExtensionDescription(path);
            if (game != null)
                yield return new ReportItem { Title = "Game/ROM extension", Detail = game };

            var nativeInstruments = NativeInstrumentsExtensionDescription(path);
            if (nativeInstruments != null)
                yield return new ReportItem { Title = "Native Instruments extension", Detail = nativeInstruments };

            var steinberg = SteinbergExtensionDescription(path);
            if (steinberg != null)
                yield return new ReportItem { Title = "Steinberg extension", Detail = steinberg };
        }

        private static string GameExtensionDescription(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".nsf": return "NES Sound Format music";
                case ".nes": return "Nintendo Entertainment System ROM";
                case ".fds": return "Famicom Disk System disk image";
                case ".gb": return "Nintendo Game Boy ROM";
                case ".gbc": return "Nintendo Game Boy Color ROM";
                case ".gba": return "Nintendo Game Boy Advance ROM";
                case ".sfc":
                case ".smc": return "Super Nintendo / Super Famicom ROM";
                case ".gen":
                case ".md":
                case ".smd": return "Sega Mega Drive / Genesis ROM";
                case ".sms": return "Sega Master System ROM";
                case ".gg": return "Sega Game Gear ROM";
                case ".n64":
                case ".z64":
                case ".v64": return "Nintendo 64 ROM";
                case ".rom": return "Generic ROM image";
                case ".chd": return "MAME Compressed Hunks of Data disk image";
                case ".wad": return "Doom/WAD game data";
                case ".cue": return "CD cue sheet";
                case ".gdi": return "Dreamcast GDI disc layout";
                case ".sav": return "Game save data";
                case ".srm": return "SRAM game save data";
                case ".pak": return "Game/resource package";
                case ".vpk": return "Valve package";
                case ".pk3": return "Quake 3 package";
                case ".pk4": return "Doom 3 package";
                case ".bsp": return "BSP game map";
                case ".bnk": return "Wwise sound bank or game audio bank";
                case ".wem": return "Wwise encoded media";
                case ".assets":
                case ".ress":
                case ".resS": return "Unity asset/resource data";
                case ".rpa": return "Ren'Py archive";
                case ".rpyc": return "Ren'Py compiled script";
                case ".acf": return "Steam app manifest";
                case ".vdf": return "Valve Data Format text";
                case ".ips": return "IPS binary patch";
                case ".bps": return "BPS binary patch";
                default: return null;
            }
        }

        private static string MobileToneExtensionDescription(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".imy": return "iMelody mobile ringtone";
                case ".mmf": return "Yamaha SMAF/MMF mobile audio or ringtone";
                case ".pmd": return "Qualcomm CMX/PMD mobile audio";
                case ".amr": return "Adaptive Multi-Rate mobile speech/audio";
                case ".ota": return "Nokia OTA ringtone or operator-logo data";
                case ".rtttl": return "RTTTL ringtone text";
                case ".rtx": return "Nokia RTX ringtone text";
                default: return null;
            }
        }

        private static string SteinbergExtensionDescription(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".all": return "Classic Steinberg Cubase song/project file";
                case ".arr": return "Classic Steinberg Cubase arrangement file";
                case ".cpr": return "Steinberg Cubase project file";
                case ".npr": return "Steinberg Nuendo project file";
                case ".prt": return "Steinberg Cubase part file";
                case ".fxb": return "VST effect/instrument bank";
                case ".fxp": return "VST effect/instrument preset";
                case ".vstpreset": return "Steinberg VST preset";
                case ".drm": return "Cubase drum map";
                case ".srf": return "Steinberg resource or settings file";
                default: return null;
            }
        }

        private static string NativeInstrumentsExtensionDescription(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".nki": return "Kontakt instrument";
                case ".nkm": return "Kontakt multi";
                case ".nkb": return "Kontakt bank";
                case ".nkr": return "Kontakt resource container";
                case ".nkx": return "Kontakt sample/library container";
                case ".nkc": return "Kontakt cache/index";
                case ".ncw": return "Kontakt compressed wave sample";
                case ".nicnt": return "Kontakt library metadata";
                case ".nksf": return "Native Kontrol Standard preset";
                case ".nksfx": return "Native Kontrol Standard effect preset";
                case ".nksn": return "Native Kontrol Standard snapshot";
                case ".ens": return "Reaktor ensemble";
                case ".ism": return "Reaktor instrument or structure";
                case ".mdl": return "Reaktor module";
                case ".rcc": return "Reaktor core cell";
                case ".kt3": return "Battery kit";
                case ".nbkt": return "Battery kit";
                case ".ksd": return "Kore/FM8/Massive/Absynth sound preset";
                case ".nfm8": return "FM8 sound preset";
                case ".nabs": return "Absynth sound preset";
                case ".nmsv": return "Massive sound preset";
                case ".nrkt": return "Reaktor/Kontour preset";
                case ".mxprj": return "Maschine project";
                case ".mxgrp": return "Maschine group";
                case ".mxsnd": return "Maschine sound";
                default: return null;
            }
        }

        private static readonly Signature[] Signatures =
        {
            new Signature("%PDF-", "PDF header", "PDF document"),
            new Signature("PK\x03\x04", "ZIP local file header", "ZIP-compatible container"),
            new Signature("PK\x05\x06", "ZIP end-of-central-directory header", "Empty ZIP-compatible container"),
            new Signature("Rar!\x1A\x07\x01\x00", "RAR 5 header", "RAR 5 archive"),
            new Signature("Rar!\x1A\x07\x00", "RAR 4 header", "RAR archive"),
            new Signature("7z\xBC\xAF\x27\x1C", "7-Zip header", "7-Zip archive"),
            new Signature("\x1F\x8B", "gzip header", "gzip compressed data"),
            new Signature("\x89PNG\r\n\x1A\n", "PNG header", "PNG image"),
            new Signature("\xFF\xD8\xFF", "JPEG header", "JPEG image"),
            new Signature("GIF87a", "GIF87a header", "GIF image"),
            new Signature("GIF89a", "GIF89a header", "GIF image"),
            new Signature("RIFF", "RIFF header", "RIFF container"),
            new Signature("OggS", "OggS header", "Ogg media container"),
            new Signature("fLaC", "FLAC marker", "FLAC audio"),
            new Signature("ID3", "ID3 tag", "MP3 audio with ID3 tag"),
            new Signature("MThd", "MIDI header", "Standard MIDI file"),
            new Signature("MZ", "MZ executable header", "Windows executable or DLL"),
            new Signature("\x7FELF", "ELF header", "ELF executable"),
            new Signature("SQLite format 3\0", "SQLite header", "SQLite database"),
            new Signature("FORM", "FORM header", "IFF/AIFF-style container"),
            new Signature("BM", "BMP header", "BMP image"),
            new Signature("BZh", "bzip2 header", "bzip2 compressed data"),
            new Signature("\xFD" + "7zXZ\0", "XZ header", "XZ compressed data"),
            new Signature("\x04\x22\x4D\x18", "LZ4 frame header", "LZ4 frame compressed data"),
            new Signature("\x28\xB5\x2F\xFD", "Zstandard header", "Zstandard compressed data"),
            new Signature("mozLz40\0", "Mozilla LZ4 header", "Firefox/Thunderbird LZ4-compressed profile data"),
            new Signature("xar!", "XAR header", "XAR archive, often used by Apple installer packages"),
            new Signature("ustar", "TAR ustar marker", "TAR archive", 257),
            new Signature("Cr24", "Chrome extension header", "Chrome extension package"),
            new Signature("MSCF", "CAB header", "Microsoft Cabinet archive"),
            new Signature("ITSF", "CHM header", "Compiled HTML help file"),
            new Signature("\xD0\xCF\x11\xE0\xA1\xB1\x1A\xE1", "OLE compound header", "OLE compound document / Microsoft structured storage"),
            new Signature("\0\x01\0\0", "TrueType sfnt header", "TrueType font"),
            new Signature("OTTO", "OpenType CFF header", "OpenType CFF font"),
            new Signature("wOFF", "WOFF header", "WOFF web font"),
            new Signature("wOF2", "WOFF2 header", "WOFF2 web font"),
            new Signature("KDMV", "VMware sparse disk header", "VMware sparse virtual disk"),
            new Signature("vhdxfile", "VHDX header", "Hyper-V VHDX virtual disk"),
            new Signature("QFI\xFB", "QCOW2 header", "QEMU QCOW2 virtual disk"),
            new Signature("CD001", "ISO 9660 descriptor", "ISO 9660 optical disc image", 0x8001),
            new Signature("FLV", "FLV header", "Flash video"),
            new Signature("DICM", "DICOM marker", "DICOM medical image", 128),
            new Signature("NES\x1A", "iNES header", "Nintendo NES ROM image"),
            new Signature("ftyp", "ISO BMFF ftyp box", "MP4/QuickTime/ISO base media file", 4),
            new Signature(new byte[] { 0x4C, 0x00, 0x00, 0x00, 0x01, 0x14, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46 }, "Shell Link header", "Windows shortcut (.lnk)")
        };

        private static bool StartsWith(byte[] data, byte[] signature)
        {
            if (data.Length < signature.Length)
                return false;
            for (var i = 0; i < signature.Length; i++)
                if (data[i] != signature[i])
                    return false;
            return true;
        }

        private static bool MatchesAt(byte[] data, byte[] signature, int offset)
        {
            if (offset < 0 || data.Length < offset + signature.Length)
                return false;
            for (var i = 0; i < signature.Length; i++)
                if (data[offset + i] != signature[i])
                    return false;
            return true;
        }

        private static string Ascii(byte[] data, int offset, int count)
        {
            var sb = new StringBuilder();
            for (var i = offset; i < offset + count && i < data.Length; i++)
                sb.Append(data[i] >= 32 && data[i] < 127 ? (char)data[i] : '.');
            return sb.ToString();
        }

        private sealed class Signature
        {
            public readonly byte[] Bytes;
            public readonly string Name;
            public readonly string Description;
            public readonly int Offset;

            public Signature(string bytes, string name, string description, int offset = 0)
            {
                Bytes = Encoding.GetEncoding(28591).GetBytes(bytes);
                Name = name;
                Description = description;
                Offset = offset;
            }

            public Signature(byte[] bytes, string name, string description, int offset = 0)
            {
                Bytes = bytes;
                Name = name;
                Description = description;
                Offset = offset;
            }
        }
    }

}

