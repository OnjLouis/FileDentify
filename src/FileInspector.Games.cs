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
{
    internal static partial class FileInspector
    {
        private static string GameFileTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (StartsWith(header, Encoding.ASCII.GetBytes("NESM\x1A"))) return "NES Sound Format music";
            if (StartsWith(header, Encoding.ASCII.GetBytes("NES\x1A"))) return "iNES Nintendo Entertainment System ROM";
            if (StartsWith(header, Encoding.ASCII.GetBytes("PACK"))) return "Quake PACK game archive";
            if (StartsWith(header, Encoding.ASCII.GetBytes("IWAD"))) return "Doom internal WAD game data";
            if (StartsWith(header, Encoding.ASCII.GetBytes("PWAD"))) return "Doom patch WAD game data";
            if (StartsWith(header, Encoding.ASCII.GetBytes("MComprHD"))) return "MAME Compressed Hunks of Data disk image";
            if (StartsWith(header, Encoding.ASCII.GetBytes("BKHD"))) return "Audiokinetic Wwise sound bank";
            if (StartsWith(header, Encoding.ASCII.GetBytes("RPA-"))) return "Ren'Py game resource archive";
            if (header.Length >= 0x104 && Encoding.ASCII.GetString(header, 0x100, 4) == "SEGA") return "Sega Mega Drive/Genesis ROM";
            if (IsNintendo64Rom(header)) return "Nintendo 64 ROM image";
            if (IsGameBoyRom(header)) return ext == ".gba" ? "Game Boy Advance ROM" : "Game Boy/Game Boy Color ROM";
            if (IsNintendoDsRom(path, header)) return "Nintendo DS ROM image";
            if (IsSegaMasterSystemOrGameGearRom(header)) return ext == ".gg" ? "Sega Game Gear ROM" : "Sega Master System ROM";
            if (IsNintendoSwitchNcaConcatSegment(path)) return "Nintendo Switch NCA split content segment";
            if (IsNintendoSwitchSaveFile(path)) return "Nintendo Switch save data";
            if (LooksLikeUnityAsset(path, header)) return "Unity asset data file";
            if (LooksLikeSteamVdf(path, header)) return ext == ".acf" ? "Steam app manifest" : "Valve Data Format text";
            if (LooksLikeRolandMt32Rom(path)) return "Roland MT-32/CM-32L ROM image";

            switch (ext)
            {
                case ".nsf": return "NES Sound Format music";
                case ".nes": return "Nintendo Entertainment System ROM";
                case ".fds": return "Famicom Disk System disk image";
                case ".gb": return "Nintendo Game Boy ROM";
                case ".gbc": return "Nintendo Game Boy Color ROM";
                case ".gba": return "Nintendo Game Boy Advance ROM";
                case ".nds": return "Nintendo DS ROM";
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
                case ".rpa": return "Ren'Py game resource archive";
                case ".rpyc": return "Ren'Py compiled script";
                case ".acf": return "Steam app manifest";
                case ".vdf": return "Valve Data Format text";
                case ".ips": return "IPS binary patch";
                case ".bps": return "BPS binary patch";
                default: return null;
            }
        }

        private static void AddGameFileInfo(List<ReportSection> sections, string path, byte[] header)
        {
            var type = GameFileTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Game/ROM data");
            Add(section, "Format hint", type);
            Add(section, "Detection basis", GameDetectionBasis(path, header));
            var hasSpecificSafetyNote = IsNintendoSwitchNcaConcatSegment(path) || IsNintendoSwitchSaveFile(path);

            if (StartsWith(header, Encoding.ASCII.GetBytes("NESM\x1A")))
                AddNsfInfo(section, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("NES\x1A")))
                AddINesInfo(section, header);
            else if (IsGameBoyRom(header))
                AddGameBoyInfo(section, header);
            else if (IsNintendoDsRom(path, header))
                AddNintendoDsInfo(section, header);
            else if (header.Length >= 0x104 && Encoding.ASCII.GetString(header, 0x100, 4) == "SEGA")
                AddSegaRomInfo(section, header);
            else if (IsSegaMasterSystemOrGameGearRom(header))
                AddSegaMasterSystemOrGameGearInfo(section, header);
            else if (IsNintendo64Rom(header))
                AddNintendo64Info(section, header);
            else if (LooksLikeSnesRom(path, header))
                AddSnesInfo(section, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("PACK")))
                AddQuakePackInfo(section, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("IWAD")) || StartsWith(header, Encoding.ASCII.GetBytes("PWAD")))
                AddDoomWadInfo(section, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("MComprHD")))
                AddChdInfo(section, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("BKHD")))
                AddWwiseBankInfo(section, header);
            else if (Path.GetExtension(path).Equals(".wem", StringComparison.OrdinalIgnoreCase))
                Add(section, "Container note", "Wwise encoded media commonly uses RIFF/WAVE framing; see RIFF and companion-tool sections when available.");
            else if (LooksLikeUnityAsset(path, header))
                AddUnityAssetInfo(section, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("RPA-")))
                Add(section, "Archive version", ReadAsciiUntil(header, 0, 32).Trim());
            else if (LooksLikeSteamVdf(path, header))
                AddSteamVdfInfo(section, header);
            else if (LooksLikeRolandMt32Rom(path))
                AddRolandMt32RomInfo(section, path);
            else if (IsNintendoSwitchNcaConcatSegment(path))
                AddNintendoSwitchNcaSegmentInfo(section, path);
            else if (IsNintendoSwitchSaveFile(path))
                AddNintendoSwitchSaveInfo(section, path);
            else if (Path.GetExtension(path).Equals(".ips", StringComparison.OrdinalIgnoreCase))
                AddIpsInfo(section, header);

            if (!hasSpecificSafetyNote)
                Add(section, "Notes", "FileDentify reports header-level evidence for game files. It does not validate ROM dumps, decrypt game archives, or emulate content.");
        }

        private static string GameDetectionBasis(string path, byte[] header)
        {
            var parts = new List<string>();
            var ext = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(ext)) parts.Add("extension " + ext);
            if (StartsWith(header, Encoding.ASCII.GetBytes("NESM\x1A"))) parts.Add("NESM marker");
            if (StartsWith(header, Encoding.ASCII.GetBytes("NES\x1A"))) parts.Add("iNES marker");
            if (StartsWith(header, Encoding.ASCII.GetBytes("PACK"))) parts.Add("PACK marker");
            if (StartsWith(header, Encoding.ASCII.GetBytes("IWAD"))) parts.Add("IWAD marker");
            if (StartsWith(header, Encoding.ASCII.GetBytes("PWAD"))) parts.Add("PWAD marker");
            if (StartsWith(header, Encoding.ASCII.GetBytes("MComprHD"))) parts.Add("MComprHD marker");
            if (StartsWith(header, Encoding.ASCII.GetBytes("BKHD"))) parts.Add("BKHD marker");
            if (StartsWith(header, Encoding.ASCII.GetBytes("RPA-"))) parts.Add("RPA marker");
            if (header.Length >= 0x104 && Encoding.ASCII.GetString(header, 0x100, 4) == "SEGA") parts.Add("SEGA header at 0x100");
            if (IsNintendo64Rom(header)) parts.Add("Nintendo 64 boot-code byte order marker");
            if (IsNintendoDsRom(path, header)) parts.Add("Nintendo DS secure-area/header fields");
            if (IsSegaMasterSystemOrGameGearRom(header)) parts.Add("TMR SEGA marker");
            if (IsNintendoSwitchNcaConcatSegment(path)) parts.Add("Nintendo Contents registered .nca.CONCAT segment path");
            if (IsNintendoSwitchSaveFile(path)) parts.Add("Nintendo save folder path");
            if (LooksLikeUnityAsset(path, header)) parts.Add("Unity version string");
            if (LooksLikeRolandMt32Rom(path)) parts.Add("Roland MT-32/CM-32L filename");
            return string.Join(", ", parts.ToArray());
        }

        private static bool LooksLikeRolandMt32Rom(string path)
        {
            if (!string.Equals(Path.GetExtension(path), ".rom", StringComparison.OrdinalIgnoreCase))
                return false;
            var name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            return name.IndexOf("MT32", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("MT-32", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("CM32", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("CM-32", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsNintendoSwitchNcaConcatSegment(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;
            var parent = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(parent) || !parent.EndsWith(".nca.CONCAT", StringComparison.OrdinalIgnoreCase))
                return false;
            var fileName = Path.GetFileName(path);
            return Regex.IsMatch(fileName ?? string.Empty, "^[0-9]{2}$", RegexOptions.IgnoreCase) &&
                path.IndexOf("\\Nintendo\\Contents\\registered\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsNintendoSwitchSaveFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;
            var fileName = Path.GetFileName(path);
            return Regex.IsMatch(fileName ?? string.Empty, "^[0-9a-f]{16}$", RegexOptions.IgnoreCase) &&
                path.IndexOf("\\Nintendo\\save\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddNintendoSwitchNcaSegmentInfo(ReportSection section, string path)
        {
            var packageDir = Path.GetDirectoryName(path);
            var shardDir = Path.GetFileName(Path.GetDirectoryName(packageDir ?? string.Empty));
            Add(section, "Container family", "Nintendo Switch registered content");
            Add(section, "Package folder", Path.GetFileName(packageDir));
            Add(section, "Shard folder", shardDir);
            Add(section, "Segment number", Path.GetFileName(path));
            Add(section, "Segment size", FormatBytes(SafeLength(path)));

            if (!string.IsNullOrWhiteSpace(packageDir) && Directory.Exists(packageDir))
            {
                var segments = Directory.GetFiles(packageDir)
                    .OrderBy(file => Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                Add(section, "Segments in package", segments.Length.ToString(CultureInfo.InvariantCulture));
                Add(section, "Package sampled size", FormatBytes(segments.Sum(segment => SafeLength(segment))));
            }

            Add(section, "Notes", "NCA content is normally encrypted and may be split into numbered .nca.CONCAT segments on console storage. FileDentify reports path and size evidence only; it does not decrypt, join, validate, or extract the content.");
        }

        private static void AddNintendoSwitchSaveInfo(ReportSection section, string path)
        {
            Add(section, "Container family", "Nintendo Switch save data");
            Add(section, "Save file ID", Path.GetFileName(path));
            Add(section, "File size", FormatBytes(SafeLength(path)));
            Add(section, "Notes", "Nintendo Switch save data can be encrypted or console/account-specific. FileDentify reports container context and size only.");
        }

        private static void AddRolandMt32RomInfo(ReportSection section, string path)
        {
            var name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            Add(section, "Synth family", "Roland MT-32 / CM-32L LA synthesis ROM");
            if (name.IndexOf("CONTROL", StringComparison.OrdinalIgnoreCase) >= 0)
                Add(section, "ROM role", "Control ROM");
            else if (name.IndexOf("PCM", StringComparison.OrdinalIgnoreCase) >= 0)
                Add(section, "ROM role", "PCM sample ROM");
            Add(section, "Common use", "Used by MT-32/CM-32L compatible emulation and restoration workflows.");
        }

        private static void AddNsfInfo(ReportSection section, byte[] h)
        {
            if (h.Length < 0x80)
                return;
            Add(section, "NSF version", h[5].ToString(CultureInfo.InvariantCulture));
            Add(section, "Songs", h[6].ToString(CultureInfo.InvariantCulture));
            Add(section, "Starting song", h[7].ToString(CultureInfo.InvariantCulture));
            Add(section, "Load address", "0x" + ReadUInt16LittleEndian(h, 8).ToString("X4", CultureInfo.InvariantCulture));
            Add(section, "Init address", "0x" + ReadUInt16LittleEndian(h, 10).ToString("X4", CultureInfo.InvariantCulture));
            Add(section, "Play address", "0x" + ReadUInt16LittleEndian(h, 12).ToString("X4", CultureInfo.InvariantCulture));
            AddFixed(section, "Title", h, 14, 32);
            AddFixed(section, "Artist", h, 46, 32);
            AddFixed(section, "Copyright", h, 78, 32);
            Add(section, "NTSC speed", ReadUInt16LittleEndian(h, 0x6E).ToString(CultureInfo.InvariantCulture) + " microseconds");
            Add(section, "PAL speed", ReadUInt16LittleEndian(h, 0x78).ToString(CultureInfo.InvariantCulture) + " microseconds");
            var banks = h.Skip(0x70).Take(8).Select(b => b.ToString("X2", CultureInfo.InvariantCulture)).ToArray();
            if (banks.Any(v => v != "00"))
                Add(section, "Bank switching bytes", string.Join(" ", banks));
            Add(section, "Video system", NsfVideoSystem(h[0x7A]));
            var chips = NsfExpansionChips(h[0x7B]);
            if (!string.IsNullOrWhiteSpace(chips))
                Add(section, "Expansion audio", chips);
        }

        private static void AddINesInfo(ReportSection section, byte[] h)
        {
            if (h.Length < 16)
                return;
            var flags6 = h[6];
            var flags7 = h[7];
            var mapper = (flags6 >> 4) | (flags7 & 0xF0);
            var nes2 = (flags7 & 0x0C) == 0x08;
            Add(section, "Header format", nes2 ? "NES 2.0" : "iNES");
            Add(section, "Mapper", mapper.ToString(CultureInfo.InvariantCulture));
            Add(section, "PRG ROM", FormatBytes(h[4] * 16L * 1024L) + " (" + h[4].ToString(CultureInfo.InvariantCulture) + " x 16 KiB)");
            Add(section, "CHR ROM", h[5] == 0 ? "0 bytes; likely CHR RAM" : FormatBytes(h[5] * 8L * 1024L) + " (" + h[5].ToString(CultureInfo.InvariantCulture) + " x 8 KiB)");
            Add(section, "Mirroring", (flags6 & 0x08) != 0 ? "Four-screen VRAM" : ((flags6 & 0x01) != 0 ? "Vertical" : "Horizontal"));
            Add(section, "Battery-backed RAM", (flags6 & 0x02) != 0 ? "Yes" : "No");
            Add(section, "Trainer", (flags6 & 0x04) != 0 ? "Present" : "Not present");
            Add(section, "Console type", INesConsoleType(flags7 & 0x03));
        }

        private static void AddGameBoyInfo(ReportSection section, byte[] h)
        {
            if (h.Length < 0x150)
                return;
            AddFixed(section, "Title", h, 0x134, 16);
            Add(section, "CGB flag", GameBoyCgbFlag(h[0x143]));
            Add(section, "SGB flag", h[0x146] == 0x03 ? "Super Game Boy supported" : "No Super Game Boy flag");
            Add(section, "Cartridge type", GameBoyCartridgeType(h[0x147]) + " (0x" + h[0x147].ToString("X2", CultureInfo.InvariantCulture) + ")");
            Add(section, "ROM size", GameBoyRomSize(h[0x148]));
            Add(section, "RAM size", GameBoyRamSize(h[0x149]));
            Add(section, "Destination", h[0x14A] == 0 ? "Japanese" : "Non-Japanese");
            Add(section, "Header checksum", "0x" + h[0x14D].ToString("X2", CultureInfo.InvariantCulture));
            if (PathLooksGbaHeader(h))
            {
                AddFixed(section, "GBA game code", h, 0xAC, 4);
                AddFixed(section, "GBA maker code", h, 0xB0, 2);
            }
        }

        private static void AddNintendoDsInfo(ReportSection section, byte[] h)
        {
            if (h.Length < 0x160)
                return;
            AddFixed(section, "Title", h, 0x00, 12);
            AddFixed(section, "Game code", h, 0x0C, 4);
            AddFixed(section, "Maker code", h, 0x10, 2);
            Add(section, "Unit code", NintendoDsUnitCode(h[0x12]) + " (0x" + h[0x12].ToString("X2", CultureInfo.InvariantCulture) + ")");
            Add(section, "Device capacity byte", "0x" + h[0x14].ToString("X2", CultureInfo.InvariantCulture) + " (" + NintendoDsCapacity(h[0x14]) + ")");
            Add(section, "ARM9 ROM offset", "0x" + ReadUInt32LittleEndian(h, 0x20).ToString("X8", CultureInfo.InvariantCulture));
            Add(section, "ARM9 load address", "0x" + ReadUInt32LittleEndian(h, 0x28).ToString("X8", CultureInfo.InvariantCulture));
            Add(section, "ARM9 size", FormatBytes(ReadUInt32LittleEndian(h, 0x2C)));
            Add(section, "ARM7 ROM offset", "0x" + ReadUInt32LittleEndian(h, 0x30).ToString("X8", CultureInfo.InvariantCulture));
            Add(section, "ARM7 load address", "0x" + ReadUInt32LittleEndian(h, 0x38).ToString("X8", CultureInfo.InvariantCulture));
            Add(section, "ARM7 size", FormatBytes(ReadUInt32LittleEndian(h, 0x3C)));
            Add(section, "File name table offset", "0x" + ReadUInt32LittleEndian(h, 0x40).ToString("X8", CultureInfo.InvariantCulture));
            Add(section, "File name table size", FormatBytes(ReadUInt32LittleEndian(h, 0x44)));
            Add(section, "Header CRC", "0x" + ReadUInt16LittleEndian(h, 0x15E).ToString("X4", CultureInfo.InvariantCulture));
        }

        private static void AddSegaMasterSystemOrGameGearInfo(ReportSection section, byte[] h)
        {
            var offset = SegaHeaderOffset(h);
            if (offset < 0)
            {
                Add(section, "Sega 8-bit header", "Known extension, but no TMR SEGA marker was found in the sampled header.");
                return;
            }
            Add(section, "Header marker", "TMR SEGA at 0x" + offset.ToString("X", CultureInfo.InvariantCulture));
            if (offset + 16 <= h.Length)
            {
                var checksum = ReadUInt16LittleEndian(h, offset + 10);
                var productCode = h[offset + 12].ToString("X2", CultureInfo.InvariantCulture) + h[offset + 13].ToString("X2", CultureInfo.InvariantCulture) + ((h[offset + 14] >> 4) & 0x0F).ToString("X1", CultureInfo.InvariantCulture);
                Add(section, "Checksum", "0x" + checksum.ToString("X4", CultureInfo.InvariantCulture));
                Add(section, "Product code", productCode);
                Add(section, "Version", (h[offset + 14] & 0x0F).ToString(CultureInfo.InvariantCulture));
                Add(section, "Region/system", SegaRegionSystemName((byte)(h[offset + 15] >> 4)));
                Add(section, "ROM size code", "0x" + (h[offset + 15] & 0x0F).ToString("X1", CultureInfo.InvariantCulture) + " (" + SegaRomSizeName((byte)(h[offset + 15] & 0x0F)) + ")");
            }
        }

        private static void AddSegaRomInfo(ReportSection section, byte[] h)
        {
            AddFixed(section, "Console", h, 0x100, 16);
            AddFixed(section, "Copyright/date", h, 0x110, 16);
            AddFixed(section, "Domestic name", h, 0x120, 48);
            AddFixed(section, "International name", h, 0x150, 48);
            AddFixed(section, "Product code", h, 0x180, 14);
            if (h.Length >= 0x18E)
                Add(section, "Checksum", "0x" + ReadUInt16BigEndian(h, 0x18E).ToString("X4", CultureInfo.InvariantCulture));
            if (h.Length >= 0x1A8)
            {
                Add(section, "ROM range", "0x" + ReadUInt32BigEndian(h, 0x1A0).ToString("X8", CultureInfo.InvariantCulture) + " - 0x" + ReadUInt32BigEndian(h, 0x1A4).ToString("X8", CultureInfo.InvariantCulture));
            }
            AddFixed(section, "I/O support", h, 0x190, 16);
            AddFixed(section, "Region", h, 0x1F0, 16);
        }

        private static void AddNintendo64Info(ReportSection section, byte[] h)
        {
            Add(section, "Byte order", Nintendo64ByteOrder(h));
            AddFixed(section, "Internal title", h, 0x20, 20);
            if (h.Length >= 0x40)
            {
                Add(section, "Clock rate", "0x" + ReadUInt32BigEndian(h, 4).ToString("X8", CultureInfo.InvariantCulture));
                Add(section, "Program counter", "0x" + ReadUInt32BigEndian(h, 8).ToString("X8", CultureInfo.InvariantCulture));
                Add(section, "CRC1", "0x" + ReadUInt32BigEndian(h, 0x10).ToString("X8", CultureInfo.InvariantCulture));
                Add(section, "CRC2", "0x" + ReadUInt32BigEndian(h, 0x14).ToString("X8", CultureInfo.InvariantCulture));
                AddFixed(section, "Game code", h, 0x3B, 4);
            }
        }

        private static void AddSnesInfo(ReportSection section, byte[] h)
        {
            var offset = BestSnesHeaderOffset(h);
            if (offset < 0)
            {
                Add(section, "SNES header", "Known extension, but no plausible internal header was found in the first sample.");
                return;
            }
            Add(section, "Probable internal header offset", "0x" + offset.ToString("X", CultureInfo.InvariantCulture));
            AddFixed(section, "Title", h, offset, 21);
            Add(section, "Map mode", "0x" + h[offset + 0x15].ToString("X2", CultureInfo.InvariantCulture));
            Add(section, "Cartridge type", "0x" + h[offset + 0x16].ToString("X2", CultureInfo.InvariantCulture));
            Add(section, "ROM size byte", "0x" + h[offset + 0x17].ToString("X2", CultureInfo.InvariantCulture));
            Add(section, "RAM size byte", "0x" + h[offset + 0x18].ToString("X2", CultureInfo.InvariantCulture));
            Add(section, "Country/region byte", "0x" + h[offset + 0x19].ToString("X2", CultureInfo.InvariantCulture));
            Add(section, "Version", h[offset + 0x1B].ToString(CultureInfo.InvariantCulture));
            Add(section, "Checksum complement", "0x" + ReadUInt16LittleEndian(h, offset + 0x1C).ToString("X4", CultureInfo.InvariantCulture));
            Add(section, "Checksum", "0x" + ReadUInt16LittleEndian(h, offset + 0x1E).ToString("X4", CultureInfo.InvariantCulture));
        }

        private static void AddQuakePackInfo(ReportSection section, byte[] h)
        {
            if (h.Length < 12)
                return;
            var dirOffset = ReadUInt32LittleEndian(h, 4);
            var dirSize = ReadUInt32LittleEndian(h, 8);
            Add(section, "Directory offset", "0x" + dirOffset.ToString("X", CultureInfo.InvariantCulture));
            Add(section, "Directory size", FormatBytes(dirSize));
            Add(section, "Directory entries", (dirSize / 64).ToString(CultureInfo.InvariantCulture));
        }

        private static void AddDoomWadInfo(ReportSection section, byte[] h)
        {
            if (h.Length < 12)
                return;
            Add(section, "WAD type", Encoding.ASCII.GetString(h, 0, 4));
            Add(section, "Lump count", ReadUInt32LittleEndian(h, 4).ToString(CultureInfo.InvariantCulture));
            Add(section, "Directory offset", "0x" + ReadUInt32LittleEndian(h, 8).ToString("X", CultureInfo.InvariantCulture));
        }

        private static void AddChdInfo(ReportSection section, byte[] h)
        {
            Add(section, "CHD marker", "MComprHD");
            if (h.Length >= 16)
            {
                Add(section, "Header length", FormatBytes(ReadUInt32BigEndian(h, 8)));
                Add(section, "CHD version", ReadUInt32BigEndian(h, 12).ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void AddWwiseBankInfo(ReportSection section, byte[] h)
        {
            if (h.Length < 8)
                return;
            Add(section, "BKHD chunk size", FormatBytes(ReadUInt32LittleEndian(h, 4)));
            if (h.Length >= 12)
                Add(section, "Bank version", ReadUInt32LittleEndian(h, 8).ToString(CultureInfo.InvariantCulture));
            if (h.Length >= 16)
                Add(section, "Bank id", ReadUInt32LittleEndian(h, 12).ToString(CultureInfo.InvariantCulture));
            AddGameChunkList(section, h, true);
        }

        private static void AddUnityAssetInfo(ReportSection section, byte[] h)
        {
            Add(section, "Unity version", FirstUnityVersionString(h));
            if (h.Length >= 0x30)
            {
                Add(section, "Metadata size", FormatBytes(ReadUInt32BigEndian(h, 0)));
                Add(section, "File size from header", FormatBytes(ReadUInt32BigEndian(h, 4)));
                Add(section, "Format version", ReadUInt32BigEndian(h, 8).ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void AddSteamVdfInfo(ReportSection section, byte[] h)
        {
            var text = Encoding.UTF8.GetString(h.Take(Math.Min(h.Length, 8192)).ToArray());
            var root = Regex.Match(text, "\"([^\"]+)\"");
            if (root.Success)
                Add(section, "Root object", root.Groups[1].Value);
            foreach (var key in new[] { "appid", "name", "installdir", "StateFlags", "buildid", "LastUpdated", "SizeOnDisk" })
            {
                var match = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase);
                if (match.Success)
                    Add(section, key, match.Groups[1].Value);
            }
        }

        private static void AddIpsInfo(ReportSection section, byte[] h)
        {
            if (StartsWith(h, Encoding.ASCII.GetBytes("PATCH")))
                Add(section, "IPS marker", "PATCH");
            if (h.Length >= 3 && Encoding.ASCII.GetString(h.Skip(Math.Max(0, h.Length - 3)).Take(3).ToArray()) == "EOF")
                Add(section, "EOF marker", "Present");
        }

        private static void AddGameChunkList(ReportSection section, byte[] h, bool littleEndian)
        {
            var chunks = new List<string>();
            var offset = 0;
            while (offset + 8 <= h.Length && chunks.Count < 24)
            {
                var id = Encoding.ASCII.GetString(h, offset, 4);
                if (!id.All(ch => ch >= 32 && ch < 127))
                    break;
                var size = littleEndian ? ReadUInt32LittleEndian(h, offset + 4) : ReadUInt32BigEndian(h, offset + 4);
                chunks.Add(id + " (" + FormatBytes(size) + ")");
                var next = offset + 8L + size + (size % 2);
                if (next <= offset || next > h.Length)
                    break;
                offset = (int)next;
            }
            if (chunks.Count > 0)
                Add(section, "Chunks", string.Join("\r\n", chunks.ToArray()));
        }

        private static bool IsNintendo64Rom(byte[] h)
        {
            return h.Length >= 4 &&
                ((h[0] == 0x80 && h[1] == 0x37 && h[2] == 0x12 && h[3] == 0x40) ||
                (h[0] == 0x40 && h[1] == 0x12 && h[2] == 0x37 && h[3] == 0x80) ||
                (h[0] == 0x37 && h[1] == 0x80 && h[2] == 0x40 && h[3] == 0x12));
        }

        private static bool IsGameBoyRom(byte[] h)
        {
            if (h.Length < 0x150)
                return false;
            return PathLooksGbaHeader(h) || (h[0x104] == 0xCE && h[0x105] == 0xED && h[0x106] == 0x66 && h[0x107] == 0x66);
        }

        private static bool IsNintendoDsRom(string path, byte[] h)
        {
            if (h.Length < 0x160)
                return false;
            if (!string.Equals(Path.GetExtension(path), ".nds", StringComparison.OrdinalIgnoreCase))
                return false;
            if (StartsWith(h, Encoding.ASCII.GetBytes("RIFF")))
                return false;
            if (!IsPrintableAscii(ReadFixedAscii(h, 0x0C, 4)) || !IsPrintableAscii(ReadFixedAscii(h, 0x10, 2)))
                return false;
            var arm9Offset = ReadUInt32LittleEndian(h, 0x20);
            var arm7Offset = ReadUInt32LittleEndian(h, 0x30);
            return arm9Offset > 0 && arm7Offset > arm9Offset && h[0x12] <= 3;
        }

        private static bool IsSegaMasterSystemOrGameGearRom(byte[] h)
        {
            return SegaHeaderOffset(h) >= 0;
        }

        private static int SegaHeaderOffset(byte[] h)
        {
            foreach (var offset in new[] { 0x7FF0, 0x3FF0, 0x1FF0 })
            {
                if (offset + 8 <= h.Length && Encoding.ASCII.GetString(h, offset, 8) == "TMR SEGA")
                    return offset;
            }
            return -1;
        }

        private static bool PathLooksGbaHeader(byte[] h)
        {
            return h.Length >= 0xC0 && h[0xB2] == 0x96;
        }

        private static bool LooksLikeUnityAsset(string path, byte[] h)
        {
            var ext = Path.GetExtension(path);
            if (!ext.Equals(".assets", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".ress", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".resS", StringComparison.OrdinalIgnoreCase))
                return false;
            return !string.IsNullOrWhiteSpace(FirstUnityVersionString(h));
        }

        private static bool LooksLikeSteamVdf(string path, byte[] h)
        {
            var ext = Path.GetExtension(path);
            if (!ext.Equals(".acf", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".vdf", StringComparison.OrdinalIgnoreCase))
                return false;
            if (!LooksLikeText(h))
                return false;
            var text = Encoding.UTF8.GetString(h.Take(Math.Min(h.Length, 512)).ToArray());
            return text.IndexOf("\"AppState\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("\"LibraryFolders\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.TrimStart().StartsWith("\"", StringComparison.Ordinal);
        }

        private static bool LooksLikeSnesRom(string path, byte[] h)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return (ext == ".sfc" || ext == ".smc") && BestSnesHeaderOffset(h) >= 0;
        }

        private static int BestSnesHeaderOffset(byte[] h)
        {
            foreach (var offset in new[] { 0x7FC0, 0xFFC0, 0x40FFC0 })
            {
                if (offset + 0x20 > h.Length)
                    continue;
                var title = ReadFixedAscii(h, offset, 21);
                if (title.Length >= 4 && title.Count(char.IsLetterOrDigit) >= 3)
                    return offset;
            }
            return -1;
        }

        private static string FirstUnityVersionString(byte[] h)
        {
            var text = Encoding.ASCII.GetString(h.Take(Math.Min(h.Length, 512)).ToArray());
            var match = Regex.Match(text, @"20\d\d\.\d+\.\d+[A-Za-z]\d+");
            return match.Success ? match.Value : string.Empty;
        }

        private static void AddFixed(ReportSection section, string label, byte[] data, int offset, int length)
        {
            var value = ReadFixedAscii(data, offset, length);
            if (!string.IsNullOrWhiteSpace(value))
                Add(section, label, value);
        }

        private static string ReadFixedAscii(byte[] data, int offset, int length)
        {
            if (offset < 0 || offset >= data.Length || length <= 0)
                return string.Empty;
            var count = Math.Min(length, data.Length - offset);
            var chars = new List<char>();
            for (var i = 0; i < count; i++)
            {
                var b = data[offset + i];
                if (b == 0)
                    break;
                chars.Add(b >= 32 && b < 127 ? (char)b : ' ');
            }
            return Regex.Replace(new string(chars.ToArray()), @"\s+", " ").Trim();
        }

        private static string ReadAsciiUntil(byte[] data, int offset, int maxLength)
        {
            return ReadFixedAscii(data, offset, Math.Min(maxLength, data.Length - offset));
        }

        private static string NsfVideoSystem(byte value)
        {
            switch (value & 0x03)
            {
                case 0: return "NTSC";
                case 1: return "PAL";
                case 2: return "Dual NTSC/PAL";
                default: return "Unknown";
            }
        }

        private static string NsfExpansionChips(byte value)
        {
            var chips = new List<string>();
            if ((value & 0x01) != 0) chips.Add("Konami VRC6");
            if ((value & 0x02) != 0) chips.Add("Konami VRC7");
            if ((value & 0x04) != 0) chips.Add("FDS");
            if ((value & 0x08) != 0) chips.Add("MMC5");
            if ((value & 0x10) != 0) chips.Add("Namco 163");
            if ((value & 0x20) != 0) chips.Add("Sunsoft 5B");
            return string.Join(", ", chips.ToArray());
        }

        private static string INesConsoleType(int value)
        {
            switch (value)
            {
                case 0: return "NES/Famicom";
                case 1: return "Nintendo Vs. System";
                case 2: return "PlayChoice-10";
                default: return "Extended console type";
            }
        }

        private static string GameBoyCgbFlag(byte value)
        {
            if (value == 0x80) return "Game Boy Color supported";
            if (value == 0xC0) return "Game Boy Color only";
            return "Original Game Boy or unspecified";
        }

        private static string GameBoyCartridgeType(byte value)
        {
            switch (value)
            {
                case 0x00: return "ROM only";
                case 0x01: return "MBC1";
                case 0x02: return "MBC1 + RAM";
                case 0x03: return "MBC1 + RAM + Battery";
                case 0x05: return "MBC2";
                case 0x06: return "MBC2 + Battery";
                case 0x08: return "ROM + RAM";
                case 0x09: return "ROM + RAM + Battery";
                case 0x0F: return "MBC3 + Timer + Battery";
                case 0x10: return "MBC3 + Timer + RAM + Battery";
                case 0x11: return "MBC3";
                case 0x12: return "MBC3 + RAM";
                case 0x13: return "MBC3 + RAM + Battery";
                case 0x19: return "MBC5";
                case 0x1A: return "MBC5 + RAM";
                case 0x1B: return "MBC5 + RAM + Battery";
                case 0x1C: return "MBC5 + Rumble";
                case 0x1D: return "MBC5 + Rumble + RAM";
                case 0x1E: return "MBC5 + Rumble + RAM + Battery";
                case 0x20: return "MBC6";
                case 0x22: return "MBC7 + Sensor + Rumble + RAM + Battery";
                default: return "Unknown/less common cartridge type";
            }
        }

        private static string GameBoyRomSize(byte value)
        {
            if (value <= 8)
                return FormatBytes(32L * 1024L << value);
            switch (value)
            {
                case 0x52: return "1.1 MiB";
                case 0x53: return "1.2 MiB";
                case 0x54: return "1.5 MiB";
                default: return "Unknown size byte 0x" + value.ToString("X2", CultureInfo.InvariantCulture);
            }
        }

        private static string GameBoyRamSize(byte value)
        {
            switch (value)
            {
                case 0x00: return "None or unspecified";
                case 0x01: return "2 KiB";
                case 0x02: return "8 KiB";
                case 0x03: return "32 KiB";
                case 0x04: return "128 KiB";
                case 0x05: return "64 KiB";
                default: return "Unknown size byte 0x" + value.ToString("X2", CultureInfo.InvariantCulture);
            }
        }

        private static string Nintendo64ByteOrder(byte[] h)
        {
            if (h[0] == 0x80) return "Big-endian .z64 style";
            if (h[0] == 0x40) return "Byte-swapped .v64 style";
            if (h[0] == 0x37) return "Little-endian .n64 style";
            return "Unknown";
        }

        private static string NintendoDsUnitCode(byte value)
        {
            switch (value)
            {
                case 0: return "Nintendo DS";
                case 2: return "Nintendo DS and DSi";
                case 3: return "Nintendo DSi";
                default: return "Unknown";
            }
        }

        private static string NintendoDsCapacity(byte value)
        {
            if (value <= 0x0D)
                return FormatBytes(128L * 1024L << value);
            return "unknown capacity code";
        }

        private static string SegaRegionSystemName(byte value)
        {
            switch (value)
            {
                case 3: return "Master System Japan";
                case 4: return "Master System export";
                case 5: return "Game Gear Japan";
                case 6: return "Game Gear export";
                case 7: return "Game Gear international";
                default: return "Unknown system code 0x" + value.ToString("X1", CultureInfo.InvariantCulture);
            }
        }

        private static string SegaRomSizeName(byte value)
        {
            switch (value)
            {
                case 0xA: return "8 KiB";
                case 0xB: return "16 KiB";
                case 0xC: return "32 KiB";
                case 0xD: return "48 KiB";
                case 0xE: return "64 KiB";
                case 0xF: return "128 KiB";
                case 0x0: return "256 KiB";
                case 0x1: return "512 KiB";
                case 0x2: return "1 MiB";
                default: return "unknown";
            }
        }
    }
}
