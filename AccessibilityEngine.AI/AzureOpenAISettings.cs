namespace AccessibilityEngine.AI;

/// <summary>
/// Configuration settings for Azure OpenAI / Azure AI Foundry integration.
/// </summary>
public sealed class AzureOpenAISettings
{
    /// <summary>
    /// The Azure OpenAI or Azure AI Foundry endpoint URL.
    /// </summary>
    public required string Endpoint { get; init; }

    /// <summary>
    /// The API key for authentication.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// The deployment/model name (e.g., "gpt-4o-mini", "gpt-5-nano").
    /// </summary>
    public required string DeploymentName { get; init; }

    /// <summary>
    /// Maximum number of findings to process in a single batch.
    /// </summary>
    public int MaxBatchSize { get; init; } = 10;

    /// <summary>
    /// Whether to only enrich findings that need AI analysis (PCF, conditional visibility, etc.).
    /// </summary>
    public bool OnlyEnrichComplexFindings { get; init; } = true;
}
