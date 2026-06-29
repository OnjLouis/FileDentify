using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static string DeveloperFormatTypeName(string path, byte[] header)
        {
            if (IsWebAssembly(header))
                return "WebAssembly binary module";
            if (IsPythonBytecode(path, header))
                return "Python bytecode cache";
            if (IsChromiumPak(path, header))
                return "Chromium/Electron resource pack";
            var speechVoiceType = SpeechVoiceTypeName(path, header);
            if (speechVoiceType != null)
                return speechVoiceType;
            var aiModelType = AiModelTypeName(path, header);
            if (aiModelType != null)
                return aiModelType;
            if (string.Equals(Path.GetExtension(path), ".msg", StringComparison.OrdinalIgnoreCase) && IsOleCompoundFile(header))
                return "Outlook or installer message/resource file";
            return null;
        }

        private static void AddDeveloperFormatInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (IsWebAssembly(header))
                AddWebAssemblyInfo(sections, header);
            if (IsPythonBytecode(path, header))
                AddPythonBytecodeInfo(sections, header);
            if (IsChromiumPak(path, header))
                AddChromiumPakInfo(sections, header);
            AddSpeechVoiceInfo(sections, path, header, new FileInfo(path).Length);
            AddAiModelInfo(sections, path, header);
            if (string.Equals(Path.GetExtension(path), ".msg", StringComparison.OrdinalIgnoreCase) && IsOleCompoundFile(header))
                AddMsgInfo(sections);
        }

        private static bool IsWebAssembly(byte[] header)
        {
            return header.Length >= 8 &&
                header[0] == 0x00 &&
                header[1] == 0x61 &&
                header[2] == 0x73 &&
                header[3] == 0x6D;
        }

        private static void AddWebAssemblyInfo(List<ReportSection> sections, byte[] header)
        {
            var section = AddSection(sections, "WebAssembly");
            Add(section, "Magic", "\\0asm");
            Add(section, "Version", ReadUInt32LittleEndian(header, 4).ToString(CultureInfo.InvariantCulture));

            var names = new List<string>();
            var offset = 8;
            while (offset < header.Length && names.Count < 24)
            {
                uint id;
                int idBytes;
                if (!ReadLeb128(header, offset, out id, out idBytes))
                    break;
                offset += idBytes;

                uint size;
                int sizeBytes;
                if (!ReadLeb128(header, offset, out size, out sizeBytes))
                    break;
                offset += sizeBytes;

                names.Add(WebAssemblySectionName(id) + " (" + FormatBytes(size) + ")");
                var next = offset + (long)size;
                if (next <= offset || next > int.MaxValue)
                    break;
                offset = (int)next;
            }

            if (names.Count > 0)
                Add(section, "Visible sections", string.Join("\r\n", names.ToArray()));
        }

        private static bool ReadLeb128(byte[] data, int offset, out uint value, out int bytesRead)
        {
            value = 0;
            bytesRead = 0;
            var shift = 0;
            while (offset + bytesRead < data.Length && bytesRead < 5)
            {
                var b = data[offset + bytesRead];
                value |= (uint)(b & 0x7F) << shift;
                bytesRead++;
                if ((b & 0x80) == 0)
                    return true;
                shift += 7;
            }
            return false;
        }

        private static string WebAssemblySectionName(uint id)
        {
            switch (id)
            {
                case 0: return "Custom";
                case 1: return "Type";
                case 2: return "Import";
                case 3: return "Function";
                case 4: return "Table";
                case 5: return "Memory";
                case 6: return "Global";
                case 7: return "Export";
                case 8: return "Start";
                case 9: return "Element";
                case 10: return "Code";
                case 11: return "Data";
                case 12: return "Data count";
                default: return "Unknown section " + id.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static bool IsPythonBytecode(string path, byte[] header)
        {
            return string.Equals(Path.GetExtension(path), ".pyc", StringComparison.OrdinalIgnoreCase) && header.Length >= 16;
        }

        private static void AddPythonBytecodeInfo(List<ReportSection> sections, byte[] header)
        {
            var section = AddSection(sections, "Python bytecode");
            Add(section, "Magic number", HexCompact(header, 0, 4));
            var flags = ReadUInt32LittleEndian(header, 4);
            Add(section, "Flags", "0x" + flags.ToString("X8", CultureInfo.InvariantCulture));
            if ((flags & 0x01) == 0)
            {
                var timestamp = ReadUInt32LittleEndian(header, 8);
                var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
                Add(section, "Invalidation mode", "Timestamp and source size");
                Add(section, "Source timestamp", date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                Add(section, "Source size", FormatBytes(ReadUInt32LittleEndian(header, 12)));
            }
            else
            {
                Add(section, "Invalidation mode", (flags & 0x02) != 0 ? "Checked hash" : "Unchecked hash");
                Add(section, "Source hash", HexCompact(header, 8, 8));
            }
        }

        private static bool IsChromiumPak(string path, byte[] header)
        {
            if (!string.Equals(Path.GetExtension(path), ".pak", StringComparison.OrdinalIgnoreCase) || header.Length < 10)
                return false;
            var version = ReadUInt32LittleEndian(header, 0);
            var encoding = ReadUInt32LittleEndian(header, 4);
            return version >= 4 && version <= 6 && encoding <= 3;
        }

        private static void AddChromiumPakInfo(List<ReportSection> sections, byte[] header)
        {
            var section = AddSection(sections, "Chromium resource pack");
            var version = ReadUInt32LittleEndian(header, 0);
            Add(section, "Version", version.ToString(CultureInfo.InvariantCulture));
            Add(section, "Text encoding", ChromiumPakEncoding(ReadUInt32LittleEndian(header, 4)));
            if (header.Length >= 10)
                Add(section, "Resource count", ReadUInt16LittleEndian(header, 8).ToString(CultureInfo.InvariantCulture));
            if (version >= 5 && header.Length >= 12)
                Add(section, "Alias count", ReadUInt16LittleEndian(header, 10).ToString(CultureInfo.InvariantCulture));
            Add(section, "Common use", "Chrome, Edge, Electron, Discord, and similar Chromium-based applications use .pak files for UI resources and localized strings.");
        }

        private static string ChromiumPakEncoding(uint value)
        {
            switch (value)
            {
                case 0: return "Binary";
                case 1: return "UTF-8";
                case 2: return "UTF-16";
                default: return "Unknown (" + value.ToString(CultureInfo.InvariantCulture) + ")";
            }
        }

        private static void AddMsgInfo(List<ReportSection> sections)
        {
            var section = AddSection(sections, "MSG structured storage");
            Add(section, "Format hint", "OLE compound message/resource container");
            Add(section, "Common use", "Outlook .msg messages and some installer/resource message files use this extension.");
        }

        private static string HexCompact(byte[] data, int offset, int count)
        {
            var end = Math.Min(data.Length, offset + count);
            var sb = new StringBuilder();
            for (var i = offset; i < end; i++)
                sb.Append(data[i].ToString("X2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
