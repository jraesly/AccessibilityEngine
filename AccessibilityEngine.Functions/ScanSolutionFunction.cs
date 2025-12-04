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
    public string? AppName { get; init; }
    public SurfaceType Surface { get; init; }
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

        var results = new List<SolutionScanResultDto>();
        foreach (var tree in trees)
        {
            var baseResult = Engine.Analyze(tree, _rules);
            var enriched = await _aiEvaluator.EnrichFindingsAsync(tree, baseResult.Findings as IReadOnlyList<Finding> ?? baseResult.Findings.ToList(), cancellationToken);
            var final = ScanResult.FromFindings(enriched);

            results.Add(new SolutionScanResultDto { AppName = tree.AppName, Surface = tree.Surface, Result = final });
        }

        var ok = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await ok.WriteStringAsync(JsonSerializer.Serialize(results));
        return ok;
    }
}
