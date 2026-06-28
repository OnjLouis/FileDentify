using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

internal static class FdConsoleStub
{
    private const string StubEnvironmentVariable = "FILEDENTIFY_CONSOLE_STUB";

    private static int Main(string[] args)
    {
        var directory = AppDomain.CurrentDomain.BaseDirectory;
        var target = Path.Combine(directory, "FileDentify.exe");
        if (!File.Exists(target))
        {
            Console.Error.WriteLine("FileDentify.exe was not found beside fd.com.");
            return 2;
        }

        var forwarded = BuildForwardedArguments(args ?? new string[0]);
        var startInfo = new ProcessStartInfo(target)
        {
            UseShellExecute = false,
            Arguments = JoinArguments(forwarded),
            WorkingDirectory = Environment.CurrentDirectory
        };
        startInfo.EnvironmentVariables[StubEnvironmentVariable] = "1";

        try
        {
            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    Console.Error.WriteLine("FileDentify.exe could not be started.");
                    return 2;
                }

                process.WaitForExit();
                return process.ExitCode;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
    }

    private static string[] BuildForwardedArguments(string[] args)
    {
        if (args.Length == 0)
            return new[] { "--help" };

        var normalized = DropShortTerminalSwitch(args);

        if (HasExplicitMode(normalized))
            return normalized;

        var forwarded = new List<string>();
        forwarded.Add("--terminal");
        forwarded.AddRange(normalized);
        return forwarded.ToArray();
    }

    private static string[] DropShortTerminalSwitch(IEnumerable<string> args)
    {
        var normalized = new List<string>();
        foreach (var arg in args)
        {
            if (arg.Equals("-t", StringComparison.OrdinalIgnoreCase))
                continue;
            normalized.Add(arg);
        }
        return normalized.ToArray();
    }

    private static bool HasExplicitMode(IEnumerable<string> args)
    {
        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg))
                continue;

            if (arg.Equals("--terminal", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/?", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--version", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--update", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-u", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--report", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-r", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--html-report", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--folder-report", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--viewer-output", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--viewer", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--close", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--install-sendto", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--uninstall-sendto", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--install-desktop", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--uninstall-desktop", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string JoinArguments(IEnumerable<string> args)
    {
        var parts = new List<string>();
        foreach (var arg in args)
            parts.Add(QuoteArgument(arg ?? string.Empty));
        return string.Join(" ", parts.ToArray());
    }

    private static string QuoteArgument(string arg)
    {
        if (arg.Length == 0)
            return "\"\"";

        var needsQuotes = arg.IndexOfAny(new[] { ' ', '\t', '\n', '\r', '"' }) >= 0;
        if (!needsQuotes)
            return arg;

        var result = "\"";
        var backslashes = 0;
        foreach (var c in arg)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            if (c == '"')
            {
                result += new string('\\', backslashes * 2 + 1);
                result += c;
                backslashes = 0;
                continue;
            }

            if (backslashes > 0)
            {
                result += new string('\\', backslashes);
                backslashes = 0;
            }
            result += c;
        }

        if (backslashes > 0)
            result += new string('\\', backslashes * 2);
        return result + "\"";
    }
}
