using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AccessibilityEngine.Core.Models;

namespace AccessibilityEngine.Functions;

/// <summary>
/// Minimal fallback parser used when the Adapters project is not available in the solution.
/// Returns no UiTrees to keep the Functions host buildable.
/// </summary>
public sealed class FallbackSolutionZipParser
{
    public Task<IReadOnlyList<UiTree>> ParseSolutionAsync(byte[] solutionZip)
    {
        if (solutionZip == null) throw new ArgumentNullException(nameof(solutionZip));
        IReadOnlyList<UiTree> empty = Array.Empty<UiTree>();
        return Task.FromResult(empty);
    }
}
