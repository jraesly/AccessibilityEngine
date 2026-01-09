using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks if link purpose can be determined from link text or context.
/// WCAG 2.4.4: The purpose of each link can be determined from the link text alone or
/// from the link text together with its programmatically determined link context.
/// </summary>
public sealed class LinkPurposeRule : IRule
{
    public string Id => "LINK_PURPOSE";
    public string Description => "Link purpose must be determinable from link text or its context.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Link control types
    private static readonly HashSet<string> LinkTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Link", "Hyperlink", "Button", "NavigationLink", "ActionLink", "Anchor"
    };

    // Non-descriptive link text patterns
    private static readonly HashSet<string> NonDescriptiveLinkText = new(StringComparer.OrdinalIgnoreCase)
    {
        "click here", "here", "click", "more", "read more", "learn more",
        "link", "this link", "go", "see more", "details", "info",
        "continue", "next", "previous", "back", "submit", "download",
        "view", "open", "start", "begin"
    };

    // Pattern for URLs used as link text
    private static readonly Regex UrlPattern = new(
        @"^(https?://|www\.)[^\s]+$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check link controls
        if (!IsLinkControl(node)) yield break;

        foreach (var finding in CheckLinkPurpose(node, context))
            yield return finding;
    }

    private bool IsLinkControl(UiNode node)
    {
        if (LinkTypes.Contains(node.Type))
            return true;

        // Check for link role
        if (node.Properties?.TryGetValue("Role", out var role) == true &&
            role?.ToString()?.Equals("link", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        // Check for navigation action
        if (node.Properties?.ContainsKey("OnSelect") == true ||
            node.Properties?.ContainsKey("Navigate") == true ||
            node.Properties?.ContainsKey("Href") == true)
            return true;

        return false;
    }

    private IEnumerable<Finding> CheckLinkPurpose(UiNode node, RuleContext context)
    {
        var linkText = GetLinkText(node);
        var hasAriaLabel = HasDescriptiveAriaLabel(node);

        // Check for empty link text
        if (string.IsNullOrWhiteSpace(linkText) && !hasAriaLabel)
        {
            yield return new Finding(
                Id: $"{Id}:EMPTY:{node.Id}",
                Severity: Severity.High,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_EMPTY",
                Message: $"Link '{node.Id}' has no visible text or accessible name.",
                WcagReference: "WCAG 2.2 – 2.4.4 Link Purpose (In Context) (Level A)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Links without text are not perceivable by screen reader users and provide no information about their destination.",
                SuggestedFix: $"Add descriptive text to link '{node.Id}' that indicates where it leads or what action it performs.",
                WcagCriterion: WcagCriterion.LinkPurposeInContext_2_4_4
            );
            yield break;
        }

        // Check for non-descriptive link text
        if (!string.IsNullOrWhiteSpace(linkText))
        {
            var normalizedText = linkText.Trim().ToLowerInvariant();

            if (NonDescriptiveLinkText.Contains(normalizedText))
            {
                // Check if there's aria-label providing context
                if (!hasAriaLabel)
                {
                    yield return new Finding(
                        Id: $"{Id}:GENERIC:{node.Id}",
                        Severity: Severity.Medium,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_GENERIC",
                        Message: $"Link '{node.Id}' has generic text '{linkText}' that doesn't describe its purpose.",
                        WcagReference: "WCAG 2.2 – 2.4.4 Link Purpose (In Context) (Level A)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Generic link text like 'click here' or 'read more' doesn't tell users where the link goes, especially when links are listed out of context.",
                        SuggestedFix: $"Replace '{linkText}' with descriptive text like 'Read the accessibility guidelines' or add aria-label with the full context.",
                        WcagCriterion: WcagCriterion.LinkPurposeInContext_2_4_4
                    );
                }
            }

            // Check for URL as link text
            if (UrlPattern.IsMatch(linkText))
            {
                yield return new Finding(
                    Id: $"{Id}:URL:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_URL_TEXT",
                    Message: $"Link '{node.Id}' uses a URL as its text, which is difficult to understand and read aloud.",
                    WcagReference: "WCAG 2.2 – 2.4.4 Link Purpose (In Context) (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "URLs are difficult for screen readers to announce and don't convey meaning to users.",
                    SuggestedFix: $"Replace the URL text with a description of the destination (e.g., 'Visit our website' instead of '{TruncateText(linkText, 30)}').",
                    WcagCriterion: WcagCriterion.LinkPurposeInContext_2_4_4
                );
            }

            // Check for very short link text
            if (linkText.Length == 1 && !hasAriaLabel)
            {
                yield return new Finding(
                    Id: $"{Id}:SHORT:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_SHORT",
                    Message: $"Link '{node.Id}' has only a single character '{linkText}' as text.",
                    WcagReference: "WCAG 2.2 – 2.4.4 Link Purpose (In Context) (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Single character links don't provide enough context for users to understand their purpose.",
                    SuggestedFix: $"Add an aria-label to '{node.Id}' describing the link's purpose, or expand the visible text.",
                    WcagCriterion: WcagCriterion.LinkPurposeInContext_2_4_4
                );
            }
        }

        // Check for duplicate link text with different destinations
        foreach (var finding in CheckDuplicateLinkText(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckDuplicateLinkText(UiNode node, RuleContext context)
    {
        // This would require comparing against other links on the same page
        // For now, we flag links that might benefit from more specific text
        
        if (node.Properties == null) yield break;

        var linkText = GetLinkText(node);
        if (string.IsNullOrWhiteSpace(linkText)) yield break;

        // Check if this is a repeated action link (like "Edit" or "Delete" in a list)
        var actionWords = new[] { "edit", "delete", "view", "open", "select", "remove" };
        var normalizedText = linkText.Trim().ToLowerInvariant();

        if (actionWords.Contains(normalizedText))
        {
            // Check if there's context from aria-label or aria-describedby
            var hasContext = node.Properties.ContainsKey("aria-label") ||
                            node.Properties.ContainsKey("aria-describedby") ||
                            node.Properties.ContainsKey("AccessibleLabel");

            if (!hasContext)
            {
                yield return new Finding(
                    Id: $"{Id}:ACTION:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_ACTION_CONTEXT",
                    Message: $"Action link '{node.Id}' with text '{linkText}' may be ambiguous when multiple similar links exist.",
                    WcagReference: "WCAG 2.2 – 2.4.4 Link Purpose (In Context) (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "When multiple links have the same text (like 'Edit'), users need additional context to know which item each link affects.",
                    SuggestedFix: $"Add aria-label to '{node.Id}' that includes the item name (e.g., 'Edit John Smith's record') or use aria-describedby to reference the item.",
                    WcagCriterion: WcagCriterion.LinkPurposeInContext_2_4_4
                );
            }
        }
    }

    private static string? GetLinkText(UiNode node)
    {
        // Check visible text
        if (!string.IsNullOrWhiteSpace(node.Text))
            return node.Text;

        if (!string.IsNullOrWhiteSpace(node.Name))
            return node.Name;

        if (node.Properties != null)
        {
            var textProps = new[] { "Text", "Content", "Label", "Value", "DisplayText" };
            foreach (var prop in textProps)
            {
                if (node.Properties.TryGetValue(prop, out var value))
                {
                    var text = value?.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }
            }
        }

        return null;
    }

    private static bool HasDescriptiveAriaLabel(UiNode node)
    {
        if (node.Properties == null) return false;

        var labelProps = new[] { "aria-label", "AriaLabel", "AccessibleLabel", "aria-labelledby" };
        
        foreach (var prop in labelProps)
        {
            if (node.Properties.TryGetValue(prop, out var value))
            {
                var label = value?.ToString();
                if (!string.IsNullOrWhiteSpace(label) && label.Length > 2)
                    return true;
            }
        }

        return false;
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}
