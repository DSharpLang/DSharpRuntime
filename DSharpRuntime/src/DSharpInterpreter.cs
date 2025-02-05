using CodeOS.Modules;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DSharpRuntime.src
{
	public class DSharpInterpreter
	{
		public Dictionary<string, object> Variables { get; private set; } = new Dictionary<string, object>();
		private Dictionary<string, IModule> importedModules = new Dictionary<string, IModule>();
		private Dictionary<string, IModule> availableModules = new Dictionary<string, IModule>();
		public object LastResult { get; set; }

		public DSharpInterpreter()
		{
			// Enregistrer les modules disponibles
			RegisterModule(new StdFile());
			RegisterModule(new StdConv());
		}

		public void Interpret(string code)
		{
			var lines = code.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (var line in lines)
			{
				ExecuteLine(line.Trim());
			}
		}

		private void ExecuteLine(string line)
		{
			if (line.StartsWith("//"))
			{
				return;
			}
			else if (line.StartsWith("import "))
			{
				var moduleName = line.Substring(7).Trim();
				ImportModule(moduleName);
			}
			else if (line.StartsWith("module "))
			{
				var moduleName = line.Substring(7).Trim();
				RegisterModule(new DModule(moduleName, this));
			}
			else if (line.StartsWith("println(") && line.EndsWith(")"))
			{
				var expression = line.Substring(8, line.Length - 9);
				Console.WriteLine(EvaluateExpression(expression));
			}
			else if (line.Contains("="))
			{
				var parts = line.Split('=');
				var variableName = parts[0].Trim();
				var expression = parts[1].Trim();
				Variables[variableName] = EvaluateExpression(expression);
			}
			else
			{
				var parts = line.Split(new[] { '.' }, 2);
				if (parts.Length == 2)
				{
					var moduleName = parts[0];
					var command = parts[1];
					if (importedModules.ContainsKey(moduleName))
					{
						importedModules[moduleName].Execute(command, this);
					}
					else
					{
						throw new Exception($"Module '{moduleName}' not imported.");
					}
				}
				else
				{
					throw new Exception($"Unknown command: {line}");
				}
			}
		}

		private void ImportModule(string moduleName)
		{
			if (importedModules.ContainsKey(moduleName))
			{
				//Console.WriteLine($"Module '{moduleName}' is already imported.");
				return;
			}

			if (availableModules.ContainsKey(moduleName))
			{
				importedModules[moduleName] = availableModules[moduleName];
				//Console.WriteLine($"Module '{moduleName}' imported successfully.");
			}
			else
			{
				throw new Exception($"Unknown module: {moduleName}");
			}
		}

		public void RegisterModule(IModule module)
		{
			availableModules[module.Name] = module;
		}

		public object EvaluateExpression(string expression)
		{
			if (expression.StartsWith("\"") && expression.EndsWith("\""))
			{
				return expression.Substring(1, expression.Length - 2);
			}
			else if (int.TryParse(expression, out int intValue))
			{
				return intValue;
			}
			else if (Variables.ContainsKey(expression))
			{
				return Variables[expression];
			}
			else
			{
				var parts = expression.Split(new[] { '.' }, 2);
				if (parts.Length == 2)
				{
					var moduleName = parts[0];
					var command = parts[1];
					if (importedModules.ContainsKey(moduleName))
					{
						importedModules[moduleName].Execute(command, this);
						return LastResult;
					}
					else
					{
						throw new Exception($"Module '{moduleName}' not imported.");
					}
				}
				else
				{
					throw new Exception($"Unknown expression: {expression}");
				}
			}
		}
	}

	public class DModule : IModule
	{
		public string Name { get; }
		private DSharpInterpreter _interpreter;
		private string _code;

		public DModule(string name, DSharpInterpreter interpreter, string code = null)
		{
			Name = name;
			_interpreter = interpreter;
			_code = code;
		}

		public void Execute(string command, DSharpInterpreter interpreter)
		{
			if (_code != null)
			{
				interpreter.Interpret(_code);
				_code = null;
			}

			var parts = command.Split('=');
			if (parts.Length == 2)
			{
				var variableName = parts[0].Trim();
				var expression = parts[1].Trim();
				_interpreter.Variables[variableName] = _interpreter.EvaluateExpression(expression);
			}
			else
			{
				throw new Exception($"Unknown command for module '{Name}': {command}");
			}
		}
	}
}
