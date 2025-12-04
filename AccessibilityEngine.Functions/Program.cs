using AccessibilityEngine.AI;
using AccessibilityEngine.Core.Rules;
using AccessibilityEngine.Functions;
using AccessibilityEngine.Rules;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

// Register services
builder.Services.AddSingleton<IReadOnlyList<IRule>>(_ => BasicRules.All);
// Register a local parser implementation (adapters project may replace this)
builder.Services.AddSingleton<SolutionZipParser>();
builder.Services.AddSingleton<IAIEvaluator, NoOpAIEvaluator>();

builder.ConfigureFunctionsWebApplication();

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
