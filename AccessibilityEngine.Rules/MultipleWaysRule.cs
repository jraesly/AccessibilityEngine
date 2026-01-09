using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for multiple ways to locate content within a set of pages.
/// WCAG 2.4.5: More than one way is available to locate a page within a set of pages,
/// except where the page is the result of or a step in a process.
/// </summary>
public sealed class MultipleWaysRule : IRule
{
    public string Id => "MULTIPLE_WAYS";
    public string Description => "Multiple ways must be available to locate pages (e.g., navigation, search, site map).";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // App-level types where we check for navigation mechanisms
    private static readonly HashSet<string> AppTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "App", "Application", "Root"
    };

    // Screen/page types
    private static readonly HashSet<string> PageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Screen", "Page", "View"
    };

    // Navigation mechanism types
    private static readonly HashSet<string> NavigationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Navigation", "Nav", "Menu", "NavBar", "NavigationPane", "Sidebar",
        "TabBar", "BottomNav", "Drawer", "Breadcrumb"
    };

    // Search control types
    private static readonly HashSet<string> SearchTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Search", "SearchBox", "SearchInput", "SearchBar", "GlobalSearch"
    };

    // Site map types
    private static readonly HashSet<string> SiteMapTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SiteMap", "TableOfContents", "TOC", "Index", "Directory"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check at app level for navigation mechanisms
        if (AppTypes.Contains(node.Type))
        {
            foreach (var finding in CheckAppNavigationMechanisms(node, context))
                yield return finding;
        }

        // Check pages that might be part of a process (which are exempt)
        if (PageTypes.Contains(node.Type))
        {
            foreach (var finding in CheckPageNavigation(node, context))
                yield return finding;
        }
    }

    private IEnumerable<Finding> CheckAppNavigationMechanisms(UiNode node, RuleContext context)
    {
        var mechanisms = new List<string>();

        // Check for various navigation mechanisms
        if (HasNavigationMenu(node))
            mechanisms.Add("navigation menu");

        if (HasSearchFunction(node))
            mechanisms.Add("search");

        if (HasSiteMap(node))
            mechanisms.Add("site map/table of contents");

        if (HasBreadcrumbs(node))
            mechanisms.Add("breadcrumbs");

        // Count screens/pages
        var pageCount = CountPages(node);

        // Only flag if there are multiple pages but limited navigation options
        if (pageCount > 1 && mechanisms.Count < 2)
        {
            var availableMechanisms = mechanisms.Count > 0 
                ? string.Join(", ", mechanisms) 
                : "none detected";

            yield return new Finding(
                Id: $"{Id}:APP:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: null,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_LIMITED_NAVIGATION",
                Message: $"App '{node.Id}' has {pageCount} pages but limited navigation options ({availableMechanisms}).",
                WcagReference: "WCAG 2.2 – 2.4.5 Multiple Ways (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Users have different preferences for finding content. Some prefer navigation menus, others prefer search, and others prefer site maps.",
                SuggestedFix: "Add at least two of the following: navigation menu, search function, site map, breadcrumbs, or related links.",
                WcagCriterion: WcagCriterion.MultipleWays_2_4_5
            );
        }
    }

    private IEnumerable<Finding> CheckPageNavigation(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check if this page is part of a process (exempt from this requirement)
        var isProcess = node.Properties.TryGetValue("IsProcessStep", out var processStep) &&
                       processStep is bool isStep && isStep;

        if (isProcess) yield break;

        // Check if the page has any way to navigate to other content
        var hasNavigation = HasNavigationControl(node);
        var hasLinks = HasInternalLinks(node);
        var hasSearch = HasSearchControl(node);

        if (!hasNavigation && !hasLinks && !hasSearch)
        {
            yield return new Finding(
                Id: $"{Id}:PAGE:{node.Id}",
                Severity: Severity.Low,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? node.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_NO_NAVIGATION",
                Message: $"Page '{node.Id}' does not appear to have navigation controls to reach other pages.",
                WcagReference: "WCAG 2.2 – 2.4.5 Multiple Ways (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Each page should provide users with ways to navigate to other parts of the application.",
                SuggestedFix: $"Add navigation controls, links to related content, or a search function to '{node.Id}'.",
                WcagCriterion: WcagCriterion.MultipleWays_2_4_5
            );
        }
    }

    private bool HasNavigationMenu(UiNode node)
    {
        return FindControlOfType(node, NavigationTypes);
    }

    private bool HasSearchFunction(UiNode node)
    {
        return FindControlOfType(node, SearchTypes);
    }

    private bool HasSiteMap(UiNode node)
    {
        return FindControlOfType(node, SiteMapTypes);
    }

    private bool HasBreadcrumbs(UiNode node)
    {
        return FindControlByName(node, "breadcrumb") || 
               FindControlOfType(node, new HashSet<string>(["Breadcrumb", "BreadcrumbBar"], StringComparer.OrdinalIgnoreCase));
    }

    private bool HasNavigationControl(UiNode node)
    {
        return FindControlOfType(node, NavigationTypes);
    }

    private bool HasSearchControl(UiNode node)
    {
        return FindControlOfType(node, SearchTypes);
    }

    private bool HasInternalLinks(UiNode node)
    {
        // Check for links that navigate within the app
        return FindControlWithProperty(node, "Navigate") ||
               FindControlWithProperty(node, "Screen") ||
               FindControlOfType(node, new HashSet<string>(["Link", "NavigationLink"], StringComparer.OrdinalIgnoreCase));
    }

    private bool FindControlOfType(UiNode node, HashSet<string> types)
    {
        if (types.Contains(node.Type))
            return true;

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                if (FindControlOfType(child, types))
                    return true;
            }
        }

        return false;
    }

    private bool FindControlOfType(UiNode node, string[] types)
    {
        return FindControlOfType(node, new HashSet<string>(types, StringComparer.OrdinalIgnoreCase));
    }

    private bool FindControlByName(UiNode node, string namePart)
    {
        if (node.Name?.Contains(namePart, StringComparison.OrdinalIgnoreCase) == true ||
            node.Id?.Contains(namePart, StringComparison.OrdinalIgnoreCase) == true)
            return true;

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                if (FindControlByName(child, namePart))
                    return true;
            }
        }

        return false;
    }

    private bool FindControlWithProperty(UiNode node, string propertyName)
    {
        if (node.Properties?.ContainsKey(propertyName) == true)
            return true;

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                if (FindControlWithProperty(child, propertyName))
                    return true;
            }
        }

        return false;
    }

    private int CountPages(UiNode node)
    {
        var count = 0;

        if (PageTypes.Contains(node.Type))
            count++;

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                count += CountPages(child);
            }
        }

        return count;
    }
}
