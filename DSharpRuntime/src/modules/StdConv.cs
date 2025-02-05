using DSharpRuntime.src;
using System;
using System.Collections.Generic;
using static CodeOS.Modules.StdConv;

namespace CodeOS.Modules
{
	public class StdConv : IModule
	{
		public class ConvException : Exception
		{
			public ConvException(string message) : base(message) { }
		}

		public string Name => "std::conv";

		public void Execute(string command, DSharpInterpreter interpreter)
		{
			if (command.StartsWith("to(") && command.EndsWith(")"))
			{
				var parameters = command.Substring(3, command.Length - 4).Split(',');
				if (parameters.Length == 2)
				{
					var targetType = parameters[0].Trim();
					var expression = parameters[1].Trim();
					var value = interpreter.EvaluateExpression(expression);
					var convertedValue = ConvertTo(targetType, value);
					interpreter.LastResult = convertedValue;
				}
				else
				{
					throw new Exception("Invalid parameters for stdconv.to");
				}
			}
			else if (command.Contains("="))
			{
				var parts = command.Split('=');
				if (parts.Length == 2)
				{
					var variableName = parts[0].Trim();
					var expression = parts[1].Trim();
					var value = interpreter.EvaluateExpression(expression);
					interpreter.Variables[variableName] = value;
				}
				else
				{
					throw new ConvException("Invalid parameters for stdconv assignment");
				}
			}
			else
			{
				throw new Exception($"Unknown command for stdconv: {command}");
			}
		}

		private object ConvertTo(string targetType, object value)
		{
			try
			{
				return targetType.ToLower() switch
				{
					"int" => Convert.ToInt32(value),
					"double" => Convert.ToDouble(value),
					"string" => Convert.ToString(value),
					"bool" => Convert.ToBoolean(value),
					_ => throw new Exception($"Unsupported target type: {targetType}")
				};
			}
			catch (Exception ex)
			{
				throw new Exception($"Error converting value: {ex.Message}");
			}
		}
	}
}
