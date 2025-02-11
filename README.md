# DSharpRuntime
The official runtime for D#

Code example:
```
import std::file; 
import std::conv;

a = 5;
println(a); 
b = 10; 
println(b); 
c = "Hello, World"; 
println(c); 
std::file.write("C:\\testOutput.txt", "This is a test file."); 
d = std::file.read("C:\\testOutput.txt");
println(d);
e = std::conv.to(int, "123");
println(e);
```
