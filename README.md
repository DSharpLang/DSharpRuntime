# DSharpRuntime
The official runtime for D#

Code example:
```
// Importer les modules nécessaires;
import std::file;
import std::conv;

// Déclaration d'une méthode void;
void greet {
    println("Hello, World!");
};

// Utilisation de la méthode greet;
greet;

// Déclaration d'une variable;
name = "DSharp";

localName = "Dsharp";
// Déclaration d'une méthode void avec une variable locale;
void personalizedGreet {
    printlnf("Hello, {}", localName);
};
// Utilisation de la méthode personalizedGreet;
personalizedGreet;

// Utilisation du module std::file pour écrire dans un fichier;
std::file.write("test.txt", "This is a test file content");

// Utilisation du module std::file pour lire depuis un fichier;
std::file.read("test.txt");
println(std::file.read("test.txt"));

// Utilisation du module std::conv pour convertir des types;
intValue = std::conv.to(int, "123");
println(intValue);
```
