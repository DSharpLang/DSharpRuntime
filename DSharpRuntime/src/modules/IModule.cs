using DSharpRuntime.src;
using System;

namespace CodeOS.Modules
{
	public interface IModule
	{
		string Name { get; }
		void Execute(string command, DSharpInterpreter interpreter);
	}
}
