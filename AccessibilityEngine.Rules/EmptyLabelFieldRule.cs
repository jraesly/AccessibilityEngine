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
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage, SurfaceType.DomSnapshot];

    // Expanded list of field types including Power Apps control types
    private static readonly HashSet<string> FieldTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Power Apps Canvas controls
        "TextInput", "Text input", "Classic/TextInput",
        "ComboBox", "Combo box", "Classic/ComboBox",
        "Dropdown", "Drop down", "Classic/Dropdown",
        "DatePicker", "Date picker", "Classic/DatePicker",
        "Slider", "Classic/Slider",
        "Toggle", "Classic/Toggle",
        "Rating", "Classic/Rating",
        "ListBox", "List box", "Classic/ListBox",
        "Radio", "RadioGroup", "Classic/Radio",
        "Checkbox", "Check box", "Classic/Checkbox",
        "PenInput", "Pen input",
        "RichTextEditor", "Rich text editor",
        
        // Model-driven / generic types
        "Field", "Lookup", "Input", "Textbox", "Select",
        "input", "textarea", "select"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;
        if (AppliesTo is null || !AppliesTo.Contains(context.Surface)) yield break;
        if (!FieldTypes.Contains(node.Type)) yield break;

        string? label = null;
        
        // Check Name property
        if (!string.IsNullOrWhiteSpace(node.Name))
            label = node.Name.Trim();
        
        // Check Text property
        if (string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(node.Text))
            label = node.Text.Trim();
        
        // Check various property names for labels
        if (string.IsNullOrWhiteSpace(label) && node.Properties != null)
        {
            var labelProps = new[] { "Label", "AccessibleLabel", "HintText", "Tooltip", "PlaceholderText", "Default" };
            foreach (var prop in labelProps)
            {
                if (node.Properties.TryGetValue(prop, out var val))
                {
                    var strVal = val?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(strVal) && !IsFormulaOrExpression(strVal))
                    {
                        label = strVal;
                        break;
                    }
                }
            }
        }

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
                Message: $"Input field '{node.Id}' is missing a visible or programmatic label.",
                WcagReference: "WCAG 2.1 – 3.3.2",
                Section508Reference: "Section 508 - Labeling",
                Rationale: "Labels ensure users understand the purpose of form fields.",
                SuggestedFix: $"Add a Label control associated with '{node.Id}', or set the AccessibleLabel property to describe what input is expected."
            );
        }
    }

    /// <summary>
    /// Checks if a value looks like a Power Apps formula rather than actual text.
    /// </summary>
    private static bool IsFormulaOrExpression(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        
        // Power Apps formulas often start with = or contain function calls
        return value.StartsWith("=") ||
               value.Contains("(") ||
               value.Contains("Self.") ||
               value.Contains("Parent.") ||
               value.Contains("ThisItem.");
    }
}

