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
            else
            {
                Console.WriteLine($"{command}: not found");
            }
        }
    }
}
