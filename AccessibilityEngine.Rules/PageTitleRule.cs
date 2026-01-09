using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for descriptive page titles.
/// WCAG 2.4.2: Pages have titles that describe topic or purpose.
/// </summary>
public sealed class PageTitleRule : IRule
{
    public string Id => "PAGE_TITLE";
    public string Description => "Pages must have titles that describe their topic or purpose.";
    public Severity Severity => Severity.High;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Screen/page level types
    private static readonly HashSet<string> PageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Screen", "Page", "View", "Form", "Dialog", "Modal", "App"
    };

    // Generic/non-descriptive title patterns
    private static readonly HashSet<string> GenericTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        "untitled", "page", "screen", "new page", "new screen", "home",
        "page 1", "page 2", "screen 1", "screen 2", "form", "view",
        "default", "main", "index", "app", "application"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Only check page-level nodes
        if (!PageTypes.Contains(node.Type)) yield break;

        // Check for page title
        foreach (var finding in CheckPageTitle(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckPageTitle(UiNode node, RuleContext context)
    {
        var title = GetPageTitle(node);

        // Check if title is missing
        if (string.IsNullOrWhiteSpace(title))
        {
            yield return new Finding(
                Id: $"{Id}:MISSING:{node.Id}",
                Severity: Severity.High,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_MISSING",
                Message: $"Page '{node.Id}' does not have a title defined.",
                WcagReference: "WCAG 2.2 – 2.4.2 Page Titled (Level A)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Page titles help users understand where they are, especially when using screen readers or browser tabs. Users rely on titles to navigate between pages.",
                SuggestedFix: $"Add a descriptive title to '{node.Id}' that describes the page's purpose or content (e.g., 'Contact Form - Company Name').",
                WcagCriterion: WcagCriterion.PageTitled_2_4_2
            );
            yield break;
        }

        // Check if title is too generic
        if (IsGenericTitle(title))
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
                Message: $"Page '{node.Id}' has a generic title '{title}' that does not describe its content or purpose.",
                WcagReference: "WCAG 2.2 – 2.4.2 Page Titled (Level A)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Generic titles like 'Page' or 'Untitled' don't help users understand the page content or distinguish between multiple open pages.",
                SuggestedFix: $"Replace '{title}' with a descriptive title that indicates the page's specific purpose (e.g., 'Employee Directory', 'Submit Expense Report').",
                WcagCriterion: WcagCriterion.PageTitled_2_4_2
            );
        }

        // Check if title is too short
        if (title.Length < 3)
        {
            yield return new Finding(
                Id: $"{Id}:SHORT:{node.Id}",
                Severity: Severity.Low,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_SHORT",
                Message: $"Page '{node.Id}' has a very short title '{title}' that may not adequately describe its content.",
                WcagReference: "WCAG 2.2 – 2.4.2 Page Titled (Level A)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Very short titles may not provide enough context for users to understand the page's purpose.",
                SuggestedFix: $"Expand the title '{title}' to be more descriptive of the page content.",
                WcagCriterion: WcagCriterion.PageTitled_2_4_2
            );
        }

        // Check if title is same as app name only (no page-specific info)
        if (context.AppName != null && 
            title.Equals(context.AppName, StringComparison.OrdinalIgnoreCase))
        {
            yield return new Finding(
                Id: $"{Id}:APP_NAME_ONLY:{node.Id}",
                Severity: Severity.Low,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_APP_NAME_ONLY",
                Message: $"Page '{node.Id}' title is just the app name '{title}' without page-specific information.",
                WcagReference: "WCAG 2.2 – 2.4.2 Page Titled (Level A)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "When multiple pages have the same title (just the app name), users cannot distinguish between them in browser history or tabs.",
                SuggestedFix: $"Add page-specific information to the title, e.g., 'Dashboard - {context.AppName}' or 'Settings - {context.AppName}'.",
                WcagCriterion: WcagCriterion.PageTitled_2_4_2
            );
        }

        // Check for dialogs/modals that should update the page title
        if (node.Type.Equals("Dialog", StringComparison.OrdinalIgnoreCase) ||
            node.Type.Equals("Modal", StringComparison.OrdinalIgnoreCase))
        {
            var dialogTitle = GetDialogTitle(node);
            
            if (string.IsNullOrWhiteSpace(dialogTitle))
            {
                yield return new Finding(
                    Id: $"{Id}:DIALOG:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_DIALOG_TITLE",
                    Message: $"Dialog '{node.Id}' does not have a title that will be announced to screen reader users.",
                    WcagReference: "WCAG 2.2 – 2.4.2 Page Titled (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Dialogs and modals represent a change in context. Screen reader users need to know what the dialog is for.",
                    SuggestedFix: $"Add an aria-label or visible title to dialog '{node.Id}' that describes its purpose.",
                    WcagCriterion: WcagCriterion.PageTitled_2_4_2
                );
            }
        }
    }

    private static string? GetPageTitle(UiNode node)
    {
        // Check direct title property
        if (node.Properties != null)
        {
            var titleProps = new[] { "Title", "PageTitle", "ScreenTitle", "Name", "DisplayName", "Label" };
            
            foreach (var prop in titleProps)
            {
                if (node.Properties.TryGetValue(prop, out var value))
                {
                    var title = value?.ToString();
                    if (!string.IsNullOrWhiteSpace(title))
                        return title;
                }
            }

            // Check aria-label
            if (node.Properties.TryGetValue("aria-label", out var ariaLabel) ||
                node.Properties.TryGetValue("AriaLabel", out ariaLabel))
            {
                var label = ariaLabel?.ToString();
                if (!string.IsNullOrWhiteSpace(label))
                    return label;
            }
        }

        // Check node name
        if (!string.IsNullOrWhiteSpace(node.Name))
            return node.Name;

        // Check meta information
        if (node.Meta?.ScreenName != null)
            return node.Meta.ScreenName;

        return null;
    }

    private static string? GetDialogTitle(UiNode node)
    {
        if (node.Properties != null)
        {
            var titleProps = new[] { "Title", "aria-label", "AriaLabel", "aria-labelledby", "Header" };
            
            foreach (var prop in titleProps)
            {
                if (node.Properties.TryGetValue(prop, out var value))
                {
                    var title = value?.ToString();
                    if (!string.IsNullOrWhiteSpace(title))
                        return title;
                }
            }
        }

        // Check for a title child element
        if (node.Children != null)
        {
            var titleChild = node.Children.FirstOrDefault(c => 
                c.Type.Equals("Title", StringComparison.OrdinalIgnoreCase) ||
                c.Type.Equals("Header", StringComparison.OrdinalIgnoreCase) ||
                c.Type.Equals("DialogTitle", StringComparison.OrdinalIgnoreCase));

            if (titleChild != null)
            {
                return titleChild.Text ?? titleChild.Name;
            }
        }

        return null;
    }

    private static bool IsGenericTitle(string title)
    {
        var normalizedTitle = title.Trim().ToLowerInvariant();
        
        // Direct match with generic titles
        if (GenericTitles.Contains(normalizedTitle))
            return true;

        // Check for numbered generic patterns like "Screen_1", "Page-2"
        var numberedPattern = normalizedTitle
            .Replace("_", " ")
            .Replace("-", " ")
            .Trim();
        
        foreach (var generic in GenericTitles)
        {
            if (numberedPattern.StartsWith(generic + " ") ||
                numberedPattern.EndsWith(" " + generic))
            {
                // Check if the rest is just numbers
                var remainder = numberedPattern
                    .Replace(generic, "")
                    .Trim();
                
                if (int.TryParse(remainder, out _))
                    return true;
            }
        }

        return false;
    }
}
