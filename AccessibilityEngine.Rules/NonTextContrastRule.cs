using System;
using System.Collections.Generic;
using System.Globalization;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks contrast for non-text elements (UI components and graphical objects).
/// WCAG 1.4.11: Visual presentation of UI components and graphical objects must have
/// a contrast ratio of at least 3:1 against adjacent colors.
/// </summary>
public sealed class NonTextContrastRule : IRule
{
    public string Id => "NON_TEXT_CONTRAST";
    public string Description => "UI components and graphical objects must have at least 3:1 contrast ratio.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Minimum contrast ratio for non-text elements
    private const double MinContrastRatio = 3.0;

    // Interactive UI component types
    private static readonly HashSet<string> InteractiveTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Button", "IconButton", "Link", "TextInput", "TextBox", "ComboBox",
        "DropDown", "Slider", "Toggle", "Checkbox", "RadioButton", "Rating",
        "DatePicker", "TimePicker", "Tab", "TabItem"
    };

    // Graphical object types
    private static readonly HashSet<string> GraphicalTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Icon", "Glyph", "Shape", "Rectangle", "Circle", "Line", "Chart",
        "Graph", "ProgressBar", "Gauge", "Indicator", "StatusIcon"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        var isInteractive = InteractiveTypes.Contains(node.Type);
        var isGraphical = GraphicalTypes.Contains(node.Type);

        if (!isInteractive && !isGraphical) yield break;

        // Check border contrast for interactive components
        if (isInteractive)
        {
            foreach (var finding in CheckBorderContrast(node, context))
                yield return finding;

            foreach (var finding in CheckFocusIndicatorContrast(node, context))
                yield return finding;
        }

        // Check fill/stroke contrast for graphical objects
        if (isGraphical)
        {
            foreach (var finding in CheckGraphicalContrast(node, context))
                yield return finding;
        }

        // Check icon contrast
        foreach (var finding in CheckIconContrast(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckBorderContrast(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Get border color and background
        var borderColor = GetColorProperty(node, "BorderColor", "Border");
        var backgroundColor = GetColorProperty(node, "Fill", "Background", "BackgroundColor");

        if (borderColor == null || backgroundColor == null) yield break;

        // Also need parent/page background if control background is transparent
        var pageBackground = GetContextBackground(context);

        var effectiveBackground = IsTransparent(backgroundColor) ? pageBackground : backgroundColor;
        if (effectiveBackground == null) yield break;

        var contrast = CalculateContrastRatio(borderColor.Value, effectiveBackground.Value);

        if (contrast < MinContrastRatio)
        {
            yield return new Finding(
                Id: $"{Id}:BORDER:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_BORDER",
                Message: $"Border of '{node.Id}' has contrast ratio of {contrast:F2}:1, below the required 3:1 minimum.",
                WcagReference: "WCAG 2.2 – 1.4.11 Non-text Contrast (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "UI component boundaries must be visually distinguishable for users with low vision.",
                SuggestedFix: $"Increase border contrast on '{node.Id}' to at least 3:1. Current: {contrast:F2}:1.",
                WcagCriterion: WcagCriterion.NonTextContrast_1_4_11
            );
        }
    }

    private IEnumerable<Finding> CheckFocusIndicatorContrast(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        var focusColor = GetColorProperty(node, "FocusBorderColor", "FocusColor", "FocusedBorderColor");
        if (focusColor == null) yield break;

        var backgroundColor = GetColorProperty(node, "Fill", "Background", "BackgroundColor");
        var pageBackground = GetContextBackground(context);
        var effectiveBackground = backgroundColor == null || IsTransparent(backgroundColor) 
            ? pageBackground 
            : backgroundColor;

        if (effectiveBackground == null) yield break;

        var contrast = CalculateContrastRatio(focusColor.Value, effectiveBackground.Value);

        if (contrast < MinContrastRatio)
        {
            yield return new Finding(
                Id: $"{Id}:FOCUS:{node.Id}",
                Severity: Severity.High,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_FOCUS",
                Message: $"Focus indicator on '{node.Id}' has contrast ratio of {contrast:F2}:1, below the required 3:1 minimum.",
                WcagReference: "WCAG 2.2 – 1.4.11 Non-text Contrast (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Focus indicators must be clearly visible for keyboard users to track their location.",
                SuggestedFix: $"Increase focus indicator contrast on '{node.Id}' to at least 3:1. Current: {contrast:F2}:1.",
                WcagCriterion: WcagCriterion.NonTextContrast_1_4_11
            );
        }
    }

    private IEnumerable<Finding> CheckGraphicalContrast(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        var fillColor = GetColorProperty(node, "Fill", "Color", "FillColor");
        var pageBackground = GetContextBackground(context);

        if (fillColor == null || pageBackground == null) yield break;

        // Skip if the graphical element is purely decorative
        if (node.Properties.TryGetValue("Decorative", out var decorative) && 
            decorative is bool isDecorative && isDecorative)
            yield break;

        var contrast = CalculateContrastRatio(fillColor.Value, pageBackground.Value);

        if (contrast < MinContrastRatio)
        {
            yield return new Finding(
                Id: $"{Id}:GRAPHICAL:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_GRAPHICAL",
                Message: $"Graphical object '{node.Id}' has contrast ratio of {contrast:F2}:1, below the required 3:1 minimum.",
                WcagReference: "WCAG 2.2 – 1.4.11 Non-text Contrast (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Meaningful graphical objects must have sufficient contrast to be perceivable.",
                SuggestedFix: $"Increase the fill color contrast on '{node.Id}' to at least 3:1. Current: {contrast:F2}:1.",
                WcagCriterion: WcagCriterion.NonTextContrast_1_4_11
            );
        }
    }

    private IEnumerable<Finding> CheckIconContrast(UiNode node, RuleContext context)
    {
        if (!node.Type.Contains("Icon", StringComparison.OrdinalIgnoreCase) &&
            !node.Type.Contains("Glyph", StringComparison.OrdinalIgnoreCase))
            yield break;

        if (node.Properties == null) yield break;

        var iconColor = GetColorProperty(node, "Color", "IconColor", "Fill");
        var pageBackground = GetContextBackground(context);

        if (iconColor == null || pageBackground == null) yield break;

        var contrast = CalculateContrastRatio(iconColor.Value, pageBackground.Value);

        if (contrast < MinContrastRatio)
        {
            yield return new Finding(
                Id: $"{Id}:ICON:{node.Id}",
                Severity: Severity.High,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_ICON",
                Message: $"Icon '{node.Id}' has contrast ratio of {contrast:F2}:1, below the required 3:1 minimum.",
                WcagReference: "WCAG 2.2 – 1.4.11 Non-text Contrast (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Icons that convey information must have sufficient contrast to be perceivable.",
                SuggestedFix: $"Change icon color on '{node.Id}' to achieve at least 3:1 contrast. Current: {contrast:F2}:1.",
                WcagCriterion: WcagCriterion.NonTextContrast_1_4_11
            );
        }
    }

    private static (int R, int G, int B)? GetColorProperty(UiNode node, params string[] propertyNames)
    {
        if (node.Properties == null) return null;

        foreach (var propName in propertyNames)
        {
            if (node.Properties.TryGetValue(propName, out var value))
            {
                var color = ParseColor(value?.ToString());
                if (color != null) return color;
            }
        }

        return null;
    }

    private static (int R, int G, int B)? GetContextBackground(RuleContext context)
    {
        // Try to get background from screen
        if (context.Screen?.Properties != null)
        {
            var bg = GetColorProperty(context.Screen, "Fill", "Background", "BackgroundColor");
            if (bg != null) return bg;
        }

        // Default to white background
        return (255, 255, 255);
    }

    private static (int R, int G, int B)? ParseColor(string? colorString)
    {
        if (string.IsNullOrWhiteSpace(colorString)) return null;

        colorString = colorString.Trim();

        // Handle hex colors
        if (colorString.StartsWith('#'))
        {
            colorString = colorString[1..];
            if (colorString.Length == 6 &&
                int.TryParse(colorString[..2], NumberStyles.HexNumber, null, out var r) &&
                int.TryParse(colorString[2..4], NumberStyles.HexNumber, null, out var g) &&
                int.TryParse(colorString[4..6], NumberStyles.HexNumber, null, out var b))
            {
                return (r, g, b);
            }
        }

        // Handle RGBA format
        if (colorString.StartsWith("RGBA(", StringComparison.OrdinalIgnoreCase))
        {
            var parts = colorString[5..^1].Split(',');
            if (parts.Length >= 3 &&
                int.TryParse(parts[0].Trim(), out var r) &&
                int.TryParse(parts[1].Trim(), out var g) &&
                int.TryParse(parts[2].Trim(), out var b))
            {
                return (r, g, b);
            }
        }

        return null;
    }

    private static bool IsTransparent((int R, int G, int B)? color)
    {
        // Simplified check - in real implementation would check alpha channel
        return color == null;
    }

    private static double CalculateContrastRatio((int R, int G, int B) color1, (int R, int G, int B) color2)
    {
        var l1 = GetRelativeLuminance(color1);
        var l2 = GetRelativeLuminance(color2);

        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double GetRelativeLuminance((int R, int G, int B) color)
    {
        var r = GetLuminanceComponent(color.R / 255.0);
        var g = GetLuminanceComponent(color.G / 255.0);
        var b = GetLuminanceComponent(color.B / 255.0);

        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static double GetLuminanceComponent(double value)
    {
        return value <= 0.03928
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
