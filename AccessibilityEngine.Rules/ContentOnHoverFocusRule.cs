using System;
using System.Collections.Generic;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for content on hover or focus accessibility issues.
/// WCAG 1.4.13: Additional content triggered by hover/focus must be dismissible, hoverable, and persistent.
/// </summary>
public sealed class ContentOnHoverFocusRule : IRule
{
    public string Id => "CONTENT_ON_HOVER_FOCUS";
    public string Description => "Additional content appearing on hover or focus must be dismissible, hoverable, and persistent.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Properties that trigger hover/focus content
    private static readonly string[] HoverFocusProperties =
    [
        "OnHover", "HoverFill", "HoverColor", "HoverBorderColor",
        "OnFocus", "FocusedFill", "FocusedBorderColor",
        "Tooltip", "ToolTip", "HelpText", "InfoText"
    ];

    // Control types that commonly show additional content on hover
    private static readonly HashSet<string> TooltipCapableTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Button", "IconButton", "Classic/Button",
        "Label", "Text", "Classic/Label",
        "Icon", "Image", "Classic/Icon",
        "Link", "HyperLink",
        "TextInput", "ComboBox", "Dropdown",
        "Component", "PCFControl", "Custom"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check for tooltip content
        foreach (var finding in CheckTooltipContent(node, context))
            yield return finding;

        // Check for hover-triggered visibility changes
        foreach (var finding in CheckHoverVisibility(node, context))
            yield return finding;

        // Check for custom popup/overlay controls
        foreach (var finding in CheckPopupOverlays(node, context))
            yield return finding;

        // Check for hover menus
        foreach (var finding in CheckHoverMenus(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckTooltipContent(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check for tooltip property
        string? tooltip = null;
        if (node.Properties.TryGetValue("Tooltip", out var tt))
            tooltip = tt?.ToString();
        else if (node.Properties.TryGetValue("ToolTip", out var t))
            tooltip = t?.ToString();
        else if (node.Properties.TryGetValue("HelpText", out var ht))
            tooltip = ht?.ToString();

        if (string.IsNullOrWhiteSpace(tooltip)) yield break;

        // Tooltips should provide informational content - check if it's meaningful
        if (tooltip.Length > 100)
        {
            yield return new Finding(
                Id: $"{Id}:LONG_TOOLTIP:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_LONG_TOOLTIP",
                Message: $"Control '{node.Id}' has a long tooltip ({tooltip.Length} chars). Ensure it remains visible long enough to read and can be hovered.",
                WcagReference: "WCAG 2.2 § 1.4.13 Content on Hover or Focus (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Long tooltips need sufficient time to read. Users should be able to move pointer over the tooltip without it disappearing.",
                SuggestedFix: $"For '{node.Id}': (1) Ensure tooltip stays visible for at least 1.5 seconds, (2) Allow users to hover over the tooltip, (3) Consider using a persistent info panel instead of tooltip for lengthy content.",
                WcagCriterion: WcagCriterion.ContentOnHoverOrFocus_1_4_13
            );
        }

        // Check if tooltip is essential (contains error info)
        if (tooltip.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            tooltip.Contains("required", StringComparison.OrdinalIgnoreCase) ||
            tooltip.Contains("invalid", StringComparison.OrdinalIgnoreCase))
        {
            yield return new Finding(
                Id: $"{Id}:ESSENTIAL_TOOLTIP:{node.Id}",
                Severity: Severity.High,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_ESSENTIAL_TOOLTIP",
                Message: $"Control '{node.Id}' has important information (error/required) in tooltip. This should be visible without hover.",
                WcagReference: "WCAG 2.2 § 1.4.13 Content on Hover or Focus (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Critical information like errors should not be hidden behind hover interactions. Screen magnifier users may miss tooltips.",
                SuggestedFix: $"Display error/required information for '{node.Id}' in persistent visible text, not just in tooltips. Use inline error messages.",
                WcagCriterion: WcagCriterion.ContentOnHoverOrFocus_1_4_13
            );
        }
    }

    private IEnumerable<Finding> CheckHoverVisibility(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check for visibility changes on hover
        if (node.Properties.TryGetValue("Visible", out var visible))
        {
            var visibleFormula = visible?.ToString() ?? "";
            
            // Check if visibility is tied to hover state
            if (visibleFormula.Contains("Hover", StringComparison.OrdinalIgnoreCase) ||
                visibleFormula.Contains("_hover", StringComparison.OrdinalIgnoreCase))
            {
                yield return new Finding(
                    Id: $"{Id}:HOVER_VISIBLE:{node.Id}",
                    Severity: Severity.High,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_HOVER_VISIBLE",
                    Message: $"Control '{node.Id}' visibility depends on hover state. Ensure content is dismissible, hoverable, and persistent.",
                    WcagReference: "WCAG 2.2 § 1.4.13 Content on Hover or Focus (Level AA)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Content appearing on hover must: (1) Be dismissible without moving pointer, (2) Stay visible when pointer moves to it, (3) Persist until dismissed or hover ends.",
                    SuggestedFix: $"For hover-triggered content '{node.Id}': (1) Add Escape key handler to dismiss, (2) Include the revealed content in hover detection area, (3) Don't auto-hide on timer.",
                    WcagCriterion: WcagCriterion.ContentOnHoverOrFocus_1_4_13
                );
            }
        }

        // Check for OnHover event handlers that show content
        if (node.Properties.TryGetValue("OnHover", out var onHover))
        {
            var hoverAction = onHover?.ToString() ?? "";
            
            if (hoverAction.Contains("Set(", StringComparison.OrdinalIgnoreCase) ||
                hoverAction.Contains("UpdateContext(", StringComparison.OrdinalIgnoreCase) ||
                hoverAction.Contains("Navigate(", StringComparison.OrdinalIgnoreCase))
            {
                yield return new Finding(
                    Id: $"{Id}:ONHOVER_ACTION:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_ONHOVER_ACTION",
                    Message: $"Control '{node.Id}' has OnHover handler that changes state or navigation. Verify hover-triggered content meets WCAG requirements.",
                    WcagReference: "WCAG 2.2 § 1.4.13 Content on Hover or Focus (Level AA)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "State changes on hover must be accessible. The triggered content must be dismissible without moving pointer and persist appropriately.",
                    SuggestedFix: $"Review OnHover handler on '{node.Id}'. If it shows additional content: (1) Also trigger on focus for keyboard users, (2) Allow dismissal via Escape key, (3) Keep content visible until user dismisses it.",
                    WcagCriterion: WcagCriterion.ContentOnHoverOrFocus_1_4_13
                );
            }
        }
    }

    private IEnumerable<Finding> CheckPopupOverlays(UiNode node, RuleContext context)
    {
        var popupTypes = new[] { "Popup", "Overlay", "Modal", "Dialog", "Flyout", "InfoBox" };
        
        var isPopupType = false;
        foreach (var popupType in popupTypes)
        {
            if (node.Type.Contains(popupType, StringComparison.OrdinalIgnoreCase) ||
                node.Name?.Contains(popupType, StringComparison.OrdinalIgnoreCase) == true)
            {
                isPopupType = true;
                break;
            }
        }

        if (!isPopupType) yield break;

        // Check if popup can be dismissed
        var hasDismissButton = HasDismissButton(node);
        var hasEscapeHandler = HasEscapeKeyHandler(node);

        if (!hasDismissButton && !hasEscapeHandler)
        {
            yield return new Finding(
                Id: $"{Id}:POPUP_DISMISS:{node.Id}",
                Severity: Severity.High,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_POPUP_DISMISS",
                Message: $"Popup/overlay '{node.Id}' should be dismissible without moving pointer (e.g., Escape key or close button).",
                WcagReference: "WCAG 2.2 § 1.4.13 Content on Hover or Focus (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Additional content must be dismissible so it doesn't permanently obscure other content. Screen magnifier users especially need this.",
                SuggestedFix: $"Add to '{node.Id}': (1) A visible close/dismiss button, (2) OnKeyDown handler for Escape key to hide the popup.",
                WcagCriterion: WcagCriterion.ContentOnHoverOrFocus_1_4_13
            );
        }
    }

    private IEnumerable<Finding> CheckHoverMenus(UiNode node, RuleContext context)
    {
        // Check for menu controls that might show on hover
        var isMenu = node.Type.Contains("Menu", StringComparison.OrdinalIgnoreCase) ||
                    node.Type.Contains("Dropdown", StringComparison.OrdinalIgnoreCase) ||
                    node.Name?.Contains("Menu", StringComparison.OrdinalIgnoreCase) == true;

        if (!isMenu) yield break;

        // Check if menu is hover-triggered
        if (node.Properties?.TryGetValue("TriggerOn", out var trigger) == true ||
            node.Properties?.TryGetValue("OpenOn", out trigger) == true)
        {
            var triggerValue = trigger?.ToString() ?? "";
            if (triggerValue.Contains("Hover", StringComparison.OrdinalIgnoreCase))
            {
                yield return new Finding(
                    Id: $"{Id}:HOVER_MENU:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_HOVER_MENU",
                    Message: $"Menu '{node.Id}' opens on hover. Ensure the menu: (1) Can be dismissed via Escape, (2) Stays open when pointer moves to menu items, (3) Also opens on click/focus.",
                    WcagReference: "WCAG 2.2 § 1.4.13 Content on Hover or Focus (Level AA)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Hover-triggered menus must be accessible. Users should be able to move to menu items without the menu closing.",
                    SuggestedFix: $"For hover menu '{node.Id}': (1) Add click trigger as alternative, (2) Extend hover detection to include menu items, (3) Add Escape key to close, (4) Ensure keyboard focus can reach menu items.",
                    WcagCriterion: WcagCriterion.ContentOnHoverOrFocus_1_4_13
                );
            }
        }
    }

    private static bool HasDismissButton(UiNode node)
    {
        if (node.Children == null) return false;

        foreach (var child in node.Children)
        {
            var name = child.Name?.ToLowerInvariant() ?? "";
            var text = child.Text?.ToLowerInvariant() ?? "";
            var type = child.Type.ToLowerInvariant();

            // Look for close/dismiss buttons
            if (type.Contains("button") || type.Contains("icon"))
            {
                if (name.Contains("close") || text.Contains("close") ||
                    name.Contains("dismiss") || text.Contains("dismiss") ||
                    name.Contains("cancel") || text.Contains("cancel") ||
                    text.Contains("×") || text.Contains("x") ||
                    name.Contains("closeicon") || name.Contains("closebtn"))
                {
                    return true;
                }
            }

            // Recursive check
            if (HasDismissButton(child))
                return true;
        }

        return false;
    }

    private static bool HasEscapeKeyHandler(UiNode node)
    {
        if (node.Properties == null) return false;

        // Check for keyboard event handlers
        if (node.Properties.TryGetValue("OnKeyDown", out var handler) ||
            node.Properties.TryGetValue("OnKeyPress", out handler) ||
            node.Properties.TryGetValue("OnEscape", out handler))
        {
            var handlerValue = handler?.ToString() ?? "";
            
            // Check if it handles Escape key
            if (handlerValue.Contains("Escape", StringComparison.OrdinalIgnoreCase) ||
                handlerValue.Contains("27", StringComparison.OrdinalIgnoreCase) || // Escape key code
                handlerValue.Contains("close", StringComparison.OrdinalIgnoreCase) ||
                handlerValue.Contains("hide", StringComparison.OrdinalIgnoreCase) ||
                handlerValue.Contains("dismiss", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
