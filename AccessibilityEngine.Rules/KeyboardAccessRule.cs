using System;
using System.Collections.Generic;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for potential keyboard trap issues.
/// WCAG 2.1.2: If keyboard focus can be moved to a component, focus can be moved away using only keyboard.
/// </summary>
public sealed class KeyboardAccessRule : IRule
{
    public string Id => "KEYBOARD_ACCESS";
    public string Description => "Users must be able to navigate to and away from all interactive controls using only the keyboard.";
    public Severity Severity => Severity.High;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage, SurfaceType.DomSnapshot];

    // Control types that could potentially trap keyboard focus
    private static readonly HashSet<string> PotentialTrapTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Rich editors can trap focus
        "RichTextEditor", "Rich text editor",
        
        // Embedded content
        "HtmlViewer", "Html viewer", "HtmlText",
        "PdfViewer", "Pdf viewer",
        "PowerBITile", "PowerBI tile",
        
        // Custom controls and PCF
        "Component", "PCFControl", "Custom",
        
        // Modal-like controls
        "Dialog", "Modal", "Popup"
    };

    // Control types that use complex focus patterns
    private static readonly HashSet<string> ComplexFocusTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Gallery", "DataTable", "EditForm", "DisplayForm",
        "TabControl", "Tab list", "TabList"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check for potential keyboard trap controls
        if (PotentialTrapTypes.Contains(node.Type))
        {
            // Check if control has keyboard exit handling
            var hasKeyboardExit = HasKeyboardExitHandling(node);
            
            if (!hasKeyboardExit)
            {
                yield return new Finding(
                    Id: $"{Id}:TRAP:{node.Id}",
                    Severity: Severity.High,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_POTENTIAL_TRAP",
                    Message: $"Control '{node.Id}' of type '{node.Type}' may trap keyboard focus. Ensure users can exit using standard keyboard navigation.",
                    WcagReference: "WCAG 2.2 § 2.1.2 No Keyboard Trap (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Keyboard users must be able to navigate away from any component using only the keyboard, without requiring specific timing.",
                    SuggestedFix: $"Add OnKeyDown or similar handler to '{node.Id}' that allows users to exit with Tab, Escape, or arrow keys. Document any non-standard exit method.",
                    WcagCriterion: WcagCriterion.NoKeyboardTrap_2_1_2
                );
            }
        }

        // Check for complex focus patterns
        if (ComplexFocusTypes.Contains(node.Type))
        {
            // Check if TabIndex is properly managed
            var hasProperTabIndex = HasProperTabIndex(node);
            
            if (!hasProperTabIndex)
            {
                yield return new Finding(
                    Id: $"{Id}:FOCUS_ORDER:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_FOCUS_ORDER",
                    Message: $"Complex control '{node.Id}' of type '{node.Type}' should have explicit TabIndex management for predictable keyboard navigation.",
                    WcagReference: "WCAG 2.2 § 2.4.3 Focus Order (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Focus order should preserve meaning and operability when navigating sequentially through interactive elements.",
                    SuggestedFix: $"Set explicit TabIndex values on '{node.Id}' and its child controls to ensure logical focus order. Consider using TabIndex 0 for natural order or positive integers for custom order.",
                    WcagCriterion: WcagCriterion.FocusOrder_2_4_3
                );
            }
        }

        // Check for controls disabled from keyboard access
        if (IsKeyboardInaccessible(node))
        {
            yield return new Finding(
                Id: $"{Id}:INACCESSIBLE:{node.Id}",
                Severity: Severity.High,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_INACCESSIBLE",
                Message: $"Control '{node.Id}' appears to be inaccessible via keyboard (TabIndex = -1 or equivalent with no alternative).",
                WcagReference: "WCAG 2.2 § 2.1.1 Keyboard (Level A)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "All interactive functionality must be operable through a keyboard interface without requiring specific timing.",
                SuggestedFix: $"Either set TabIndex to 0 or a positive value on '{node.Id}', or ensure there is an alternative keyboard-accessible way to perform the same function.",
                WcagCriterion: WcagCriterion.Keyboard_2_1_1
            );
        }
    }

    private static bool HasKeyboardExitHandling(UiNode node)
    {
        if (node.Properties == null) return false;

        // Check for event handlers that might handle keyboard exit
        var keyboardHandlers = new[] { "OnKeyDown", "OnKeyUp", "OnKeyPress", "OnEscape" };
        foreach (var handler in keyboardHandlers)
        {
            if (node.Properties.TryGetValue(handler, out var val) && val != null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasProperTabIndex(UiNode node)
    {
        if (node.Properties == null) return false;

        // Check if TabIndex is explicitly set
        if (node.Properties.TryGetValue("TabIndex", out var tabIndex))
        {
            // TabIndex should be set to manage focus
            return tabIndex != null;
        }

        return false;
    }

    private static bool IsKeyboardInaccessible(UiNode node)
    {
        // Only check interactive control types
        var interactiveTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Button", "IconButton", "TextInput", "ComboBox", "Dropdown",
            "Checkbox", "Radio", "Toggle", "Slider", "DatePicker", "Link"
        };

        if (!interactiveTypes.Contains(node.Type))
            return false;

        if (node.Properties == null)
            return false;

        // Check for TabIndex = -1 which removes from tab order
        if (node.Properties.TryGetValue("TabIndex", out var tabIndex))
        {
            if (tabIndex is int i && i < 0)
                return true;
            if (tabIndex is long l && l < 0)
                return true;
            if (tabIndex is string s && int.TryParse(s, out var parsed) && parsed < 0)
                return true;
        }

        return false;
    }
}
