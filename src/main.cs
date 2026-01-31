class Program
{
    static void Main()
    {
        while (true)
        {
            Console.Write("$ ");

            string command = Console.ReadLine();

            if (command == "exit")
            {
                break;
            }
            else
            {
                Console.WriteLine($"{command}: not found");
            }
        }
    }
}
