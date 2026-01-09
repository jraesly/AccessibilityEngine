using System.Collections.Generic;

namespace AccessibilityEngine.Core.Models;

/// <summary>
/// Identifies which kind of UI surface is being scanned.
/// </summary>
public enum SurfaceType
{
    CanvasApp,
    ModelDrivenApp,
    PortalPage,
    DomSnapshot
}

/// <summary>
/// Severity levels for findings.
/// </summary>
public enum Severity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Metadata about where a node lives in the app.
/// </summary>
public sealed record UiMeta(
    SurfaceType Surface, 
    string? ScreenName = null, 
    string? FormName = null, 
    string? SourcePath = null,
    string? EntityName = null,
    string? TabName = null,
    string? SectionName = null
);

/// <summary>
/// A generic UI element in the normalized tree.
/// </summary>
public sealed record UiNode(
    string Id,
    string Type,
    string? Role,
    string? Name,
    string? Text,
    IReadOnlyDictionary<string, object?> Properties,
    IReadOnlyList<UiNode> Children,
    UiMeta Meta
);

/// <summary>
/// Root for a given surface/app.
/// </summary>
/// <param name="Surface">The surface type being scanned.</param>
/// <param name="AppName">For Canvas: the app name. For MDA: the table/entity name.</param>
/// <param name="Nodes">The UI nodes in this tree.</param>
/// <param name="MdaAppName">For MDA only: the Model-Driven App name (e.g., "test MDA").</param>
public sealed record UiTree(
    SurfaceType Surface,
    string? AppName,
    IReadOnlyList<UiNode> Nodes,
    string? MdaAppName = null
);

/// <summary>
/// A finding produced by a rule evaluation.
/// </summary>
public sealed record Finding(
    string Id,
    Severity Severity,
    SurfaceType Surface,
    string? AppName,
    string? Screen,
    string? ControlId,
    string? ControlType,
    string IssueType,
    string Message,
    string? WcagReference = null,
    string? Section508Reference = null,
    string? Rationale = null,
    string? EntityName = null,
    string? TabName = null,
    string? SectionName = null,
    string? SuggestedFix = null,
    WcagCriterion WcagCriterion = WcagCriterion.None
);

public sealed record ScanSummary(int Total, IReadOnlyDictionary<Severity, int> BySeverity);

public sealed record ScanResult(IReadOnlyList<Finding> Findings, ScanSummary Summary)
{
    public static ScanResult FromFindings(IReadOnlyList<Finding> findings)
    {
        var list = findings ?? Array.Empty<Finding>();
        var bySeverity = Enum.GetValues(typeof(Severity)).Cast<Severity>().ToDictionary(s => s, s => 0);

        foreach (var f in list)
        {
            if (bySeverity.ContainsKey(f.Severity))
                bySeverity[f.Severity]++;
        }

        var summary = new ScanSummary(list.Count, bySeverity);
        return new ScanResult(list, summary);
    }
}
