using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for images of text instead of actual text.
/// WCAG 1.4.5: If the technologies being used can achieve the visual presentation,
/// text is used to convey information rather than images of text.
/// </summary>
public sealed class ImagesOfTextRule : IRule
{
    public string Id => "IMAGES_OF_TEXT";
    public string Description => "Text should be used instead of images of text, except for logos and essential customization.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Image control types
    private static readonly HashSet<string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Image", "Picture", "Img", "MediaImage", "AddPicture"
    };

    // Patterns that suggest an image contains text
    private static readonly Regex TextImagePatterns = new(
        @"\b(banner|header|title|logo_?text|text_?image|button_?image|heading|label_?image|caption)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // File naming patterns suggesting text content
    private static readonly Regex TextFileNamePatterns = new(
        @"(text|banner|header|title|heading|button|label|caption|quote|slogan|tagline).*\.(png|jpg|jpeg|gif|svg|webp)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Only check image controls
        if (!ImageTypes.Contains(node.Type)) yield break;

        // Check for indicators that the image contains text
        foreach (var finding in CheckImageForText(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckImageForText(UiNode node, RuleContext context)
    {
        var imageSource = GetImageSource(node);
        var altText = GetAltText(node);
        var imageName = node.Name ?? node.Id ?? "";

        // Check if image name suggests it contains text
        if (TextImagePatterns.IsMatch(imageName))
        {
            // Exception for logos
            if (!IsLikelyLogo(imageName, imageSource, altText))
            {
                yield return CreateFinding(node, context, "NAME",
                    $"Image '{node.Id}' has a name suggesting it contains text. Consider using actual text instead.");
                yield break;
            }
        }

        // Check if file name suggests text content
        if (!string.IsNullOrWhiteSpace(imageSource) && TextFileNamePatterns.IsMatch(imageSource))
        {
            if (!IsLikelyLogo(imageName, imageSource, altText))
            {
                yield return CreateFinding(node, context, "FILENAME",
                    $"Image '{node.Id}' has a filename suggesting it contains text content.");
                yield break;
            }
        }

        // Check if alt text is substantial (suggesting the image conveys textual information)
        if (!string.IsNullOrWhiteSpace(altText))
        {
            var wordCount = altText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            
            // If alt text is very long, it might be describing text in the image
            if (wordCount > 10)
            {
                if (!IsLikelyLogo(imageName, imageSource, altText))
                {
                    yield return CreateFinding(node, context, "ALT_TEXT",
                        $"Image '{node.Id}' has lengthy alt text ({wordCount} words), suggesting it may contain text that should be rendered as actual text.");
                }
            }
        }

        // Check for explicit text content properties
        if (node.Properties != null)
        {
            if (node.Properties.TryGetValue("ContainsText", out var containsText) &&
                containsText is bool hasText && hasText)
            {
                yield return CreateFinding(node, context, "CONTAINS_TEXT",
                    $"Image '{node.Id}' is marked as containing text. Use actual text for better accessibility.");
            }

            // Check for image role that suggests text content
            if (node.Properties.TryGetValue("ImageType", out var imageType))
            {
                var typeStr = imageType?.ToString()?.ToLowerInvariant() ?? "";
                if (typeStr.Contains("text") || typeStr.Contains("banner") || typeStr.Contains("header"))
                {
                    yield return CreateFinding(node, context, "IMAGE_TYPE",
                        $"Image '{node.Id}' has an image type suggesting text content. Consider using actual text.");
                }
            }
        }
    }

    private static string? GetImageSource(UiNode node)
    {
        if (node.Properties == null) return null;

        var sourceProps = new[] { "Image", "Source", "Src", "ImageSource", "Url" };
        
        foreach (var prop in sourceProps)
        {
            if (node.Properties.TryGetValue(prop, out var value))
            {
                return value?.ToString();
            }
        }

        return null;
    }

    private static string? GetAltText(UiNode node)
    {
        var alt = node.Text ?? "";
        
        if (string.IsNullOrWhiteSpace(alt) && node.Properties != null)
        {
            var altProps = new[] { "Alt", "AltText", "AccessibleLabel", "Description", "Tooltip" };
            
            foreach (var prop in altProps)
            {
                if (node.Properties.TryGetValue(prop, out var value))
                {
                    var propValue = value?.ToString();
                    if (!string.IsNullOrWhiteSpace(propValue))
                        return propValue;
                }
            }
        }

        return alt;
    }

    private static bool IsLikelyLogo(string name, string? source, string? altText)
    {
        var combined = $"{name} {source} {altText}".ToLowerInvariant();
        
        return combined.Contains("logo") || 
               combined.Contains("brand") ||
               combined.Contains("trademark") ||
               combined.Contains("wordmark") ||
               combined.Contains("company") ||
               combined.Contains("icon");
    }

    private Finding CreateFinding(UiNode node, RuleContext context, string subType, string message)
    {
        return new Finding(
            Id: $"{Id}:{subType}:{node.Id}",
            Severity: Severity.Medium,
            Surface: context.Surface,
            AppName: context.AppName,
            Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
            ControlId: node.Id,
            ControlType: node.Type,
            IssueType: $"{Id}_{subType}",
            Message: message,
            WcagReference: "WCAG 2.2 – 1.4.5 Images of Text (Level AA)",
            Section508Reference: "Section 508 E207.2 - WCAG Conformance",
            Rationale: "Images of text cannot be resized, customized, or read reliably by assistive technologies. Real text allows users to adjust font, spacing, and colors.",
            SuggestedFix: $"Replace the image in '{node.Id}' with actual styled text. If the image is a logo or the specific presentation is essential, ensure comprehensive alt text is provided.",
            WcagCriterion: WcagCriterion.ImagesOfText_1_4_5
        );
    }
}
