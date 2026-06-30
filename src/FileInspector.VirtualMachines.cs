using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static string VirtualMachineMetadataTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".vmx":
                    return LooksLikeText(header) ? "VMware virtual machine configuration" : null;
                case ".vmsd":
                    return "VMware snapshot metadata";
                case ".vmxf":
                    return LooksLikeText(header) ? "VMware extended virtual machine metadata" : null;
                case ".nvram":
                    return "VMware virtual machine NVRAM";
                case ".scoreboard":
                    return "VMware virtual machine runtime scoreboard";
                default:
                    return null;
            }
        }

        private static void AddVirtualMachineMetadataInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var type = VirtualMachineMetadataTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Virtual machine metadata");
            Add(section, "Format hint", type);
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "VM folder", Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty));

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if ((ext == ".vmx" || ext == ".vmsd") && LooksLikeText(header))
                AddVmwareKeyValueInfo(section, Encoding.GetEncoding(1252).GetString(header));
            else if (ext == ".vmxf" && LooksLikeText(header))
                AddVmwareXmlInfo(section, Encoding.UTF8.GetString(header));
            else if (ext == ".nvram")
                Add(section, "Role", "Virtual firmware/NVRAM state used by a VMware virtual machine.");
            else if (ext == ".scoreboard")
                Add(section, "Role", "Runtime coordination/state file created while a VMware virtual machine is active or was recently active.");

            Add(section, "Notes", "VMware metadata is reported from safe headers and selected non-identity fields. FileDentify does not expose UUIDs, MAC addresses, or full host paths here.");
        }

        private static void AddVmwareKeyValueInfo(ReportSection section, string text)
        {
            var values = ParseVmwareKeyValues(text);
            AddVmwareValue(section, values, "displayName", "Display name");
            AddVmwareValue(section, values, "guestOS", "Guest OS hint");
            AddVmwareValue(section, values, "virtualHW.version", "Virtual hardware version");
            AddVmwareValue(section, values, "memsize", "Configured memory (MiB)");
            AddVmwareValue(section, values, "numvcpus", "Virtual CPUs");
            AddVmwareValue(section, values, "firmware", "Firmware");
            AddVmwareValue(section, values, "config.version", "Config version");

            var disks = values.Keys.Count(k => Regex.IsMatch(k, @"^(scsi|sata|ide)\d+:\d+\.fileName$", RegexOptions.IgnoreCase));
            var controllers = values.Keys.Count(k => Regex.IsMatch(k, @"^(scsi|sata|ide)\d+\.present$", RegexOptions.IgnoreCase) && IsTrue(values[k]));
            var networks = values.Keys.Count(k => Regex.IsMatch(k, @"^ethernet\d+\.present$", RegexOptions.IgnoreCase) && IsTrue(values[k]));
            var snapshots = values.Keys.Count(k => k.StartsWith("snapshot", StringComparison.OrdinalIgnoreCase));
            if (controllers > 0)
                Add(section, "Controller count", controllers.ToString(CultureInfo.InvariantCulture));
            if (disks > 0)
                Add(section, "Referenced disk files", disks.ToString(CultureInfo.InvariantCulture));
            if (networks > 0)
                Add(section, "Network adapters", networks.ToString(CultureInfo.InvariantCulture));
            if (snapshots > 0)
                Add(section, "Snapshot metadata entries", snapshots.ToString(CultureInfo.InvariantCulture));

            var deviceKinds = values.Keys
                .Where(k => k.EndsWith(".virtualDev", StringComparison.OrdinalIgnoreCase))
                .Select(k => values[k])
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToArray();
            if (deviceKinds.Length > 0)
                Add(section, "Virtual device types", string.Join(Environment.NewLine, deviceKinds));
        }

        private static void AddVmwareXmlInfo(ReportSection section, string text)
        {
            var root = Regex.Match(text ?? string.Empty, "<\\s*(?<name>[A-Za-z0-9_:-]+)");
            if (root.Success)
                Add(section, "Root element", root.Groups["name"].Value);
            AddSimpleXmlTagValue(section, text, "installError", "Tools install error");
            AddSimpleXmlTagValue(section, text, "updateCounter", "Tools update counter");
            if (Regex.IsMatch(text ?? string.Empty, "<\\s*vmxPathName\\b", RegexOptions.IgnoreCase))
                Add(section, "VMX reference", "present");
        }

        private static Dictionary<string, string> ParseVmwareKeyValues(string text)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in (text ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                var index = line.IndexOf('=');
                if (index <= 0)
                    continue;
                var key = line.Substring(0, index).Trim();
                var value = line.Substring(index + 1).Trim().Trim('"');
                if (IsSensitiveVmwareKey(key))
                    continue;
                values[key] = CleanMetadataText(value);
            }
            return values;
        }

        private static bool IsSensitiveVmwareKey(string key)
        {
            return key.IndexOf("uuid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf("macAddress", StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.EndsWith(".generatedAddress", StringComparison.OrdinalIgnoreCase) ||
                key.EndsWith(".address", StringComparison.OrdinalIgnoreCase) ||
                key.IndexOf("sasWWID", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddVmwareValue(ReportSection section, Dictionary<string, string> values, string key, string label)
        {
            string value;
            if (values.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
                Add(section, label, value);
        }

        private static bool IsTrue(string value)
        {
            return string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
