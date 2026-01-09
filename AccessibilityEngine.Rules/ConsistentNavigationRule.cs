using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for consistent navigation and identification across pages.
/// WCAG 3.2.3: Navigational mechanisms repeated on multiple pages occur in the same relative order.
/// WCAG 3.2.4: Components with the same functionality are identified consistently.
/// </summary>
public sealed class ConsistentNavigationRule : IRule
{
    public string Id => "CONSISTENT_NAVIGATION";
    public string Description => "Navigation and component identification must be consistent across pages.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Navigation component types
    private static readonly HashSet<string> NavigationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Navigation", "Nav", "Menu", "NavBar", "Header", "Footer",
        "Sidebar", "TopBar", "BottomNav", "TabBar"
    };

    // Common action types that should be identified consistently
    private static readonly HashSet<string> ActionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Button", "Link", "IconButton", "ActionButton"
    };

    // Common functionality labels that should be consistent
    private static readonly Dictionary<string[], string> CommonFunctionalities = new()
    {
        { ["search", "find", "lookup"], "Search" },
        { ["save", "submit", "send"], "Save/Submit" },
        { ["cancel", "close", "dismiss"], "Cancel/Close" },
        { ["delete", "remove", "trash"], "Delete" },
        { ["edit", "modify", "change"], "Edit" },
        { ["add", "create", "new"], "Add/Create" },
        { ["back", "return", "previous"], "Back" },
        { ["next", "forward", "continue"], "Next" },
        { ["help", "support", "info"], "Help" },
        { ["settings", "preferences", "options"], "Settings" },
        { ["profile", "account", "user"], "Profile" },
        { ["logout", "signout", "sign out"], "Logout" },
        { ["login", "signin", "sign in"], "Login" }
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check navigation components for consistency markers
        if (NavigationTypes.Contains(node.Type))
        {
            foreach (var finding in CheckNavigationConsistency(node, context))
                yield return finding;
        }

        // Check action components for consistent identification
        if (ActionTypes.Contains(node.Type))
        {
            foreach (var finding in CheckIdentificationConsistency(node, context))
                yield return finding;
        }

        // Check for component templates/reusable components
        foreach (var finding in CheckComponentReuse(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckNavigationConsistency(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check if navigation is marked as a reusable component
        var isReusable = node.Properties.ContainsKey("IsComponent") ||
                        node.Properties.ContainsKey("ComponentName") ||
                        node.Properties.ContainsKey("Template");

        // Check if navigation has a consistent identifier
        var hasConsistentId = node.Properties.ContainsKey("ComponentId") ||
                             !string.IsNullOrWhiteSpace(node.Name);

        if (!isReusable && !hasConsistentId)
        {
            yield return new Finding(
                Id: $"{Id}:NAV_REUSE:{node.Id}",
                Severity: Severity.Low,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_NAV_NOT_COMPONENT",
                Message: $"Navigation '{node.Id}' is not implemented as a reusable component, which may lead to inconsistent navigation across pages.",
                WcagReference: "WCAG 2.2 – 3.2.3 Consistent Navigation (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Navigation that appears on multiple pages should be implemented as a shared component to ensure consistent order and behavior.",
                SuggestedFix: $"Convert '{node.Id}' to a reusable component that can be shared across all pages to maintain consistent navigation.",
                WcagCriterion: WcagCriterion.ConsistentNavigation_3_2_3
            );
        }

        // Check if navigation order properties are defined
        if (node.Children?.Count > 0)
        {
            var hasExplicitOrder = node.Children.All(c => 
                c.Properties?.ContainsKey("Order") == true ||
                c.Properties?.ContainsKey("TabIndex") == true ||
                c.Properties?.ContainsKey("Position") == true);

            if (!hasExplicitOrder && node.Children.Count > 3)
            {
                yield return new Finding(
                    Id: $"{Id}:NAV_ORDER:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_NAV_ORDER",
                    Message: $"Navigation '{node.Id}' items don't have explicit ordering, which may lead to inconsistent order across pages.",
                    WcagReference: "WCAG 2.2 – 3.2.3 Consistent Navigation (Level AA)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Navigation items should maintain the same relative order on every page where they appear.",
                    SuggestedFix: $"Add explicit Order or Position properties to navigation items in '{node.Id}' to ensure consistent ordering.",
                    WcagCriterion: WcagCriterion.ConsistentNavigation_3_2_3
                );
            }
        }
    }

    private IEnumerable<Finding> CheckIdentificationConsistency(UiNode node, RuleContext context)
    {
        var actionText = GetActionText(node);
        if (string.IsNullOrWhiteSpace(actionText)) yield break;

        var normalizedText = actionText.ToLowerInvariant();

        // Check if the action uses standard terminology
        foreach (var functionality in CommonFunctionalities)
        {
            var matchedTerm = functionality.Key.FirstOrDefault(term => 
                normalizedText.Contains(term));

            if (matchedTerm != null)
            {
                // Check if the icon/visual matches the text
                var hasMatchingIcon = HasMatchingIcon(node, matchedTerm);
                var hasConsistentLabel = HasConsistentLabel(node, functionality.Value);

                if (!hasConsistentLabel && !hasMatchingIcon)
                {
                    yield return new Finding(
                        Id: $"{Id}:IDENTIFY:{node.Id}",
                        Severity: Severity.Low,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_INCONSISTENT_ID",
                        Message: $"Action '{node.Id}' with text '{actionText}' may not be consistently identified across the application.",
                        WcagReference: "WCAG 2.2 – 3.2.4 Consistent Identification (Level AA)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Components with the same functionality should be labeled consistently throughout the application to avoid confusion.",
                        SuggestedFix: $"Ensure '{actionText}' actions use the same label, icon, and accessible name across all pages. Consider using '{functionality.Value}' consistently.",
                        WcagCriterion: WcagCriterion.ConsistentIdentification_3_2_4
                    );
                }

                break;
            }
        }

        // Check for icon-only buttons that might be inconsistent
        if (IsIconOnly(node))
        {
            var iconName = GetIconName(node);
            
            if (!string.IsNullOrWhiteSpace(iconName))
            {
                yield return new Finding(
                    Id: $"{Id}:ICON_ONLY:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_ICON_CONSISTENCY",
                    Message: $"Icon-only control '{node.Id}' using '{iconName}' should use the same icon for this functionality throughout the app.",
                    WcagReference: "WCAG 2.2 – 3.2.4 Consistent Identification (Level AA)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Icon-only controls are especially dependent on consistent identification since users must learn what each icon means.",
                    SuggestedFix: $"Verify that the '{iconName}' icon is used consistently for this action across all pages, with the same accessible label.",
                    WcagCriterion: WcagCriterion.ConsistentIdentification_3_2_4
                );
            }
        }
    }

    private IEnumerable<Finding> CheckComponentReuse(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check for components that should be shared but aren't
        var isHeader = node.Type.Equals("Header", StringComparison.OrdinalIgnoreCase) ||
                      node.Name?.Contains("header", StringComparison.OrdinalIgnoreCase) == true;
        
        var isFooter = node.Type.Equals("Footer", StringComparison.OrdinalIgnoreCase) ||
                      node.Name?.Contains("footer", StringComparison.OrdinalIgnoreCase) == true;

        if (isHeader || isFooter)
        {
            var isSharedComponent = node.Properties.ContainsKey("IsComponent") ||
                                   node.Properties.ContainsKey("SharedComponent") ||
                                   node.Properties.ContainsKey("ComponentLibrary");

            if (!isSharedComponent)
            {
                var componentType = isHeader ? "Header" : "Footer";
                
                yield return new Finding(
                    Id: $"{Id}:SHARED:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_NOT_SHARED",
                    Message: $"{componentType} '{node.Id}' should be a shared component to ensure consistency across pages.",
                    WcagReference: "WCAG 2.2 – 3.2.3 Consistent Navigation (Level AA)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: $"{componentType}s that appear on multiple pages should be shared components to maintain consistent content and order.",
                    SuggestedFix: $"Convert '{node.Id}' to a component in a component library so it can be reused consistently.",
                    WcagCriterion: WcagCriterion.ConsistentNavigation_3_2_3
                );
            }
        }
    }

    private static string? GetActionText(UiNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.Text))
            return node.Text;

        if (!string.IsNullOrWhiteSpace(node.Name))
            return node.Name;

        if (node.Properties != null)
        {
            var textProps = new[] { "Text", "Label", "Content", "AccessibleLabel" };
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

    private static bool HasMatchingIcon(UiNode node, string functionality)
    {
        if (node.Properties == null) return false;

        if (node.Properties.TryGetValue("Icon", out var icon))
        {
            var iconStr = icon?.ToString()?.ToLowerInvariant() ?? "";
            return iconStr.Contains(functionality);
        }

        return false;
    }

    private static bool HasConsistentLabel(UiNode node, string expectedLabel)
    {
        if (node.Properties == null) return false;

        if (node.Properties.TryGetValue("AccessibleLabel", out var label))
        {
            var labelStr = label?.ToString() ?? "";
            return labelStr.Contains(expectedLabel, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsIconOnly(UiNode node)
    {
        if (node.Type.Contains("Icon", StringComparison.OrdinalIgnoreCase))
            return true;

        if (node.Properties?.TryGetValue("IsIconOnly", out var isIconOnly) == true &&
            isIconOnly is bool ico && ico)
            return true;

        // Check if there's an icon but no text
        var hasIcon = node.Properties?.ContainsKey("Icon") == true;
        var hasText = !string.IsNullOrWhiteSpace(node.Text) || 
                     !string.IsNullOrWhiteSpace(GetActionText(node));

        return hasIcon && !hasText;
    }

    private static string? GetIconName(UiNode node)
    {
        if (node.Properties?.TryGetValue("Icon", out var icon) == true)
            return icon?.ToString();

        return null;
    }
}
