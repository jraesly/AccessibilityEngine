using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for mechanisms to bypass blocks of repeated content.
/// WCAG 2.4.1: A mechanism is available to bypass blocks of content that are repeated on multiple pages.
/// </summary>
public sealed class BypassBlocksRule : IRule
{
    public string Id => "BYPASS_BLOCKS";
    public string Description => "Pages must provide a mechanism to bypass blocks of repeated content.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Screen/page level types
    private static readonly HashSet<string> PageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Screen", "Page", "View", "Form", "App"
    };

    // Navigation/header types that users might want to skip
    private static readonly HashSet<string> RepeatableBlockTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Header", "Navigation", "Nav", "Menu", "Sidebar", "Footer",
        "TopBar", "NavBar", "HeaderContainer", "NavigationPane"
    };

    // Types that provide skip functionality
    private static readonly HashSet<string> SkipLinkTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Link", "Button", "SkipLink", "SkipNavigation", "SkipToContent"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check at page/screen level for skip link mechanism
        if (PageTypes.Contains(node.Type))
        {
            foreach (var finding in CheckPageBypassMechanism(node, context))
                yield return finding;
        }

        // Check repeatable blocks for proper landmarks
        if (RepeatableBlockTypes.Contains(node.Type))
        {
            foreach (var finding in CheckBlockLandmarks(node, context))
                yield return finding;
        }
    }

    private IEnumerable<Finding> CheckPageBypassMechanism(UiNode node, RuleContext context)
    {
        // Look for skip links or bypass mechanisms
        var hasSkipLink = HasSkipLinkMechanism(node);
        var hasLandmarks = HasProperLandmarks(node);
        var hasHeadingStructure = HasHeadingStructure(node);

        // If the page has repeated content but no bypass mechanism
        var hasRepeatableContent = HasRepeatableBlocks(node);

        if (hasRepeatableContent && !hasSkipLink && !hasLandmarks && !hasHeadingStructure)
        {
            yield return new Finding(
                Id: $"{Id}:PAGE:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_NO_BYPASS",
                Message: $"Page '{node.Id}' has repeated content blocks but no mechanism to bypass them.",
                WcagReference: "WCAG 2.2 – 2.4.1 Bypass Blocks (Level A)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Keyboard and screen reader users need a way to skip past navigation and other repeated content to reach the main content quickly.",
                SuggestedFix: $"Add a 'Skip to main content' link at the beginning of '{node.Id}', or use proper landmark roles (main, navigation, banner) to enable assistive technology navigation.",
                WcagCriterion: WcagCriterion.BypassBlocks_2_4_1
            );
        }

        // Check if skip link exists but might be hidden improperly
        if (hasSkipLink)
        {
            var skipLink = FindSkipLink(node);
            if (skipLink != null)
            {
                // Check if skip link is visible on focus
                var isVisibleOnFocus = IsVisibleOnFocus(skipLink);
                
                if (!isVisibleOnFocus)
                {
                    yield return new Finding(
                        Id: $"{Id}:SKIP_HIDDEN:{skipLink.Id}",
                        Severity: Severity.Low,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: skipLink.Id,
                        ControlType: skipLink.Type,
                        IssueType: $"{Id}_SKIP_HIDDEN",
                        Message: $"Skip link '{skipLink.Id}' may not be visible when focused.",
                        WcagReference: "WCAG 2.2 – 2.4.1 Bypass Blocks (Level A)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Skip links should become visible when they receive keyboard focus so users know they exist.",
                        SuggestedFix: $"Ensure '{skipLink.Id}' becomes visible when focused using CSS :focus styles.",
                        WcagCriterion: WcagCriterion.BypassBlocks_2_4_1
                    );
                }
            }
        }
    }

    private IEnumerable<Finding> CheckBlockLandmarks(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check if repeatable blocks have proper landmark roles
        var hasRole = node.Properties.TryGetValue("Role", out var role) ||
                     node.Properties.TryGetValue("AriaRole", out role) ||
                     node.Properties.TryGetValue("aria-role", out role);

        var roleValue = role?.ToString()?.ToLowerInvariant() ?? "";
        
        var expectedRoles = GetExpectedRolesForType(node.Type);

        if (!hasRole || !expectedRoles.Contains(roleValue))
        {
            yield return new Finding(
                Id: $"{Id}:LANDMARK:{node.Id}",
                Severity: Severity.Low,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_MISSING_LANDMARK",
                Message: $"Repeatable block '{node.Id}' does not have an appropriate landmark role.",
                WcagReference: "WCAG 2.2 – 2.4.1 Bypass Blocks (Level A)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Landmark roles help screen reader users navigate directly to different sections of the page.",
                SuggestedFix: $"Add role='{expectedRoles.FirstOrDefault() ?? "region"}' to '{node.Id}' to enable landmark navigation.",
                WcagCriterion: WcagCriterion.BypassBlocks_2_4_1
            );
        }
    }

    private bool HasSkipLinkMechanism(UiNode node)
    {
        return FindSkipLink(node) != null;
    }

    private UiNode? FindSkipLink(UiNode node)
    {
        // Check if this node is a skip link
        if (IsSkipLink(node))
            return node;

        // Search children
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                var skipLink = FindSkipLink(child);
                if (skipLink != null)
                    return skipLink;
            }
        }

        return null;
    }

    private bool IsSkipLink(UiNode node)
    {
        // Check by type
        if (SkipLinkTypes.Contains(node.Type) && 
            (node.Type.Contains("Skip", StringComparison.OrdinalIgnoreCase) ||
             node.Name?.Contains("skip", StringComparison.OrdinalIgnoreCase) == true ||
             node.Text?.Contains("skip", StringComparison.OrdinalIgnoreCase) == true))
        {
            return true;
        }

        // Check by name/text
        var text = (node.Text ?? node.Name ?? "").ToLowerInvariant();
        if (text.Contains("skip") && (text.Contains("content") || text.Contains("main") || text.Contains("nav")))
        {
            return true;
        }

        // Check by target (links to #main, #content, etc.)
        if (node.Properties != null)
        {
            if (node.Properties.TryGetValue("Href", out var href) ||
                node.Properties.TryGetValue("Target", out href) ||
                node.Properties.TryGetValue("Navigate", out href))
            {
                var hrefValue = href?.ToString()?.ToLowerInvariant() ?? "";
                if (hrefValue.Contains("#main") || hrefValue.Contains("#content") || 
                    hrefValue.Contains("#skip"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool HasProperLandmarks(UiNode node)
    {
        var landmarks = new HashSet<string> { "main", "navigation", "banner", "contentinfo", "search" };
        return HasAnyLandmark(node, landmarks);
    }

    private bool HasAnyLandmark(UiNode node, HashSet<string> landmarks)
    {
        if (node.Properties != null)
        {
            if (node.Properties.TryGetValue("Role", out var role) ||
                node.Properties.TryGetValue("AriaRole", out role))
            {
                if (landmarks.Contains(role?.ToString()?.ToLowerInvariant() ?? ""))
                    return true;
            }
        }

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                if (HasAnyLandmark(child, landmarks))
                    return true;
            }
        }

        return false;
    }

    private bool HasHeadingStructure(UiNode node)
    {
        // Check if there's a heading structure that enables navigation
        return HasHeading(node, 1) && HasHeading(node, 2);
    }

    private bool HasHeading(UiNode node, int level)
    {
        if (node.Properties != null)
        {
            if (node.Properties.TryGetValue("HeadingLevel", out var headingLevel) ||
                node.Properties.TryGetValue("Level", out headingLevel))
            {
                if (headingLevel is int l && l == level)
                    return true;
                if (int.TryParse(headingLevel?.ToString(), out var parsedLevel) && parsedLevel == level)
                    return true;
            }

            if (node.Properties.TryGetValue("Role", out var role) && 
                role?.ToString()?.ToLowerInvariant() == "heading")
            {
                if (node.Properties.TryGetValue("aria-level", out var ariaLevel))
                {
                    if (int.TryParse(ariaLevel?.ToString(), out var aLevel) && aLevel == level)
                        return true;
                }
            }
        }

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                if (HasHeading(child, level))
                    return true;
            }
        }

        return false;
    }

    private bool HasRepeatableBlocks(UiNode node)
    {
        if (node.Children == null) return false;

        foreach (var child in node.Children)
        {
            if (RepeatableBlockTypes.Contains(child.Type))
                return true;
            
            if (HasRepeatableBlocks(child))
                return true;
        }

        return false;
    }

    private bool IsVisibleOnFocus(UiNode node)
    {
        if (node.Properties == null) return true; // Assume visible if no properties

        // Check for visibility properties
        if (node.Properties.TryGetValue("Visible", out var visible))
        {
            if (visible is bool v && !v)
            {
                // Check if there's focus visibility
                return node.Properties.ContainsKey("VisibleOnFocus") ||
                       node.Properties.ContainsKey("ShowOnFocus");
            }
        }

        // Check for off-screen positioning that becomes visible on focus
        if (node.Properties.TryGetValue("Position", out var position))
        {
            var posValue = position?.ToString()?.ToLowerInvariant() ?? "";
            if (posValue.Contains("absolute") || posValue.Contains("fixed"))
            {
                // Check if it moves into view on focus
                return node.Properties.ContainsKey("FocusStyles") ||
                       node.Properties.ContainsKey("OnFocus");
            }
        }

        return true;
    }

    private static HashSet<string> GetExpectedRolesForType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "header" or "topbar" or "headercontainer" => ["banner"],
            "navigation" or "nav" or "menu" or "navbar" or "navigationpane" => ["navigation"],
            "footer" => ["contentinfo"],
            "sidebar" => ["complementary", "navigation"],
            "main" or "content" or "maincontent" => ["main"],
            _ => ["region"]
        };
    }
}
