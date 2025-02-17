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
		private Dictionary<string, StructDefinition> structs = new Dictionary<string, StructDefinition>();
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
			else if (line.StartsWith("struct "))
			{
				DeclareStruct(line.Substring(7).Trim());
			}
			else if (line.StartsWith("field "))
			{
				// Ignore field definitions in ExecuteLine
				return;
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
			else if (line.Contains("{") && line.Contains("}"))
			{
				var parts = line.Split(new[] { '{', '}' }, StringSplitOptions.RemoveEmptyEntries);
				var structName = parts[0].Trim();
				var structBody = parts[1].Trim();
				InstantiateStruct(structName, structBody);
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
			else if (expression.EndsWith("[]"))
			{
				return EvaluateArrayExpression(expression);
			}
			else if (Variables.ContainsKey(expression))
			{
				return Variables[expression];
			}
			else if (expression.Contains("."))
			{
				if (IsStructExpression(expression))
				{
					return EvaluateStructExpression(expression);
				}
				else
				{
					return EvaluateModuleExpression(expression);
				}
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
				throw new Exception($"Unknown expression: {expression}");
			}
		}

		private bool IsStructExpression(string expression)
		{
			var parts = expression.Split('.');
			return Variables.ContainsKey(parts[0]) && Variables[parts[0]] is Dictionary<string, object>;
		}

		private object EvaluateArrayExpression(string expression)
		{
			var arrayName = expression.Substring(0, expression.Length - 2);
			if (Variables.ContainsKey(arrayName) && Variables[arrayName] is List<object> arrayInstance)
			{
				return arrayInstance;
			}
			else
			{
				throw new Exception($"Array '{arrayName}' not found");
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

		private object EvaluateStructField(Dictionary<string, object> structInstance, string fieldName)
		{
			if (structInstance.ContainsKey(fieldName))
			{
				return structInstance[fieldName];
			}
			else
			{
				throw new Exception($"Field '{fieldName}' not found in struct");
			}
		}

		private object EvaluateStructExpression(string expression)
		{
			var parts = expression.Split('.');
			var structName = parts[0];
			var fieldName = parts[1];

			if (Variables.ContainsKey(structName) && Variables[structName] is Dictionary<string, object> structInstance)
			{
				return EvaluateStructField(structInstance, fieldName);
			}
			else
			{
				throw new Exception($"Struct '{structName}' not found");
			}
		}

		private object EvaluateModuleCommand(string moduleName, string command)
		{
			importedModules[moduleName].Execute(command, this);
			return LastResult;
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

			// Replace each occurrence of '{}' with {0}, {1}, etc.
			int index = 0;
			format = Regex.Replace(format, @"{}", _ => $"{{{index++}}}");

			Console.WriteLine(string.Format(format, args));
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

			if (expression.StartsWith("new struct "))
			{
				var structName = expression.Substring(11).Trim();
				Variables[variableName] = InstantiateStruct(structName);
			}
			else if (expression.Contains("{") && expression.Contains("}"))
			{
				var structName = expression.Substring(0, expression.IndexOf('{')).Trim();
				var structBody = expression.Substring(expression.IndexOf('{')).Trim();
				Variables[variableName] = InstantiateStruct(structName, structBody);
			}
			else
			{
				Variables[variableName] = EvaluateExpression(expression);
			}
		}

		private object InstantiateStruct(string structName)
		{
			if (!structs.ContainsKey(structName))
			{
				throw new Exception($"Struct '{structName}' not defined");
			}

			var structDefinition = structs[structName];
			var structInstance = new Dictionary<string, object>();

			foreach (var field in structDefinition.Fields.Keys)
			{
				structInstance[field] = null;
			}

			return structInstance;
		}

		private object InstantiateStruct(string structName, string structBody)
		{
			if (!structs.ContainsKey(structName))
			{
				throw new Exception($"Struct '{structName}' not defined");
			}

			var structDefinition = structs[structName];
			var structInstance = new Dictionary<string, object>();
			var fieldAssignments = structBody.Trim('{', '}').Split(',');

			foreach (var assignment in fieldAssignments)
			{
				var fieldParts = assignment.Split('=');
				var fieldName = fieldParts[0].Trim();
				var fieldValue = EvaluateExpression(fieldParts[1].Trim());

				if (structDefinition.Fields.ContainsKey(fieldName))
				{
					structInstance[fieldName] = fieldValue;
				}
				else
				{
					throw new Exception($"Field '{fieldName}' not defined in struct '{structName}'");
				}
			}

			return structInstance;
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

		private void DeclareStruct(string structDeclaration)
		{
			var parts = structDeclaration.Split(new[] { ' ' }, 2);
			var structName = parts[0];
			var structBody = parts[1].Trim().TrimStart('{').Trim();

			var fields = new Dictionary<string, string>();
			var fieldLines = structBody.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (var fieldLine in fieldLines)
			{
				var fieldParts = fieldLine.Trim().Split(' ');
				if (fieldParts.Length == 2 && fieldParts[0] == "field")
				{
					fields[fieldParts[1]] = fieldParts[0];
				}
			}

			structs[structName] = new StructDefinition(structName, fields);
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

	public class StructDefinition
	{
		public string Name { get; }
		public Dictionary<string, string> Fields { get; }

		public StructDefinition(string name, Dictionary<string, string> fields)
		{
			Name = name;
			Fields = fields;
		}
	}
}
