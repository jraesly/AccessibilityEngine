using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AccessibilityEngine.AI;
using AccessibilityEngine.Core.Engine;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AccessibilityEngine.Functions;

public sealed class ScanSolutionRequest
{
    public string? Base64Zip { get; init; }
}

public sealed class SolutionScanResultDto
{
    /// <summary>
    /// The app name (Canvas App name or MDA app name like "test MDA")
    /// </summary>
    public string? AppName { get; init; }
    
    /// <summary>
    /// The surface type (CanvasApp, ModelDrivenApp, Portal)
    /// </summary>
    public SurfaceType Surface { get; init; }
    
    /// <summary>
    /// The scan results containing findings
    /// </summary>
    public ScanResult Result { get; init; } = default!;
}

public class ScanSolutionFunction
{
    private readonly SolutionZipParser _parser;
    private readonly IReadOnlyList<IRule> _rules;
    private readonly IAIEvaluator _aiEvaluator;

    public ScanSolutionFunction(SolutionZipParser parser, IReadOnlyList<IRule> rules, IAIEvaluator aiEvaluator)
    {
        _parser = parser;
        _rules = rules;
        _aiEvaluator = aiEvaluator;
    }

    [Function("ScanSolution")]
    public async Task<HttpResponseData> Run([
        HttpTrigger(AuthorizationLevel.Function, "post", Route = "scan-solution")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        ScanSolutionRequest? request = null;
        try
        {
            request = JsonSerializer.Deserialize<ScanSolutionRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid request payload");
            return bad;
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Base64Zip))
        {
            var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Missing Base64Zip in request");
            return bad;
        }

        byte[] zipBytes;
        try
        {
            zipBytes = Convert.FromBase64String(request.Base64Zip);
        }
        catch (FormatException)
        {
            var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Base64Zip is not valid base64");
            return bad;
        }

        var trees = await _parser.ParseSolutionAsync(zipBytes);

        // Collect all findings per tree first
        var treeFindings = new List<(UiTree Tree, IReadOnlyList<Finding> Findings)>();
        foreach (var tree in trees)
        {
            var baseResult = Engine.Analyze(tree, _rules);
            var enriched = await _aiEvaluator.EnrichFindingsAsync(tree, baseResult.Findings as IReadOnlyList<Finding> ?? baseResult.Findings.ToList(), cancellationToken);
            treeFindings.Add((tree, enriched));
        }

        var results = new List<SolutionScanResultDto>();

        // Group Canvas apps - one result per app
        var canvasGroups = treeFindings
            .Where(t => t.Tree.Surface == SurfaceType.CanvasApp)
            .GroupBy(t => t.Tree.AppName ?? "Unknown");

        foreach (var group in canvasGroups)
        {
            var allFindings = group.SelectMany(t => t.Findings)
                .DistinctBy(f => f.Id) // Deduplicate by finding Id
                .ToList();
            
            results.Add(new SolutionScanResultDto
            {
                AppName = group.Key,
                Surface = SurfaceType.CanvasApp,
                Result = ScanResult.FromFindings(allFindings)
            });
        }

        // Group MDA findings by MdaAppName - one result per MDA app
        var mdaGroups = treeFindings
            .Where(t => t.Tree.Surface == SurfaceType.ModelDrivenApp)
            .GroupBy(t => t.Tree.MdaAppName ?? "Model-Driven App");

        foreach (var group in mdaGroups)
        {
            var allFindings = group.SelectMany(t => t.Findings)
                .DistinctBy(f => f.Id) // Deduplicate by finding Id
                .ToList();
            
            results.Add(new SolutionScanResultDto
            {
                AppName = group.Key,
                Surface = SurfaceType.ModelDrivenApp,
                Result = ScanResult.FromFindings(allFindings)
            });
        }

        var ok = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await ok.WriteStringAsync(JsonSerializer.Serialize(results));
        return ok;
    }
}