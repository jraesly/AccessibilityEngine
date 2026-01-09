using System;
using System.Collections.Generic;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for text spacing override support.
/// WCAG 1.4.12: No loss of content or functionality occurs when users override text spacing properties.
/// </summary>
public sealed class TextSpacingRule : IRule
{
    public string Id => "TEXT_SPACING";
    public string Description => "Content must support text spacing adjustments without loss of content or functionality.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Control types that contain text
    private static readonly HashSet<string> TextControlTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Label", "Text", "TextBlock", "TextInput", "TextBox", "RichText", "HtmlText",
        "Button", "Link", "Title", "Header", "Paragraph"
    };

    // Container types that may clip text
    private static readonly HashSet<string> ContainerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Container", "Section", "Card", "Group", "Panel", "Box"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check for fixed line heights that prevent spacing adjustments
        foreach (var finding in CheckFixedLineHeight(node, context))
            yield return finding;

        // Check for containers that may clip text when spacing increases
        foreach (var finding in CheckContainerClipping(node, context))
            yield return finding;

        // Check for fixed letter/word spacing
        foreach (var finding in CheckFixedSpacing(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckFixedLineHeight(UiNode node, RuleContext context)
    {
        if (!TextControlTypes.Contains(node.Type)) yield break;
        if (node.Properties == null) yield break;

        // Check for fixed line height that's too restrictive
        if (node.Properties.TryGetValue("LineHeight", out var lineHeight))
        {
            var lineHeightStr = lineHeight?.ToString()?.ToLowerInvariant() ?? "";
            
            // Fixed pixel line heights may not accommodate 1.5x spacing requirement
            if (lineHeightStr.EndsWith("px"))
            {
                var lineHeightValue = ParseDimension(lineHeight);
                
                // Also check font size to calculate ratio
                if (node.Properties.TryGetValue("FontSize", out var fontSize))
                {
                    var fontSizeValue = ParseDimension(fontSize);
                    
                    if (fontSizeValue > 0 && lineHeightValue > 0)
                    {
                        var ratio = (double)lineHeightValue / fontSizeValue;
                        
                        // WCAG requires support for 1.5x line height
                        if (ratio < 1.5)
                        {
                            yield return new Finding(
                                Id: $"{Id}:LINE_HEIGHT:{node.Id}",
                                Severity: Severity.Medium,
                                Surface: context.Surface,
                                AppName: context.AppName,
                                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                                ControlId: node.Id,
                                ControlType: node.Type,
                                IssueType: $"{Id}_LINE_HEIGHT",
                                Message: $"Text control '{node.Id}' has fixed line height ({lineHeightValue}px) that may not support 1.5x spacing requirement.",
                                WcagReference: "WCAG 2.2 – 1.4.12 Text Spacing (Level AA)",
                                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                                Rationale: "Users with dyslexia or low vision need to increase line spacing to at least 1.5 times the font size.",
                                SuggestedFix: $"Use relative line height (e.g., 1.5 or 150%) for '{node.Id}' instead of fixed pixels, or ensure the container can accommodate increased spacing.",
                                WcagCriterion: WcagCriterion.TextSpacing_1_4_12
                            );
                        }
                    }
                }
            }
        }
    }

    private IEnumerable<Finding> CheckContainerClipping(UiNode node, RuleContext context)
    {
        if (!ContainerTypes.Contains(node.Type)) yield break;
        if (node.Properties == null) yield break;

        // Check for containers with fixed height and overflow hidden
        var hasFixedHeight = node.Properties.ContainsKey("Height") && 
                            !node.Properties.ContainsKey("MinHeight");
        
        var hasOverflowHidden = false;
        if (node.Properties.TryGetValue("Overflow", out var overflow))
        {
            var overflowValue = overflow?.ToString()?.ToLowerInvariant() ?? "";
            hasOverflowHidden = overflowValue == "hidden" || overflowValue == "clip";
        }

        // Check if container has text children (simplified check)
        var hasTextContent = node.Children?.Count > 0;

        if (hasFixedHeight && hasOverflowHidden && hasTextContent)
        {
            yield return new Finding(
                Id: $"{Id}:CONTAINER:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_CONTAINER_CLIP",
                Message: $"Container '{node.Id}' has fixed height with hidden overflow, which may clip text when spacing is increased.",
                WcagReference: "WCAG 2.2 – 1.4.12 Text Spacing (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "When users increase text spacing, content should not be clipped or overlap. Containers must accommodate expanded text.",
                SuggestedFix: $"Change '{node.Id}' to use min-height instead of fixed height, or change overflow to 'auto' or 'visible'.",
                WcagCriterion: WcagCriterion.TextSpacing_1_4_12
            );
        }
    }

    private IEnumerable<Finding> CheckFixedSpacing(UiNode node, RuleContext context)
    {
        if (!TextControlTypes.Contains(node.Type)) yield break;
        if (node.Properties == null) yield break;

        // Check for letter spacing that's too tight
        if (node.Properties.TryGetValue("LetterSpacing", out var letterSpacing))
        {
            var spacingValue = ParseDimension(letterSpacing);
            
            // Negative letter spacing may cause issues when users try to increase it
            if (spacingValue < 0)
            {
                yield return new Finding(
                    Id: $"{Id}:LETTER_SPACING:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_LETTER_SPACING",
                    Message: $"Text control '{node.Id}' uses negative letter spacing which may interfere with user spacing adjustments.",
                    WcagReference: "WCAG 2.2 – 1.4.12 Text Spacing (Level AA)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Users need to be able to increase letter spacing to 0.12 times the font size. Negative spacing may cause issues.",
                    SuggestedFix: $"Remove negative letter spacing from '{node.Id}' or ensure the layout accommodates user-adjusted spacing.",
                    WcagCriterion: WcagCriterion.TextSpacing_1_4_12
                );
            }
        }

        // Check for paragraph spacing restrictions
        if (node.Properties.TryGetValue("ParagraphSpacing", out var paragraphSpacing) ||
            node.Properties.TryGetValue("MarginBottom", out paragraphSpacing))
        {
            var spacingStr = paragraphSpacing?.ToString()?.ToLowerInvariant() ?? "";
            
            // Check if spacing is set to 0 or very small
            if (spacingStr == "0" || spacingStr == "0px")
            {
                yield return new Finding(
                    Id: $"{Id}:PARAGRAPH_SPACING:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_PARAGRAPH_SPACING",
                    Message: $"Text control '{node.Id}' has no paragraph spacing, which may make it difficult for users to distinguish paragraphs.",
                    WcagReference: "WCAG 2.2 – 1.4.12 Text Spacing (Level AA)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Users need to be able to increase paragraph spacing to 2 times the font size for readability.",
                    SuggestedFix: $"Add appropriate paragraph spacing to '{node.Id}' and ensure the layout can accommodate increased spacing.",
                    WcagCriterion: WcagCriterion.TextSpacing_1_4_12
                );
            }
        }
    }

    private static int ParseDimension(object? value)
    {
        if (value == null) return 0;
        
        var str = value.ToString() ?? "";
        str = str.Replace("px", "").Replace("pt", "").Replace("em", "").Trim();
        
        if (double.TryParse(str, out var result))
            return (int)result;
        
        return 0;
    }
}
