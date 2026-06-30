using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;

namespace FileDentify
{
    internal static class TemporaryFileService
    {
        private const string LegacyLibmagicPrefix = "FileDentify-libmagic-";
        private const string LegacyTolkPrefix = "FileDentify-tolk-";
        private static readonly object Sync = new object();
        private static bool cleanupRegistered;

        public static string RootDirectory
        {
            get { return Path.Combine(Path.GetTempPath(), "FileDentify"); }
        }

        public static string CurrentProcessDirectory
        {
            get { return Path.Combine(RootDirectory, Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture)); }
        }

        public static void CleanupForLaunch()
        {
            RegisterCleanup();
            CleanupStaleArtifacts();
        }

        public static string GetProcessDirectory(string componentName)
        {
            RegisterCleanup();
            var dir = Path.Combine(CurrentProcessDirectory, SafeName(componentName));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static void CleanupForExit()
        {
            CleanupCurrentProcessArtifacts();
            CleanupStaleArtifacts();
            ScheduleDeferredCleanupForCurrentProcess();
        }

        public static bool TryRunCleanupHelper(string[] args)
        {
            if (args == null || args.Length < 3 || !args[0].Equals("--cleanup-temp", StringComparison.OrdinalIgnoreCase))
                return false;

            int ownerProcessId;
            if (!int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out ownerProcessId))
                return true;

            var target = args[2];
            if (!IsPathUnderTempRoot(target))
                return true;

            WaitForProcessExit(ownerProcessId, 8000);
            TryDeleteDirectory(target);
            TryDeleteEmptyDirectory(RootDirectory);
            return true;
        }

        public static void CleanupStaleArtifacts()
        {
            try
            {
                var temp = Path.GetTempPath();
                CleanupLegacyPattern(temp, LegacyLibmagicPrefix);
                CleanupLegacyPattern(temp, LegacyTolkPrefix);
                CleanupProcessRoot();
            }
            catch
            {
            }
        }

        private static void RegisterCleanup()
        {
            lock (Sync)
            {
                if (cleanupRegistered)
                    return;

                AppDomain.CurrentDomain.ProcessExit += delegate { CleanupForExit(); };
                cleanupRegistered = true;
            }
        }

        private static void CleanupCurrentProcessArtifacts()
        {
            TryDeleteDirectory(CurrentProcessDirectory);
            TryDeleteEmptyDirectory(RootDirectory);
        }

        private static void CleanupLegacyPattern(string temp, string prefix)
        {
            foreach (var dir in SafeGetDirectories(temp, prefix + "*"))
            {
                var pid = ParseLegacyProcessId(Path.GetFileName(dir), prefix);
                if (pid.HasValue && IsProcessRunning(pid.Value))
                    continue;

                TryDeleteDirectory(dir);
            }
        }

        private static void CleanupProcessRoot()
        {
            if (!Directory.Exists(RootDirectory))
                return;

            foreach (var dir in SafeGetDirectories(RootDirectory, "*"))
            {
                var name = Path.GetFileName(dir);
                int pid;
                if (!int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out pid))
                    continue;

                if (pid == Process.GetCurrentProcess().Id || IsProcessRunning(pid))
                    continue;

                TryDeleteDirectory(dir);
            }

            TryDeleteEmptyDirectory(RootDirectory);
        }

        private static string[] SafeGetDirectories(string path, string pattern)
        {
            try
            {
                if (!Directory.Exists(path))
                    return new string[0];
                return Directory.GetDirectories(path, pattern);
            }
            catch
            {
                return new string[0];
            }
        }

        private static int? ParseLegacyProcessId(string name, string prefix)
        {
            if (string.IsNullOrEmpty(name) || !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return null;

            int pid;
            if (int.TryParse(name.Substring(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out pid))
                return pid;
            return null;
        }

        private static bool IsProcessRunning(int processId)
        {
            if (processId <= 0)
                return false;

            try
            {
                using (Process.GetProcessById(processId))
                    return true;
            }
            catch
            {
                return false;
            }
        }

        private static void WaitForProcessExit(int processId, int timeoutMs)
        {
            try
            {
                using (var process = Process.GetProcessById(processId))
                    process.WaitForExit(timeoutMs);
            }
            catch
            {
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
            }
        }

        private static void TryDeleteEmptyDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length == 0)
                    Directory.Delete(path, false);
            }
            catch
            {
            }
        }

        private static void ScheduleDeferredCleanupForCurrentProcess()
        {
            try
            {
                var target = CurrentProcessDirectory;
                if (!Directory.Exists(target))
                    return;

                var exe = Assembly.GetExecutingAssembly().Location;
                var psi = new ProcessStartInfo(exe, "--cleanup-temp " + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) + " " + Quote(target));
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            catch
            {
            }
        }

        private static bool IsPathUnderTempRoot(string path)
        {
            try
            {
                var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var root = Path.GetFullPath(RootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return fullPath.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                    fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "component";

            foreach (var c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '-');
            return value;
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
