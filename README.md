# DSharpRuntime
The official runtime for D#

Code example:
```
import std::file; 
import std::conv;

println(a); 
b = 10; 
println(b); 
c = ""Hello, World!""; 
println(c); 
std::file.write(""0:\\testOutput.txt"", ""This is a test file.""); 
d = std::file.read(""0:\\testOutput.txt"");
println(d);
e = std::conv.to(int, ""123"");
println(e);
```
