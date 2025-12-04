using System.IO;
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

public class AnalyzeUiFunction
{
    private readonly IReadOnlyList<IRule> _rules;
    private readonly IAIEvaluator _aiEvaluator;

    public AnalyzeUiFunction(IReadOnlyList<IRule> rules, IAIEvaluator aiEvaluator)
    {
        _rules = rules;
        _aiEvaluator = aiEvaluator;
    }

    [Function("AnalyzeUi")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "analyze-ui")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        UiTree? uiTree = null;
        try
        {
            uiTree = JsonSerializer.Deserialize<UiTree>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid JSON payload");
            return bad;
        }

        if (uiTree == null)
        {
            var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Missing UiTree payload");
            return bad;
        }

        var baseResult = Engine.Analyze(uiTree, _rules);
        var enriched = await _aiEvaluator.EnrichFindingsAsync(uiTree, baseResult.Findings as IReadOnlyList<Finding> ?? baseResult.Findings.ToList(), cancellationToken);
        var final = ScanResult.FromFindings(enriched);

        var ok = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await ok.WriteStringAsync(JsonSerializer.Serialize(final));
        return ok;
    }
}
