using System;
using System.Collections.Generic;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for proper status message handling for screen readers.
/// WCAG 4.1.3: Status messages can be programmatically determined through role or properties 
/// such that they can be presented to the user by assistive technologies without receiving focus.
/// </summary>
public sealed class StatusMessageRule : IRule
{
    public string Id => "STATUS_MESSAGE";
    public string Description => "Status messages must be announced to screen reader users without requiring focus change.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage, SurfaceType.DomSnapshot];

    // Control types that commonly display status messages
    private static readonly HashSet<string> StatusDisplayTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Label", "Text", "Classic/Label", "Notification", "Banner", "Alert", "Message"
    };

    // Control types that trigger status changes
    private static readonly HashSet<string> ActionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Button", "IconButton", "Classic/Button", "Link"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check status display controls
        if (StatusDisplayTypes.Contains(node.Type))
        {
            // Check if this appears to be a status/notification control
            if (AppearsToBeStatusMessage(node))
            {
                var hasLiveRegion = HasLiveRegionConfiguration(node);
                
                if (!hasLiveRegion)
                {
                    yield return new Finding(
                        Id: $"{Id}:LIVE_REGION:{node.Id}",
                        Severity: Severity.Medium,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_NO_LIVE_REGION",
                        Message: $"Control '{node.Id}' appears to display status messages but is not configured as a live region for screen readers.",
                        WcagReference: "WCAG 2.2 § 4.1.3 Status Messages (Level AA)",
                        Section508Reference: "Section 508 E207.2 - Status Messages",
                        Rationale: "Status messages must be announced to screen reader users without moving focus, using ARIA live regions or equivalent.",
                        SuggestedFix: $"Set the Live property on '{node.Id}' to 'Polite' or 'Assertive' (for urgent messages), or use the Notify function to programmatically announce messages.",
                        WcagCriterion: WcagCriterion.StatusMessages_4_1_3
                    );
                }
            }

            // Check for dynamic content updates
            if (HasDynamicContent(node) && !HasLiveRegionConfiguration(node))
            {
                yield return new Finding(
                    Id: $"{Id}:DYNAMIC:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_DYNAMIC_CONTENT",
                    Message: $"Control '{node.Id}' has dynamic content that may not be announced to screen reader users when it changes.",
                    WcagReference: "WCAG 2.2 § 4.1.3 Status Messages (Level AA)",
                    Section508Reference: "Section 508 E207.2 - Status Messages",
                    Rationale: "When content changes dynamically without a page reload, screen reader users may not be aware of the change unless it's properly announced.",
                    SuggestedFix: $"If '{node.Id}' displays important updates, set Live='Polite' so changes are announced. For non-critical updates, ensure the update is visually prominent.",
                    WcagCriterion: WcagCriterion.StatusMessages_4_1_3
                );
            }
        }

        // Check action controls for notification handling
        if (ActionTypes.Contains(node.Type))
        {
            if (HasActionWithoutNotification(node))
            {
                yield return new Finding(
                    Id: $"{Id}:ACTION:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_ACTION_NO_FEEDBACK",
                    Message: $"Button '{node.Id}' performs an action but may not provide accessible status feedback to screen reader users.",
                    WcagReference: "WCAG 2.2 § 4.1.3 Status Messages (Level AA)",
                    Section508Reference: "Section 508 E207.2 - Status Messages",
                    Rationale: "When users perform actions, they should receive confirmation or error messages that are accessible to screen readers.",
                    SuggestedFix: $"Use the Notify function in '{node.Id}' OnSelect to announce success/failure messages, or update a live region with status information.",
                    WcagCriterion: WcagCriterion.StatusMessages_4_1_3
                );
            }
        }

        // Check for loading indicators
        if (IsLoadingIndicator(node))
        {
            if (!HasLiveRegionConfiguration(node))
            {
                yield return new Finding(
                    Id: $"{Id}:LOADING:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_LOADING_NOT_ANNOUNCED",
                    Message: $"Loading indicator '{node.Id}' is not configured to announce loading/busy state to screen readers.",
                    WcagReference: "WCAG 2.2 § 4.1.3 Status Messages (Level AA)",
                    Section508Reference: "Section 508 E207.2 - Status Messages",
                    Rationale: "Screen reader users should be informed when the app is loading or busy processing, so they know to wait.",
                    SuggestedFix: $"When '{node.Id}' becomes visible, use Notify to announce 'Loading...' or similar. When complete, announce the result. Alternatively, set Live='Assertive' on the loading indicator.",
                    WcagCriterion: WcagCriterion.StatusMessages_4_1_3
                );
            }
        }

        // Check for search results count
        if (IsSearchResultsDisplay(node))
        {
            if (!HasLiveRegionConfiguration(node))
            {
                yield return new Finding(
                    Id: $"{Id}:SEARCH:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_SEARCH_RESULTS",
                    Message: $"Search results count '{node.Id}' should be announced to screen readers when results update.",
                    WcagReference: "WCAG 2.2 § 4.1.3 Status Messages (Level AA)",
                    Section508Reference: "Section 508 E207.2 - Status Messages",
                    Rationale: "When search results update, screen reader users should be informed of the number of results found without having to navigate to find this information.",
                    SuggestedFix: $"Set Live='Polite' on '{node.Id}' so that when the search completes, the results count (e.g., '5 results found') is announced automatically.",
                    WcagCriterion: WcagCriterion.StatusMessages_4_1_3
                );
            }
        }
    }

    private static bool AppearsToBeStatusMessage(UiNode node)
    {
        // Check ID/Name patterns
        var idLower = node.Id?.ToLowerInvariant() ?? "";
        var nameLower = node.Name?.ToLowerInvariant() ?? "";

        var statusPatterns = new[] { "status", "message", "notification", "alert", "error", "success", "info", "warning", "banner" };
        
        foreach (var pattern in statusPatterns)
        {
            if (idLower.Contains(pattern) || nameLower.Contains(pattern))
                return true;
        }

        // Check text content
        var text = node.Text ?? node.Properties?.GetValueOrDefault("Text")?.ToString();
        if (text != null)
        {
            var textLower = text.ToLowerInvariant();
            if (textLower.Contains("saved") ||
                textLower.Contains("error") ||
                textLower.Contains("success") ||
                textLower.Contains("failed") ||
                textLower.Contains("loading") ||
                textLower.Contains("submitted"))
            {
                return true;
            }
        }

        // Check visibility conditions (often status messages are conditionally visible)
        if (node.Properties?.TryGetValue("Visible", out var visible) == true)
        {
            var visibleStr = visible?.ToString();
            if (visibleStr != null && visibleStr.Contains("If(", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLiveRegionConfiguration(UiNode node)
    {
        if (node.Properties == null) return false;

        // Check for Live property
        if (node.Properties.TryGetValue("Live", out var live))
        {
            var liveStr = live?.ToString();
            return liveStr != null && 
                   (liveStr.Contains("Polite", StringComparison.OrdinalIgnoreCase) ||
                    liveStr.Contains("Assertive", StringComparison.OrdinalIgnoreCase));
        }

        // Check for ARIA live attribute
        if (node.Properties.TryGetValue("AriaLive", out var ariaLive))
        {
            return ariaLive != null;
        }

        // Check Role for status/alert
        var role = node.Role ?? node.Properties?.GetValueOrDefault("Role")?.ToString();
        if (role != null && 
            (role.Contains("status", StringComparison.OrdinalIgnoreCase) ||
             role.Contains("alert", StringComparison.OrdinalIgnoreCase) ||
             role.Contains("log", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static bool HasDynamicContent(UiNode node)
    {
        if (node.Properties == null) return false;

        // Check for formulas in Text property
        if (node.Properties.TryGetValue("Text", out var text))
        {
            var textStr = text?.ToString();
            if (textStr != null && 
                (textStr.Contains("CountRows", StringComparison.OrdinalIgnoreCase) ||
                 textStr.Contains("Filter", StringComparison.OrdinalIgnoreCase) ||
                 textStr.Contains("Concat", StringComparison.OrdinalIgnoreCase) ||
                 textStr.Contains("Text(", StringComparison.OrdinalIgnoreCase) ||
                 textStr.Contains("If(", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasActionWithoutNotification(UiNode node)
    {
        if (node.Properties == null) return false;

        // Check OnSelect for actions that should provide feedback
        if (node.Properties.TryGetValue("OnSelect", out var onSelect))
        {
            var actionStr = onSelect?.ToString();
            if (actionStr != null)
            {
                // Actions that should have feedback
                var feedbackActions = new[] { "Patch(", "Remove(", "SubmitForm(", "ResetForm(", "Navigate(", "Set(", "UpdateContext(" };
                var hasFeedbackAction = false;
                
                foreach (var action in feedbackActions)
                {
                    if (actionStr.Contains(action, StringComparison.OrdinalIgnoreCase))
                    {
                        hasFeedbackAction = true;
                        break;
                    }
                }

                // Check if it already has notification
                var hasNotification = actionStr.Contains("Notify(", StringComparison.OrdinalIgnoreCase);

                return hasFeedbackAction && !hasNotification;
            }
        }

        return false;
    }

    private static bool IsLoadingIndicator(UiNode node)
    {
        var idLower = node.Id?.ToLowerInvariant() ?? "";
        var nameLower = node.Name?.ToLowerInvariant() ?? "";
        var typeLower = node.Type?.ToLowerInvariant() ?? "";

        return idLower.Contains("loading") || 
               idLower.Contains("spinner") || 
               idLower.Contains("progress") ||
               nameLower.Contains("loading") ||
               typeLower.Contains("spinner") ||
               typeLower.Contains("progress");
    }

    private static bool IsSearchResultsDisplay(UiNode node)
    {
        var idLower = node.Id?.ToLowerInvariant() ?? "";
        var nameLower = node.Name?.ToLowerInvariant() ?? "";
        
        // Check for search results patterns
        if (idLower.Contains("result") || idLower.Contains("count") || 
            nameLower.Contains("result") || nameLower.Contains("found"))
        {
            // Check if text contains count formula
            var text = node.Properties?.GetValueOrDefault("Text")?.ToString();
            if (text != null && 
                (text.Contains("CountRows", StringComparison.OrdinalIgnoreCase) ||
                 text.Contains("result", StringComparison.OrdinalIgnoreCase) ||
                 text.Contains("found", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
