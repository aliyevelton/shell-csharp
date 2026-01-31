class Program
{
    static void Main()
    {
        Console.Write("$ ");

        string command = Console.ReadLine();

        Console.WriteLine($"{command}: not found");
    }
}
