using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AccessibilityEngine.Core.Models;

namespace AccessibilityEngine.AI;

public interface IAIEvaluator
{
    Task<IReadOnlyList<Finding>> EnrichFindingsAsync(UiTree uiTree, IReadOnlyList<Finding> existingFindings, CancellationToken cancellationToken = default);
}
