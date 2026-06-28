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
    {        private static void AddPeInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (header.Length < 0x40 || header[0] != 'M' || header[1] != 'Z')
                return;

            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var br = new BinaryReader(fs))
                {
                    fs.Position = 0x3c;
                    var peOffset = br.ReadInt32();
                    if (peOffset <= 0 || peOffset > fs.Length - 256)
                        return;

                    fs.Position = peOffset;
                    if (br.ReadUInt32() != 0x00004550)
                        return;

                    var machine = br.ReadUInt16();
                    var sectionCount = br.ReadUInt16();
                    var timestamp = br.ReadUInt32();
                    br.ReadUInt32();
                    br.ReadUInt32();
                    var optionalHeaderSize = br.ReadUInt16();
                    var characteristics = br.ReadUInt16();
                    var optionalStart = fs.Position;
                    var magic = br.ReadUInt16();
                    var isPe32Plus = magic == 0x20b;
                    var isPe32 = magic == 0x10b;
                    if (!isPe32 && !isPe32Plus)
                        return;

                    fs.Position = optionalStart + 2;
                    var linkerMajor = br.ReadByte();
                    var linkerMinor = br.ReadByte();
                    fs.Position = optionalStart + 16;
                    var entryPointRva = br.ReadUInt32();
                    fs.Position = optionalStart + (isPe32Plus ? 24 : 28);
                    var imageBaseText = isPe32Plus ? "0x" + br.ReadUInt64().ToString("X", CultureInfo.InvariantCulture) : "0x" + br.ReadUInt32().ToString("X", CultureInfo.InvariantCulture);
                    fs.Position = optionalStart + 40;
                    var osMajor = br.ReadUInt16();
                    var osMinor = br.ReadUInt16();
                    var imageMajor = br.ReadUInt16();
                    var imageMinor = br.ReadUInt16();
                    var subsystemMajor = br.ReadUInt16();
                    var subsystemMinor = br.ReadUInt16();
                    fs.Position = optionalStart + 68;
                    var subsystem = br.ReadUInt16();
                    var dllCharacteristics = br.ReadUInt16();
                    fs.Position = optionalStart + (isPe32Plus ? 108 : 92);
                    var dataDirectoryCount = br.ReadUInt32();
                    var clrRva = 0u;
                    var clrSize = 0u;
                    if (dataDirectoryCount > 14)
                    {
                        fs.Position = optionalStart + (isPe32Plus ? 112 : 96) + (14 * 8);
                        clrRva = br.ReadUInt32();
                        clrSize = br.ReadUInt32();
                    }

                    var section = AddSection(sections, "Windows executable");
                    Add(section, "PE format", isPe32Plus ? "PE32+ (usually 64-bit)" : "PE32 (usually 32-bit)");
                    Add(section, "Machine", MachineName(machine) + " (0x" + machine.ToString("X4", CultureInfo.InvariantCulture) + ")");
                    Add(section, "Subsystem", SubsystemName(subsystem) + " (0x" + subsystem.ToString("X4", CultureInfo.InvariantCulture) + ")");
                    Add(section, "Sections", sectionCount.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Entry point RVA", "0x" + entryPointRva.ToString("X", CultureInfo.InvariantCulture));
                    Add(section, "Image base", imageBaseText);
                    Add(section, "Linker version", linkerMajor.ToString(CultureInfo.InvariantCulture) + "." + linkerMinor.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Required OS version", osMajor + "." + osMinor);
                    Add(section, "Image version", imageMajor + "." + imageMinor);
                    Add(section, "Subsystem version", subsystemMajor + "." + subsystemMinor);
                    Add(section, "Characteristics", "0x" + characteristics.ToString("X4", CultureInfo.InvariantCulture));
                    Add(section, "DLL characteristics", DllCharacteristicsText(dllCharacteristics));
                    if (timestamp != 0)
                    {
                        var dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timestamp).ToLocalTime();
                        Add(section, "PE timestamp", dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                    }
                    Add(section, ".NET CLR header", clrRva != 0 && clrSize != 0 ? "Present" : "Not present");
                    fs.Position = optionalStart + optionalHeaderSize;
                    var sectionRows = new List<string>();
                    for (var i = 0; i < sectionCount && i < 32 && fs.Position + 40 <= fs.Length; i++)
                    {
                        var nameBytes = br.ReadBytes(8);
                        var name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0', ' ');
                        var virtualSize = br.ReadUInt32();
                        var virtualAddress = br.ReadUInt32();
                        var rawSize = br.ReadUInt32();
                        var rawPointer = br.ReadUInt32();
                        fs.Position += 16;
                        sectionRows.Add(name + "  RVA 0x" + virtualAddress.ToString("X", CultureInfo.InvariantCulture) + ", virtual " + FormatBytes(virtualSize) + ", raw " + FormatBytes(rawSize) + " at 0x" + rawPointer.ToString("X", CultureInfo.InvariantCulture));
                    }
                    if (sectionRows.Count > 0)
                        Add(section, "Section table", string.Join("\r\n", sectionRows.ToArray()));
                }
            }
            catch (Exception ex)
            {
                var section = AddSection(sections, "Windows executable");
                Add(section, "PE parse error", ex.Message);
            }
        }

        private static string MachineName(ushort machine)
        {
            switch (machine)
            {
                case 0x014c: return "Intel 386";
                case 0x8664: return "x64";
                case 0x01c0: return "ARM";
                case 0x01c4: return "ARMv7";
                case 0xaa64: return "ARM64";
                default: return "Unknown";
            }
        }

        private static string SubsystemName(ushort subsystem)
        {
            switch (subsystem)
            {
                case 1: return "Native";
                case 2: return "Windows GUI";
                case 3: return "Windows console";
                case 7: return "POSIX console";
                case 9: return "Windows CE GUI";
                case 10: return "EFI application";
                case 11: return "EFI boot service driver";
                case 12: return "EFI runtime driver";
                case 14: return "Xbox";
                case 16: return "Windows boot application";
                default: return "Unknown";
            }
        }

        private static string DllCharacteristicsText(ushort value)
        {
            var flags = new List<string>();
            if ((value & 0x0020) != 0) flags.Add("High entropy VA");
            if ((value & 0x0040) != 0) flags.Add("Dynamic base / ASLR");
            if ((value & 0x0080) != 0) flags.Add("Force integrity");
            if ((value & 0x0100) != 0) flags.Add("NX compatible");
            if ((value & 0x0200) != 0) flags.Add("No isolation");
            if ((value & 0x0400) != 0) flags.Add("No SEH");
            if ((value & 0x0800) != 0) flags.Add("No bind");
            if ((value & 0x1000) != 0) flags.Add("AppContainer");
            if ((value & 0x4000) != 0) flags.Add("Control Flow Guard");
            if ((value & 0x8000) != 0) flags.Add("Terminal Server aware");
            return "0x" + value.ToString("X4", CultureInfo.InvariantCulture) + (flags.Count == 0 ? "" : " (" + string.Join(", ", flags.ToArray()) + ")");
        }
    }
}

