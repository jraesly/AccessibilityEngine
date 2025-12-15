using AccessibilityEngine.AI;
using AccessibilityEngine.Core.Rules;
using AccessibilityEngine.Functions;
using AccessibilityEngine.Rules;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

// Register services
builder.Services.AddSingleton<IReadOnlyList<IRule>>(_ => BasicRules.All);
builder.Services.AddSingleton<SolutionZipParser>();

// Configure Azure OpenAI settings from configuration
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    
    return new AzureOpenAISettings
    {
        Endpoint = config["AzureOpenAI:Endpoint"] ?? "",
        ApiKey = config["AzureOpenAI:ApiKey"] ?? "",
        DeploymentName = config["AzureOpenAI:DeploymentName"] ?? "gpt-4o-mini",
        MaxBatchSize = int.TryParse(config["AzureOpenAI:MaxBatchSize"], out var batch) ? batch : 10,
        OnlyEnrichComplexFindings = !bool.TryParse(config["AzureOpenAI:EnrichAllFindings"], out var enrichAll) || !enrichAll
    };
});

// Register AI Evaluator - use Azure OpenAI if configured, otherwise NoOp
builder.Services.AddSingleton<IAIEvaluator>(sp =>
{
    var settings = sp.GetRequiredService<AzureOpenAISettings>();
    
    if (string.IsNullOrEmpty(settings.Endpoint) || string.IsNullOrEmpty(settings.ApiKey))
    {
        Console.WriteLine("[Startup] Azure OpenAI not configured, using NoOpAIEvaluator");
        return new NoOpAIEvaluator();
    }
    
    Console.WriteLine($"[Startup] Using AzureOpenAIEvaluator with deployment: {settings.DeploymentName}");
    return new AzureOpenAIEvaluator(settings);
});

builder.ConfigureFunctionsWebApplication();

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
