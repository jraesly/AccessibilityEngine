using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for predictable behavior on focus and input.
/// WCAG 3.2.1: When any UI component receives focus, it must not initiate a change of context.
/// WCAG 3.2.2: Changing the setting of any UI component must not automatically cause a change of context
/// unless the user has been advised of the behavior before using the component.
/// </summary>
public sealed class PredictableBehaviorRule : IRule
{
    public string Id => "PREDICTABLE_BEHAVIOR";
    public string Description => "Focus and input changes must not cause unexpected context changes.";
    public Severity Severity => Severity.High;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Interactive control types
    private static readonly HashSet<string> InteractiveTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Button", "Link", "TextInput", "TextBox", "ComboBox", "DropDown",
        "Checkbox", "RadioButton", "Toggle", "Slider", "DatePicker",
        "ListBox", "Select", "Tab", "TabItem"
    };

    // Form input types
    private static readonly HashSet<string> InputTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "TextInput", "TextBox", "ComboBox", "DropDown", "Checkbox",
        "RadioButton", "Toggle", "Slider", "DatePicker", "ListBox", "Select"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check for context changes on focus (3.2.1)
        foreach (var finding in CheckOnFocusBehavior(node, context))
            yield return finding;

        // Check for context changes on input (3.2.2)
        foreach (var finding in CheckOnInputBehavior(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckOnFocusBehavior(UiNode node, RuleContext context)
    {
        if (!InteractiveTypes.Contains(node.Type)) yield break;
        if (node.Properties == null) yield break;

        // Check for OnFocus handlers that might change context
        var focusHandlerProps = new[] { "OnFocus", "OnGotFocus", "OnEnter", "FocusHandler" };

        foreach (var prop in focusHandlerProps)
        {
            if (!node.Properties.TryGetValue(prop, out var handler)) continue;
            
            var handlerStr = handler?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(handlerStr)) continue;

            // Check for navigation/context change patterns
            if (CausesContextChange(handlerStr))
            {
                yield return new Finding(
                    Id: $"{Id}:FOCUS:{node.Id}",
                    Severity: Severity.High,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_ON_FOCUS",
                    Message: $"Control '{node.Id}' appears to change context when receiving focus.",
                    WcagReference: "WCAG 2.2 – 3.2.1 On Focus (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Unexpected navigation or form submission when a control receives focus is disorienting, especially for screen reader and keyboard users.",
                    SuggestedFix: $"Remove automatic navigation/submission from '{node.Id}' OnFocus handler. Context changes should only occur on explicit user action (click, Enter key).",
                    WcagCriterion: WcagCriterion.OnFocus_3_2_1
                );
            }

            // Check for popup/dialog opening on focus
            if (OpensPopupOrDialog(handlerStr))
            {
                yield return new Finding(
                    Id: $"{Id}:FOCUS_POPUP:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_FOCUS_POPUP",
                    Message: $"Control '{node.Id}' opens a popup or dialog when receiving focus.",
                    WcagReference: "WCAG 2.2 – 3.2.1 On Focus (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Opening dialogs on focus can trap keyboard users and disrupt screen reader announcements.",
                    SuggestedFix: $"Change '{node.Id}' to open the popup on click/activation rather than on focus.",
                    WcagCriterion: WcagCriterion.OnFocus_3_2_1
                );
            }
        }
    }

    private IEnumerable<Finding> CheckOnInputBehavior(UiNode node, RuleContext context)
    {
        if (!InputTypes.Contains(node.Type)) yield break;
        if (node.Properties == null) yield break;

        // Check for OnChange handlers that might change context
        var changeHandlerProps = new[] { "OnChange", "OnValueChange", "OnSelect", "OnInput", "OnCheck" };

        foreach (var prop in changeHandlerProps)
        {
            if (!node.Properties.TryGetValue(prop, out var handler)) continue;
            
            var handlerStr = handler?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(handlerStr)) continue;

            // Check for navigation/context change patterns
            if (CausesContextChange(handlerStr))
            {
                // Check if user is warned about this behavior
                var hasWarning = HasBehaviorWarning(node);

                if (!hasWarning)
                {
                    yield return new Finding(
                        Id: $"{Id}:INPUT:{node.Id}",
                        Severity: Severity.High,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_ON_INPUT",
                        Message: $"Control '{node.Id}' causes navigation or submission when its value changes without prior warning.",
                        WcagReference: "WCAG 2.2 – 3.2.2 On Input (Level A)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Automatic form submission or navigation when changing a dropdown or checkbox value is unexpected and can cause data loss.",
                        SuggestedFix: $"Either: (1) Add a visible instruction warning users about the automatic behavior, (2) Use a separate submit button, or (3) Remove automatic context change from '{node.Id}'.",
                        WcagCriterion: WcagCriterion.OnInput_3_2_2
                    );
                }
            }
        }

        // Check for auto-submit forms
        if (node.Type.Equals("Form", StringComparison.OrdinalIgnoreCase))
        {
            if (node.Properties.TryGetValue("AutoSubmit", out var autoSubmit) &&
                autoSubmit is bool isAutoSubmit && isAutoSubmit)
            {
                var hasWarning = HasBehaviorWarning(node);

                if (!hasWarning)
                {
                    yield return new Finding(
                        Id: $"{Id}:AUTOSUBMIT:{node.Id}",
                        Severity: Severity.High,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_AUTO_SUBMIT",
                        Message: $"Form '{node.Id}' is configured to auto-submit without user warning.",
                        WcagReference: "WCAG 2.2 – 3.2.2 On Input (Level A)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Auto-submitting forms can cause unexpected data submission and navigation.",
                        SuggestedFix: $"Add a visible warning about auto-submit behavior or use an explicit submit button for '{node.Id}'.",
                        WcagCriterion: WcagCriterion.OnInput_3_2_2
                    );
                }
            }
        }

        // Check for dropdowns that navigate immediately
        if (node.Type.Equals("DropDown", StringComparison.OrdinalIgnoreCase) ||
            node.Type.Equals("ComboBox", StringComparison.OrdinalIgnoreCase) ||
            node.Type.Equals("Select", StringComparison.OrdinalIgnoreCase))
        {
            // Check if selection triggers navigation
            if (node.Properties.TryGetValue("OnChange", out var onChange))
            {
                var onChangeStr = onChange?.ToString() ?? "";
                if (onChangeStr.Contains("Navigate", StringComparison.OrdinalIgnoreCase))
                {
                    yield return new Finding(
                        Id: $"{Id}:DROPDOWN_NAV:{node.Id}",
                        Severity: Severity.Medium,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_DROPDOWN_NAVIGATE",
                        Message: $"Dropdown '{node.Id}' navigates immediately when selection changes.",
                        WcagReference: "WCAG 2.2 – 3.2.2 On Input (Level A)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Keyboard users navigating through dropdown options may accidentally trigger navigation before making their intended selection.",
                        SuggestedFix: $"Add a 'Go' button next to '{node.Id}' to confirm selection before navigating, or clearly warn users that selection navigates immediately.",
                        WcagCriterion: WcagCriterion.OnInput_3_2_2
                    );
                }
            }
        }
    }

    private static bool CausesContextChange(string handler)
    {
        var contextChangePatterns = new[]
        {
            "Navigate", "Redirect", "window.location", "href",
            "Submit", "Post", "Save", "Patch", "Update",
            "Launch", "OpenUrl", "Open("
        };

        return contextChangePatterns.Any(p => 
            handler.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static bool OpensPopupOrDialog(string handler)
    {
        var popupPatterns = new[]
        {
            "ShowPopup", "OpenDialog", "Modal", "Overlay",
            "window.open", "Popup", "Dialog"
        };

        return popupPatterns.Any(p => 
            handler.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasBehaviorWarning(UiNode node)
    {
        if (node.Properties == null) return false;

        // Check for instruction text or warnings
        var warningProps = new[] { "Instructions", "HelpText", "Description", "Tooltip", "Warning" };
        
        foreach (var prop in warningProps)
        {
            if (node.Properties.TryGetValue(prop, out var value))
            {
                var text = value?.ToString()?.ToLowerInvariant() ?? "";
                if (text.Contains("automatic") || text.Contains("immediately") ||
                    text.Contains("will navigate") || text.Contains("will submit"))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
