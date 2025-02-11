using DSharpRuntime.src;

class Program
{
	static DSharpInterpreter dSharpInterpreter = new DSharpInterpreter();
	static void Main(string[] args)
	{
		if (args.Length == 0)
		{
			Console.WriteLine("Usage: DSharpRuntime <file>");
			return;
		}

		var code = File.ReadAllText(args[0]);

		dSharpInterpreter.Interpret(code);
	}
}