using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

public sealed class EmptyLabelFieldRule : IRule
{
    public string Id => "MISSING_FIELD_LABEL";
    public string Description => "Input fields should have visible or programmatic labels.";
    public SurfaceType[] AppliesTo => new[] { SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp };

    private static readonly HashSet<string> FieldTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "TextInput", "ComboBox", "Dropdown", "Field", "Lookup", "Input", "Textbox"
    };

    public IEnumerable<Finding> Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;
        if (!AppliesTo.Contains(context.Surface)) yield break;
        if (!FieldTypes.Contains(node.Type)) yield break;

        string? label = null;
        if (node.Name != null) label = node.Name.Trim();
        if (string.IsNullOrWhiteSpace(label) && node.Text != null) label = node.Text.Trim();
        if (string.IsNullOrWhiteSpace(label) && node.Properties != null && node.Properties.TryGetValue("Label", out var l) && l is string ls)
            label = ls.Trim();

        if (string.IsNullOrWhiteSpace(label))
        {
            yield return new Finding(
                Id: $"{Id}:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: Id,
                Message: "Input field is missing a visible or programmatic label.",
                WcagReference: "WCAG 2.1 – 3.3.2",
                Section508Reference: "Section 508 - Labeling",
                Rationale: "Labels ensure users understand the purpose of form fields."
            );
        }
    }
}
