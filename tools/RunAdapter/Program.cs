using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AccessibilityEngine.Adapters.PowerApps;
using AccessibilityEngine.Core.Models;

if (args.Length == 0)
{
    Console.WriteLine("Usage: RunAdapter <base64file or zipfile>");
    return;
}

var path = args[0];
byte[] bytes;
if (File.Exists(path))
{
    // If the file is a .zip, read raw bytes. If it's a base64 text file (.b64 or .txt), decode the contents.
    var ext = Path.GetExtension(path);
    if (string.Equals(ext, ".b64", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".txt", StringComparison.OrdinalIgnoreCase))
    {
        var text = File.ReadAllText(path).Trim();
        try
        {
            bytes = Convert.FromBase64String(text);
        }
        catch (FormatException ex)
        {
            Console.Error.WriteLine($"Input file appears to contain invalid base64: {ex.Message}");
            return;
        }
    }
    else
    {
        bytes = File.ReadAllBytes(path);
    }
}
else
{
    // If the argument is not a file path, treat it as a base64 string
    try
    {
        bytes = Convert.FromBase64String(path);
    }
    catch (FormatException ex)
    {
        Console.Error.WriteLine($"Argument is not a file and not valid base64: {ex.Message}");
        return;
    }
}

var parser = new SolutionZipParser();
var trees = await parser.ParseSolutionAsync(bytes);
Console.WriteLine(JsonSerializer.Serialize(trees, new JsonSerializerOptions { WriteIndented = true }));
