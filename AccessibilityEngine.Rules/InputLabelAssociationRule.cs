using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for proper label-input association.
/// WCAG 1.3.1: Information, structure, and relationships conveyed through presentation can be programmatically determined.
/// WCAG 3.3.2: Labels or instructions are provided when content requires user input.
/// WCAG 2.5.3: For components with labels that include text, the name contains the text that is presented visually.
/// </summary>
public sealed class InputLabelAssociationRule : IRule
{
    public string Id => "INPUT_LABEL_ASSOCIATION";
    public string Description => "Input controls must have properly associated labels that are programmatically determinable.";
    public Severity Severity => Severity.High;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage, SurfaceType.DomSnapshot];

    // Input control types that require labels
    private static readonly HashSet<string> InputTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "TextInput", "Text input", "Classic/TextInput",
        "ComboBox", "Combo box", "Classic/ComboBox",
        "Dropdown", "Drop down", "Classic/Dropdown",
        "DatePicker", "Date picker", "Classic/DatePicker",
        "Slider", "Classic/Slider",
        "Rating", "Classic/Rating",
        "Checkbox", "Check box", "Classic/Checkbox",
        "Radio", "RadioGroup", "Classic/Radio",
        "Toggle", "Classic/Toggle",
        "ListBox", "List box", "Classic/ListBox",
        "PenInput", "Pen input",
        "RichTextEditor", "Rich text editor"
    };

    // Label control types
    private static readonly HashSet<string> LabelTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Label", "Text", "Classic/Label"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Only check input controls
        if (!InputTypes.Contains(node.Type))
            yield break;

        // Check for label association
        var hasAssociatedLabel = HasAssociatedLabel(node, context);
        var hasAccessibleName = HasAccessibleName(node);
        var visualLabel = FindVisualLabel(node, context);
        var accessibleName = GetAccessibleName(node);

        // Check 1: Must have either associated label or accessible name
        if (!hasAssociatedLabel && !hasAccessibleName)
        {
            yield return new Finding(
                Id: $"{Id}:MISSING:{node.Id}",
                Severity: Severity.High,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_MISSING_LABEL",
                Message: $"Input control '{node.Id}' does not have an associated label or accessible name. Screen reader users will not know what to enter.",
                WcagReference: "WCAG 2.2 § 3.3.2 Labels or Instructions (Level A)",
                Section508Reference: "Section 508 E207.2 - Labels or Instructions",
                Rationale: "Labels or instructions must be provided when content requires user input, so all users understand what information is needed.",
                SuggestedFix: $"Add a Label control positioned near '{node.Id}' and set the AccessibleLabel property on the input to match the label text, or set AccessibleLabel directly if a visible label is not appropriate.",
                WcagCriterion: WcagCriterion.LabelsOrInstructions_3_3_2
            );
            yield break;
        }

        // Check 2: If there's a visual label, accessible name should contain it (Label in Name)
        if (visualLabel != null && accessibleName != null)
        {
            var visualText = ExtractLabelText(visualLabel);
            if (!string.IsNullOrWhiteSpace(visualText) && 
                !accessibleName.Contains(visualText, StringComparison.OrdinalIgnoreCase))
            {
                yield return new Finding(
                    Id: $"{Id}:MISMATCH:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_LABEL_NAME_MISMATCH",
                    Message: $"Input control '{node.Id}' has an accessible name ('{accessibleName}') that doesn't contain its visual label text ('{visualText}'). This may confuse voice control users.",
                    WcagReference: "WCAG 2.2 § 2.5.3 Label in Name (Level A)",
                    Section508Reference: "Section 508 E207.2 - Label in Name",
                    Rationale: "When a control has a visible label, the accessible name should contain that text so voice control users can activate controls by speaking their visible labels.",
                    SuggestedFix: $"Update the AccessibleLabel on '{node.Id}' to include the visual label text '{visualText}'. A best practice is to have the label text at the start of the accessible name.",
                    WcagCriterion: WcagCriterion.LabelInName_2_5_3
                );
            }
        }

        // Check 3: Placeholder text is not a substitute for labels
        if (!hasAccessibleName && HasOnlyPlaceholder(node))
        {
            yield return new Finding(
                Id: $"{Id}:PLACEHOLDER:{node.Id}",
                Severity: Severity.High,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_PLACEHOLDER_NOT_LABEL",
                Message: $"Input control '{node.Id}' uses placeholder text instead of a proper label. Placeholder text disappears when users start typing.",
                WcagReference: "WCAG 2.2 § 3.3.2 Labels or Instructions (Level A)",
                Section508Reference: "Section 508 E207.2 - Labels or Instructions",
                Rationale: "Placeholder text is not a substitute for labels because it disappears when users enter data, making it unavailable for reference.",
                SuggestedFix: $"Add a persistent Label control for '{node.Id}' and set the AccessibleLabel property. The placeholder can remain as additional hint text.",
                WcagCriterion: WcagCriterion.LabelsOrInstructions_3_3_2
            );
        }

        // Check 4: Labels should be positioned appropriately
        if (visualLabel != null)
        {
            var labelPosition = GetLabelPosition(visualLabel, node);
            if (labelPosition == LabelPosition.Unknown || labelPosition == LabelPosition.Far)
            {
                yield return new Finding(
                    Id: $"{Id}:POSITION:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_LABEL_POSITION",
                    Message: $"The label for input '{node.Id}' may not be clearly associated visually. Labels should be positioned close to their inputs.",
                    WcagReference: "WCAG 2.2 § 1.3.1 Info and Relationships (Level A)",
                    Section508Reference: "Section 508 E207.2 - Info and Relationships",
                    Rationale: "Visual proximity helps all users understand the relationship between labels and inputs. For left-to-right languages, labels typically appear above or to the left of inputs.",
                    SuggestedFix: $"Position the label immediately above or to the left of '{node.Id}' (for left-to-right languages). For checkboxes and radio buttons, labels typically appear to the right.",
                    WcagCriterion: WcagCriterion.InfoAndRelationships_1_3_1
                );
            }
        }
    }

    private static bool HasAssociatedLabel(UiNode node, RuleContext context)
    {
        // Check for LabelFor-style association
        foreach (var sibling in context.Siblings)
        {
            if (LabelTypes.Contains(sibling.Type))
            {
                // Check if label references this input
                if (sibling.Properties?.TryGetValue("For", out var forValue) == true)
                {
                    if (forValue?.ToString() == node.Id)
                        return true;
                }
            }
        }

        return false;
    }

    private static bool HasAccessibleName(UiNode node)
    {
        // Check direct accessible name properties
        var accessibleProps = new[] { "AccessibleLabel", "AriaLabel", "Title" };
        
        foreach (var prop in accessibleProps)
        {
            if (node.Properties?.TryGetValue(prop, out var val) == true)
            {
                var strVal = val?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(strVal) && !IsFormula(strVal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string? GetAccessibleName(UiNode node)
    {
        var accessibleProps = new[] { "AccessibleLabel", "AriaLabel", "Title", "Name" };
        
        foreach (var prop in accessibleProps)
        {
            if (node.Properties?.TryGetValue(prop, out var val) == true)
            {
                var strVal = val?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(strVal) && !IsFormula(strVal))
                {
                    return strVal;
                }
            }
        }

        return node.Name;
    }

    private static UiNode? FindVisualLabel(UiNode node, RuleContext context)
    {
        // Look for nearby labels that might be associated by proximity
        foreach (var sibling in context.Siblings)
        {
            if (LabelTypes.Contains(sibling.Type))
            {
                // Simple heuristic: label with similar name or positioned nearby
                var labelText = ExtractLabelText(sibling);
                var inputId = node.Id?.ToLowerInvariant() ?? "";
                
                if (labelText != null)
                {
                    var labelTextLower = labelText.ToLowerInvariant().Replace(" ", "").Replace(":", "");
                    if (inputId.Contains(labelTextLower) || labelTextLower.Contains(inputId.Replace("input", "").Replace("txt", "")))
                    {
                        return sibling;
                    }
                }
            }
        }

        return null;
    }

    private static string? ExtractLabelText(UiNode label)
    {
        // Get the visible text from a label control
        if (!string.IsNullOrWhiteSpace(label.Text) && !IsFormula(label.Text))
            return label.Text;

        if (label.Properties?.TryGetValue("Text", out var textVal) == true)
        {
            var text = textVal?.ToString();
            if (!string.IsNullOrWhiteSpace(text) && !IsFormula(text))
                return text.TrimEnd(':').Trim();
        }

        return null;
    }

    private static bool HasOnlyPlaceholder(UiNode node)
    {
        var hasPlaceholder = node.Properties?.TryGetValue("HintText", out var hint) == true ||
                             node.Properties?.TryGetValue("Placeholder", out var placeholder) == true;
        
        var hasLabel = node.Properties?.TryGetValue("Label", out var label) == true &&
                       !string.IsNullOrWhiteSpace(label?.ToString());

        return hasPlaceholder && !hasLabel;
    }

    private enum LabelPosition
    {
        Above,
        Left,
        Right,
        Below,
        Far,
        Unknown
    }

    private static LabelPosition GetLabelPosition(UiNode label, UiNode input)
    {
        // Try to determine label position from X/Y properties
        var labelX = GetNumericProperty(label, "X");
        var labelY = GetNumericProperty(label, "Y");
        var inputX = GetNumericProperty(input, "X");
        var inputY = GetNumericProperty(input, "Y");

        if (!labelX.HasValue || !labelY.HasValue || !inputX.HasValue || !inputY.HasValue)
            return LabelPosition.Unknown;

        var xDiff = inputX.Value - labelX.Value;
        var yDiff = inputY.Value - labelY.Value;

        // Label is above if Y is smaller and X is similar
        if (yDiff > 0 && Math.Abs(xDiff) < 50)
            return LabelPosition.Above;

        // Label is to the left if X is smaller and Y is similar
        if (xDiff > 0 && Math.Abs(yDiff) < 30)
            return LabelPosition.Left;

        // Label is to the right (common for checkboxes)
        if (xDiff < 0 && Math.Abs(yDiff) < 30)
            return LabelPosition.Right;

        // Label is below
        if (yDiff < 0 && Math.Abs(xDiff) < 50)
            return LabelPosition.Below;

        // Too far apart
        if (Math.Abs(xDiff) > 200 || Math.Abs(yDiff) > 100)
            return LabelPosition.Far;

        return LabelPosition.Unknown;
    }

    private static double? GetNumericProperty(UiNode node, string propName)
    {
        if (node.Properties?.TryGetValue(propName, out var val) == true)
        {
            if (val is double d) return d;
            if (val is int i) return i;
            if (val is long l) return l;
            if (val is string s && double.TryParse(s, out var parsed)) return parsed;
        }
        return null;
    }

    private static bool IsFormula(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        
        return value.StartsWith("=") ||
               value.Contains("(") ||
               value.Contains("Self.") ||
               value.Contains("Parent.") ||
               value.Contains("ThisItem.");
    }
}
