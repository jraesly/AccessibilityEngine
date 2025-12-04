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
            using var entryStream = entry.Open();
            using var nested = new ZipArchive(entryStream, ZipArchiveMode.Read, leaveOpen: true);
            var appJsonEntry = nested.GetEntry("App.json");
            if (appJsonEntry == null) continue;

            using var appStream = appJsonEntry.Open();
            using var doc = JsonDocument.Parse(appStream);
            var parser = new CanvasAppParser();
            var tree = parser.ParseCanvasApp(Path.GetFileNameWithoutExtension(entry.Name), doc);
            trees.Add(tree);
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
