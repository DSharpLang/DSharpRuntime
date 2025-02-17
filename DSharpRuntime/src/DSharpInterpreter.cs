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
			else if (line.StartsWith("if(")) // Ajout du support de if/else if/else
			{
				ExecuteIfElse(line);
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

		private void ExecuteIfElse(string line)
		{
			// Cette méthode gère une chaîne de type :
			// if(condition1) { bloc1 } else if (condition2) { bloc2 } else { bloc3 }
			int index = 0;
			var branches = new List<(string? condition, string block)>();

			while (index < line.Length)
			{
				// Traiter les clauses "if" ou "else if"
				if (line.Substring(index).TrimStart().StartsWith("if(") ||
					line.Substring(index).TrimStart().StartsWith("elseif(")) // Correction ici
				{
					// Positionne l'index sur la parenthèse ouvrante
					int parenOpen = line.IndexOf('(', index);
					int parenClose = line.IndexOf(')', parenOpen);
					if (parenOpen < 0 || parenClose < 0)
						throw new Exception("Syntaxe invalide dans l'instruction if/else.");

					string condition = line.Substring(parenOpen + 1, parenClose - parenOpen - 1).Trim();

					// Rechercher le bloc associé (entre les accolades)
					int braceOpen = line.IndexOf('{', parenClose);
					if (braceOpen < 0)
						throw new Exception("Bloc manquant après la condition.");
					int braceClose = FindMatchingBrace(line, braceOpen);
					string block = line.Substring(braceOpen + 1, braceClose - braceOpen - 1).Trim();

					branches.Add((condition, block));
					index = braceClose + 1;
				}
				else if (line.Substring(index).TrimStart().StartsWith("else"))
				{
					// On s'attend à "else { bloc }"
					int elseIndex = line.IndexOf("else", index);
					int braceOpen = line.IndexOf('{', elseIndex);
					if (braceOpen < 0)
						throw new Exception("Bloc manquant après 'else'.");
					int braceClose = FindMatchingBrace(line, braceOpen);
					string block = line.Substring(braceOpen + 1, braceClose - braceOpen - 1).Trim();

					branches.Add((null, block));
					index = braceClose + 1;
				}
				else
				{
					break;
				}
			}

			// Exécution des branches dans l'ordre
			foreach (var branch in branches)
			{
				if (branch.condition != null)
				{
					if (EvaluateCondition(branch.condition))
					{
						Interpret(branch.block);
						return;
					}
				}
				else
				{
					Interpret(branch.block);
					return;
				}
			}
		}


		private int FindMatchingBrace(string input, int braceOpenIndex)
		{
			int depth = 0;
			for (int i = braceOpenIndex; i < input.Length; i++)
			{
				if (input[i] == '{')
					depth++;
				else if (input[i] == '}')
				{
					depth--;
					if (depth == 0)
						return i;
				}
			}
			throw new Exception("Aucune accolade fermante trouvée.");
		}

		private bool EvaluateCondition(string condition)
		{
			// Support de conditions simples avec les opérateurs : ==, !=, >=, <=, >, <
			var match = Regex.Match(condition, @"^(.*?)\s*(==|!=|>=|<=|>|<)\s*(.*?)$");
			if (match.Success)
			{
				var leftVal = EvaluateExpression(match.Groups[1].Value.Trim());
				var rightVal = EvaluateExpression(match.Groups[3].Value.Trim());
				string op = match.Groups[2].Value;

				// Comparaison en fonction du type
				try
				{
					decimal left = Convert.ToDecimal(leftVal);
					decimal right = Convert.ToDecimal(rightVal);
					switch (op)
					{
						case "==": return left == right;
						case "!=": return left != right;
						case ">": return left > right;
						case "<": return left < right;
						case ">=": return left >= right;
						case "<=": return left <= right;
						default: throw new Exception($"Opérateur inconnu '{op}' dans la condition.");
					}
				}
				catch
				{
					// Si la conversion en nombre échoue, on compare sous forme de chaîne
					string left = leftVal?.ToString() ?? "";
					string right = rightVal?.ToString() ?? "";
					switch (op)
					{
						case "==": return left == right;
						case "!=": return left != right;
						default: throw new Exception("Opérateur non supporté pour la comparaison de chaînes.");
					}
				}
			}
			else
			{
				// Si l'expression ne contient pas d'opérateur, on évalue directement comme booléen.
				var result = EvaluateExpression(condition);
				if (result is bool b)
					return b;
				throw new Exception($"Condition invalide : {condition}");
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
			else if (IsArrayElementExpression(expression))
			{
				return EvaluateArrayElementExpression(expression);
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
				throw new Exception($"Unknown expression: {expression} - This expression couldn't be evaluated.");
			}
		}

		private bool IsArrayElementExpression(string expression)
		{
			var match = Regex.Match(expression, @"(\w+)\[(\d+)\]");
			return match.Success && Variables.ContainsKey(match.Groups[1].Value) && Variables[match.Groups[1].Value] is List<object>;
		}

		private object EvaluateArrayElementExpression(string expression)
		{
			var match = Regex.Match(expression, @"(\w+)\[(\d+)\]");
			if (match.Success)
			{
				var arrayName = match.Groups[1].Value;
				var index = int.Parse(match.Groups[2].Value);

				if (Variables.ContainsKey(arrayName) && Variables[arrayName] is List<object> arrayInstance)
				{
					if (index >= 0 && index < arrayInstance.Count)
					{
						return arrayInstance[index];
					}
					else
					{
						throw new Exception($"Index '{index}' out of bounds for array '{arrayName}'");
					}
				}
				else
				{
					throw new Exception($"Array '{arrayName}' not found");
				}
			}
			else
			{
				throw new Exception($"Invalid array element expression: {expression}");
			}
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


		private bool IsStructExpression(string expression)
		{
			var parts = expression.Split('.');
			return Variables.ContainsKey(parts[0]) && Variables[parts[0]] is Dictionary<string, object>;
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
				var evaluated = EvaluateExpression(parts[i].Trim());
				// Check if evaluated result is a list or array
				if (evaluated is Array array)
				{
					args[i - 1] = string.Join(", ", array.Cast<object>());
				}
				else if (evaluated is List<object> list)
				{
					args[i - 1] = string.Join(", ", list);
				}
				else
				{
					args[i - 1] = evaluated.ToString();
				}
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
			else if (expression.StartsWith("[") && expression.EndsWith("]"))
			{
				var arrayElements = expression.Substring(1, expression.Length - 2).Split(',');
				var arrayInstance = new List<object>();
				foreach (var element in arrayElements)
				{
					arrayInstance.Add(EvaluateExpression(element.Trim()));
				}
				Variables[variableName.TrimEnd(new char[] { '[', ']' })] = arrayInstance;
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
