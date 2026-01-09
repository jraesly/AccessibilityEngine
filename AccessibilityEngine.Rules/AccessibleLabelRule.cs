using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks all controls for accessible labels.
/// This is a comprehensive rule that applies to any control type.
/// </summary>
public sealed class AccessibleLabelRule : IRule
{
    public string Id => "ACCESSIBLE_LABEL";
    public string Description => "All interactive controls should have an accessible label.";
    public Severity Severity => Severity.High;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage, SurfaceType.DomSnapshot];

    // Control types that are purely decorative or structural (don't need accessible labels)
    private static readonly HashSet<string> DecorativeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Screen", "Container", "Group", "Gallery", "Form", "Rectangle", "Circle", "Line",
        "HtmlViewer", "PdfViewer", "Timer", "Component"
    };

    // Control types that are interactive and require accessible labels
    private static readonly HashSet<string> InteractiveTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Buttons
        "Button", "IconButton", "Classic/Button",
        
        // Input controls
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
        
        // Display controls that may need labels
        "Label", "Text", "Classic/Label",
        "Image", "Classic/Image",
        "Icon", "Classic/Icon",
        
        // Links
        "Link", "HyperLink"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Skip decorative/structural types
        if (DecorativeTypes.Contains(node.Type))
            yield break;

        // Check if this is an interactive control or if it's a Label type
        var isInteractive = InteractiveTypes.Contains(node.Type);
        var isLabel = node.Type.Contains("Label", StringComparison.OrdinalIgnoreCase) ||
                      node.Type.Contains("Text", StringComparison.OrdinalIgnoreCase);

        // For non-interactive, non-label controls that we don't recognize, skip
        if (!isInteractive && !isLabel)
        {
            // Log unknown type for debugging but don't flag it
            Console.WriteLine($"[AccessibleLabelRule] Unknown control type: {node.Type} (Id: {node.Id})");
            yield break;
        }

        // Check for accessible label
        var hasAccessibleLabel = HasAccessibleLabel(node);

        if (!hasAccessibleLabel)
        {
            var severity = isInteractive ? Severity.High : Severity.Medium;
            var typeDescription = isInteractive ? "Interactive control" : "Control";

            yield return new Finding(
                Id: $"{Id}:{node.Id}",
                Severity: severity,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: Id,
                Message: $"{typeDescription} '{node.Id}' of type '{node.Type}' is missing an accessible label.",
                WcagReference: "WCAG 2.1 – 4.1.2 Name, Role, Value",
                Section508Reference: "Section 508 - Name, Role, Value",
                Rationale: "Accessible labels ensure assistive technologies can convey the purpose of controls to users.",
                EntityName: node.Meta?.EntityName ?? context.EntityName,
                TabName: node.Meta?.TabName,
                SectionName: node.Meta?.SectionName,
                SuggestedFix: $"Set the AccessibleLabel property on '{node.Id}' to a descriptive text that conveys the control's purpose to screen reader users."
            );
        }
    }

    private static bool HasAccessibleLabel(UiNode node)
    {
        // Check direct Name and Text properties
        if (!string.IsNullOrWhiteSpace(node.Name) && !IsFormulaOrExpression(node.Name))
            return true;

        if (!string.IsNullOrWhiteSpace(node.Text) && !IsFormulaOrExpression(node.Text))
            return true;

        // Check Properties dictionary for accessibility-related values
        if (node.Properties != null)
        {
            var accessibilityProps = new[] { "AccessibleLabel", "Label", "Text", "Title", "Tooltip", "Alt", "AriaLabel" };
            foreach (var prop in accessibilityProps)
            {
                if (node.Properties.TryGetValue(prop, out var val))
                {
                    var strVal = val?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(strVal) && !IsFormulaOrExpression(strVal))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a value looks like a Power Apps formula rather than actual text.
    /// </summary>
    private static bool IsFormulaOrExpression(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        
        // Power Apps formulas often start with = or contain function calls
        return value.StartsWith("=") ||
               value.Contains("(") ||
               value.Contains("Self.") ||
               value.Contains("Parent.") ||
               value.Contains("ThisItem.") ||
               value.StartsWith("\"") && value.EndsWith("\""); // Quoted string formula
    }
}
