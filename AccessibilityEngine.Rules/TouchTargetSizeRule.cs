using System;
using System.Collections.Generic;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for minimum touch target sizes on interactive controls.
/// Ensures controls are large enough to be easily tapped on touch devices.
/// </summary>
public sealed class TouchTargetSizeRule : IRule
{
    public string Id => "TOUCH_TARGET_SIZE";
    public string Description => "Interactive controls should meet minimum touch target size requirements.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp];

    // WCAG 2.2 Level AA: 24x24 CSS pixels minimum
    // WCAG 2.2 Level AAA: 44x44 CSS pixels
    // iOS/Android guidelines: 44x44 points/dp
    private const int MinTargetSizeAA = 24;
    private const int MinTargetSizeAAA = 44;
    private const int RecommendedTargetSize = 44;

    // Interactive control types that need adequate touch targets
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
        "Icon", "Classic/Icon",
        "Image" // When used as a button
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Only check interactive control types
        if (!InteractiveTypes.Contains(node.Type))
            yield break;

        // Get dimensions
        var width = GetDimension(node, "Width");
        var height = GetDimension(node, "Height");

        // Skip if dimensions are formula-based (dynamic)
        if (width < 0 || height < 0)
            yield break;

        var minDimension = Math.Min(width, height);

        // Critical: Below WCAG AA minimum (24x24)
        if (width < MinTargetSizeAA || height < MinTargetSizeAA)
        {
            yield return new Finding(
                Id: $"{Id}:TOO_SMALL:{node.Id}",
                Severity: Severity.High,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_TOO_SMALL",
                Message: $"Control '{node.Id}' ({width}x{height}px) is below minimum touch target size (24x24px).",
                WcagReference: "WCAG 2.2 – 2.5.8 Target Size (Minimum)",
                Section508Reference: "Section 508 - Target Size",
                Rationale: "Small touch targets are difficult to activate for users with motor impairments or on mobile devices.",
                SuggestedFix: $"Increase Width and Height of '{node.Id}' to at least 24x24 pixels (44x44 recommended for optimal touch accessibility)."
            );
            yield break;
        }

        // Warning: Below recommended size (44x44)
        if (width < RecommendedTargetSize || height < RecommendedTargetSize)
        {
            // Check for adequate spacing (WCAG allows smaller if there's enough space)
            var hasAdequateSpacing = CheckAdequateSpacing(node, context);

            if (!hasAdequateSpacing)
            {
                yield return new Finding(
                    Id: $"{Id}:BELOW_RECOMMENDED:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_BELOW_RECOMMENDED",
                    Message: $"Control '{node.Id}' ({width}x{height}px) is below recommended touch target size (44x44px).",
                    WcagReference: "WCAG 2.2 – 2.5.5 Target Size (Enhanced)",
                    Section508Reference: "Section 508 - Target Size",
                    Rationale: "Larger touch targets improve usability for all users, especially on mobile devices.",
                    SuggestedFix: $"Consider increasing Width and Height of '{node.Id}' to 44x44 pixels for optimal touch accessibility."
                );
            }
        }

        // Check for icon-only buttons (common issue)
        if (IsIconOnlyButton(node) && minDimension < RecommendedTargetSize)
        {
            yield return new Finding(
                Id: $"{Id}:ICON_BUTTON:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_ICON_BUTTON",
                Message: $"Icon button '{node.Id}' ({width}x{height}px) should be at least 44x44px for easy touch activation.",
                WcagReference: "WCAG 2.2 – 2.5.8 Target Size (Minimum)",
                Section508Reference: "Section 508 - Target Size",
                Rationale: "Icon-only buttons are harder to tap accurately. Ensure adequate size.",
                SuggestedFix: $"Set Width and Height of '{node.Id}' to at least 44x44 pixels. If the icon must remain small, add padding to increase the touch target area."
            );
        }

        // Check for closely spaced controls
        if (HasCloselySpacedSiblings(node, context))
        {
            yield return new Finding(
                Id: $"{Id}:CLOSE_SPACING:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_CLOSE_SPACING",
                Message: $"Control '{node.Id}' appears to be closely spaced with sibling controls. Ensure adequate spacing to prevent accidental taps.",
                WcagReference: "WCAG 2.2 – 2.5.8 Target Size (Minimum)",
                Section508Reference: "Section 508 - Target Size",
                Rationale: "Closely spaced touch targets increase the risk of accidental activation.",
                SuggestedFix: $"Add at least 8 pixels of spacing between '{node.Id}' and adjacent interactive controls, or increase the control sizes to 44x44 pixels."
            );
        }
    }

    private static int GetDimension(UiNode node, string propertyName)
    {
        if (node.Properties?.TryGetValue(propertyName, out var value) == true)
        {
            // Direct numeric value
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is double d) return (int)d;
            
            // String value
            if (value is string s)
            {
                // Check for formula (not a static value)
                if (s.Contains("(") || s.Contains("Self.") || s.Contains("Parent."))
                    return -1; // Formula-based, can't determine

                if (int.TryParse(s, out var parsed))
                    return parsed;
            }
        }
        return -1; // Unknown
    }

    private static bool IsIconOnlyButton(UiNode node)
    {
        var type = node.Type;
        if (type.Contains("Icon", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check if it's a button with no text
        if (type.Contains("Button", StringComparison.OrdinalIgnoreCase))
        {
            var hasText = !string.IsNullOrWhiteSpace(node.Text) ||
                         (node.Properties?.TryGetValue("Text", out var text) == true &&
                          !string.IsNullOrWhiteSpace(text?.ToString()));
            return !hasText;
        }

        return false;
    }

    private static bool CheckAdequateSpacing(UiNode node, RuleContext context)
    {
        // WCAG allows smaller targets if there's at least 24px spacing
        // This is a simplified check - in reality would need position calculations
        
        // If there are no siblings, spacing is adequate
        if (context.Siblings.Count == 0)
            return true;

        // For now, assume spacing is not adequate if there are interactive siblings
        foreach (var sibling in context.Siblings)
        {
            if (InteractiveTypes.Contains(sibling.Type))
                return false;
        }

        return true;
    }

    private static bool HasCloselySpacedSiblings(UiNode node, RuleContext context)
    {
        if (context.Siblings.Count == 0)
            return false;

        var nodeX = GetDimension(node, "X");
        var nodeY = GetDimension(node, "Y");
        var nodeWidth = GetDimension(node, "Width");
        var nodeHeight = GetDimension(node, "Height");

        if (nodeX < 0 || nodeY < 0 || nodeWidth < 0 || nodeHeight < 0)
            return false;

        const int minSpacing = 8; // Minimum spacing in pixels

        foreach (var sibling in context.Siblings)
        {
            if (!InteractiveTypes.Contains(sibling.Type))
                continue;

            var sibX = GetDimension(sibling, "X");
            var sibY = GetDimension(sibling, "Y");
            var sibWidth = GetDimension(sibling, "Width");
            var sibHeight = GetDimension(sibling, "Height");

            if (sibX < 0 || sibY < 0)
                continue;

            // Calculate distance between controls
            var horizontalGap = Math.Max(0, Math.Max(sibX - (nodeX + nodeWidth), nodeX - (sibX + sibWidth)));
            var verticalGap = Math.Max(0, Math.Max(sibY - (nodeY + nodeHeight), nodeY - (sibY + sibHeight)));

            // If controls overlap or are very close
            if ((horizontalGap < minSpacing && verticalGap < minSpacing) ||
                (horizontalGap == 0 && verticalGap < minSpacing) ||
                (verticalGap == 0 && horizontalGap < minSpacing))
            {
                return true;
            }
        }

        return false;
    }

    private static int GetDimension(UiNode node, string propertyName, int defaultValue = -1)
    {
        if (node.Properties?.TryGetValue(propertyName, out var value) == true)
        {
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is double d) return (int)d;
            if (value is string s && int.TryParse(s, out var parsed)) return parsed;
        }
        return defaultValue;
    }
}
