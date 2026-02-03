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

    static (string[] args, string? redirectStdout, bool stdoutAppend, string? redirectStderr, bool stderrAppend) ExtractRedirections(string[] parts)
    {
        var args = new List<string>();
        string? redirectStdout = null;
        bool stdoutAppend = false;
        string? redirectStderr = null;
        bool stderrAppend = false;
        for (int i = 0; i < parts.Length; i++)
        {
            if ((parts[i] == ">" || parts[i] == "1>") && i + 1 < parts.Length)
            {
                redirectStdout = parts[i + 1];
                stdoutAppend = false;
                i++;
            }
            else if ((parts[i] == ">>" || parts[i] == "1>>") && i + 1 < parts.Length)
            {
                redirectStdout = parts[i + 1];
                stdoutAppend = true;
                i++;
            }
            else if (parts[i] == "2>" && i + 1 < parts.Length)
            {
                redirectStderr = parts[i + 1];
                stderrAppend = false;
                i++;
            }
            else if (parts[i] == "2>>" && i + 1 < parts.Length)
            {
                redirectStderr = parts[i + 1];
                stderrAppend = true;
                i++;
            }
            else
            {
                args.Add(parts[i]);
            }
        }
        return (args.ToArray(), redirectStdout, stdoutAppend, redirectStderr, stderrAppend);
    }

    static void WithRedirects(string? stdoutFile, bool stdoutAppend, string? stderrFile, bool stderrAppend, Action run)
    {
        TextWriter? savedOut = null, savedErr = null;
        StreamWriter? outStream = null, errStream = null;
        if (!string.IsNullOrEmpty(stdoutFile))
        {
            savedOut = Console.Out;
            outStream = new StreamWriter(stdoutFile, append: stdoutAppend);
            Console.SetOut(outStream);
        }
        if (!string.IsNullOrEmpty(stderrFile))
        {
            savedErr = Console.Error;
            errStream = new StreamWriter(stderrFile, append: stderrAppend);
            Console.SetError(errStream);
        }
        try { run(); }
        finally
        {
            if (outStream is not null && savedOut is not null) { Console.SetOut(savedOut); outStream.Dispose(); }
            if (errStream is not null && savedErr is not null) { Console.SetError(savedErr); errStream.Dispose(); }
        }
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

    static string? CompleteBuiltinPrefix(string word)
    {
        bool echoMatch = "echo".StartsWith(word, StringComparison.Ordinal);
        bool exitMatch = "exit".StartsWith(word, StringComparison.Ordinal);
        if (echoMatch && !exitMatch) return "echo ";
        if (exitMatch && !echoMatch) return "exit ";
        return null;
    }

    static string? ReadLineWithCompletion()
    {
        Console.Write("$ ");
        Console.Out.Flush();

        if (Console.IsInputRedirected)
        {
            string? inputLine = Console.ReadLine();
            if (string.IsNullOrEmpty(inputLine)) return inputLine;
            int tabIndex = inputLine.IndexOf('\t');
            if (tabIndex >= 0)
            {
                string word = inputLine.Substring(0, tabIndex);
                string? completion = CompleteBuiltinPrefix(word);
                if (completion is not null)
                    inputLine = completion + inputLine.Substring(tabIndex + 1);
                else
                    inputLine = inputLine.Remove(tabIndex, 1);
            }
            return inputLine;
        }

        int promptLeft = Console.CursorLeft;
        int promptTop = Console.CursorTop;
        int startLeft = Console.CursorLeft;
        int startTop = Console.CursorTop;
        var line = new StringBuilder();
        int cursorPosition = 0;
        int displayedLength = 0;

        void Redraw()
        {
            Console.SetCursorPosition(promptLeft, promptTop);
            Console.Write("$ ");
            Console.Write(line.ToString());
            int padding = displayedLength - line.Length;
            if (padding > 0)
                Console.Write(new string(' ', padding));
            displayedLength = line.Length;
            Console.SetCursorPosition(startLeft + cursorPosition, startTop);
        }

        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return line.Length > 0 ? line.ToString() : null;
            }
            if (key.Key == ConsoleKey.Tab)
            {
                string word = line.ToString().Substring(0, cursorPosition);
                string? completion = CompleteBuiltinPrefix(word);
                if (completion is not null)
                {
                    line.Remove(0, cursorPosition);
                    line.Insert(0, completion);
                    cursorPosition = completion.Length;
                    Redraw();
                }
                continue;
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (cursorPosition > 0)
                {
                    line.Remove(cursorPosition - 1, 1);
                    cursorPosition--;
                    Redraw();
                }
                continue;
            }
            if (key.KeyChar >= ' ' && key.KeyChar <= '~')
            {
                line.Insert(cursorPosition, key.KeyChar);
                cursorPosition++;
                Redraw();
            }
        }
    }

    static void Main()
    {
        while (true)
        {
            string? input = ReadLineWithCompletion();
            string[] parts = ParseCommandLine(input);

            if (parts.Length == 0)
            {
                continue;
            }

            (string[] args, string? redirectStdout, bool redirectStdoutAppend, string? redirectStderr, bool redirectStderrAppend) = ExtractRedirections(parts);
            if (args.Length == 0)
            {
                continue;
            }

            string command = args[0];

            if (command == "exit")
            {
                break;
            }
            else if (command == "echo")
            {
                WithRedirects(redirectStdout, redirectStdoutAppend, redirectStderr, redirectStderrAppend, () =>
                    Console.WriteLine(args.Length > 1 ? string.Join(" ", args[1..]) : ""));
            }
            else if (command == "pwd")
            {
                WithRedirects(redirectStdout, redirectStdoutAppend, redirectStderr, redirectStderrAppend, () =>
                    Console.WriteLine(Directory.GetCurrentDirectory()));
            }
            else if (command == "cd")
            {
                WithRedirects(null, false, redirectStderr, redirectStderrAppend, () =>
                {
                    string path = args.Length > 1 ? args[1] : "";
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
                });
            }
            else if (command == "type")
            {
                string[] builtins = ["echo", "exit", "type", "pwd", "cd"];
                string name = args.Length > 1 ? args[1] : "";
                WithRedirects(redirectStdout, redirectStdoutAppend, redirectStderr, redirectStderrAppend, () =>
                {
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
                });
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
                foreach (string arg in args[1..])
                    fullCommand += " \"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                psi.ArgumentList.Add(fullCommand);
                using var process = Process.Start(psi);
                if (process is not null)
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (redirectStdout is not null)
                    {
                        if (redirectStdoutAppend)
                            File.AppendAllText(redirectStdout, output);
                        else
                            File.WriteAllText(redirectStdout, output);
                    }
                    else
                        Console.Write(output);
                    if (redirectStderr is not null)
                    {
                        if (redirectStderrAppend)
                            File.AppendAllText(redirectStderr, error);
                        else
                            File.WriteAllText(redirectStderr, error);
                    }
                    else
                        Console.Write(error);
                }
            }
            else
            {
                WithRedirects(null, false, redirectStderr, redirectStderrAppend, () =>
                    Console.WriteLine($"{command}: not found"));
            }
        }
    }
}
