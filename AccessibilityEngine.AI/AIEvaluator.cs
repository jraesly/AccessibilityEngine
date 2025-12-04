using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AccessibilityEngine.Core.Models;

namespace AccessibilityEngine.AI;

public class AIEvaluator : IAIEvaluator
{
    public Task<IReadOnlyList<Finding>> EnrichFindingsAsync(UiTree uiTree, IReadOnlyList<Finding> existingFindings, CancellationToken cancellationToken = default)
    {
        // For now, pass through findings unchanged. Future: call Azure OpenAI + RAG.
        return Task.FromResult(existingFindings);
    }
}
