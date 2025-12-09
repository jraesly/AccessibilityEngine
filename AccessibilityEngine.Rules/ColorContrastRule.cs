using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for potential color contrast issues in Power Apps controls.
/// Validates Color/Fill combinations against WCAG contrast requirements.
/// </summary>
public sealed class ColorContrastRule : IRule
{
    public string Id => "COLOR_CONTRAST";
    public string Description => "Text must have sufficient color contrast against its background.";
    public Severity Severity => Severity.High;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp];

    // WCAG 2.1 AA requirements
    private const double NormalTextMinRatio = 4.5;
    private const double LargeTextMinRatio = 3.0;
    private const int LargeTextSizeThreshold = 18; // 18pt or 14pt bold

    // Control types that display text
    private static readonly HashSet<string> TextControlTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Label", "Text", "Classic/Label",
        "Button", "IconButton", "Classic/Button",
        "TextInput", "Text input", "Classic/TextInput",
        "Link", "HyperLink"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Only check text-displaying controls
        if (!TextControlTypes.Contains(node.Type))
            yield break;

        // Get foreground and background colors
        var foreground = GetColorProperty(node, "Color");
        var background = GetColorProperty(node, "Fill");

        // If we can't determine both colors, we can't check contrast
        if (foreground == null && background == null)
            yield break;

        // Check for known problematic patterns
        if (foreground != null && background != null)
        {
            var fgColor = ParseRgbaColor(foreground);
            var bgColor = ParseRgbaColor(background);

            if (fgColor.HasValue && bgColor.HasValue)
            {
                var ratio = CalculateContrastRatio(fgColor.Value, bgColor.Value);
                var fontSize = GetFontSize(node);
                var isBold = IsBoldText(node);
                var isLargeText = fontSize >= LargeTextSizeThreshold || (fontSize >= 14 && isBold);
                var requiredRatio = isLargeText ? LargeTextMinRatio : NormalTextMinRatio;

                if (ratio < requiredRatio)
                {
                    yield return new Finding(
                        Id: $"{Id}:{node.Id}",
                        Severity: Severity.High,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: Id,
                        Message: $"Insufficient color contrast ({ratio:F2}:1). Required: {requiredRatio}:1 for {(isLargeText ? "large" : "normal")} text.",
                        WcagReference: "WCAG 2.1 – 1.4.3 Contrast (Minimum)",
                        Section508Reference: "Section 508 - Color Contrast",
                        Rationale: "Text must have a contrast ratio of at least 4.5:1 (3:1 for large text) to be readable by users with low vision.",
                        SuggestedFix: $"Adjust the Color or Fill property on '{node.Id}' to achieve at least {requiredRatio}:1 contrast ratio. Use a contrast checker tool to verify."
                    );
                }
            }
        }

        // Check for color-only information (e.g., error states)
        if (HasColorOnlyIndication(node))
        {
            yield return new Finding(
                Id: $"{Id}:COLOR_ONLY:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_COLOR_ONLY",
                Message: $"Control '{node.Id}' may use color alone to convey information. Consider adding icons or text.",
                WcagReference: "WCAG 2.1 – 1.4.1 Use of Color",
                Section508Reference: "Section 508 - Use of Color",
                Rationale: "Information conveyed through color must also be available through other means (text, icons, patterns).",
                SuggestedFix: $"Add a text label, icon, or pattern to '{node.Id}' that conveys the same information as the color change (e.g., add an error icon alongside red text for errors)."
            );
        }

        // Check for transparent/semi-transparent backgrounds
        if (background != null && HasLowAlpha(background))
        {
            yield return new Finding(
                Id: $"{Id}:TRANSPARENT:{node.Id}",
                Severity: Severity.Low,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_TRANSPARENT",
                Message: $"Control '{node.Id}' has a transparent or semi-transparent background. Verify contrast against all possible backgrounds.",
                WcagReference: "WCAG 2.1 – 1.4.3 Contrast (Minimum)",
                Section508Reference: "Section 508 - Color Contrast",
                Rationale: "Transparent backgrounds may result in insufficient contrast depending on what appears behind the control.",
                SuggestedFix: $"Either set an opaque Fill color on '{node.Id}', or verify the text color has sufficient contrast against all possible background colors in the app."
            );
        }
    }

    private static string? GetColorProperty(UiNode node, string propertyName)
    {
        if (node.Properties?.TryGetValue(propertyName, out var value) == true)
        {
            return value?.ToString();
        }
        return null;
    }

    /// <summary>
    /// Parses RGBA color values from Power Apps format: RGBA(r, g, b, a)
    /// </summary>
    private static (int R, int G, int B, double A)? ParseRgbaColor(string colorValue)
    {
        if (string.IsNullOrWhiteSpace(colorValue))
            return null;

        // Handle RGBA(r, g, b, a) format
        var rgbaMatch = Regex.Match(colorValue, @"RGBA\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*([\d.]+)\s*\)", RegexOptions.IgnoreCase);
        if (rgbaMatch.Success)
        {
            return (
                int.Parse(rgbaMatch.Groups[1].Value),
                int.Parse(rgbaMatch.Groups[2].Value),
                int.Parse(rgbaMatch.Groups[3].Value),
                double.Parse(rgbaMatch.Groups[4].Value, CultureInfo.InvariantCulture)
            );
        }

        // Handle Color.Name format (common colors)
        if (colorValue.StartsWith("Color.", StringComparison.OrdinalIgnoreCase))
        {
            var colorName = colorValue.Substring(6).ToLowerInvariant();
            return colorName switch
            {
                "white" => (255, 255, 255, 1.0),
                "black" => (0, 0, 0, 1.0),
                "red" => (255, 0, 0, 1.0),
                "green" => (0, 128, 0, 1.0),
                "blue" => (0, 0, 255, 1.0),
                "yellow" => (255, 255, 0, 1.0),
                "gray" or "grey" => (128, 128, 128, 1.0),
                "lightgray" or "lightgrey" => (211, 211, 211, 1.0),
                "darkgray" or "darkgrey" => (169, 169, 169, 1.0),
                _ => null
            };
        }

        return null;
    }

    /// <summary>
    /// Calculates the contrast ratio between two colors per WCAG formula.
    /// </summary>
    private static double CalculateContrastRatio((int R, int G, int B, double A) fg, (int R, int G, int B, double A) bg)
    {
        var fgLuminance = CalculateRelativeLuminance(fg.R, fg.G, fg.B);
        var bgLuminance = CalculateRelativeLuminance(bg.R, bg.G, bg.B);

        var lighter = Math.Max(fgLuminance, bgLuminance);
        var darker = Math.Min(fgLuminance, bgLuminance);

        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    /// Calculates relative luminance per WCAG 2.1 formula.
    /// </summary>
    private static double CalculateRelativeLuminance(int r, int g, int b)
    {
        var rSrgb = r / 255.0;
        var gSrgb = g / 255.0;
        var bSrgb = b / 255.0;

        var rLinear = rSrgb <= 0.03928 ? rSrgb / 12.92 : Math.Pow((rSrgb + 0.055) / 1.055, 2.4);
        var gLinear = gSrgb <= 0.03928 ? gSrgb / 12.92 : Math.Pow((gSrgb + 0.055) / 1.055, 2.4);
        var bLinear = bSrgb <= 0.03928 ? bSrgb / 12.92 : Math.Pow((bSrgb + 0.055) / 1.055, 2.4);

        return 0.2126 * rLinear + 0.7152 * gLinear + 0.0722 * bLinear;
    }

    private static int GetFontSize(UiNode node)
    {
        if (node.Properties?.TryGetValue("Size", out var size) == true)
        {
            if (size is int i) return i;
            if (size is long l) return (int)l;
            if (size is string s && int.TryParse(s, out var parsed)) return parsed;
        }
        return 13; // Default Power Apps font size
    }

    private static bool IsBoldText(UiNode node)
    {
        if (node.Properties?.TryGetValue("FontWeight", out var weight) == true)
        {
            var w = weight?.ToString();
            return w?.Contains("Bold", StringComparison.OrdinalIgnoreCase) == true ||
                   w?.Contains("Semibold", StringComparison.OrdinalIgnoreCase) == true;
        }
        return false;
    }

    private static bool HasColorOnlyIndication(UiNode node)
    {
        // Check if control uses conditional coloring that might indicate state
        if (node.Properties != null)
        {
            foreach (var prop in node.Properties)
            {
                var value = prop.Value?.ToString();
                if (value != null && (
                    value.Contains("If(", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("Switch(", StringComparison.OrdinalIgnoreCase)) &&
                    (prop.Key.Contains("Color", StringComparison.OrdinalIgnoreCase) ||
                     prop.Key.Contains("Fill", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool HasLowAlpha(string colorValue)
    {
        var color = ParseRgbaColor(colorValue);
        return color.HasValue && color.Value.A < 0.5;
    }
}
