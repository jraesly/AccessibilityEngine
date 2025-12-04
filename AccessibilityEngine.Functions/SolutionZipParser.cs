using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AccessibilityEngine.Core.Models;

namespace AccessibilityEngine.Functions;

/// <summary>
/// Local fallback SolutionZipParser for Functions project.
/// Returns no UiTrees; adapters project should provide a richer implementation.
/// This keeps the Functions project buildable when the Adapters project is not loaded.
/// </summary>
public sealed class SolutionZipParser
{
    public Task<IReadOnlyList<UiTree>> ParseSolutionAsync(byte[] solutionZip)
    {
        if (solutionZip == null) throw new ArgumentNullException(nameof(solutionZip));
        IReadOnlyList<UiTree> empty = Array.Empty<UiTree>();
        return Task.FromResult(empty);
    }
}
