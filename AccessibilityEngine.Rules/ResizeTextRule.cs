using System;
using System.Collections.Generic;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for text resize support.
/// WCAG 1.4.4: Text can be resized without assistive technology up to 200% without loss of content or functionality.
/// </summary>
public sealed class ResizeTextRule : IRule
{
    public string Id => "RESIZE_TEXT";
    public string Description => "Text must be resizable up to 200% without loss of content or functionality.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Control types that contain text
    private static readonly HashSet<string> TextControlTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Label", "Text", "TextBlock", "TextInput", "TextBox", "RichText", "HtmlText",
        "Button", "Link", "Title", "Header", "Paragraph"
    };

    // Screen/container types
    private static readonly HashSet<string> ContainerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Screen", "Page", "Container", "Section", "Form", "App"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check text controls for fixed pixel sizes
        foreach (var finding in CheckFixedFontSize(node, context))
            yield return finding;

        // Check containers for viewport-based sizing that may clip text
        foreach (var finding in CheckContainerOverflow(node, context))
            yield return finding;

        // Check for text truncation settings
        foreach (var finding in CheckTextTruncation(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckFixedFontSize(UiNode node, RuleContext context)
    {
        if (!TextControlTypes.Contains(node.Type)) yield break;
        if (node.Properties == null) yield break;

        // Check for fixed pixel font sizes that may not scale
        if (node.Properties.TryGetValue("FontSize", out var fontSize))
        {
            var fontSizeStr = fontSize?.ToString() ?? "";
            
            // Check if using absolute pixels without responsive scaling
            if (fontSizeStr.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            {
                // Also check if there's responsive scaling enabled
                var hasResponsiveScaling = 
                    node.Properties.ContainsKey("ScaleToFit") ||
                    node.Properties.ContainsKey("ResponsiveText") ||
                    node.Properties.ContainsKey("AutoScale");

                if (!hasResponsiveScaling)
                {
                    yield return new Finding(
                        Id: $"{Id}:FIXED_FONT:{node.Id}",
                        Severity: Severity.Low,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_FIXED_FONT",
                        Message: $"Text control '{node.Id}' uses fixed pixel font size ({fontSizeStr}) which may not scale with user preferences.",
                        WcagReference: "WCAG 2.2 – 1.4.4 Resize Text (Level AA)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Users with low vision need to resize text up to 200%. Fixed pixel sizes may prevent proper scaling.",
                        SuggestedFix: $"Use relative units (em, rem, %) for font size in '{node.Id}', or ensure the app respects user text size preferences.",
                        WcagCriterion: WcagCriterion.ResizeText_1_4_4
                    );
                }
            }
        }
    }

    private IEnumerable<Finding> CheckContainerOverflow(UiNode node, RuleContext context)
    {
        if (!ContainerTypes.Contains(node.Type)) yield break;
        if (node.Properties == null) yield break;

        // Check for overflow: hidden which may clip enlarged text
        if (node.Properties.TryGetValue("Overflow", out var overflow))
        {
            var overflowValue = overflow?.ToString()?.ToLowerInvariant() ?? "";
            
            if (overflowValue == "hidden" || overflowValue == "clip")
            {
                // Check if the container has fixed dimensions
                var hasFixedDimensions = 
                    node.Properties.ContainsKey("Height") || 
                    node.Properties.ContainsKey("MaxHeight");

                if (hasFixedDimensions)
                {
                    yield return new Finding(
                        Id: $"{Id}:OVERFLOW:{node.Id}",
                        Severity: Severity.Medium,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_OVERFLOW_HIDDEN",
                        Message: $"Container '{node.Id}' has overflow hidden with fixed dimensions, which may clip enlarged text.",
                        WcagReference: "WCAG 2.2 – 1.4.4 Resize Text (Level AA)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "When text is enlarged, containers with hidden overflow and fixed heights can clip content, making it inaccessible.",
                        SuggestedFix: $"Change overflow to 'auto' or 'scroll' for '{node.Id}', or use min-height instead of fixed height to allow content expansion.",
                        WcagCriterion: WcagCriterion.ResizeText_1_4_4
                    );
                }
            }
        }
    }

    private IEnumerable<Finding> CheckTextTruncation(UiNode node, RuleContext context)
    {
        if (!TextControlTypes.Contains(node.Type)) yield break;
        if (node.Properties == null) yield break;

        // Check for text truncation that loses content
        var truncationProps = new[] { "TextTruncation", "Truncate", "Ellipsis", "TextOverflow" };
        
        foreach (var prop in truncationProps)
        {
            if (node.Properties.TryGetValue(prop, out var value))
            {
                var truncValue = value?.ToString()?.ToLowerInvariant() ?? "";
                
                if (truncValue == "true" || truncValue == "ellipsis" || truncValue == "clip")
                {
                    // Check if there's a tooltip or other way to access full content
                    var hasTooltip = node.Properties.ContainsKey("Tooltip") || 
                                     node.Properties.ContainsKey("Title");

                    if (!hasTooltip)
                    {
                        yield return new Finding(
                            Id: $"{Id}:TRUNCATION:{node.Id}",
                            Severity: Severity.Medium,
                            Surface: context.Surface,
                            AppName: context.AppName,
                            Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                            ControlId: node.Id,
                            ControlType: node.Type,
                            IssueType: $"{Id}_TRUNCATION",
                            Message: $"Text control '{node.Id}' truncates content without providing alternative access to full text.",
                            WcagReference: "WCAG 2.2 – 1.4.4 Resize Text (Level AA)",
                            Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                            Rationale: "Truncated text can cause loss of information, especially when users cannot resize text to see full content.",
                            SuggestedFix: $"Add a tooltip or expand mechanism to '{node.Id}' so users can access the full text content.",
                            WcagCriterion: WcagCriterion.ResizeText_1_4_4
                        );
                    }
                }
            }
        }
    }
}
