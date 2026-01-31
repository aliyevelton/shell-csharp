using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

class Program
{
    static bool IsExecutable(string path)
    {
        if (!File.Exists(path))
            return false;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return true;
        try
        {
            var mode = File.GetUnixFileMode(path);
            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch
        {
            return false;
        }
    }

    static bool TryFindExecutableInPath(string name, out string? fullPath)
    {
        fullPath = null;
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return false;
        string[] dirs = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (string dir in dirs)
        {
            string candidate = Path.Combine(dir, name);
            if (IsExecutable(candidate))
            {
                fullPath = Path.GetFullPath(candidate);
                return true;
            }
        }
        return false;
    }

    static void Main()
    {
        while (true)
        {
            Console.Write("$ ");

            string input = Console.ReadLine();
            string[] parts = input?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

            if (parts.Length == 0)
            {
                continue;
            }

            string command = parts[0];

            if (command == "exit")
            {
                break;
            }
            else if (command == "echo")
            {
                Console.WriteLine(parts.Length > 1 ? string.Join(" ", parts[1..]) : "");
            }
            else if (command == "type")
            {
                string[] builtins = ["echo", "exit", "type"];
                string name = parts.Length > 1 ? parts[1] : "";
                if (string.IsNullOrEmpty(name))
                {
                    Console.WriteLine(": not found");
                }
                else if (builtins.Contains(name))
                {
                    Console.WriteLine($"{name} is a shell builtin");
                }
                else if (TryFindExecutableInPath(name, out string? fullPath) && fullPath is not null)
                {
                    Console.WriteLine($"{name} is {fullPath}");
                }
                else
                {
                    Console.WriteLine($"{name}: not found");
                }
            }
            else if (TryFindExecutableInPath(command, out string? exePath) && exePath is not null)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/sh",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                psi.ArgumentList.Add("-c");
                string fullCommand = "exec " + command;
                foreach (string arg in parts[1..])
                    fullCommand += " \"" + arg.Replace("\"", "\\\"") + "\"";
                psi.ArgumentList.Add(fullCommand);
                using var process = Process.Start(psi);
                if (process is not null)
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    Console.Write(output);
                    Console.Write(error);
                }
            }
            else
            {
                Console.WriteLine($"{command}: not found");
            }
        }
    }
}
