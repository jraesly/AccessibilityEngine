using System.Collections.Generic;
using AccessibilityEngine.Core.Models;

namespace AccessibilityEngine.Core.Rules;

/// <summary>
/// Context provided to each rule during evaluation.
/// </summary>
public sealed record RuleContext(SurfaceType Surface, string? AppName);

public interface IRule
{
    string Id { get; }
    string Description { get; }
    SurfaceType[] AppliesTo { get; }

    IEnumerable<Finding> Evaluate(UiNode node, RuleContext context);
}
