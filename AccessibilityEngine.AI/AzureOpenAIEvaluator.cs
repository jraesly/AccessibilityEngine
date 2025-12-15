using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AccessibilityEngine.Core.Models;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace AccessibilityEngine.AI;

/// <summary>
/// AI Evaluator that uses Azure OpenAI / Azure AI Foundry to enrich accessibility findings
/// with contextual remediation suggestions.
/// </summary>
public sealed class AzureOpenAIEvaluator : IAIEvaluator
{
    private readonly AzureOpenAISettings _settings;
    private readonly ChatClient _chatClient;

    // Issue types that benefit from AI-enhanced suggestions
    private static readonly HashSet<string> ComplexIssueTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "MDA_PCF_CONTROL",
        "MDA_CONDITIONAL_VISIBILITY",
        "MDA_EMBEDDED_CONTENT",
        "SCREEN_READER_ORDER_ZINDEX",
        "SCREEN_READER_ORDER_ORDER",
        "COLOR_CONTRAST",
        "COLOR_CONTRAST_COLOR_ONLY"
    };

    public AzureOpenAIEvaluator(AzureOpenAISettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        var credential = new ApiKeyCredential(_settings.ApiKey);
        var client = new AzureOpenAIClient(new Uri(_settings.Endpoint), credential);
        _chatClient = client.GetChatClient(_settings.DeploymentName);
    }

    public async Task<IReadOnlyList<Finding>> EnrichFindingsAsync(
        UiTree uiTree,
        IReadOnlyList<Finding> existingFindings,
        CancellationToken cancellationToken = default)
    {
        if (existingFindings.Count == 0)
            return existingFindings;

        // Filter findings that need AI enrichment
        var findingsToEnrich = _settings.OnlyEnrichComplexFindings
            ? existingFindings.Where(f => ComplexIssueTypes.Contains(f.IssueType)).ToList()
            : existingFindings.ToList();

        if (findingsToEnrich.Count == 0)
            return existingFindings;

        // Process in batches
        var enrichedFindings = new Dictionary<string, Finding>();
        var batches = findingsToEnrich.Chunk(_settings.MaxBatchSize);

        foreach (var batch in batches)
        {
            try
            {
                var enriched = await EnrichBatchAsync(uiTree, batch, cancellationToken);
                foreach (var finding in enriched)
                {
                    enrichedFindings[finding.Id] = finding;
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail - return original findings for this batch
                Console.WriteLine($"[AzureOpenAIEvaluator] Error enriching batch: {ex.Message}");
                foreach (var finding in batch)
                {
                    enrichedFindings[finding.Id] = finding;
                }
            }
        }

        // Merge enriched findings back with originals
        return existingFindings
            .Select(f => enrichedFindings.TryGetValue(f.Id, out var enriched) ? enriched : f)
            .ToList();
    }

    private async Task<IReadOnlyList<Finding>> EnrichBatchAsync(
        UiTree uiTree,
        IEnumerable<Finding> findings,
        CancellationToken cancellationToken)
    {
        var findingsList = findings.ToList();
        var prompt = BuildEnrichmentPrompt(uiTree, findingsList);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(GetSystemPrompt()),
            new UserChatMessage(prompt)
        };

        var response = await _chatClient.CompleteChatAsync(messages, null, cancellationToken);
        var content = response.Value.Content[0].Text;

        return ParseEnrichedFindings(findingsList, content);
    }

    private static string GetSystemPrompt()
    {
        return """
            You are an accessibility expert specializing in Power Apps (Canvas and Model-Driven Apps).
            Your task is to provide specific, actionable remediation suggestions for accessibility issues.
            
            You have deep knowledge of:
            - WCAG 2.1 and 2.2 guidelines
            - Section 508 requirements
            - Power Apps accessibility features and limitations
            - PCF (Power Apps Component Framework) control accessibility
            - Screen reader behavior with Power Apps
            
            When providing suggestions:
            1. Be specific to the control type and context
            2. Reference specific Power Apps properties when applicable
            3. Provide step-by-step guidance when needed
            4. Consider the broader user experience
            
            Respond in JSON format only.
            """;
    }

    private static string BuildEnrichmentPrompt(UiTree uiTree, List<Finding> findings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze these accessibility findings and provide enhanced remediation suggestions.");
        sb.AppendLine();
        sb.AppendLine("App Context:");
        sb.AppendLine($"- Surface Type: {uiTree.Surface}");
        sb.AppendLine($"- App Name: {uiTree.AppName ?? "Unknown"}");
        if (uiTree.MdaAppName != null)
            sb.AppendLine($"- MDA App Name: {uiTree.MdaAppName}");
        sb.AppendLine();
        sb.AppendLine("Findings to enrich:");
        sb.AppendLine();

        foreach (var finding in findings)
        {
            sb.AppendLine($"Finding ID: {finding.Id}");
            sb.AppendLine($"  Issue Type: {finding.IssueType}");
            sb.AppendLine($"  Control: {finding.ControlId} (Type: {finding.ControlType})");
            sb.AppendLine($"  Message: {finding.Message}");
            sb.AppendLine($"  Current Suggestion: {finding.SuggestedFix ?? "None"}");
            sb.AppendLine($"  WCAG: {finding.WcagReference}");
            if (finding.EntityName != null)
                sb.AppendLine($"  Entity: {finding.EntityName}");
            sb.AppendLine();
        }

        sb.AppendLine("""
            Respond with a JSON object containing an array of findings with enhanced suggestions:
            {
              "findings": [
                {
                  "id": "finding-id",
                  "enhancedSuggestion": "Detailed, actionable remediation steps..."
                }
              ]
            }
            
            Make suggestions specific to Power Apps and the control context.
            """);

        return sb.ToString();
    }

    private static IReadOnlyList<Finding> ParseEnrichedFindings(List<Finding> originalFindings, string aiResponse)
    {
        try
        {
            // Extract JSON from response (handle markdown code blocks)
            var json = aiResponse;
            if (json.Contains("```"))
            {
                var start = json.IndexOf('{');
                var end = json.LastIndexOf('}');
                if (start >= 0 && end > start)
                    json = json.Substring(start, end - start + 1);
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("findings", out var findingsArray))
                return originalFindings;

            var enrichmentMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in findingsArray.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var idProp) &&
                    item.TryGetProperty("enhancedSuggestion", out var suggestionProp))
                {
                    var id = idProp.GetString();
                    var suggestion = suggestionProp.GetString();
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(suggestion))
                    {
                        enrichmentMap[id] = suggestion;
                    }
                }
            }

            // Apply enrichments
            return originalFindings.Select(f =>
            {
                if (enrichmentMap.TryGetValue(f.Id, out var enhanced))
                {
                    return f with { SuggestedFix = enhanced };
                }
                return f;
            }).ToList();
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[AzureOpenAIEvaluator] Failed to parse AI response: {ex.Message}");
            return originalFindings;
        }
    }
}
