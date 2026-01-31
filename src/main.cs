using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

class Program
{
    static string[] ParseCommandLine(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return Array.Empty<string>();
        var parts = new List<string>();
        var current = new StringBuilder();
        bool inSingleQuotes = false;
        bool inDoubleQuotes = false;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (inSingleQuotes)
            {
                if (c == '\'')
                    inSingleQuotes = false;
                else
                    current.Append(c);
            }
            else if (inDoubleQuotes)
            {
                if (c == '"')
                    inDoubleQuotes = false;
                else if (c == '\\' && i + 1 < input.Length)
                {
                    char next = input[i + 1];
                    if (next == '"' || next == '\\')
                    {
                        current.Append(next);
                        i++;
                    }
                    else
                    {
                        current.Append('\\');
                        current.Append(next);
                        i++;
                    }
                }
                else
                    current.Append(c);
            }
            else
            {
                // Outside quotes: backslash escapes next char (append it and skip).
                if (c == '\\')
                {
                    if (i + 1 < input.Length)
                    {
                        current.Append(input[i + 1]);
                        i++;
                    }
                    else
                    {
                        current.Append('\\');
                    }
                }
                else if (c == '\'')
                    inSingleQuotes = true;
                else if (c == '"')
                    inDoubleQuotes = true;
                else if (c == ' ' || c == '\t')
                {
                    if (current.Length > 0)
                    {
                        parts.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                    current.Append(c);
            }
        }
        if (current.Length > 0)
            parts.Add(current.ToString());
        return parts.ToArray();
    }

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
            string[] parts = ParseCommandLine(input);

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
            else if (command == "pwd")
            {
                Console.WriteLine(Directory.GetCurrentDirectory());
            }
            else if (command == "cd")
            {
                string path = parts.Length > 1 ? parts[1] : "";
                if (!string.IsNullOrEmpty(path) && (path == "~" || path.StartsWith("~/")))
                {
                    string? home = Environment.GetEnvironmentVariable("HOME")
                        ?? Environment.GetEnvironmentVariable("USERPROFILE");
                    if (!string.IsNullOrEmpty(home))
                        path = path.Length == 1 ? home : Path.Combine(home, path.Substring(2));
                }
                if (!string.IsNullOrEmpty(path))
                {
                    if (Directory.Exists(path))
                        Directory.SetCurrentDirectory(path);
                    else
                        Console.WriteLine($"cd: {path}: No such file or directory");
                }
            }
            else if (command == "type")
            {
                string[] builtins = ["echo", "exit", "type", "pwd", "cd"];
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
                string fullCommand = "exec \"" + command.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                foreach (string arg in parts[1..])
                    fullCommand += " \"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
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
