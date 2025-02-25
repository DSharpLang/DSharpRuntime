using DSharpRuntime.src;
using System;
using System.Collections.Generic;
using System.IO;

namespace CodeOS.Modules
{
	public static class ModuleLoader
	{
		public static List<IModule> LoadModules(string path, DSharpInterpreter interpreter)
		{
			var modules = new List<IModule>();
			LoadModulesRecursive(path, interpreter, modules, path);
			return modules;
		}

		private static void LoadModulesRecursive(string path, DSharpInterpreter interpreter, List<IModule> modules, string basePath)
		{
			foreach (var file in Directory.GetFiles(path, "*.ds"))
			{
				var relativePath = Path.GetRelativePath(basePath, file);
				var moduleName = relativePath.Replace(Path.DirectorySeparatorChar, ':').Replace(".ds", "").Replace(":", "::");
				var code = File.ReadAllText(file);
				var module = new DModule(moduleName, interpreter, code);
				modules.Add(module);
			}

			foreach (var directory in Directory.GetDirectories(path))
			{
				LoadModulesRecursive(directory, interpreter, modules, basePath);
			}
		}
	}
}
