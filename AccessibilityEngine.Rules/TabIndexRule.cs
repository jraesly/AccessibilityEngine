using System;
using System.Collections.Generic;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for proper TabIndex usage in Power Apps controls.
/// Ensures keyboard navigation follows a logical order.
/// </summary>
public sealed class TabIndexRule : IRule
{
    public string Id => "TAB_INDEX";
    public string Description => "Controls should have proper TabIndex for keyboard navigation.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Interactive control types that should be focusable
    private static readonly HashSet<string> FocusableTypes = new(StringComparer.OrdinalIgnoreCase)
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
        "Link", "HyperLink"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Only check focusable control types
        if (!FocusableTypes.Contains(node.Type))
            yield break;

        var findings = new List<Finding>();

        // Check for TabIndex property
        var hasTabIndex = node.Properties?.ContainsKey("TabIndex") == true;
        var tabIndexValue = GetTabIndexValue(node);

        // Check 1: Positive TabIndex (anti-pattern)
        if (tabIndexValue > 0)
        {
            yield return new Finding(
                Id: $"{Id}:POSITIVE:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_POSITIVE",
                Message: $"Control '{node.Id}' has a positive TabIndex ({tabIndexValue}). Use 0 for natural tab order or -1 to remove from tab order.",
                WcagReference: "WCAG 2.1 – 2.4.3 Focus Order",
                Section508Reference: "Section 508 - Focus Order",
                Rationale: "Positive TabIndex values create unpredictable keyboard navigation. Use 0 to follow DOM order.",
                SuggestedFix: $"Set TabIndex to 0 on '{node.Id}' to follow natural DOM order, or restructure controls so their visual order matches the desired tab sequence."
            );
        }

        // Check 2: Interactive control removed from tab order
        if (tabIndexValue < 0)
        {
            // Check if there's a reason (e.g., DisplayMode = Disabled)
            var isDisabled = IsControlDisabled(node);
            if (!isDisabled)
            {
                yield return new Finding(
                    Id: $"{Id}:REMOVED:{node.Id}",
                    Severity: Severity.High,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_REMOVED",
                    Message: $"Interactive control '{node.Id}' is removed from keyboard navigation (TabIndex < 0) but is not disabled.",
                    WcagReference: "WCAG 2.1 – 2.1.1 Keyboard",
                    Section508Reference: "Section 508 - Keyboard Accessible",
                    Rationale: "All interactive controls must be keyboard accessible unless they are disabled.",
                    SuggestedFix: $"Either set TabIndex to 0 on '{node.Id}' to make it keyboard accessible, or set DisplayMode to Disabled if the control should not be interactive."
                );
            }
        }

        // Check 3: Sibling controls with inconsistent TabIndex
        if (context.Siblings.Count > 0 && hasTabIndex)
        {
            var siblingsWithTabIndex = 0;
            var siblingsWithoutTabIndex = 0;

            foreach (var sibling in context.Siblings)
            {
                if (FocusableTypes.Contains(sibling.Type))
                {
                    if (sibling.Properties?.ContainsKey("TabIndex") == true)
                        siblingsWithTabIndex++;
                    else
                        siblingsWithoutTabIndex++;
                }
            }

            if (siblingsWithTabIndex > 0 && siblingsWithoutTabIndex > 0)
            {
                yield return new Finding(
                    Id: $"{Id}:INCONSISTENT:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_INCONSISTENT",
                    Message: $"Inconsistent TabIndex usage: some sibling controls have TabIndex set while others don't.",
                    WcagReference: "WCAG 2.1 – 2.4.3 Focus Order",
                    Section508Reference: "Section 508 - Focus Order",
                    Rationale: "Consistent TabIndex usage ensures predictable keyboard navigation.",
                    SuggestedFix: "Apply TabIndex consistently to all sibling controls, preferably using 0 for all focusable controls to follow natural DOM order."
                );
            }
        }
    }

    private static int GetTabIndexValue(UiNode node)
    {
        if (node.Properties?.TryGetValue("TabIndex", out var value) == true)
        {
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is string s && int.TryParse(s, out var parsed)) return parsed;
        }
        return 0; // Default
    }

    private static bool IsControlDisabled(UiNode node)
    {
        if (node.Properties?.TryGetValue("DisplayMode", out var displayMode) == true)
        {
            var mode = displayMode?.ToString();
            return mode?.Contains("Disabled", StringComparison.OrdinalIgnoreCase) == true ||
                   mode?.Contains("View", StringComparison.OrdinalIgnoreCase) == true;
        }
        return false;
    }
}
