# DSharpRuntime
The official runtime for D#

Code example:
```
// Import modules;
import std::file;
import std::conv;

void greet {
    println("Hello, World!");
};

greet;

Name = "Dsharp";
void personalizedGreet {
    printlnf("Hello, {}", Name);
};
personalizedGreet;

std::file.write("test.txt", "This is a test file content");
std::file.read("test.txt");
println(std::file.read("test.txt"));
intValue = std::conv.to(int, "123");
println(intValue);
```
