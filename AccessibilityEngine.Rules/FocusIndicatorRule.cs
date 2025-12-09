using System;
using System.Collections.Generic;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for visible focus indicators on interactive controls.
/// Ensures keyboard users can see which element has focus.
/// </summary>
public sealed class FocusIndicatorRule : IRule
{
    public string Id => "FOCUS_INDICATOR";
    public string Description => "Interactive controls must have a visible focus indicator.";
    public Severity Severity => Severity.High;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp];

    // Interactive control types that need focus indicators
    private static readonly HashSet<string> InteractiveTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Button", "IconButton", "Classic/Button",
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
        "Link", "HyperLink",
        "Gallery", "DataTable"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Only check interactive control types
        if (!InteractiveTypes.Contains(node.Type))
            yield break;

        // Check for focus-related properties
        var hasFocusedBorderColor = HasNonTransparentProperty(node, "FocusedBorderColor");
        var hasFocusedBorderThickness = HasPositiveNumericProperty(node, "FocusedBorderThickness");
        var hasFocusedFill = HasNonTransparentProperty(node, "FocusedFill");

        // Check if focus indicator is explicitly removed
        if (IsFocusIndicatorRemoved(node))
        {
            yield return new Finding(
                Id: $"{Id}:REMOVED:{node.Id}",
                Severity: Severity.Critical,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_REMOVED",
                Message: $"Control '{node.Id}' has focus indicator explicitly removed (FocusedBorderThickness = 0 or transparent FocusedBorderColor).",
                WcagReference: "WCAG 2.1 – 2.4.7 Focus Visible",
                Section508Reference: "Section 508 - Focus Visible",
                Rationale: "Keyboard users must be able to see which element has focus. Never remove focus indicators.",
                SuggestedFix: $"Set FocusedBorderThickness to at least 2 and FocusedBorderColor to a visible, high-contrast color (e.g., RGBA(0, 120, 212, 1)) on '{node.Id}'."
            );
            yield break;
        }

        // Check if FocusedBorderColor is same as BorderColor (no visible change)
        if (HasSameColorOnFocus(node, "FocusedBorderColor", "BorderColor"))
        {
            yield return new Finding(
                Id: $"{Id}:SAME_COLOR:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_SAME_COLOR",
                Message: $"Control '{node.Id}' has FocusedBorderColor same as BorderColor, reducing focus visibility.",
                WcagReference: "WCAG 2.1 – 2.4.7 Focus Visible",
                Section508Reference: "Section 508 - Focus Visible",
                Rationale: "Focus state should be visually distinct from unfocused state.",
                SuggestedFix: $"Set FocusedBorderColor to a contrasting color that differs from BorderColor on '{node.Id}' (e.g., use a bright blue or orange)."
            );
        }

        // Check if using Self.BorderColor for FocusedBorderColor
        if (UsesSelfReference(node, "FocusedBorderColor", "Self.BorderColor"))
        {
            yield return new Finding(
                Id: $"{Id}:SELF_REF:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_SELF_REF",
                Message: $"Control '{node.Id}' uses 'Self.BorderColor' for FocusedBorderColor, providing no visual focus change.",
                WcagReference: "WCAG 2.1 – 2.4.7 Focus Visible",
                Section508Reference: "Section 508 - Focus Visible",
                Rationale: "Focus state should be visually distinct. Use a contrasting color for FocusedBorderColor.",
                SuggestedFix: $"Replace 'Self.BorderColor' with a distinct color value for FocusedBorderColor on '{node.Id}' (e.g., RGBA(0, 120, 212, 1))."
            );
        }

        // Check for sufficient border thickness on focus
        var borderThickness = GetNumericProperty(node, "FocusedBorderThickness");
        if (borderThickness > 0 && borderThickness < 2)
        {
            yield return new Finding(
                Id: $"{Id}:THIN_BORDER:{node.Id}",
                Severity: Severity.Low,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_THIN_BORDER",
                Message: $"Control '{node.Id}' has a thin focus border ({borderThickness}px). Consider at least 2px for visibility.",
                WcagReference: "WCAG 2.1 – 2.4.7 Focus Visible",
                Section508Reference: "Section 508 - Focus Visible",
                Rationale: "A thicker focus border is more visible, especially for users with low vision.",
                SuggestedFix: $"Increase FocusedBorderThickness to at least 2 on '{node.Id}' for better visibility."
            );
        }
    }

    private static bool HasNonTransparentProperty(UiNode node, string propertyName)
    {
        if (node.Properties?.TryGetValue(propertyName, out var value) == true)
        {
            var strValue = value?.ToString();
            if (string.IsNullOrWhiteSpace(strValue)) return false;
            
            // Check for transparent
            if (strValue.Contains("0, 0, 0, 0)", StringComparison.OrdinalIgnoreCase) ||
                strValue.Contains("Transparent", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }
        return false;
    }

    private static bool HasPositiveNumericProperty(UiNode node, string propertyName)
    {
        var value = GetNumericProperty(node, propertyName);
        return value > 0;
    }

    private static double GetNumericProperty(UiNode node, string propertyName)
    {
        if (node.Properties?.TryGetValue(propertyName, out var value) == true)
        {
            if (value is int i) return i;
            if (value is long l) return l;
            if (value is double d) return d;
            if (value is string s && double.TryParse(s, out var parsed)) return parsed;
        }
        return -1; // Not set
    }

    private static bool IsFocusIndicatorRemoved(UiNode node)
    {
        // Check if FocusedBorderThickness is 0
        var thickness = GetNumericProperty(node, "FocusedBorderThickness");
        if (thickness == 0) return true;

        // Check if FocusedBorderColor is transparent
        if (node.Properties?.TryGetValue("FocusedBorderColor", out var color) == true)
        {
            var colorStr = color?.ToString();
            if (colorStr != null && (
                colorStr.Contains("0, 0, 0, 0)", StringComparison.OrdinalIgnoreCase) ||
                colorStr.Equals("Transparent", StringComparison.OrdinalIgnoreCase) ||
                colorStr.Contains(", 0)", StringComparison.OrdinalIgnoreCase))) // Alpha = 0
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSameColorOnFocus(UiNode node, string focusProperty, string normalProperty)
    {
        if (node.Properties?.TryGetValue(focusProperty, out var focusValue) == true &&
            node.Properties?.TryGetValue(normalProperty, out var normalValue) == true)
        {
            var focusStr = focusValue?.ToString()?.Trim();
            var normalStr = normalValue?.ToString()?.Trim();

            if (!string.IsNullOrEmpty(focusStr) && !string.IsNullOrEmpty(normalStr))
            {
                return string.Equals(focusStr, normalStr, StringComparison.OrdinalIgnoreCase);
            }
        }
        return false;
    }

    private static bool UsesSelfReference(UiNode node, string propertyName, string selfReference)
    {
        if (node.Properties?.TryGetValue(propertyName, out var value) == true)
        {
            var strValue = value?.ToString();
            return strValue?.Contains(selfReference, StringComparison.OrdinalIgnoreCase) == true;
        }
        return false;
    }
}
