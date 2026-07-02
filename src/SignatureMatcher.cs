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

            if (data.Length >= 8 && data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x00 && data[3] == 0x01 && data[4] == (byte)'B' && data[5] == (byte)'u' && data[6] == (byte)'d' && data[7] == (byte)'1')
                yield return new ReportItem { Title = "Apple Finder metadata", Detail = "macOS .DS_Store folder metadata" };
            if (data.Length >= 6 && data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x01 && data[3] == 0x00)
                yield return new ReportItem { Title = "Windows icon", Detail = "Windows ICO icon resource" };
            if (data.Length >= 6 && data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x02 && data[3] == 0x00)
                yield return new ReportItem { Title = "Windows cursor", Detail = "Windows CUR cursor resource" };
            if (data.Length >= 0x40 && StartsWith(data, Encoding.ASCII.GetBytes("MZ")))
            {
                var neOffset = (int)ReadUInt32LittleEndian(data, 0x3C);
                if (neOffset >= 0 && neOffset + 2 <= data.Length && data[neOffset] == (byte)'N' && data[neOffset + 1] == (byte)'E')
                    yield return new ReportItem { Title = "Windows NE executable", Detail = "Windows New Executable resource/application file" };
            }

            if (data.Length >= 32 && Ascii(data, 0, Math.Min(64, data.Length)).Contains("Roland SRX"))
                yield return new ReportItem { Title = "Roland SRX", Detail = "Roland SRX expansion ROM marker found at file start." };
            if (data.Length >= 4 && BitConverter.ToUInt32(data, 0) == 0x10201A7A)
                yield return new ReportItem { Title = "Symbian SIS", Detail = "Symbian installation package UID" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("_PT_")))
                yield return new ReportItem { Title = "Firmware marker", Detail = "PC BIOS/UEFI firmware image marker" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("ANDROID!")))
                yield return new ReportItem { Title = "Android boot image", Detail = "Android boot/recovery image header" };
            if (data.Length >= 4 && data[0] == 0x27 && data[1] == 0x05 && data[2] == 0x19 && data[3] == 0x56)
                yield return new ReportItem { Title = "U-Boot uImage", Detail = "U-Boot legacy image header" };
            if (data.Length >= 512 && data[0] == 0x55 && data[1] == 0x46 && data[2] == 0x32 && data[3] == 0x0A)
                yield return new ReportItem { Title = "UF2 firmware", Detail = "UF2 microcontroller firmware block" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("HDR0")))
                yield return new ReportItem { Title = "TRX firmware", Detail = "Broadcom/OpenWrt TRX firmware image" };
            if (data.Length >= 4 && data[0] == 0xD0 && data[1] == 0x0D && data[2] == 0xFE && data[3] == 0xED)
                yield return new ReportItem { Title = "Device Tree Blob", Detail = "Flattened Device Tree binary" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("ElfFile\0")))
                yield return new ReportItem { Title = "Windows Event Log", Detail = "EVTX event log" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("SZDD")))
                yield return new ReportItem { Title = "Windows compressed setup file", Detail = "SZDD single-file setup compression" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("$SDI0001")))
                yield return new ReportItem { Title = "Windows SDI", Detail = "Windows boot SDI ramdisk image" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("aLuZ")))
                yield return new ReportItem { Title = "InstallShield script", Detail = "InstallShield compiled setup script" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("ISc(")))
                yield return new ReportItem { Title = "InstallShield header", Detail = "InstallShield setup data/header file" };
            if (data.Length >= 12 && Encoding.ASCII.GetString(data, 8, 4) == "zdbf")
                yield return new ReportItem { Title = "Windows SDB", Detail = "Windows application compatibility database" };
            if (data.Length >= 4 &&
                ((data[0] == 0xDE && data[1] == 0x12 && data[2] == 0x04 && data[3] == 0x95) ||
                 (data[0] == 0x95 && data[1] == 0x04 && data[2] == 0x12 && data[3] == 0xDE)))
                yield return new ReportItem { Title = "gettext MO", Detail = "GNU gettext compiled message catalog" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("RSVQ")))
                yield return new ReportItem { Title = "Roland SVQ", Detail = "Roland sequencer song" };
            if (data.Length >= 6 && data[2] == (byte)'S' && data[3] == (byte)'V' && data[4] == (byte)'D' && data[5] == (byte)'1')
                yield return new ReportItem { Title = "Roland SVD", Detail = "Roland sound/backup data" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("RFWV")))
                yield return new ReportItem { Title = "Roland sample", Detail = "Roland FA sample waveform data" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("FantomXLibrarianFile")))
                yield return new ReportItem { Title = "Roland Fantom Librarian", Detail = "Roland Fantom-X Librarian patch/library data" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("FantomSLibrarianFile")))
                yield return new ReportItem { Title = "Roland Fantom Librarian", Detail = "Roland Fantom-S Librarian patch/library data" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("UTG VPRM")))
                yield return new ReportItem { Title = "Yamaha S-YXG table", Detail = "Yamaha S-YXG software-synthesizer voice/program table" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("vawt")))
                yield return new ReportItem { Title = "Surge wavetable", Detail = "Surge wavetable file" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("MinPHR")))
                yield return new ReportItem { Title = "OpenAL Soft HRTF", Detail = "OpenAL Soft minimum-phase HRTF data" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("CcnK")))
                yield return new ReportItem { Title = "VST preset chunk", Detail = "VST FXP/FXB preset or bank" };
            if (data.Length >= 12 && StartsWith(data, Encoding.ASCII.GetBytes("FORM")) && Encoding.ASCII.GetString(data, 8, 4) == "PTCH")
                yield return new ReportItem { Title = "Reason NN-XT patch", Detail = "Reason NN-XT sampler patch" };
            if (data.Length >= 12 && StartsWith(data, Encoding.ASCII.GetBytes("RIFF")))
            {
                var musicForm = Encoding.ASCII.GetString(data, 8, 4);
                if (musicForm == "APRG")
                    yield return new ReportItem { Title = "Akai sampler program", Detail = "Akai RIFF APRG program data" };
                else if (musicForm == "AMUL")
                    yield return new ReportItem { Title = "Akai sampler multi", Detail = "Akai RIFF AMUL multi data" };
                else if (musicForm == "SEKD")
                    yield return new ReportItem { Title = "MAGIX/SEK'D", Detail = "MAGIX/SEK'D RIFF metadata" };
            }
            if (data.Length >= 12 && StartsWith(data, Encoding.ASCII.GetBytes("CAT ")) && Encoding.ASCII.GetString(data, 8, 4) == "PRBM")
                yield return new ReportItem { Title = "ReBirth mod", Detail = "Propellerhead ReBirth mod/package" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("gchT")))
                yield return new ReportItem { Title = "GarageBand chord table", Detail = "GarageBand chord/tuning table" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("GNOA")))
                yield return new ReportItem { Title = "GN Audio", Detail = "GN Audio container, often with embedded MIDI" };
            if (Ascii(data, 0, Math.Min(data.Length, 64)).IndexOf("Barrcode AA", StringComparison.OrdinalIgnoreCase) >= 0)
                yield return new ReportItem { Title = "Barrcode AA", Detail = "Barrcode broadcast playout audio resource marker" };
            if (data.Length >= 4 && data[0] == 0xff && (data[1] & 0xe0) == 0xe0)
                yield return new ReportItem { Title = "MPEG audio frame", Detail = "MPEG audio stream frame" };
            if (Ascii(data, 0, Math.Min(4096, data.Length)).IndexOf("\"Format\":\"FileDentify report\"", StringComparison.OrdinalIgnoreCase) >= 0)
                yield return new ReportItem { Title = "FileDentify report", Detail = "Native FileDentify saved report" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("ITOLITLS")))
                yield return new ReportItem { Title = "Microsoft Reader LIT", Detail = "Legacy Microsoft Reader ebook" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("ITSF")))
                yield return new ReportItem { Title = "CHM help", Detail = "Compiled HTML Help file" };
            if (data.Length > 0 && data[0] == 0xF0)
                yield return new ReportItem { Title = "MIDI SysEx", Detail = "MIDI System Exclusive data" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("NESM\x1A")))
                yield return new ReportItem { Title = "NES Sound Format", Detail = "Nintendo Entertainment System music file" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("UEF File!")))
                yield return new ReportItem { Title = "UEF", Detail = "BBC Micro UEF tape/state file" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("Vgm ")))
                yield return new ReportItem { Title = "VGM", Detail = "Video Game Music chiptune log" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("PSID")))
                yield return new ReportItem { Title = "PSID", Detail = "PlaySID Commodore 64 SID music" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("RSID")))
                yield return new ReportItem { Title = "RSID", Detail = "Real C64 SID music" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("S98")))
                yield return new ReportItem { Title = "S98", Detail = "S98 chiptune sound log" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("GF1PATCH110")))
                yield return new ReportItem { Title = "Gravis UltraSound patch", Detail = "GF1 instrument patch/sample file" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("XMF_")))
                yield return new ReportItem { Title = "XMF", Detail = "Extensible Music Format container" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("melo")))
                yield return new ReportItem { Title = "MFi/MLD", Detail = "MFi mobile melody container" };
            if (data.Length >= 12 && StartsWith(data, Encoding.ASCII.GetBytes("RIFF")) && Encoding.ASCII.GetString(data, 8, 4) == "QLCM")
                yield return new ReportItem { Title = "QCP", Detail = "Qualcomm PureVoice / QCELP audio" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("PSF$")))
                yield return new ReportItem { Title = "mini2SF", Detail = "Nintendo DS 2SF music stub" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("IREZ")))
                yield return new ReportItem { Title = "Beatnik RMF", Detail = "Beatnik Rich Music Format" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("QSEQ")))
                yield return new ReportItem { Title = "QSEQ", Detail = "QSEQ DOS MIDI sequencer song/project" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("RCM-PC98")))
                yield return new ReportItem { Title = "Recomposer RCP", Detail = "COME ON MUSIC Recomposer sequence" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("COME ON MUSIC RECOMPOSER")))
                yield return new ReportItem { Title = "Recomposer", Detail = "COME ON MUSIC Recomposer sequence/support file" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("MAKI")))
                yield return new ReportItem { Title = "MAG graphics", Detail = "Maki-chan / MAG graphics file used by old MIDI collections" };
            if (data.Length >= 12 && StartsWith(data, Encoding.ASCII.GetBytes("RIFF")) && Encoding.ASCII.GetString(data, 8, 4) == "SFIP")
                yield return new ReportItem { Title = "SFI/SFIP", Detail = "RIFF SFIP sampler or impulse-response data" };
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
            if (StartsWith(data, Encoding.ASCII.GetBytes("ECLW")))
                yield return new ReportItem { Title = "Creative ECW", Detail = "Creative/E-mu ECW wavetable bank" };
            if (data.Length >= 8 && StartsWith(data, new byte[] { 0x00, 0x61, 0x73, 0x6D }))
                yield return new ReportItem { Title = "WebAssembly", Detail = "WebAssembly binary module" };

            var headText = Ascii(data, 0, Math.Min(512, data.Length)).TrimStart('\uFEFF', '.', ' ', '\t', '\r', '\n');
            if (headText.IndexOf("BEGIN:VCARD", StringComparison.OrdinalIgnoreCase) >= 0)
                yield return new ReportItem { Title = "vCard", Detail = "vCard contact data" };
            if (headText.IndexOf("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase) >= 0)
                yield return new ReportItem { Title = "iCalendar", Detail = "iCalendar calendar/reminder data" };
            if (headText.IndexOf("<opml", StringComparison.OrdinalIgnoreCase) >= 0)
                yield return new ReportItem { Title = "OPML", Detail = "OPML outline/subscription list" };
            if (headText.IndexOf("<WMEncoder", StringComparison.OrdinalIgnoreCase) >= 0)
                yield return new ReportItem { Title = "Windows Media Encoder", Detail = "Windows Media Encoder session XML" };
            if (string.Equals(Path.GetExtension(path), ".wpl", StringComparison.OrdinalIgnoreCase) &&
                (headText.IndexOf("<?wpl", StringComparison.OrdinalIgnoreCase) >= 0 || headText.IndexOf("<smil", StringComparison.OrdinalIgnoreCase) >= 0))
                yield return new ReportItem { Title = "Windows Media playlist", Detail = "Windows Media Player playlist" };
            if (data.Length >= 18 && headText.StartsWith("[InternetShortcut]", StringComparison.OrdinalIgnoreCase))
                yield return new ReportItem { Title = "Internet Shortcut", Detail = "Windows Internet shortcut (.url)" };
            else if (string.Equals(Path.GetExtension(path), ".url", StringComparison.OrdinalIgnoreCase) &&
                (headText.IndexOf("URL=", StringComparison.OrdinalIgnoreCase) >= 0 ||
                headText.IndexOf("BASEURL=", StringComparison.OrdinalIgnoreCase) >= 0 ||
                headText.IndexOf("ORIGURL=", StringComparison.OrdinalIgnoreCase) >= 0))
                yield return new ReportItem { Title = "Internet Shortcut", Detail = "Windows Internet shortcut or saved web favorite (.url)" };

            if (string.Equals(Path.GetExtension(path), ".ufs", StringComparison.OrdinalIgnoreCase))
                yield return new ReportItem { Title = "UFS extension", Detail = "Sample-library container using .ufs extension" };
            if (string.Equals(Path.GetExtension(path), ".pat", StringComparison.OrdinalIgnoreCase) && LooksLikeSynologyPat(data))
                yield return new ReportItem { Title = "Synology PAT", Detail = "Synology DSM/SRM update package" };
            if (string.Equals(Path.GetExtension(path), ".blob", StringComparison.OrdinalIgnoreCase))
                yield return new ReportItem { Title = "Blob extension", Detail = "Binary blob asset or metadata container" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("CLIPDB1")))
                yield return new ReportItem { Title = "Clipman database", Detail = "Clipman CLIPDB1 compressed history database" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("CLIPDB2")))
                yield return new ReportItem { Title = "Clipman database", Detail = "Clipman CLIPDB2 encrypted history database" };
            if (string.Equals(Path.GetExtension(path), ".clipdb", StringComparison.OrdinalIgnoreCase))
                yield return new ReportItem { Title = "Clipman extension", Detail = "Clipman history database" };
            if (string.Equals(Path.GetExtension(path), ".sis", StringComparison.OrdinalIgnoreCase))
                yield return new ReportItem { Title = "Symbian extension", Detail = "Symbian installation package" };
            if (string.Equals(Path.GetExtension(path), ".sisx", StringComparison.OrdinalIgnoreCase))
                yield return new ReportItem { Title = "Symbian extension", Detail = "Symbian signed installation package" };
            var symbianResource = SymbianResourceExtensionDescription(path);
            if (symbianResource != null)
                yield return new ReportItem { Title = "Symbian resource extension", Detail = symbianResource };
            var midlet = JavaMidletExtensionDescription(path);
            if (midlet != null)
                yield return new ReportItem { Title = "Java MIDlet extension", Detail = midlet };
            if (string.Equals(Path.GetExtension(path), ".fdreport", StringComparison.OrdinalIgnoreCase))
                yield return new ReportItem { Title = "FileDentify report extension", Detail = "Native FileDentify saved report" };
            if (string.Equals(Path.GetExtension(path), ".vmg", StringComparison.OrdinalIgnoreCase))
                yield return new ReportItem { Title = "VMG extension", Detail = "Nokia/Symbian saved message" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("Spitfire")))
                yield return new ReportItem { Title = "Spitfire marker", Detail = "Spitfire Audio sample container or metadata" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("BOMStore")))
                yield return new ReportItem { Title = "Apple asset catalog", Detail = "Compiled Apple asset catalog" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("xar!")))
                yield return new ReportItem { Title = "XAR archive", Detail = "macOS installer/package archive" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("VEXP")))
                yield return new ReportItem { Title = "Roland Cloud expansion", Detail = "Roland Cloud VEXP expansion package" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("KoaBankFile")))
                yield return new ReportItem { Title = "Roland Cloud preset bank", Detail = "Roland Cloud KoaBankFile preset bank" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("<FileSystem>")))
                yield return new ReportItem { Title = "Embedded file-system index", Detail = "Readable FileSystem index, used by some sample-library containers" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("Korg")))
                yield return new ReportItem { Title = "Korg marker", Detail = "Korg sample-library or synthesizer data" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("WMMS")))
                yield return new ReportItem { Title = "Korg WaveMotion marker", Detail = "Korg WaveMotion sample/keymap data" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("WzooWzoo")))
                yield return new ReportItem { Title = "AIR Music Technology archive", Detail = "AIR Music Technology content archive" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("MSE ")))
                yield return new ReportItem { Title = "Maize Sampler marker", Detail = "Maize Sampler exported instrument" };
            if (data.Length >= 4 && data[0] == 0x1B && data[1] == (byte)'L' && data[2] == (byte)'u' && data[3] == (byte)'a')
                yield return new ReportItem { Title = "Lua bytecode", Detail = "Lua bytecode or Lua-based resource bundle" };
            if (StartsWith(data, Encoding.ASCII.GetBytes("GGUF")))
                yield return new ReportItem { Title = "GGUF model", Detail = "GGUF machine-learning model" };
            if (string.Equals(Path.GetExtension(path), ".asar", StringComparison.OrdinalIgnoreCase) && data.Length >= 24)
                yield return new ReportItem { Title = "Electron ASAR extension", Detail = "Electron application resource archive" };
            if (string.Equals(Path.GetExtension(path), ".nupkg", StringComparison.OrdinalIgnoreCase))
                yield return new ReportItem { Title = "NuGet extension", Detail = "NuGet package archive" };
            if (string.Equals(Path.GetExtension(path), ".ckpt", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".pt", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".pth", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".safetensors", StringComparison.OrdinalIgnoreCase))
                yield return new ReportItem { Title = "AI model extension", Detail = "Machine-learning model checkpoint or tensor file" };
            var vmware = VmwareMetadataExtensionDescription(path);
            if (vmware != null)
                yield return new ReportItem { Title = "VMware metadata extension", Detail = vmware };
            if (string.Equals(Path.GetExtension(path), ".qvcu", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".nuul216", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".clb", StringComparison.OrdinalIgnoreCase) && path.IndexOf("Acapela", StringComparison.OrdinalIgnoreCase) >= 0)
                yield return new ReportItem { Title = "Acapela voice extension", Detail = "Acapela speech voice data" };
            var speechVoice = SpeechVoiceExtensionDescription(path, data);
            if (speechVoice != null)
                yield return new ReportItem { Title = "Speech voice extension", Detail = speechVoice };
            if (data.Length > 196 && data[4] == 0x47 && data[196] == 0x47)
                yield return new ReportItem { Title = "Blu-ray transport stream", Detail = "MPEG-2 transport stream with 192-byte Blu-ray source packets" };
            if (data.Length > 188 && data[0] == 0x47 && data[188] == 0x47)
                yield return new ReportItem { Title = "MPEG transport stream", Detail = "MPEG-2 transport stream" };

            var mobileTone = MobileToneExtensionDescription(path);
            if (mobileTone != null)
                yield return new ReportItem { Title = "Mobile tone extension", Detail = mobileTone };

            var legacyMusic = LegacyMusicExtensionDescription(path);
            if (legacyMusic != null)
                yield return new ReportItem { Title = "Legacy music extension", Detail = legacyMusic };

            var game = GameExtensionDescription(path);
            if (game != null)
                yield return new ReportItem { Title = "Game/ROM extension", Detail = game };

            var ebook = EbookExtensionDescription(path);
            if (ebook != null)
                yield return new ReportItem { Title = "Ebook/help extension", Detail = ebook };

            var nativeInstruments = NativeInstrumentsExtensionDescription(path);
            if (nativeInstruments != null)
                yield return new ReportItem { Title = "Native Instruments extension", Detail = nativeInstruments };

            var steinberg = SteinbergExtensionDescription(path);
            if (steinberg != null)
                yield return new ReportItem { Title = "Steinberg extension", Detail = steinberg };

            var musicProject = MusicProjectExtensionDescription(path);
            if (musicProject != null)
                yield return new ReportItem { Title = "Music/project extension", Detail = musicProject };

            var sampleLibrary = SampleLibraryExtensionDescription(path);
            if (sampleLibrary != null)
                yield return new ReportItem { Title = "Sample-library extension", Detail = sampleLibrary };

            var developer = DeveloperExtensionDescription(path, data);
            if (developer != null)
                yield return new ReportItem { Title = "Programming/resource extension", Detail = developer };

            if (data.Length >= 7 && data[2] == (byte)'-' && data[6] == (byte)'-')
            {
                var method = Encoding.ASCII.GetString(data, 2, 5);
                if (method.StartsWith("-lh", StringComparison.OrdinalIgnoreCase) || method.StartsWith("-lz", StringComparison.OrdinalIgnoreCase))
                    yield return new ReportItem { Title = "LHA archive method", Detail = method };
            }

            if (data.Length >= 1084)
            {
                var mod = Encoding.ASCII.GetString(data, 1080, 4);
                if (mod == "M.K." || mod == "M!K!" || mod == "M&K!" || mod == "N.T." || mod == "FLT4" || mod == "FLT8")
                    yield return new ReportItem { Title = "Tracker module signature", Detail = "ProTracker/Amiga MOD marker " + mod };
            }
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
                case ".ssd": return "BBC Micro single-sided disk image";
                case ".uef": return "BBC Micro UEF tape/state file";
                case ".abm":
                case ".cbl":
                    return path.IndexOf("\\BeebEm\\", StringComparison.OrdinalIgnoreCase) >= 0 ? "BBC Micro / Torch MCP program image" : null;
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
                case ".bpak": return "Protected game package";
                case ".nvda-addon": return "NVDA add-on package";
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
                case ".qcp": return "Qualcomm PureVoice/QCELP mobile audio";
                case ".mld": return "MFi/MLD mobile melody";
                case ".mxmf": return "Mobile XMF music container";
                case ".amr": return "Adaptive Multi-Rate mobile speech/audio";
                case ".ota": return "Nokia OTA ringtone or operator-logo data";
                case ".rtttl": return "RTTTL ringtone text";
                case ".rtx": return "Nokia RTX ringtone text";
                default: return null;
            }
        }

        private static string LegacyMusicExtensionDescription(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".vgm": return "Video Game Music chiptune log";
                case ".vgz": return "gzip-compressed Video Game Music log";
                case ".sid": return "Commodore 64 SID music";
                case ".s98": return "S98 chiptune sound log";
                case ".rsn": return "SNES RSN/SPC music archive";
                case ".mini2sf": return "Nintendo DS mini2SF music stub";
                case ".xmf": return "Extensible Music Format container";
                case ".rmf": return "Beatnik Rich Music Format";
                case ".rcp": return "Recomposer RCP sequence";
                case ".hed": return "Recomposer header/metadata sidecar";
                case ".g36": return "Recomposer G36 sequence";
                case ".wrd": return "WRD MIDI lyric/graphics script";
                case ".mag": return "MAG graphics file used by old MIDI collections";
                case ".lyc": return "Roland/Karaoke lyric sidecar";
                case ".zel": return "Text music macro/source file";
                case ".gmc": return "Text music macro/source file";
                case ".qsq": return "QSEQ DOS MIDI sequencer song/project";
                case ".ovw": return "Cubase waveform overview sidecar";
                case ".sfk": return "Sound Forge waveform overview sidecar";
                case ".peak": return "Audio waveform peak sidecar";
                case ".sfi": return "Sampler or impulse-response SFI data";
                case ".sam": return "Raw sampler sample";
                case ".tun": return "Microtuning map file";
                default: return null;
            }
        }

        private static string EbookExtensionDescription(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".lit": return "Microsoft Reader LIT ebook";
                case ".mobi": return "Mobipocket/Kindle ebook";
                case ".prc": return "Palm/Mobipocket resource database";
                case ".pdb": return "Palm/eReader database";
                case ".lrf": return "Sony Reader LRF ebook";
                case ".brf": return "Braille Ready Format text";
                case ".cbr": return "Comic Book RAR archive";
                case ".cbz": return "Comic Book ZIP archive";
                case ".chm": return "Compiled HTML Help file";
                case ".hlp": return "WinHelp help file";
                case ".hhc": return "HTML Help table of contents";
                default: return null;
            }
        }

        private static string VmwareMetadataExtensionDescription(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".vmx": return "VMware virtual machine configuration";
                case ".vmsd": return "VMware snapshot metadata";
                case ".vmxf": return "VMware extended virtual machine metadata";
                case ".nvram": return "VMware virtual machine NVRAM";
                case ".scoreboard": return "VMware virtual machine runtime scoreboard";
                default: return null;
            }
        }

        private static string SymbianResourceExtensionDescription(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (Regex.IsMatch(ext, @"^\.[0-9]+x[0-9]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) &&
                (path.IndexOf("\\System\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 path.IndexOf("\\_PAlbTN\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 path.IndexOf("\\Images\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 path.IndexOf("\\Videos\\", StringComparison.OrdinalIgnoreCase) >= 0))
                return "Symbian gallery thumbnail image";

            switch (ext)
            {
                case ".app": return "Symbian OS application binary";
                case ".aif": return "Symbian application information file";
                case ".rsc": return "Symbian compiled resource file";
                case ".mbm": return "Symbian multi-bitmap image resource";
                case ".mif": return "Symbian icon/resource file";
                case ".mdl": return "Symbian recognizer or plug-in module";
                case ".mask": return path.IndexOf("\\System\\", StringComparison.OrdinalIgnoreCase) >= 0 ? "Symbian image mask sidecar" : null;
                default: return null;
            }
        }

        private static string JavaMidletExtensionDescription(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".jad": return "Java ME MIDlet application descriptor";
                case ".jar":
                    return path.IndexOf("\\MIDlets\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        path.IndexOf("\\MIDlet\\", StringComparison.OrdinalIgnoreCase) >= 0
                        ? "Java ME MIDlet archive"
                        : null;
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
                case ".nkp": return "Kontakt preset";
                case ".nka": return "Kontakt script array or sample-add data";
                case ".nkr": return "Kontakt resource container";
                case ".nkx": return "Kontakt sample/library container";
                case ".nkc": return "Kontakt cache/index";
                case ".ncw": return "Kontakt compressed wave sample";
                case ".nicnt": return "Kontakt library metadata";
                case ".nkl": return "Kontakt Leap kit";
                case ".nksf": return "Native Kontrol Standard preset";
                case ".nksfx": return "Native Kontrol Standard effect preset";
                case ".nksn": return "Kontakt snapshot";
                case ".nksr": return "Reaktor rack / NKS rack preset";
                case ".ens": return "Reaktor ensemble";
                case ".ism": return "Reaktor instrument or structure";
                case ".mdl": return "Reaktor module";
                case ".rcc": return "Reaktor core cell";
                case ".rkplr": return "Reaktor Player file";
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
                case ".mxfx": return "Maschine effect preset";
                case ".mxinst": return "Maschine instrument preset";
                case ".mprj": return "Maschine 1 project";
                case ".mgrp": return "Maschine 1 group";
                case ".msnd": return "Maschine 1 sound";
                case ".ngrr": return "Guitar Rig rack preset";
                case ".ndx": return "Native Instruments sample index/support data";
                default: return null;
            }
        }

        private static string MusicProjectExtensionDescription(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".ablbundle": return "Ableton Move/Live bundle";
                case ".abl": return "Ableton Move/Live song JSON";
                case ".ablpreset": return "Ableton preset";
                case ".rpp": return "REAPER project";
                case ".rpp-bak": return "REAPER project backup";
                case ".als": return "Ableton Live Set";
                case ".adg": return "Ableton device rack";
                case ".adv": return "Ableton device preset";
                case ".lha":
                case ".lzh": return "LHA/LZH archive";
                case ".mod": return "ProTracker/Amiga MOD tracker module";
                case ".xm": return "FastTracker XM tracker module";
                case ".s3m": return "Scream Tracker 3 module";
                case ".it": return "Impulse Tracker module";
                case ".mogg": return "MOGG multitrack Ogg audio";
                case ".sfz": return "SFZ sampler instrument";
                case ".exs": return "Apple Logic EXS sampler instrument";
                case ".dls": return "Downloadable Sounds instrument bank";
                case ".sbk": return "Sound Blaster / E-mu sound bank";
                case ".ecw": return "Creative/E-mu ECW wavetable bank";
                case ".sxt": return "Reason NN-XT sampler patch";
                case ".kit": return "Drum kit or sampler kit file";
                case ".bank": return "FMOD/Wwise/game audio bank";
                case ".syx": return "MIDI System Exclusive data";
                case ".jlw":
                case ".vop": return "Legacy JLW/VOP sound bank or voice data";
                case ".svd": return "Roland sound/backup data";
                case ".svq": return "Roland sequencer song";
                case ".fxl": return "Roland Fantom-X Librarian patch/library data";
                case ".fsl": return "Roland Fantom-S Librarian patch/library data";
                case ".tbl":
                    if (path.IndexOf("\\yamaha\\", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        (path.IndexOf("s-yxg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         path.IndexOf("syxg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         Path.GetFileName(path).StartsWith("sxg", StringComparison.OrdinalIgnoreCase)))
                        return "Yamaha S-YXG software-synthesizer table";
                    return null;
                case ".smp": return "Sampler or device sample data";
                case ".efe": return "Ensoniq EPS instrument file";
                case ".eda": return "Ensoniq ASR-10 disk image";
                case ".edt": return "Ensoniq TS disk image";
                case ".helm": return "Helm synthesizer preset";
                case ".wt": return "Wavetable data";
                case ".arta": return "Arturia sample payload";
                case ".astr": return "Arturia bitmap/UI resource";
                case ".eiiwav": return "Arturia Emulator II V sample audio";
                case ".roliaudio": return "ROLI Equator sample audio";
                case ".ignitex": return "Initial Audio Sektor sample data";
                case ".grir": return "Native Instruments Guitar Rig impulse response";
                case ".sdir": return "Apple Space Designer impulse response";
                case ".caf": return "Core Audio Format audio";
                case ".scl": return "Scala tuning scale";
                case ".nam": return "Neural Amp Modeler model";
                case ".mtdrum": return "Microtonic drum preset";
                case ".chords": return "Chord preset";
                case ".spitfire": return "Spitfire Audio sample container";
                case ".zmulti": return "Spitfire Audio multi/patch data";
                case ".zpreset": return "Spitfire Audio preset data";
                case ".zconfig": return "Spitfire Audio configuration data";
                case ".lm": return "Spitfire Audio licence or library metadata";
                case ".aupreset": return "Apple Audio Unit preset";
                case ".component": return "Apple Audio Unit plug-in bundle";
                case ".vst": return "Mac VST plug-in bundle";
                case ".vst3": return "VST3 plug-in bundle";
                case ".clap": return "CLAP plug-in bundle";
                case ".aaxplugin": return "Avid AAX plug-in bundle";
                case ".strings": return "Apple localization strings";
                case ".car": return "Apple compiled asset catalog";
                case ".nib": return "Apple Interface Builder nib";
                case ".mobileconfig": return "Apple configuration profile";
                case ".ipa": return "iOS application archive";
                case ".ipsw": return "Apple device firmware restore package";
                case ".pkg": return "macOS installer package";
                case ".crash": return "Apple crash report";
                case ".ips": return "Apple diagnostic report";
                case ".exz": return "Roland Cloud expansion package";
                case ".xpak": return "XLN Audio sample pack";
                case ".mlt_omn": return "Spectrasonics Omnisphere multi";
                case ".mlt_key": return "Spectrasonics Keyscape multi";
                case ".mlt_trl": return "Spectrasonics Trilian multi";
                case ".mlt_rmx": return "Spectrasonics Stylus RMX multi";
                case ".fxp_rmx": return "Spectrasonics Stylus RMX effect preset";
                case ".fxr_rmx": return "Spectrasonics Stylus RMX effect rack";
                case ".kit_rmx": return "Spectrasonics Stylus RMX kit";
                case ".prt_rmx": return "Spectrasonics Stylus RMX part";
                case ".ctb": return "liblouis braille contraction/translation table";
                case ".utb": return "liblouis Unicode braille translation table";
                case ".uti": return "liblouis include table";
                case ".cti": return "liblouis character/include table";
                case ".rock": return "Rockbox plug-in module";
                case ".milk": return "MilkDrop visualisation preset";
                case ".maki": return "Winamp Modern skin MAKI script";
                case ".avb": return "Microsoft Chat Comic Art avatar";
                case ".jgl": return "Roland Juno-G Librarian file";
                case ".mhr": return "OpenAL Soft HRTF data";
                case ".ambdec": return "Ambisonic decoder configuration";
                case ".m3u":
                case ".m3u8": return "M3U/M3U8 media playlist";
                case ".lso": return "Emagic Logic song/project";
                case ".chtr": return "GarageBand chord/tuning table";
                case ".nac": return "Native Instruments sample-add data";
                case ".nov": return "Native Instruments sample-add instrument data";
                case ".h0": return "MAGIX/SEK'D waveform overview";
                case ".hdp": return "MAGIX/SEK'D sample-library metadata";
                case ".ovm": return "MAGIX object/volume metadata";
                case ".akp": return "Akai sampler program";
                case ".akm": return "Akai sampler multi";
                case ".rbm": return "Propellerhead ReBirth mod";
                case ".mdd": return "GN Audio container with embedded MIDI";
                default: return null;
            }
        }

        private static string SampleLibraryExtensionDescription(string path)
        {
            var lowerPath = path.ToLowerInvariant();
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".wmss": return "Korg WaveMotion sample set";
                case ".adsr": return "Korg wavestate ADSR randomization data";
                case ".voiceamp": return "Korg wavestate voice-amp randomization data";
                case ".pitch": return "Korg wavestate pitch randomization data";
                case ".dynamicarpeggiator": return "Korg dynamic arpeggiator data";
                case ".classicvectoreg": return "Korg classic vector envelope data";
                case ".cpt2": return "GForce M-Tron tape bank";
                case ".k4s":
                case ".mks":
                    return lowerPath.Contains("midi learn") && lowerPath.Contains("spectrasonics") ? "Spectrasonics Stylus RMX Korg controller scene" : null;
                case ".obw": return "Toontrack sound library data";
                case ".dspreset": return "Decent Sampler preset";
                case ".dsbundle": return "Decent Sampler bundle";
                case ".big":
                    return lowerPath.Contains("air music technology") ? "AIR Music Technology content archive" : null;
                case ".patch":
                    return lowerPath.Contains("air music technology") || lowerPath.Contains("\\structure\\") ? "AIR Structure patch" : null;
                case ".mse": return "Maize Sampler exported instrument";
                case ".vpreset": return "Valhalla DSP preset";
                case ".cir": return lowerPath.Contains("universal audio") ? "Universal Audio LUNA convolution/impulse data" : null;
                case ".cmr": return lowerPath.Contains("universal audio") ? "Universal Audio LUNA model/resource data" : null;
                case ".rev": return lowerPath.Contains("universal audio") ? "Universal Audio LUNA reverb/response data" : null;
                case ".aasbank": return "Applied Acoustics Systems bank";
                case ".aas-gui": return "Applied Acoustics Systems GUI resource";
                case ".lbin": return lowerPath.Contains("applied acoustics systems") ? "Applied Acoustics Systems resource bundle" : null;
                case ".ptq": return lowerPath.Contains("modartt") || lowerPath.Contains("pianoteq") ? "Modartt Pianoteq add-on package" : null;
                case ".mfxp": return lowerPath.Contains("modartt") || lowerPath.Contains("pianoteq") ? "Modartt Pianoteq preset" : null;
                case ".cv-1 preset":
                case ".cv-2 preset":
                case ".cv-3 preset":
                case ".cv-2 effect preset":
                case ".cv-3 effect preset":
                case ".ep-4 bank":
                case ".vs-2 bank":
                case ".vs-3 pack":
                case ".va-2 bank":
                case ".chromaphone 2 bank":
                case ".objeq delay bank":
                    return "Applied Acoustics Systems bank, pack, or preset";
                default: return null;
            }
        }

        private static string DeveloperExtensionDescription(string path, byte[] data)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".apk": return "Android application package";
                case ".pyc": return "Python bytecode cache";
                case ".wasm": return "WebAssembly binary module";
                case ".msg": return "Outlook or installer message/resource file";
                case ".pak":
                    if (data.Length >= 10)
                    {
                        var version = BitConverter.ToUInt32(data, 0);
                        var encoding = BitConverter.ToUInt32(data, 4);
                        if (version >= 4 && version <= 6 && encoding <= 3)
                            return "Chromium/Electron resource pack";
                    }
                    return null;
                case ".hyb":
                    if (data.Length >= 4 && data[0] == 0x68 && data[1] == 0x79 && data[2] == 0xAD && data[3] == 0x62)
                        return "Chromium hyphenation dictionary";
                    return null;
                case ".w5s":
                    return path.IndexOf("\\Winamp\\", StringComparison.OrdinalIgnoreCase) >= 0 ? "Winamp 5 / Wasabi system plug-in module" : null;
                case ".ide":
                    return "antivirus identity/signature file";
                case ".appcache":
                    return "HTML5 application cache manifest";
                case ".catalog":
                    return path.IndexOf("\\VLC\\", StringComparison.OrdinalIgnoreCase) >= 0 ? "VLC skin catalog" : null;
                case ".soc":
                case ".sod":
                case ".soe":
                case ".sog":
                case ".soh":
                case ".sor":
                    return path.IndexOf("libreoffice", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        path.IndexOf("openoffice", StringComparison.OrdinalIgnoreCase) >= 0
                        ? "LibreOffice/OpenOffice application resource"
                        : null;
                default: return null;
            }
        }

        private static string SpeechVoiceExtensionDescription(string path, byte[] data)
        {
            var lower = path.ToLowerInvariant();
            var name = Path.GetFileName(path) ?? string.Empty;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (lower.Contains("\\ivona\\") || name.StartsWith("ivona", StringComparison.OrdinalIgnoreCase))
                return "IVONA speech voice package";
            if (lower.Contains("\\modeltalker\\") && ext == ".cbk")
                return "ModelTalker speech data";
            if (lower.Contains("\\acapela\\") && ext == ".clb")
                return "Acapela/Infovox speech library module";
            if (lower.Contains("\\loquendo") || name.IndexOf("Loquendo_TTS", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Loquendo TTS package";
            if (lower.Contains("\\supertonic\\") && ext == ".onnx")
                return "SuperTonic ONNX neural TTS model";
            if (lower.Contains("\\orpheus\\") && ext == ".tts")
                return "Dolphin Orpheus TTS language data";
            if ((lower.Contains("\\eloquence\\") || lower.Contains("\\ibmtts\\")) && ext == ".syn")
                return "Eloquence/IBM TTS synthesizer language module";
            if (lower.Contains("\\rhvoice") && (name.Equals("voice.data", StringComparison.OrdinalIgnoreCase) || ext == ".fst"))
                return "RHVoice speech data";
            if (lower.Contains("\\rhvoice") && ext == ".pdf" && !StartsWith(data, Encoding.ASCII.GetBytes("%PDF")))
                return "RHVoice acoustic model data using .pdf extension";
            if (lower.Contains("\\flexvoice\\") && (ext == ".bin" || ext == ".dat"))
                return "Mindmaker FlexVoice speech data";
            if ((lower.Contains("\\espeak-ng-data\\") || lower.Contains("\\espeak-data\\")) &&
                (name.EndsWith("_dict", StringComparison.OrdinalIgnoreCase) || name.StartsWith("phon", StringComparison.OrdinalIgnoreCase)))
                return "eSpeak NG speech synthesis data";
            if (lower.Contains("\\stspeech\\") || lower.Contains("\\bestspeech\\") || lower.Contains("\\tgspeechbox\\"))
                return "NVDA speech engine support data";
            return null;
        }

        private static bool LooksLikeSynologyPat(byte[] data)
        {
            if (data.Length >= 265)
            {
                var name = Ascii(data, 0, Math.Min(100, data.Length)).TrimEnd('.');
                if ((name.Equals("VERSION", StringComparison.OrdinalIgnoreCase) ||
                     name.Equals("GRUB_VER", StringComparison.OrdinalIgnoreCase) ||
                     name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase) ||
                     name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)) &&
                    Ascii(data, 257, Math.Min(8, data.Length - 257)).StartsWith("ustar", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return data.Length >= 4 &&
                ((data[1] == 0xAD && data[2] == 0xBE && data[3] == 0xEF) ||
                 (data[1] == 0xBF && data[2] == 0xBA && data[3] == 0xAD) ||
                 (data[0] == 0xAD && data[1] == 0xBE && data[2] == 0xEF) ||
                 (data[0] == 0xBF && data[1] == 0xBA && data[2] == 0xAD));
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
            new Signature(new byte[] { 0x30, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11, 0xA6, 0xD9, 0x00, 0xAA, 0x00, 0x62, 0xCE, 0x6C }, "ASF header", "Windows Media/ASF container"),
            new Signature("fLaC", "FLAC marker", "FLAC audio"),
            new Signature("ID3", "ID3 tag", "MP3 audio with ID3 tag"),
            new Signature("MThd", "MIDI header", "Standard MIDI file"),
            new Signature("MZ", "MZ executable header", "Windows executable or DLL"),
            new Signature("\x7FELF", "ELF header", "ELF executable"),
            new Signature("SQLite format 3\0", "SQLite header", "SQLite database"),
            new Signature(@"{\rtf", "RTF header", "Rich Text Format document"),
            new Signature("Standard Jet DB", "Jet database marker", "Microsoft Access/Jet database", 4),
            new Signature(new byte[] { 0x00, 0x61, 0x73, 0x6D }, "WebAssembly header", "WebAssembly binary module"),
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

        private static uint ReadUInt32LittleEndian(byte[] data, int offset)
        {
            if (offset < 0 || offset + 4 > data.Length)
                return 0;
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
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

