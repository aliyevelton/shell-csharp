class Program
{
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
                if (builtins.Contains(name))
                {
                    Console.WriteLine($"{name} is a shell builtin");
                }
                else
                {
                    Console.WriteLine(string.IsNullOrEmpty(name) ? ": not found" : $"{name}: not found");
                }
            }
            else
            {
                Console.WriteLine($"{command}: not found");
            }
        }
    }
}
