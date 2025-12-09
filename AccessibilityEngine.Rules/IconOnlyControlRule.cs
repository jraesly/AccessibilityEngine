using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

public sealed class IconOnlyControlRule : IRule
{
    public string Id => "ICON_ONLY_CONTROL_NO_LABEL";
    public string Description => "Icon-only controls must expose an accessible label.";
    public Severity Severity => Severity.High;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage, SurfaceType.DomSnapshot];

    private static readonly HashSet<string> IconTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Icon", "IconButton", "Glyph"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        var isIconOnly = false;

        if (IconTypes.Contains(node.Type))
            isIconOnly = true;

        if (!isIconOnly && node.Properties != null && node.Properties.TryGetValue("IsIconOnly", out var iso) && iso is bool bis && bis)
            isIconOnly = true;

        if (!isIconOnly) yield break;

        var name = node.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = node.Text?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            yield return new Finding(
                Id: $"{Id}:{node.Id}",
                Severity: Severity.High,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: Id,
                Message: "Icon-only control must expose an accessible label describing its action.",
                WcagReference: "WCAG 2.1 – 4.1.2",
                Section508Reference: "Section 508 - Name, Role, Value",
                Rationale: "Icon-only controls without labels are not discoverable by assistive technologies.",
                SuggestedFix: $"Set the AccessibleLabel property on icon '{node.Id}' to describe its action (e.g., 'Delete item', 'Edit record', 'Open menu')."
            );
        }
    }
}
