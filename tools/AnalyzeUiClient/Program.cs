using System.Net.Http.Json;
using System.Text.Json;
using AccessibilityEngine.Core.Models;

// Simple console client to POST a UiTree to the Functions endpoint
var client = new HttpClient();
var url = "http://localhost:7071/api/analyze-ui"; // adjust if your Functions host uses a different port

var sampleNode = new UiNode(
    Id: "btn1",
    Type: "Button",
    Role: "button",
    Name: null,
    Text: null,
    Properties: new Dictionary<string, object?>(),
    Children: new List<UiNode>(),
    Meta: new UiMeta(SurfaceType.DomSnapshot, "Home")
);

var tree = new UiTree(SurfaceType.DomSnapshot, "SampleApp", new List<UiNode> { sampleNode });

var response = await client.PostAsJsonAsync(url, tree, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
if (!response.IsSuccessStatusCode)
{
    Console.WriteLine($"Request failed: {response.StatusCode}");
    Console.WriteLine(await response.Content.ReadAsStringAsync());
    return;
}

var content = await response.Content.ReadAsStringAsync();
Console.WriteLine("Scan result:\n" + content);
