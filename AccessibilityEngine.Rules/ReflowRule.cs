using System;
using System.Collections.Generic;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for content reflow at 320 CSS pixels width.
/// WCAG 1.4.10: Content can be presented without loss of information or functionality,
/// and without requiring scrolling in two dimensions.
/// </summary>
public sealed class ReflowRule : IRule
{
    public string Id => "REFLOW";
    public string Description => "Content must reflow at 320px width without two-dimensional scrolling.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Control types that may have fixed widths
    private static readonly HashSet<string> LayoutControlTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Screen", "Page", "Container", "Gallery", "DataTable", "Table",
        "Form", "Section", "HorizontalContainer", "VerticalContainer"
    };

    // Maximum width before content might not reflow properly
    private const int MaxFixedWidth = 320;

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check for fixed width containers
        foreach (var finding in CheckFixedWidth(node, context))
            yield return finding;

        // Check for horizontal scrolling requirements
        foreach (var finding in CheckHorizontalScroll(node, context))
            yield return finding;

        // Check for multi-column layouts without responsive behavior
        foreach (var finding in CheckMultiColumnLayouts(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckFixedWidth(UiNode node, RuleContext context)
    {
        if (!LayoutControlTypes.Contains(node.Type)) yield break;
        if (node.Properties == null) yield break;

        // Check for fixed minimum width larger than 320px
        if (node.Properties.TryGetValue("MinWidth", out var minWidth))
        {
            var width = ParseDimension(minWidth);
            if (width > MaxFixedWidth)
            {
                yield return new Finding(
                    Id: $"{Id}:MIN_WIDTH:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_MIN_WIDTH",
                    Message: $"Container '{node.Id}' has minimum width of {width}px which exceeds 320px, potentially requiring horizontal scrolling.",
                    WcagReference: "WCAG 2.2 – 1.4.10 Reflow (Level AA)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Content should reflow to a single column at 320px width (400% zoom) without requiring horizontal scrolling.",
                    SuggestedFix: $"Remove or reduce MinWidth on '{node.Id}' to allow content to reflow at narrow widths. Use responsive layout techniques.",
                    WcagCriterion: WcagCriterion.Reflow_1_4_10
                );
            }
        }

        // Check for fixed width without responsive behavior
        if (node.Properties.TryGetValue("Width", out var widthValue))
        {
            var width = ParseDimension(widthValue);
            var hasResponsive = node.Properties.ContainsKey("Responsive") ||
                               node.Properties.ContainsKey("FlexWrap") ||
                               node.Properties.ContainsKey("LayoutMode");

            if (width > MaxFixedWidth && !hasResponsive)
            {
                yield return new Finding(
                    Id: $"{Id}:FIXED_WIDTH:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_FIXED_WIDTH",
                    Message: $"Container '{node.Id}' has fixed width of {width}px without responsive behavior.",
                    WcagReference: "WCAG 2.2 – 1.4.10 Reflow (Level AA)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Fixed-width containers may cause horizontal scrolling at high zoom levels or on narrow viewports.",
                    SuggestedFix: $"Use percentage-based or flexible width for '{node.Id}', or implement responsive breakpoints.",
                    WcagCriterion: WcagCriterion.Reflow_1_4_10
                );
            }
        }
    }

    private IEnumerable<Finding> CheckHorizontalScroll(UiNode node, RuleContext context)
    {
        if (!LayoutControlTypes.Contains(node.Type)) yield break;
        if (node.Properties == null) yield break;

        // Check for forced horizontal scrolling
        if (node.Properties.TryGetValue("OverflowX", out var overflowX) ||
            node.Properties.TryGetValue("HorizontalScroll", out overflowX))
        {
            var overflowValue = overflowX?.ToString()?.ToLowerInvariant() ?? "";
            
            if (overflowValue == "scroll" || overflowValue == "auto" || overflowValue == "true")
            {
                // Check if this is a data table or similar where 2D scrolling may be acceptable
                var isDataGrid = node.Type.Contains("Table", StringComparison.OrdinalIgnoreCase) ||
                                node.Type.Contains("Grid", StringComparison.OrdinalIgnoreCase) ||
                                node.Type.Contains("DataTable", StringComparison.OrdinalIgnoreCase);

                if (!isDataGrid)
                {
                    yield return new Finding(
                        Id: $"{Id}:HORIZONTAL_SCROLL:{node.Id}",
                        Severity: Severity.Medium,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_HORIZONTAL_SCROLL",
                        Message: $"Container '{node.Id}' enables horizontal scrolling which may cause two-dimensional scrolling requirement.",
                        WcagReference: "WCAG 2.2 – 1.4.10 Reflow (Level AA)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Two-dimensional scrolling makes content difficult to read for users who zoom or use screen magnification.",
                        SuggestedFix: $"Redesign '{node.Id}' to use vertical scrolling only. If horizontal scrolling is required for data tables, ensure it's limited to that specific content.",
                        WcagCriterion: WcagCriterion.Reflow_1_4_10
                    );
                }
            }
        }
    }

    private IEnumerable<Finding> CheckMultiColumnLayouts(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check for multi-column layouts without responsive wrapping
        if (node.Properties.TryGetValue("Columns", out var columns) ||
            node.Properties.TryGetValue("ColumnCount", out columns))
        {
            var columnCount = ParseInt(columns);
            
            if (columnCount > 1)
            {
                var hasWrap = node.Properties.ContainsKey("FlexWrap") ||
                             node.Properties.ContainsKey("Wrap") ||
                             node.Properties.ContainsKey("Responsive");

                if (!hasWrap)
                {
                    yield return new Finding(
                        Id: $"{Id}:MULTI_COLUMN:{node.Id}",
                        Severity: Severity.Low,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_MULTI_COLUMN",
                        Message: $"Layout '{node.Id}' uses {columnCount} columns without responsive wrapping configured.",
                        WcagReference: "WCAG 2.2 – 1.4.10 Reflow (Level AA)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Multi-column layouts should collapse to a single column at 320px width to avoid horizontal scrolling.",
                        SuggestedFix: $"Add responsive behavior to '{node.Id}' so columns wrap to a single column at narrow widths.",
                        WcagCriterion: WcagCriterion.Reflow_1_4_10
                    );
                }
            }
        }

        // Check for horizontal flex without wrap
        if (node.Properties.TryGetValue("LayoutDirection", out var direction) ||
            node.Properties.TryGetValue("FlexDirection", out direction))
        {
            var dirValue = direction?.ToString()?.ToLowerInvariant() ?? "";
            
            if (dirValue == "row" || dirValue == "horizontal")
            {
                var hasWrap = false;
                if (node.Properties.TryGetValue("FlexWrap", out var wrap) ||
                    node.Properties.TryGetValue("Wrap", out wrap))
                {
                    hasWrap = wrap?.ToString()?.ToLowerInvariant() == "wrap" ||
                             wrap?.ToString()?.ToLowerInvariant() == "true";
                }

                if (!hasWrap && node.Children?.Count > 2)
                {
                    yield return new Finding(
                        Id: $"{Id}:HORIZONTAL_FLEX:{node.Id}",
                        Severity: Severity.Low,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_HORIZONTAL_FLEX",
                        Message: $"Horizontal layout '{node.Id}' with {node.Children.Count} children does not have wrapping enabled.",
                        WcagReference: "WCAG 2.2 – 1.4.10 Reflow (Level AA)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Horizontal layouts without wrapping may overflow at narrow widths.",
                        SuggestedFix: $"Enable flex-wrap on '{node.Id}' or switch to vertical layout at narrow widths.",
                        WcagCriterion: WcagCriterion.Reflow_1_4_10
                    );
                }
            }
        }
    }

    private static int ParseDimension(object? value)
    {
        if (value == null) return 0;
        
        var str = value.ToString() ?? "";
        str = str.Replace("px", "").Replace("pt", "").Trim();
        
        return int.TryParse(str, out var result) ? result : 0;
    }

    private static int ParseInt(object? value)
    {
        if (value == null) return 0;
        if (value is int i) return i;
        
        return int.TryParse(value.ToString(), out var result) ? result : 0;
    }
}
