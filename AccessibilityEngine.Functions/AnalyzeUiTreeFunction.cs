using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AccessibilityEngine.AI;
using AccessibilityEngine.Core.Engine;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AccessibilityEngine.Functions;

/// <summary>
/// Request payload for analyzing a UI tree.
/// </summary>
public sealed class AnalyzeUiTreeRequest
{
    /// <summary>
    /// The UI tree to analyze. Can be a single tree or array of trees.
    /// </summary>
    public UiTreeInputDto? Tree { get; init; }

    /// <summary>
    /// Multiple UI trees to analyze.
    /// </summary>
    public IReadOnlyList<UiTreeInputDto>? Trees { get; init; }

    /// <summary>
    /// Whether to include AI-enriched findings. Default is false.
    /// </summary>
    public bool IncludeAiEnrichment { get; init; } = false;
}

/// <summary>
/// Input DTO for UI tree analysis.
/// </summary>
public sealed class UiTreeInputDto
{
    public string? AppName { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SurfaceType Surface { get; init; } = SurfaceType.CanvasApp;

    public IReadOnlyList<UiNodeInputDto>? Nodes { get; init; }
}

/// <summary>
/// Input DTO for UI node.
/// </summary>
public sealed class UiNodeInputDto
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string? Role { get; init; }
    public string? Name { get; init; }
    public string? Text { get; init; }
    public Dictionary<string, object?>? Properties { get; init; }
    public IReadOnlyList<UiNodeInputDto>? Children { get; init; }
    public string? ScreenName { get; init; }
}

/// <summary>
/// Response containing analysis results.
/// </summary>
public sealed class AnalyzeUiTreeResponse
{
    public IReadOnlyList<AppAnalysisResult> Results { get; init; } = Array.Empty<AppAnalysisResult>();
    public int TotalFindings { get; init; }
    public IReadOnlyDictionary<string, int> FindingsBySeverity { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<string> RulesEvaluated { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Analysis result for a single app.
/// </summary>
public sealed class AppAnalysisResult
{
    public string? AppName { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SurfaceType Surface { get; init; }

    public int NodesAnalyzed { get; init; }
    public ScanResult Result { get; init; } = default!;
}

/// <summary>
/// Azure Function that analyzes UI trees with accessibility rules.
/// This function is focused solely on rule evaluation.
/// </summary>
public class AnalyzeUiTreeFunction
{
    private readonly IReadOnlyList<IRule> _rules;
    private readonly IAIEvaluator _aiEvaluator;

    public AnalyzeUiTreeFunction(IReadOnlyList<IRule> rules, IAIEvaluator aiEvaluator)
    {
        _rules = rules;
        _aiEvaluator = aiEvaluator;
    }

    /// <summary>
    /// Analyzes UI trees with accessibility rules.
    /// </summary>
    [Function("AnalyzeUiTree")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "analyze-uitree")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken);

        AnalyzeUiTreeRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<AnalyzeUiTreeRequest>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            var badRequest = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync($"Invalid JSON payload: {ex.Message}", cancellationToken);
            return badRequest;
        }

        if (request == null || (request.Tree == null && (request.Trees == null || request.Trees.Count == 0)))
        {
            var badRequest = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Missing Tree or Trees in request", cancellationToken);
            return badRequest;
        }

        // Collect all trees to analyze
        var inputTrees = new List<UiTreeInputDto>();
        if (request.Tree != null)
        {
            inputTrees.Add(request.Tree);
        }
        if (request.Trees != null)
        {
            inputTrees.AddRange(request.Trees);
        }

        var results = new List<AppAnalysisResult>();
        var totalFindings = 0;
        var findingsBySeverity = new Dictionary<string, int>
        {
            ["Critical"] = 0,
            ["High"] = 0,
            ["Medium"] = 0,
            ["Low"] = 0,
            ["Info"] = 0
        };

        foreach (var inputTree in inputTrees)
        {
            // Convert DTO to domain model
            var tree = ConvertToUiTree(inputTree);
            var nodeCount = CountNodes(tree.Nodes);

            // Run analysis
            var baseResult = Engine.Analyze(tree, _rules);

            // Optionally enrich with AI
            ScanResult finalResult;
            if (request.IncludeAiEnrichment)
            {
                var enriched = await _aiEvaluator.EnrichFindingsAsync(
                    tree,
                    baseResult.Findings as IReadOnlyList<Finding> ?? baseResult.Findings.ToList(),
                    cancellationToken);
                finalResult = ScanResult.FromFindings(enriched);
            }
            else
            {
                finalResult = baseResult;
            }

            // Aggregate counts
            totalFindings += finalResult.Findings.Count;
            foreach (var finding in finalResult.Findings)
            {
                var severity = finding.Severity.ToString();
                if (findingsBySeverity.ContainsKey(severity))
                {
                    findingsBySeverity[severity]++;
                }
            }

            results.Add(new AppAnalysisResult
            {
                AppName = tree.AppName,
                Surface = tree.Surface,
                NodesAnalyzed = nodeCount,
                Result = finalResult
            });
        }

        var response = new AnalyzeUiTreeResponse
        {
            Results = results,
            TotalFindings = totalFindings,
            FindingsBySeverity = findingsBySeverity,
            RulesEvaluated = _rules.Select(r => r.Id).ToList()
        };

        var okResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
        okResponse.Headers.Add("Content-Type", "application/json");
        await okResponse.WriteStringAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }), cancellationToken);

        return okResponse;
    }

    private static UiTree ConvertToUiTree(UiTreeInputDto dto)
    {
        var nodes = dto.Nodes != null ? ConvertNodes(dto.Nodes, dto.AppName) : new List<UiNode>();
        return new UiTree(dto.Surface, dto.AppName, nodes);
    }

    private static IReadOnlyList<UiNode> ConvertNodes(IReadOnlyList<UiNodeInputDto> dtos, string? defaultScreenName)
    {
        var result = new List<UiNode>();

        foreach (var dto in dtos)
        {
            var children = dto.Children != null && dto.Children.Count > 0
                ? ConvertNodes(dto.Children, dto.ScreenName ?? defaultScreenName)
                : new List<UiNode>();

            var props = dto.Properties != null
                ? new Dictionary<string, object?>(dto.Properties)
                : new Dictionary<string, object?>();

            var meta = new UiMeta(SurfaceType.CanvasApp, dto.ScreenName ?? defaultScreenName, null, null);

            result.Add(new UiNode(
                dto.Id,
                dto.Type,
                dto.Role,
                dto.Name,
                dto.Text,
                props,
                children,
                meta
            ));
        }

        return result;
    }

    private static int CountNodes(IReadOnlyList<UiNode> nodes)
    {
        var count = nodes.Count;
        foreach (var node in nodes)
        {
            if (node.Children.Count > 0)
            {
                count += CountNodes(node.Children);
            }
        }
        return count;
    }
}
