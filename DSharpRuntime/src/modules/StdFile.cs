using DSharpRuntime.src;
using System;
using System.IO;

namespace CodeOS.Modules
{
	public class StdFile : IModule
	{
		public string Name => "std::file";

		public void Execute(string command, DSharpInterpreter interpreter)
		{
			if (command.StartsWith("write(") && command.EndsWith(")"))
			{
				var parameters = command.Substring(6, command.Length - 7).Split(',');
				if (parameters.Length == 2)
				{
					var path = interpreter.EvaluateExpression(parameters[0].Trim()).ToString();
					var content = interpreter.EvaluateExpression(parameters[1].Trim()).ToString();
					Write(path, content);
				}
				else
				{
					throw new Exception("Invalid parameters for stdfile.write");
				}
			}
			else if (command.StartsWith("read(") && command.EndsWith(")"))
			{
				var parameter = command.Substring(5, command.Length - 6).Trim();
				var path = interpreter.EvaluateExpression(parameter).ToString();
				var content = Read(path);
				interpreter.LastResult = content;
			}
			else
			{
				throw new Exception($"Unknown command for stdfile: {command}");
			}
		}

		private void Write(string path, string content)
		{
			try
			{
				File.WriteAllText(path, content);
				//Console.WriteLine($"File written successfully: {path}");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error writing file: {ex.Message}");
			}
		}

		private string Read(string path)
		{
			try
			{
				if (File.Exists(path))
				{
					return File.ReadAllText(path);
				}
				else
				{
					throw new FileNotFoundException($"File not found: {path}");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error reading file: {ex.Message}");
				return null;
			}
		}
	}
}
