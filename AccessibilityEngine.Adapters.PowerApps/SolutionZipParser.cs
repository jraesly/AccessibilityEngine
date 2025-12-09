using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using YamlDotNet.RepresentationModel;
using System.Threading.Tasks;
using System.Xml.Linq;
using AccessibilityEngine.Core.Models;

namespace AccessibilityEngine.Adapters.PowerApps;

/// <summary>
/// Parses Power Apps solution ZIP files and extracts UI trees for accessibility scanning.
/// Supports canvas apps (.msapp), YAML-based source format, and model-driven apps.
/// </summary>
public class SolutionZipParser
{
    private readonly CanvasAppParser _canvasParser = new();
    private readonly ModelDrivenParser _modelDrivenParser = new();

    /// <summary>
    /// Parses a Power Apps solution ZIP and returns all UI trees found.
    /// </summary>
    public async Task<IReadOnlyList<UiTree>> ParseSolutionAsync(byte[] solutionZip)
    {
        if (solutionZip == null) throw new ArgumentNullException(nameof(solutionZip));

        using var ms = new MemoryStream(solutionZip);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);

        var trees = new List<UiTree>();

        // Extract solution name from solution.xml
        var solutionName = ExtractSolutionName(archive);

        // Parse canvas apps
        var canvasTrees = await ParseCanvasAppsAsync(archive, solutionName);
        trees.AddRange(canvasTrees);

        // Parse model-driven apps from customizations.xml
        var modelDrivenTrees = ParseModelDrivenApps(archive, solutionName);
        trees.AddRange(modelDrivenTrees);

        return trees;
    }

    /// <summary>
    /// Extracts the solution name from solution.xml.
    /// </summary>
    private static string? ExtractSolutionName(ZipArchive archive)
    {
        var solutionEntry = archive.Entries.FirstOrDefault(e =>
            string.Equals(e.Name, "solution.xml", StringComparison.OrdinalIgnoreCase) ||
            e.FullName.EndsWith("/solution.xml", StringComparison.OrdinalIgnoreCase));

        if (solutionEntry == null) return null;

        try
        {
            using var stream = solutionEntry.Open();
            var xdoc = XDocument.Load(stream);

            // Try to get LocalizedName first (friendly name)
            var localizedName = xdoc.Descendants("LocalizedName")
                .FirstOrDefault(ln => ln.Attribute("languagecode")?.Value == "1033")?
                .Attribute("description")?.Value;
            
            if (!string.IsNullOrEmpty(localizedName))
            {
                Console.WriteLine($"[SolutionZipParser] Found solution LocalizedName: {localizedName}");
                return localizedName;
            }

            // Fall back to UniqueName
            var uniqueName = xdoc.Descendants("UniqueName").FirstOrDefault()?.Value;
            if (!string.IsNullOrEmpty(uniqueName))
            {
                Console.WriteLine($"[SolutionZipParser] Found solution UniqueName: {uniqueName}");
                return FormatSolutionName(uniqueName);
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SolutionZipParser] Failed to parse solution.xml: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Formats a solution unique name into a display name.
    /// </summary>
    private static string FormatSolutionName(string uniqueName)
    {
        if (string.IsNullOrEmpty(uniqueName)) return uniqueName;

        // Remove common prefixes (publisher prefix like jcr_, new_)
        var cleanName = uniqueName;
        if (cleanName.Length > 4 && cleanName[3] == '_' && cleanName.Substring(0, 3).All(char.IsLetter))
            cleanName = cleanName[4..];
        else if (cleanName.StartsWith("new_", StringComparison.OrdinalIgnoreCase))
            cleanName = cleanName[4..];

        // Convert underscores to spaces and capitalize
        var words = cleanName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                // Capitalize first letter, keep rest as is for proper names
                words[i] = char.ToUpper(words[i][0]) + words[i][1..];
            }
        }

        return string.Join(" ", words);
    }

    /// <summary>
    /// Extracts and parses all canvas apps from the solution archive.
    /// </summary>
    private async Task<List<UiTree>> ParseCanvasAppsAsync(ZipArchive archive, string? solutionName)
    {
        var trees = new List<UiTree>();

        // Find canvas app entries: .msapp files or entries in CanvasApps/ folder
        var canvasEntries = archive.Entries
            .Where(e => e.FullName.EndsWith(".msapp", StringComparison.OrdinalIgnoreCase) ||
                       (e.FullName.StartsWith("CanvasApps/", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrEmpty(e.Name) &&
                        !e.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))) // Skip loose JSON files
            .ToList();

        foreach (var entry in canvasEntries)
        {
            // For canvas apps, use solution name if available, otherwise extract from entry
            var appName = solutionName ?? ExtractAppNameFromEntry(entry);
            var tree = await ParseCanvasAppEntryAsync(entry, appName);
            if (tree != null)
            {
                trees.Add(tree);
            }
        }

        return trees;
    }

    /// <summary>
    /// Extracts a clean app name from a ZIP entry.
    /// </summary>
    private static string ExtractAppNameFromEntry(ZipArchiveEntry entry)
    {
        var name = Path.GetFileNameWithoutExtension(entry.Name);
        
        // Remove common suffixes added by Power Platform
        if (name.Contains("_DocumentUri"))
        {
            name = name.Substring(0, name.IndexOf("_DocumentUri", StringComparison.OrdinalIgnoreCase));
        }
        
        // Remove GUID suffixes (pattern: _xxxxx where x is hex)
        var lastUnderscore = name.LastIndexOf('_');
        if (lastUnderscore > 0 && name.Length - lastUnderscore <= 6)
        {
            var suffix = name.Substring(lastUnderscore + 1);
            if (suffix.All(c => char.IsLetterOrDigit(c)))
            {
                name = name.Substring(0, lastUnderscore);
            }
        }

        return name;
    }

    /// <summary>
    /// Parses a single canvas app entry from the archive.
    /// </summary>
    private async Task<UiTree?> ParseCanvasAppEntryAsync(ZipArchiveEntry entry, string appName)
    {
        using var entryStream = entry.Open();
        using var buffer = new MemoryStream();
        await entryStream.CopyToAsync(buffer);
        buffer.Position = 0;

        // Try to open as nested ZIP archive (.msapp is a ZIP)
        ZipArchive? nestedArchive = null;
        try
        {
            nestedArchive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            // Not a valid ZIP, try parsing as direct JSON
            buffer.Position = 0;
            return TryParseAsDirectJson(buffer, appName);
        }

        using (nestedArchive)
        {
            return await ParseNestedCanvasAppAsync(nestedArchive, appName, entry.FullName);
        }
    }

    /// <summary>
    /// Attempts to parse a stream as a direct JSON app definition.
    /// </summary>
    private UiTree? TryParseAsDirectJson(MemoryStream buffer, string appName)
    {
        try
        {
            using var doc = JsonDocument.Parse(buffer);
            if (IsAppJsonDocument(doc.RootElement))
            {
                return _canvasParser.ParseCanvasApp(appName, doc);
            }
        }
        catch (JsonException)
        {
            // Not valid JSON
        }
        return null;
    }

    /// <summary>
    /// Parses a nested canvas app archive (.msapp contents).
    /// </summary>
    private async Task<UiTree?> ParseNestedCanvasAppAsync(ZipArchive archive, string appName, string entryPath)
    {
        // Log archive contents for debugging
        LogArchiveContents(archive, entryPath);

        // Strategy 1: Parse YAML source format (Src/*.pa.yaml) - This is the modern Power Apps format
        var tree = await TryParseYamlSourceFormatAsync(archive, appName, entryPath);
        if (tree != null && tree.Nodes.Count > 0) return tree;

        // Strategy 2: Parse EditorState JSON files
        tree = await TryParseEditorStateAsync(archive, appName);
        if (tree != null && tree.Nodes.Count > 0) return tree;

        // Strategy 3: Look for App.json (legacy format)
        tree = TryParseAppJson(archive, appName);
        if (tree != null && tree.Nodes.Count > 0) return tree;

        // Strategy 4: Aggregate control JSON files from Controls/ folder
        tree = await TryParseControlsFolderAsync(archive, appName);
        if (tree != null && tree.Nodes.Count > 0) return tree;

        // Strategy 5: Look for any JSON file that looks like an app definition
        tree = TryParseCandidateJsonFiles(archive, appName);
        if (tree != null && tree.Nodes.Count > 0) return tree;

        // Return empty tree if no controls found
        return new UiTree(SurfaceType.CanvasApp, appName, new List<UiNode>());
    }

    /// <summary>
    /// Logs archive contents for debugging.
    /// </summary>
    private static void LogArchiveContents(ZipArchive archive, string entryPath)
    {
        Console.WriteLine($"[SolutionZipParser] Parsing msapp: {entryPath}");
        Console.WriteLine($"[SolutionZipParser] Archive contains {archive.Entries.Count} entries:");
        foreach (var e in archive.Entries.Take(20))
        {
            Console.WriteLine($"  - {e.FullName}");
        }
        if (archive.Entries.Count > 20)
        {
            Console.WriteLine($"  ... and {archive.Entries.Count - 20} more entries");
        }
    }

    /// <summary>
    /// Parses EditorState JSON files which contain control definitions.
    /// </summary>
    private async Task<UiTree?> TryParseEditorStateAsync(ZipArchive archive, string appName)
    {
        // Look for EditorState/*.json files
        var editorStateEntries = archive.Entries
            .Where(e => e.FullName.StartsWith("EditorState/", StringComparison.OrdinalIgnoreCase) &&
                       e.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (editorStateEntries.Count == 0)
        {
            // Also check References/ folder
            editorStateEntries = archive.Entries
                .Where(e => e.FullName.StartsWith("References/", StringComparison.OrdinalIgnoreCase) &&
                           e.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (editorStateEntries.Count == 0) return null;

        Console.WriteLine($"[SolutionZipParser] Found {editorStateEntries.Count} EditorState/References JSON files");

        var allNodes = new List<UiNode>();

        foreach (var entry in editorStateEntries)
        {
            try
            {
                using var stream = entry.Open();
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(json)) continue;

                using var doc = JsonDocument.Parse(json);
                var tree = _canvasParser.ParseCanvasApp(appName, doc);
                
                if (tree.Nodes.Count > 0)
                {
                    Console.WriteLine($"[SolutionZipParser] Parsed {tree.Nodes.Count} nodes from {entry.Name}");
                    allNodes.AddRange(tree.Nodes);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SolutionZipParser] Error parsing {entry.FullName}: {ex.Message}");
            }
        }

        return allNodes.Count > 0 
            ? new UiTree(SurfaceType.CanvasApp, appName, allNodes) 
            : null;
    }

    /// <summary>
    /// Tries to find and parse App.json in the archive.
    /// </summary>
    private UiTree? TryParseAppJson(ZipArchive archive, string appName)
    {
        var appJsonEntry = archive.GetEntry("App.json") ??
                          archive.Entries.FirstOrDefault(e =>
                              string.Equals(e.Name, "App.json", StringComparison.OrdinalIgnoreCase));

        if (appJsonEntry == null) return null;

        try
        {
            using var stream = appJsonEntry.Open();
            using var doc = JsonDocument.Parse(stream);
            return _canvasParser.ParseCanvasApp(appName, doc);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Scans JSON files in the archive to find one that looks like an app definition.
    /// </summary>
    private UiTree? TryParseCandidateJsonFiles(ZipArchive archive, string appName)
    {
        // Prioritize certain files
        var priorityFiles = new[] { "CanvasManifest.json", "Header.json", "Properties.json" };
        
        var jsonEntries = archive.Entries
            .Where(e => e.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                       !e.FullName.StartsWith("Controls/", StringComparison.OrdinalIgnoreCase) &&
                       !e.FullName.StartsWith("EditorState/", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => priorityFiles.Contains(e.Name) ? 1 : 0)
            .ToList();

        foreach (var candidate in jsonEntries)
        {
            try
            {
                using var stream = candidate.Open();
                using var doc = JsonDocument.Parse(stream);

                if (IsAppJsonDocument(doc.RootElement))
                {
                    Console.WriteLine($"[SolutionZipParser] Found app definition in {candidate.FullName}");
                    return _canvasParser.ParseCanvasApp(appName, doc);
                }
            }
            catch
            {
                // Continue to next candidate
            }
        }

        return null;
    }

    /// <summary>
    /// Parses YAML source format used in newer Power Apps exports.
    /// </summary>
    private async Task<UiTree?> TryParseYamlSourceFormatAsync(ZipArchive archive, string appName, string entryPath)
    {
        // Find screen YAML files (exclude App.pa.yaml and _EditorState.pa.yaml)
        var yamlEntries = archive.Entries
            .Where(e => (e.FullName.StartsWith("Src/", StringComparison.OrdinalIgnoreCase) ||
                        e.FullName.StartsWith("src/", StringComparison.OrdinalIgnoreCase)) &&
                       (e.FullName.EndsWith(".pa.yaml", StringComparison.OrdinalIgnoreCase) ||
                        e.FullName.EndsWith(".fx.yaml", StringComparison.OrdinalIgnoreCase)) &&
                       !e.Name.StartsWith("App.", StringComparison.OrdinalIgnoreCase) &&
                       !e.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (yamlEntries.Count == 0) return null;

        Console.WriteLine($"[SolutionZipParser] Found {yamlEntries.Count} screen YAML files in Src/");

        try
        {
            var allNodes = new List<UiNode>();

            foreach (var yamlEntry in yamlEntries)
            {
                var nodes = await ParseYamlScreenToNodesAsync(yamlEntry);
                if (nodes.Count > 0)
                {
                    Console.WriteLine($"[SolutionZipParser] Parsed {nodes.Count} controls from {yamlEntry.Name}");
                    allNodes.AddRange(nodes);
                }
            }

            // Note: We don't load Controls/ JSON files here since YAML already contains the full control hierarchy
            // The Controls/ folder contains the same controls in a different format for backwards compatibility

            if (allNodes.Count == 0) return null;

            return new UiTree(SurfaceType.CanvasApp, appName, allNodes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SolutionZipParser] Failed to parse YAML source format in '{entryPath}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses a YAML screen file directly into UiNodes.
    /// Returns a list containing the screen node with its children nested inside.
    /// </summary>
    private async Task<List<UiNode>> ParseYamlScreenToNodesAsync(ZipArchiveEntry entry)
    {
        var nodes = new List<UiNode>();
        
        try
        {
            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            var yamlText = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(yamlText)) return nodes;

            var yaml = new YamlStream();
            yaml.Load(new StringReader(yamlText));

            if (yaml.Documents.Count == 0) return nodes;

            var root = yaml.Documents[0].RootNode as YamlMappingNode;
            if (root == null) return nodes;

            // Parse the root level - this should be the screen definition
            var screenNode = ParseYamlControlNode(root, null);
            if (screenNode != null)
            {
                nodes.Add(screenNode);
            }

            return nodes;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SolutionZipParser] Error parsing YAML {entry.Name}: {ex.Message}");
            return nodes;
        }
    }

    /// <summary>
    /// Parses a YAML mapping node that may contain a control definition.
    /// Returns a UiNode if this node represents a control (has "X As Y" format key).
    /// </summary>
    private UiNode? ParseYamlControlNode(YamlMappingNode node, string? parentScreenName)
    {
        // Find the control definition key (format: "ControlName As ControlType")
        foreach (var child in node.Children)
        {
            var key = (child.Key as YamlScalarNode)?.Value;
            if (key == null) continue;

            if (key.Contains(" As ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = key.Split(" As ", StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var controlName = parts[0].Trim();
                    var controlType = parts[1].Trim().TrimEnd(':');

                    // Determine screen name - if this is a Screen, use its name; otherwise use parent's
                    var screenName = controlType.Equals("Screen", StringComparison.OrdinalIgnoreCase) 
                        ? controlName 
                        : parentScreenName ?? controlName;

                    var properties = new Dictionary<string, object?>();
                    var childNodes = new List<UiNode>();

                    // Parse the control's content (properties and nested controls)
                    if (child.Value is YamlMappingNode mappingNode)
                    {
                        foreach (var prop in mappingNode.Children)
                        {
                            var propKey = (prop.Key as YamlScalarNode)?.Value;
                            if (propKey == null) continue;

                            // Check if this is a nested control
                            if (propKey.Contains(" As ", StringComparison.OrdinalIgnoreCase))
                            {
                                // Create a temporary mapping node for the nested control
                                var nestedMapping = new YamlMappingNode();
                                nestedMapping.Add(prop.Key, prop.Value);
                                
                                var nestedNode = ParseYamlControlNode(nestedMapping, screenName);
                                if (nestedNode != null)
                                {
                                    childNodes.Add(nestedNode);
                                }
                            }
                            else
                            {
                                // This is a property
                                if (prop.Value is YamlScalarNode scalar)
                                {
                                    properties[propKey] = scalar.Value;
                                }
                                else if (prop.Value is YamlSequenceNode sequence)
                                {
                                    properties[propKey] = sequence.Children
                                        .Select(c => (c as YamlScalarNode)?.Value)
                                        .ToList();
                                }
                            }
                        }
                    }

                    // Extract accessibility-related properties
                    string? accessibleLabel = null;
                    if (properties.TryGetValue("AccessibleLabel", out var al) && al is string alStr)
                    {
                        accessibleLabel = alStr;
                    }
                    else if (properties.TryGetValue("Text", out var text) && text is string textStr)
                    {
                        accessibleLabel = textStr;
                    }

                    var meta = new UiMeta(SurfaceType.CanvasApp, screenName, null, null);
                    return new UiNode(
                        controlName,
                        controlType,
                        null,
                        accessibleLabel,
                        accessibleLabel,
                        properties,
                        childNodes,
                        meta
                    );
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Parses control JSON files into UiNodes.
    /// </summary>
    private async Task<List<UiNode>> ParseControlJsonFilesAsync(List<ZipArchiveEntry> controlEntries, string defaultScreenName)
    {
        var nodes = new List<UiNode>();

        foreach (var entry in controlEntries)
        {
            try
            {
                using var stream = entry.Open();
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(json)) continue;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Try to parse as a control definition
                var node = ParseJsonControlToUiNode(root, defaultScreenName);
                if (node != null)
                {
                    nodes.Add(node);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SolutionZipParser] Error parsing control JSON {entry.Name}: {ex.Message}");
            }
        }

        return nodes;
    }

    /// <summary>
    /// Parses a JSON control element into a UiNode.
    /// </summary>
    private UiNode? ParseJsonControlToUiNode(JsonElement element, string screenName)
    {
        // Try to get control ID/Name
        string? id = null;
        foreach (var prop in new[] { "Name", "ControlUniqueId", "ControlId" })
        {
            if (element.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.String)
            {
                id = val.GetString();
                if (!string.IsNullOrEmpty(id)) break;
            }
        }

        if (string.IsNullOrEmpty(id)) return null;

        // Get control type - use helper to check multiple locations and avoid generic types
        var type = GetControlTypeFromJson(element, id);

        // Extract properties
        var props = new Dictionary<string, object?>();
        if (element.TryGetProperty("Properties", out var propsElement) && propsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in propsElement.EnumerateObject())
            {
                props[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => prop.Value.ToString()
                };
            }
        }

        // Extract Rules (Power Apps formulas)
        if (element.TryGetProperty("Rules", out var rules) && rules.ValueKind == JsonValueKind.Array)
        {
            foreach (var rule in rules.EnumerateArray())
            {
                if (rule.TryGetProperty("Property", out var propName) &&
                    rule.TryGetProperty("InvariantScript", out var script))
                {
                    var key = propName.GetString();
                    if (!string.IsNullOrEmpty(key) && !props.ContainsKey(key))
                    {
                        props[key] = script.GetString();
                    }
                }
            }
        }

        // Get accessible label
        string? accessibleLabel = null;
        if (props.TryGetValue("AccessibleLabel", out var al) && al is string alStr)
        {
            accessibleLabel = alStr;
        }
        else if (props.TryGetValue("Text", out var text) && text is string textStr)
        {
            accessibleLabel = textStr;
        }

        // Parse children
        var children = new List<UiNode>();
        if (element.TryGetProperty("Children", out var childrenElement) && childrenElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in childrenElement.EnumerateArray())
            {
                var childNode = ParseJsonControlToUiNode(child, screenName);
                if (childNode != null)
                {
                    children.Add(childNode);
                }
            }
        }

        var meta = new UiMeta(SurfaceType.CanvasApp, screenName, null, null);
        return new UiNode(id, type, null, accessibleLabel, accessibleLabel, props, children, meta);
    }

    /// <summary>
    /// Extracts the screen name from YAML content or falls back to filename.
    /// </summary>
    private string ExtractScreenNameFromYaml(YamlMappingNode root, string fileName)
    {
        // Try to get name from YAML properties
        foreach (var child in root.Children)
        {
            var key = (child.Key as YamlScalarNode)?.Value;
            if (string.Equals(key, "Name", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "ScreenName", StringComparison.OrdinalIgnoreCase))
            {
                return (child.Value as YamlScalarNode)?.Value ?? fileName;
            }

            // Check if the key itself is a screen type (e.g., "Screen1 As Screen:")
            if (key?.Contains(" As ", StringComparison.OrdinalIgnoreCase) == true)
            {
                var namePart = key.Split(" As ", StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(namePart))
                {
                    return namePart.Trim();
                }
            }
        }

        // Fall back to filename without extension
        return Path.GetFileNameWithoutExtension(fileName).Replace(".pa", "").Replace(".fx", "");
    }

    /// <summary>
    /// Extracts properties from a YAML node into a dictionary.
    /// </summary>
    private Dictionary<string, object?> ExtractPropertiesFromYamlNode(YamlNode node)
    {
        var props = new Dictionary<string, object?>();

        if (node is YamlMappingNode mapping)
        {
            foreach (var child in mapping.Children)
            {
                var key = (child.Key as YamlScalarNode)?.Value;
                if (key == null || key.Contains(" As ")) continue; // Skip nested controls

                if (child.Value is YamlScalarNode scalar)
                {
                    props[key] = scalar.Value;
                }
                else if (child.Value is YamlSequenceNode sequence)
                {
                    props[key] = sequence.Children.Select(c => (c as YamlScalarNode)?.Value).ToList();
                }
            }
        }

        return props;
    }

    /// <summary>
    /// Aggregates control JSON files from the Controls/ folder into a synthetic app.
    /// </summary>
    private async Task<UiTree?> TryParseControlsFolderAsync(ZipArchive archive, string appName)
    {
        var controlEntries = archive.Entries
            .Where(e => e.FullName.StartsWith("Controls/", StringComparison.OrdinalIgnoreCase) &&
                       e.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (controlEntries.Count == 0) return null;

        Console.WriteLine($"[SolutionZipParser] Found {controlEntries.Count} control JSON files in Controls/");

        try
        {
            var nodes = await ParseControlJsonFilesAsync(controlEntries, appName);
            if (nodes.Count == 0) return null;

            return new UiTree(SurfaceType.CanvasApp, appName, nodes);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if a JSON element represents an app definition document.
    /// </summary>
    private static bool IsAppJsonDocument(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return false;

        // Check for various app definition markers
        return element.TryGetProperty("Screens", out _) ||
               element.TryGetProperty("Controls", out _) ||
               element.TryGetProperty("TopParent", out _) ||
               element.TryGetProperty("ControlStates", out _) ||
               (element.TryGetProperty("Name", out _) && element.TryGetProperty("Children", out _));
    }

    /// <summary>
    /// Parses model-driven apps from customizations.xml.
    /// </summary>
    private IReadOnlyList<UiTree> ParseModelDrivenApps(ZipArchive archive, string? solutionName)
    {
        var customEntry = archive.Entries.FirstOrDefault(e =>
            string.Equals(e.Name, "customizations.xml", StringComparison.OrdinalIgnoreCase) ||
            e.FullName.EndsWith("/customizations.xml", StringComparison.OrdinalIgnoreCase));

        if (customEntry == null) return Array.Empty<UiTree>();

        try
        {
            using var stream = customEntry.Open();
            var xdoc = XDocument.Load(stream);
            return _modelDrivenParser.ParseCustomizationsXml(xdoc, solutionName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SolutionZipParser] Failed to parse customizations.xml: {ex.Message}");
            return Array.Empty<UiTree>();
        }
    }

    /// <summary>
    /// Extracts the control type from various JSON locations used by Power Apps.
    /// Avoids returning generic types like "ControlInfo" by inferring from name if needed.
    /// </summary>
    private static string GetControlTypeFromJson(JsonElement element, string controlName)
    {
        string? type = null;

        // Strategy 1: Template.Name (most reliable in Power Apps Controls/*.json)
        if (element.TryGetProperty("Template", out var template))
        {
            if (template.TryGetProperty("Name", out var templateName) && templateName.ValueKind == JsonValueKind.String)
            {
                type = templateName.GetString();
                if (!string.IsNullOrEmpty(type) && !IsGenericType(type))
                    return type;
            }
            if (template.TryGetProperty("Id", out var templateId) && templateId.ValueKind == JsonValueKind.String)
            {
                type = templateId.GetString();
                if (!string.IsNullOrEmpty(type) && !IsGenericType(type))
                    return type;
            }
        }

        // Strategy 2: Direct ControlType property
        if (element.TryGetProperty("ControlType", out var ct) && ct.ValueKind == JsonValueKind.String)
        {
            type = ct.GetString();
            if (!string.IsNullOrEmpty(type) && !IsGenericType(type))
                return type;
        }

        // Strategy 3: Type property
        if (element.TryGetProperty("Type", out var t) && t.ValueKind == JsonValueKind.String)
        {
            type = t.GetString();
            if (!string.IsNullOrEmpty(type) && !IsGenericType(type))
                return type;
        }

        // Strategy 4: TemplateName property
        if (element.TryGetProperty("TemplateName", out var tn) && tn.ValueKind == JsonValueKind.String)
        {
            type = tn.GetString();
            if (!string.IsNullOrEmpty(type) && !IsGenericType(type))
                return type;
        }

        // Strategy 5: Infer from control name patterns
        return InferControlTypeFromName(controlName);
    }

    /// <summary>
    /// Checks if a type is a generic/wrapper type that should be replaced with a more specific type.
    /// </summary>
    private static bool IsGenericType(string type)
    {
        return type.Equals("ControlInfo", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("Control", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("GroupControl", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("TypedDataCard", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Attempts to infer control type from the control name using common Power Apps naming conventions.
    /// </summary>
    private static string InferControlTypeFromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Control";

        // Power Apps default naming: "Button1", "Label1", "TextInput1", "Icon1", etc.
        // Also handle common prefixes: "btn", "lbl", "txt", "ico"
        
        // Check for numbered suffix patterns (e.g., "Button1", "Label2")
        var patterns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Button", "Button" },
            { "btn", "Button" },
            { "Label", "Label" },
            { "lbl", "Label" },
            { "TextInput", "TextInput" },
            { "txt", "TextInput" },
            { "Icon", "Icon" },
            { "ico", "Icon" },
            { "Image", "Image" },
            { "img", "Image" },
            { "Gallery", "Gallery" },
            { "gal", "Gallery" },
            { "Form", "Form" },
            { "frm", "Form" },
            { "Screen", "Screen" },
            { "scr", "Screen" },
            { "Dropdown", "Dropdown" },
            { "dd", "Dropdown" },
            { "ComboBox", "ComboBox" },
            { "cb", "ComboBox" },
            { "Checkbox", "Checkbox" },
            { "chk", "Checkbox" },
            { "Toggle", "Toggle" },
            { "tog", "Toggle" },
            { "Slider", "Slider" },
            { "DatePicker", "DatePicker" },
            { "Rating", "Rating" },
            { "Timer", "Timer" },
            { "Video", "Video" },
            { "Audio", "Audio" },
            { "Camera", "Camera" },
            { "Barcode", "Barcode" },
            { "PenInput", "PenInput" },
            { "RichTextEditor", "RichTextEditor" },
            { "HtmlText", "HtmlText" },
            { "Rectangle", "Rectangle" },
            { "Circle", "Circle" },
            { "Container", "Container" }
        };

        foreach (var pattern in patterns)
        {
            // Check if name starts with the pattern (e.g., "Button1", "btnSubmit")
            if (name.StartsWith(pattern.Key, StringComparison.OrdinalIgnoreCase))
            {
                return pattern.Value;
            }
            // Also check if pattern appears anywhere in the name
            if (name.Contains(pattern.Key, StringComparison.OrdinalIgnoreCase))
            {
                return pattern.Value;
            }
        }

        return "Control";
    }
}
