using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AccessibilityEngine.Core.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AccessibilityEngine.Functions;

/// <summary>
/// Request payload for extracting UI trees from a Power Apps solution.
/// </summary>
public sealed class ExtractUiTreeRequest
{
    /// <summary>
    /// Base64-encoded Power Apps solution ZIP file.
    /// </summary>
    public string? Base64Zip { get; init; }
}

/// <summary>
/// Response containing extracted UI trees from a solution.
/// </summary>
public sealed class ExtractUiTreeResponse
{
    /// <summary>
    /// List of UI trees extracted from the solution.
    /// </summary>
    public IReadOnlyList<UiTreeDto> Trees { get; init; } = Array.Empty<UiTreeDto>();

    /// <summary>
    /// Total number of controls found across all trees.
    /// </summary>
    public int TotalControls { get; init; }

    /// <summary>
    /// Any warnings or informational messages from parsing.
    /// </summary>
    public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
}

/// <summary>
/// DTO representation of a UI tree for JSON serialization.
/// </summary>
public sealed class UiTreeDto
{
    public string? AppName { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SurfaceType Surface { get; init; }

    public IReadOnlyList<UiNodeDto> Nodes { get; init; } = Array.Empty<UiNodeDto>();
}

/// <summary>
/// DTO representation of a UI node for JSON serialization.
/// </summary>
public sealed class UiNodeDto
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string? Role { get; init; }
    public string? Name { get; init; }
    public string? Text { get; init; }
    public IReadOnlyDictionary<string, object?>? Properties { get; init; }
    public IReadOnlyList<UiNodeDto>? Children { get; init; }
    public string? ScreenName { get; init; }
}

/// <summary>
/// Azure Function that extracts UI trees from a Power Apps solution ZIP.
/// This function is focused solely on parsing and extracting the UI structure.
/// </summary>
public class ExtractUiTreeFunction
{
    private readonly SolutionZipParser _parser;

    public ExtractUiTreeFunction(SolutionZipParser parser)
    {
        _parser = parser;
    }

    /// <summary>
    /// Extracts UI trees from a Power Apps solution ZIP file.
    /// </summary>
    /// <param name="req">HTTP request containing Base64-encoded ZIP in the body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON response with extracted UI trees.</returns>
    [Function("ExtractUiTree")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "extract-uitree")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken);

        ExtractUiTreeRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<ExtractUiTreeRequest>(body, new JsonSerializerOptions
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

        if (request == null || string.IsNullOrWhiteSpace(request.Base64Zip))
        {
            var badRequest = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Missing Base64Zip in request", cancellationToken);
            return badRequest;
        }

        byte[] zipBytes;
        try
        {
            zipBytes = Convert.FromBase64String(request.Base64Zip);
        }
        catch (FormatException)
        {
            var badRequest = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Base64Zip is not valid base64", cancellationToken);
            return badRequest;
        }

        var messages = new List<string>();
        IReadOnlyList<UiTree> trees;

        try
        {
            trees = await _parser.ParseSolutionAsync(zipBytes);
            messages.Add($"Successfully parsed solution with {trees.Count} app(s)");
        }
        catch (Exception ex)
        {
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Failed to parse solution: {ex.Message}", cancellationToken);
            return errorResponse;
        }

        // Convert to DTOs
        var treeDtos = new List<UiTreeDto>();
        var totalControls = 0;

        foreach (var tree in trees)
        {
            var nodeDtos = ConvertNodes(tree.Nodes);
            totalControls += CountNodes(tree.Nodes);

            treeDtos.Add(new UiTreeDto
            {
                AppName = tree.AppName,
                Surface = tree.Surface,
                Nodes = nodeDtos
            });

            messages.Add($"App '{tree.AppName}': {CountNodes(tree.Nodes)} control(s) found");
        }

        var response = new ExtractUiTreeResponse
        {
            Trees = treeDtos,
            TotalControls = totalControls,
            Messages = messages
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

    private static IReadOnlyList<UiNodeDto> ConvertNodes(IReadOnlyList<UiNode> nodes)
    {
        var result = new List<UiNodeDto>();

        foreach (var node in nodes)
        {
            result.Add(new UiNodeDto
            {
                Id = node.Id,
                Type = node.Type,
                Role = node.Role,
                Name = node.Name,
                Text = node.Text,
                Properties = node.Properties,
                Children = node.Children.Count > 0 ? ConvertNodes(node.Children) : null,
                ScreenName = node.Meta?.ScreenName
            });
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
