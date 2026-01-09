using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

public sealed class MissingAccessibleNameRule : IRule
{
    public string Id => "MISSING_ACCESSIBLE_NAME";
    public string Description => "Interactive controls must have an accessible name.";
    public Severity Severity => Severity.High;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage, SurfaceType.DomSnapshot];

    private static readonly HashSet<string> InteractiveTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Button", "IconButton", "Link", "ImageButton", "Submit", "Command",
        "a", "button", "input-button"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        if (!InteractiveTypes.Contains(node.Type))
            yield break;

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
                Message: "Interactive control is missing an accessible name (label/text).",
                WcagReference: "WCAG 2.1 – 4.1.2",
                Section508Reference: "Section 508 - Name, Role, Value",
                Rationale: "Assistive technologies rely on accessible names to convey control purpose to users.",
                EntityName: node.Meta?.EntityName ?? context.EntityName,
                TabName: node.Meta?.TabName,
                SectionName: node.Meta?.SectionName,
                SuggestedFix: $"Set the Text property or AccessibleLabel property on '{node.Id}' to describe the control's action or purpose."
            );
        }
    }
}

