using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FileDentify
{
    internal sealed class SingleInstanceService : IDisposable
    {
        private readonly Form owner;
        private readonly Action<string[]> receiveFiles;
        private readonly Thread listenerThread;
        private volatile bool stopping;

        private SingleInstanceService(Form owner, Action<string[]> receiveFiles)
        {
            this.owner = owner;
            this.receiveFiles = receiveFiles;
            listenerThread = new Thread(ListenLoop);
            listenerThread.IsBackground = true;
            listenerThread.Name = "FileDentify single-instance listener";
            listenerThread.Start();
        }

        public static SingleInstanceService Start(Form owner, Action<string[]> receiveFiles)
        {
            return new SingleInstanceService(owner, receiveFiles);
        }

        public static bool ArePlainGuiInputArguments(string[] args)
        {
            return args != null &&
                args.Length > 0 &&
                !(args.Length == 1 && SavedReportStore.IsSavedReportPath(args[0])) &&
                args.All(arg => !string.IsNullOrWhiteSpace(arg) &&
                    !arg.StartsWith("-", StringComparison.Ordinal) &&
                    !arg.Equals("/?", StringComparison.OrdinalIgnoreCase));
        }

        public static bool TrySendFilesToExistingInstance(string[] args)
        {
            if (!ArePlainGuiInputArguments(args))
                return false;

            try
            {
                using (var client = new NamedPipeClientStream(".", PipeName(), PipeDirection.Out))
                {
                    client.Connect(250);
                    var message = new JavaScriptSerializer().Serialize(new OpenFilesMessage
                    {
                        Files = args
                            .Where(item => !string.IsNullOrWhiteSpace(item))
                            .Select(Path.GetFullPath)
                            .ToArray()
                    });
                    using (var writer = new StreamWriter(client, Encoding.UTF8))
                    {
                        writer.Write(message);
                    }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            stopping = true;
            try
            {
                using (var client = new NamedPipeClientStream(".", PipeName(), PipeDirection.Out))
                    client.Connect(50);
            }
            catch
            {
            }
        }

        private void ListenLoop()
        {
            while (!stopping)
            {
                try
                {
                    using (var server = new NamedPipeServerStream(PipeName(), PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None))
                    {
                        server.WaitForConnection();
                        if (stopping)
                            continue;

                        string message;
                        using (var reader = new StreamReader(server, Encoding.UTF8))
                            message = reader.ReadToEnd();

                        var payload = new JavaScriptSerializer().Deserialize<OpenFilesMessage>(message);
                        var files = payload == null || payload.Files == null
                            ? new string[0]
                            : payload.Files.Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
                        if (files.Length == 0 || owner.IsDisposed)
                            continue;

                        owner.BeginInvoke((MethodInvoker)delegate
                        {
                            if (owner.IsDisposed)
                                return;
                            try
                            {
                                owner.WindowState = FormWindowState.Normal;
                                owner.Activate();
                                receiveFiles(files);
                            }
                            catch
                            {
                            }
                        });
                    }
                }
                catch
                {
                    if (!stopping)
                        Thread.Sleep(200);
                }
            }
        }

        private static string PipeName()
        {
            var basis = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/').ToUpperInvariant();
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(basis));
                return "FileDentify-" + BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private sealed class OpenFilesMessage
        {
            public string[] Files { get; set; }
        }
    }
}
