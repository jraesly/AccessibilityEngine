using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using AccessibilityEngine.Core.Models;

namespace AccessibilityEngine.Adapters.PowerApps;

public class SolutionZipParser
{
    public async Task<IReadOnlyList<UiTree>> ParseSolutionAsync(byte[] solutionZip)
    {
        if (solutionZip == null) throw new ArgumentNullException(nameof(solutionZip));

        using var ms = new MemoryStream(solutionZip);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);

        var trees = new List<UiTree>();

        // Canvas apps (.msapp)
        var msappEntries = archive.Entries.Where(e => e.FullName.EndsWith(".msapp", StringComparison.OrdinalIgnoreCase) || e.FullName.StartsWith("CanvasApps/", StringComparison.OrdinalIgnoreCase));

        foreach (var entry in msappEntries)
        {
            // Entry streams from ZipArchive may not be seekable. Copy the entry to a MemoryStream
            // before opening as a nested ZipArchive to ensure the central directory can be read.
            using var entryStream = entry.Open();
            using var buffer = new MemoryStream();
            await entryStream.CopyToAsync(buffer);
            buffer.Position = 0;

            ZipArchive? nested = null;
            try
            {
                nested = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: false);
            }
            catch (System.IO.InvalidDataException ex)
            {
                // Not a valid nested zip. Maybe the entry itself is a JSON blob representing the app.
                buffer.Position = 0;
                try
                {
                    using var doc2 = JsonDocument.Parse(buffer);
                    if (doc2.RootElement.ValueKind == JsonValueKind.Object && (doc2.RootElement.TryGetProperty("Screens", out _) || doc2.RootElement.TryGetProperty("Controls", out _)))
                    {
                        var parser = new CanvasAppParser();
                        var tree = parser.ParseCanvasApp(Path.GetFileNameWithoutExtension(entry.Name), doc2);
                        trees.Add(tree);
                        continue;
                    }
                }
                catch { /* not JSON either */ }

                Console.WriteLine($"Skipping entry '{entry.FullName}': not a valid nested zip and not an App.json JSON blob — {ex.Message}");
                continue;
            }

            // Try to find App.json inside the nested archive; if missing, scan for any JSON that looks like an App.json
            var appJsonEntry = nested.GetEntry("App.json") ?? nested.Entries.FirstOrDefault(e => string.Equals(e.Name, "App.json", StringComparison.OrdinalIgnoreCase));
            if (appJsonEntry == null)
            {
                // Look for any .json entry that contains Screens or Controls
                foreach (var cand in nested.Entries.Where(e => e.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        using var s = cand.Open();
                        using var doc3 = JsonDocument.Parse(s);
                        if (doc3.RootElement.ValueKind == JsonValueKind.Object && (doc3.RootElement.TryGetProperty("Screens", out _) || doc3.RootElement.TryGetProperty("Controls", out _)))
                        {
                            var parser = new CanvasAppParser();
                            var tree = parser.ParseCanvasApp(Path.GetFileNameWithoutExtension(entry.Name), doc3);
                            trees.Add(tree);
                            appJsonEntry = cand; // mark as handled
                            break;
                        }
                    }
                    catch { /* ignore parse errors */ }
                }
            }

            if (appJsonEntry == null) continue;

            using var appStream = appJsonEntry.Open();
            using var doc = JsonDocument.Parse(appStream);
            var parser2 = new CanvasAppParser();
            var tree2 = parser2.ParseCanvasApp(Path.GetFileNameWithoutExtension(entry.Name), doc);
            trees.Add(tree2);
        }

        // Model-driven: customizations.xml
        var customEntry = archive.Entries.FirstOrDefault(e => string.Equals(e.Name, "customizations.xml", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith("/customizations.xml", StringComparison.OrdinalIgnoreCase));
        if (customEntry != null)
        {
            using var customStream = customEntry.Open();
            var xdoc = XDocument.Load(customStream);
            var md = new ModelDrivenParser();
            var mdTrees = md.ParseCustomizationsXml(xdoc);
            trees.AddRange(mdTrees);
        }

        return trees;
    }
}
