using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace FileDentify
{
    internal static class ScreenReaderOutput
    {
        private static readonly object Sync = new object();
        private static readonly object DetectLock = new object();
        private static readonly TimeSpan DetectedScreenReadersCacheAge = TimeSpan.FromSeconds(2);
        private static readonly Dictionary<string, string> SupportedScreenReaderProcessNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "nvda", "NVDA" },
            { "jfw", "JAWS" },
            { "narrator", "Narrator" },
            { "supernova", "SuperNova" },
            { "zoomtext", "ZoomText" },
            { "fusion", "Fusion" },
            { "systemaccess", "System Access" }
        };

        private static bool loadAttempted;
        private static bool loaded;
        private static string loadError = "Tolk is not loaded.";
        private static string extractedDirectory;
        private static bool cleanupRegistered;
        private static List<string> cachedDetectedScreenReaders = new List<string>();
        private static DateTime cachedDetectedScreenReadersUtc = DateTime.MinValue;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("Tolk.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Load();

        [DllImport("Tolk.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Tolk_IsLoaded();

        [DllImport("Tolk.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Unload();

        [DllImport("Tolk.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_TrySAPI([MarshalAs(UnmanagedType.I1)] bool trySAPI);

        [DllImport("Tolk.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_PreferSAPI([MarshalAs(UnmanagedType.I1)] bool preferSAPI);

        [DllImport("Tolk.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Tolk_Output([MarshalAs(UnmanagedType.LPWStr)] string text, [MarshalAs(UnmanagedType.I1)] bool interrupt);

        public static bool TrySpeakForActiveScreenReader(string text)
        {
            string error;
            return TrySpeakForActiveScreenReader(text, true, out error);
        }

        public static bool TrySpeakForActiveScreenReader(string text, bool interrupt, out string error)
        {
            if (!IsActiveScreenReaderDetected)
            {
                error = "No supported screen reader process is active.";
                return false;
            }

            return TrySpeak(text, interrupt, out error);
        }

        public static bool IsActiveScreenReaderDetected
        {
            get { return DetectSupportedScreenReaders().Count > 0; }
        }

        public static List<string> DetectSupportedScreenReaders()
        {
            lock (DetectLock)
            {
                if ((DateTime.UtcNow - cachedDetectedScreenReadersUtc) <= DetectedScreenReadersCacheAge)
                    return new List<string>(cachedDetectedScreenReaders);
            }

            var found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var process in Process.GetProcesses())
                {
                    using (process)
                    {
                        string label;
                        if (SupportedScreenReaderProcessNames.TryGetValue(process.ProcessName ?? string.Empty, out label) && !string.IsNullOrWhiteSpace(label))
                            found.Add(label);
                    }
                }
            }
            catch
            {
            }

            var result = found.ToList();
            lock (DetectLock)
            {
                cachedDetectedScreenReaders = new List<string>(result);
                cachedDetectedScreenReadersUtc = DateTime.UtcNow;
            }

            return result;
        }

        public static ThirdPartyNotice NoticeEntry()
        {
            return new ThirdPartyNotice(
                "Tolk screen-reader library notice",
                "FileDentify embeds Tolk and its NVDA controller client companion DLL for optional screen-reader speech announcements." + Environment.NewLine + Environment.NewLine +
                ReadResourceText("FileDentify.Embedded.Tolk.LICENSE.txt"));
        }

        public static void Shutdown()
        {
            lock (Sync)
            {
                if (loaded)
                {
                    try { Tolk_Unload(); } catch { }
                }
                try { SetDllDirectory(null); } catch { }
                loaded = false;
            }
        }

        private static bool TrySpeak(string text, bool interrupt, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return true;

            if (!EnsureLoaded(out error))
                return false;

            try
            {
                if (!Tolk_Output(text, interrupt))
                {
                    error = "Tolk did not accept the message.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool EnsureLoaded(out string error)
        {
            lock (Sync)
            {
                if (loaded)
                {
                    error = string.Empty;
                    return true;
                }

                if (loadAttempted)
                {
                    error = loadError;
                    return false;
                }

                loadAttempted = true;
                try
                {
                    var dir = EnsureExtracted();
                    SetDllDirectory(dir);
                    Tolk_TrySAPI(false);
                    Tolk_PreferSAPI(false);
                    Tolk_Load();
                    loaded = Tolk_IsLoaded();
                    if (loaded)
                    {
                        error = string.Empty;
                        return true;
                    }

                    loadError = "Tolk did not report itself as loaded.";
                }
                catch (DllNotFoundException)
                {
                    loadError = "Tolk.dll could not be loaded from FileDentify's embedded resources.";
                }
                catch (BadImageFormatException)
                {
                    loadError = "The embedded Tolk.dll is not compatible with this FileDentify build.";
                }
                catch (Exception ex)
                {
                    loadError = ex.Message;
                }

                error = loadError;
                return false;
            }
        }

        private static string EnsureExtracted()
        {
            if (!string.IsNullOrEmpty(extractedDirectory) && Directory.Exists(extractedDirectory))
                return extractedDirectory;

            var dir = TemporaryFileService.GetProcessDirectory("tolk");
            Directory.CreateDirectory(dir);
            ExtractResource("FileDentify.Embedded.nvdaControllerClient64.dll", Path.Combine(dir, "nvdaControllerClient64.dll"));
            ExtractResource("FileDentify.Embedded.Tolk.dll", Path.Combine(dir, "Tolk.dll"));
            extractedDirectory = dir;

            if (!cleanupRegistered)
            {
                AppDomain.CurrentDomain.ProcessExit += delegate
                {
                    Shutdown();
                    Cleanup();
                };
                cleanupRegistered = true;
            }

            return extractedDirectory;
        }

        private static void ExtractResource(string name, string path)
        {
            if (File.Exists(path))
                return;

            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (input == null)
                    throw new InvalidOperationException("Missing embedded resource: " + name);
                using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                    input.CopyTo(output);
            }
        }

        private static string ReadResourceText(string name)
        {
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (input == null)
                    return "Missing embedded notice: " + name;
                using (var reader = new StreamReader(input, Encoding.UTF8, true))
                    return reader.ReadToEnd().TrimEnd();
            }
        }

        private static void Cleanup()
        {
            try
            {
                if (!string.IsNullOrEmpty(extractedDirectory) && Directory.Exists(extractedDirectory))
                    Directory.Delete(extractedDirectory, true);
            }
            catch
            {
            }
        }
    }
}
