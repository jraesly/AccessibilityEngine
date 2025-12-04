using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AccessibilityEngine.AI;
using AccessibilityEngine.Core.Models;

namespace AccessibilityEngine.Functions;

internal sealed class NoOpAIEvaluator : IAIEvaluator
{
    public Task<IReadOnlyList<Finding>> EnrichFindingsAsync(UiTree uiTree, IReadOnlyList<Finding> existingFindings, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(existingFindings);
    }
}
