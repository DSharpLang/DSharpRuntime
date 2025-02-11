using CodeOS.Modules;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DSharpRuntime.src
{
	public class DSharpInterpreter
	{
		public Dictionary<string, object> Variables { get; private set; } = new Dictionary<string, object>();
		private Stack<Dictionary<string, object>> scopeStack = new Stack<Dictionary<string, object>>();
		private Dictionary<string, IModule> importedModules = new Dictionary<string, IModule>();
		private Dictionary<string, IModule> availableModules = new Dictionary<string, IModule>();
		private Dictionary<string, Action> methods = new Dictionary<string, Action>();
		public object? LastResult { get; set; }

		public DSharpInterpreter()
		{
			// Enregistrer les modules disponibles
			RegisterModule(new StdFile());
			RegisterModule(new StdConv());
		}

		public void Interpret(string code)
		{
			var lines = code.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			var block = new List<string>();
			int blockDepth = 0;

			foreach (var line in lines)
			{
				var trimmedLine = line.Trim();

				if (trimmedLine.StartsWith("//"))
				{
					continue;
				}

				if (trimmedLine == "{")
				{
					blockDepth++;
					block.Add(trimmedLine);
				}
				else if (trimmedLine == "}")
				{
					blockDepth--;
					block.Add(trimmedLine);

					if (blockDepth == 0)
					{
						ExecuteBlock(block);
						block.Clear();
					}
				}
				else if (blockDepth > 0)
				{
					block.Add(trimmedLine);
				}
				else
				{
					ExecuteLine(trimmedLine);
				}
			}
		}

		private void ExecuteBlock(List<string> block)
		{
			EnterScope();
			foreach (var line in block)
			{
				ExecuteLine(line);
			}
			ExitScope();
		}

		private void ExecuteLine(string line)
		{
			if (line.StartsWith("//"))
			{
				return;
			}
			else if (line.StartsWith("import "))
			{
				ImportModule(line.Substring(7).Trim());
			}
			else if (line.StartsWith("module "))
			{
				RegisterModule(new DModule(line.Substring(7).Trim(), this));
			}
			else if (line.StartsWith("println(") && line.EndsWith(")"))
			{
				Println(line.Substring(8, line.Length - 9));
			}
			else if (line.StartsWith("printlnf(") && line.EndsWith(")"))
			{
				Printlnf(line.Substring(9, line.Length - 10));
			}
			else if (line.StartsWith("void "))
			{
				DeclareMethod(line.Substring(5).Trim());
			}
			else if (line == "{")
			{
				EnterScope();
			}
			else if (line == "}")
			{
				ExitScope();
			}
			else if (line.Contains("="))
			{
				AssignVariable(line);
			}
			else if (methods.ContainsKey(line))
			{
				methods[line]();
			}
			else
			{
				ExecuteModuleCommand(line);
			}
		}

		private void ImportModule(string moduleName)
		{
			if (importedModules.ContainsKey(moduleName))
			{
				return;
			}

			if (availableModules.ContainsKey(moduleName))
			{
				importedModules[moduleName] = availableModules[moduleName];
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
				return EvaluateStringExpression(expression.Substring(1, expression.Length - 2));
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
				foreach (var scope in scopeStack)
				{
					if (scope.ContainsKey(expression))
					{
						return scope[expression];
					}
				}
				return EvaluateModuleExpression(expression);
			}
		}

		private string EvaluateStringExpression(string expression)
		{
			var regex = new Regex(@"\$(\w+)");
			return regex.Replace(expression, match =>
			{
				var varName = match.Groups[1].Value;
				return Variables.ContainsKey(varName) ? Variables[varName].ToString() : match.Value;
			});
		}

		private void Println(string expression)
		{
			Console.WriteLine(EvaluateExpression(expression));
		}

		private void Printlnf(string expression)
		{
			var parts = SplitArguments(expression);
			var format = EvaluateExpression(parts[0].Trim()).ToString();
			var args = new object[parts.Length - 1];

			for (int i = 1; i < parts.Length; i++)
			{
				args[i - 1] = EvaluateExpression(parts[i].Trim());
			}

			var formattedString = format;
			for (int i = 0; i < args.Length; i++)
			{
				formattedString = formattedString.Replace("{}", args[i].ToString());
			}

			Console.WriteLine(formattedString);
		}

		private string[] SplitArguments(string input)
		{
			var regex = new Regex(@",(?=(?:[^""]*""[^""]*"")*[^""]*$)");
			return regex.Split(input);
		}

		private void AssignVariable(string line)
		{
			var parts = line.Split('=');
			var variableName = parts[0].Trim();
			var expression = parts[1].Trim();
			Variables[variableName] = EvaluateExpression(expression);
		}

		private void ExecuteModuleCommand(string line)
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

		private object EvaluateModuleExpression(string expression)
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

		private void DeclareMethod(string methodDeclaration)
		{
			var parts = methodDeclaration.Split(new[] { ' ' }, 2);
			var methodName = parts[0];
			var methodBody = parts[1].TrimStart('{').Trim();

			methods[methodName] = () => Interpret(methodBody);
		}

		private void EnterScope()
		{
			scopeStack.Push(new Dictionary<string, object>(Variables));
		}

		private void ExitScope()
		{
			if (scopeStack.Count > 0)
			{
				Variables = scopeStack.Pop();
			}
			else
			{
				throw new Exception("No scope to exit.");
			}
		}
	}

	public class DModule : IModule
	{
		public string Name { get; }
		private DSharpInterpreter _interpreter;
		private string? _code;

		public DModule(string name, DSharpInterpreter interpreter, string? code = null)
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



