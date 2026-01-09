using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for proper heading structure and hierarchy.
/// WCAG 1.3.1: Information, structure, and relationships can be programmatically determined.
/// WCAG 2.4.6: Headings and labels describe topic or purpose.
/// </summary>
public sealed class HeadingStructureRule : IRule
{
    public string Id => "HEADING_STRUCTURE";
    public string Description => "Content should use proper heading structure to organize information.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Controls that function as headings
    private static readonly HashSet<string> HeadingTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Label", "Text", "Classic/Label", "Title", "Header"
    };

    // Section container types
    private static readonly HashSet<string> SectionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Container", "Group", "GroupContainer", "Section", "Card", "Tab", "TabItem"
    };

    // Screen types
    private static readonly HashSet<string> ScreenTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Screen", "Page", "View"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check screens for main heading
        if (ScreenTypes.Contains(node.Type))
        {
            var hasMainHeading = HasMainHeading(node);
            
            if (!hasMainHeading)
            {
                yield return new Finding(
                    Id: $"{Id}:SCREEN:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? node.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_NO_MAIN_HEADING",
                    Message: $"Screen '{node.Id}' does not appear to have a main heading. Screen readers use headings to help users understand page structure.",
                    WcagReference: "WCAG 2.2 – 2.4.6 Headings and Labels (Level AA)",
                    Section508Reference: "Section 508 E207.2 - Headings and Labels",
                    Rationale: "Headings help users understand the organization of content and navigate efficiently. Each screen should have a clear main heading.",
                    EntityName: node.Meta?.EntityName ?? context.EntityName,
                    TabName: node.Meta?.TabName,
                    SectionName: node.Meta?.SectionName,
                    SuggestedFix: $"Add a prominent Label at the top of screen '{node.Id}' that describes the screen's purpose. Set the Role property to 'Heading' and Level to 1 for main headings.",
                    WcagCriterion: WcagCriterion.HeadingsAndLabels_2_4_6
                );
            }
        }

        // Check sections for section headings
        if (SectionTypes.Contains(node.Type))
        {
            var hasSectionHeading = HasSectionHeading(node);
            
            if (!hasSectionHeading && HasSignificantContent(node))
            {
                yield return new Finding(
                    Id: $"{Id}:SECTION:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_NO_SECTION_HEADING",
                    Message: $"Section container '{node.Id}' does not have a visible heading. Consider adding a heading to describe its content.",
                    WcagReference: "WCAG 2.2 – 1.3.1 Info and Relationships (Level A)",
                    Section508Reference: "Section 508 E207.2 - Info and Relationships",
                    Rationale: "Section headings help users understand content organization and navigate between sections.",
                    EntityName: node.Meta?.EntityName ?? context.EntityName,
                    TabName: node.Meta?.TabName,
                    SectionName: node.Meta?.SectionName,
                    SuggestedFix: $"Add a Label as the first child of '{node.Id}' that describes the section content. Set Role='Heading' and appropriate Level (2, 3, etc.).",
                    WcagCriterion: WcagCriterion.InfoAndRelationships_1_3_1
                );
            }
        }

        // Check potential headings for proper role
        if (HeadingTypes.Contains(node.Type))
        {
            var appearsToBeHeading = AppearsToBeHeading(node);
            var hasHeadingRole = HasHeadingRole(node);
            
            if (appearsToBeHeading && !hasHeadingRole)
            {
                yield return new Finding(
                    Id: $"{Id}:ROLE:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_MISSING_HEADING_ROLE",
                    Message: $"Label '{node.Id}' appears to be a heading (based on style/position) but is not marked with a heading role for screen readers.",
                    WcagReference: "WCAG 2.2 – 1.3.1 Info and Relationships (Level A)",
                    Section508Reference: "Section 508 E207.2 - Info and Relationships",
                    Rationale: "Visual headings should be programmatically marked as headings so screen reader users can navigate by heading.",
                    EntityName: node.Meta?.EntityName ?? context.EntityName,
                    TabName: node.Meta?.TabName,
                    SectionName: node.Meta?.SectionName,
                    SuggestedFix: $"Set the Role property on '{node.Id}' to 'Heading' and set an appropriate Level (1 for main heading, 2 for sub-sections, etc.).",
                    WcagCriterion: WcagCriterion.InfoAndRelationships_1_3_1
                );
            }

            // Check for vague heading text
            if (hasHeadingRole || appearsToBeHeading)
            {
                var headingText = GetHeadingText(node);
                if (IsVagueHeading(headingText))
                {
                    yield return new Finding(
                        Id: $"{Id}:VAGUE:{node.Id}",
                        Severity: Severity.Low,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_VAGUE_HEADING",
                        Message: $"Heading '{node.Id}' has vague text ('{headingText}'). Headings should clearly describe the section content.",
                        WcagReference: "WCAG 2.2 – 2.4.6 Headings and Labels (Level AA)",
                        Section508Reference: "Section 508 E207.2 - Headings and Labels",
                        Rationale: "Headings should describe the topic or purpose of the content they introduce, not just say 'Section' or 'Details'.",
                        EntityName: node.Meta?.EntityName ?? context.EntityName,
                        TabName: node.Meta?.TabName,
                        SectionName: node.Meta?.SectionName,
                        SuggestedFix: $"Update the text of '{node.Id}' to specifically describe the content that follows (e.g., 'Customer Information' instead of 'Section 1').",
                        WcagCriterion: WcagCriterion.HeadingsAndLabels_2_4_6
                    );
                }
            }
        }
    }

    private static bool HasMainHeading(UiNode screen)
    {
        // Check first few children for a heading
        return screen.Children
            .Take(5)
            .Any(child => HeadingTypes.Contains(child.Type) && 
                         (HasHeadingRole(child) || AppearsToBeHeading(child)));
    }

    private static bool HasSectionHeading(UiNode section)
    {
        // Check first child for a heading
        if (section.Children.Count == 0) return false;
        
        var firstChild = section.Children[0];
        return HeadingTypes.Contains(firstChild.Type) || 
               firstChild.Role?.Contains("heading", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool HasSignificantContent(UiNode section)
    {
        // Consider a section significant if it has multiple children or nested content
        return section.Children.Count > 2 ||
               section.Children.Any(c => c.Children.Count > 0);
    }

    private static bool AppearsToBeHeading(UiNode node)
    {
        // Check font size - larger text is likely a heading
        var fontSize = GetFontSize(node);
        if (fontSize >= 18) return true;

        // Check font weight
        if (node.Properties?.TryGetValue("FontWeight", out var weight) == true)
        {
            var weightStr = weight?.ToString();
            if (weightStr?.Contains("Bold", StringComparison.OrdinalIgnoreCase) == true ||
                weightStr?.Contains("Semibold", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }
        }

        // Check if positioned at top of screen or section (Y coordinate)
        var y = GetNumericProperty(node, "Y");
        if (y.HasValue && y.Value < 100) return true;

        // Check common heading text patterns
        var text = GetHeadingText(node);
        if (text != null)
        {
            // Common heading patterns
            if (text.EndsWith(":") ||
                text.All(c => char.IsUpper(c) || char.IsWhiteSpace(c)) ||
                text.StartsWith("Welcome", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Overview", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasHeadingRole(UiNode node)
    {
        // Check Role property
        if (node.Role?.Contains("heading", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        if (node.Properties?.TryGetValue("Role", out var role) == true)
        {
            return role?.ToString()?.Contains("heading", StringComparison.OrdinalIgnoreCase) == true;
        }

        // Check for heading level
        if (node.Properties?.ContainsKey("Level") == true ||
            node.Properties?.ContainsKey("HeadingLevel") == true)
        {
            return true;
        }

        return false;
    }

    private static string? GetHeadingText(UiNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.Text))
            return node.Text;

        if (node.Properties?.TryGetValue("Text", out var text) == true)
            return text?.ToString();

        return node.Name;
    }

    private static bool IsVagueHeading(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;

        var vaguePatterns = new[]
        {
            "section", "details", "information", "data", "content",
            "header", "title", "heading", "untitled", "new"
        };

        var lowerText = text.ToLowerInvariant().Trim();
        
        // Check for very short text
        if (lowerText.Length < 3) return true;

        // Check for numbered sections without description
        if (System.Text.RegularExpressions.Regex.IsMatch(lowerText, @"^(section|part|step)\s*\d+$"))
            return true;

        // Check for vague single-word headings
        return vaguePatterns.Contains(lowerText);
    }

    private static int GetFontSize(UiNode node)
    {
        if (node.Properties?.TryGetValue("Size", out var size) == true)
        {
            if (size is int i) return i;
            if (size is long l) return (int)l;
            if (size is string s && int.TryParse(s, out var parsed)) return parsed;
        }
        return 13; // Default
    }

    private static double? GetNumericProperty(UiNode node, string propName)
    {
        if (node.Properties?.TryGetValue(propName, out var val) == true)
        {
            if (val is double d) return d;
            if (val is int i) return i;
            if (val is long l) return l;
            if (val is string s && double.TryParse(s, out var parsed)) return parsed;
        }
        return null;
    }
}
