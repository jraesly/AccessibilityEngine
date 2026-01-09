using System;
using System.Collections.Generic;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for language identification at the app/page level.
/// WCAG 3.1.1: The default human language of each page can be programmatically determined.
/// </summary>
public sealed class LanguageOfPageRule : IRule
{
    public string Id => "LANGUAGE_OF_PAGE";
    public string Description => "Apps and screens should have their language programmatically determinable.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage, SurfaceType.DomSnapshot];

    // Screen-level node type
    private static readonly HashSet<string> ScreenTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Screen", "screen", "Page", "View"
    };

    // Valid ISO 639-1 language codes (subset of common ones)
    private static readonly HashSet<string> ValidLanguageCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "en", "es", "fr", "de", "it", "pt", "nl", "ru", "zh", "ja", "ko", "ar", "hi",
        "en-us", "en-gb", "es-es", "es-mx", "fr-fr", "fr-ca", "de-de", "pt-br", "zh-cn", "zh-tw"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check screen-level language
        if (ScreenTypes.Contains(node.Type))
        {
            var hasLanguage = HasLanguageProperty(node);
            
            if (!hasLanguage)
            {
                yield return new Finding(
                    Id: $"{Id}:SCREEN:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? node.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_MISSING",
                    Message: $"Screen '{node.Id}' does not have a language property set. Screen readers need language information to pronounce content correctly.",
                    WcagReference: "WCAG 2.2 § 3.1.1 Language of Page (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Screen readers use language information to select the correct pronunciation rules. Without it, content may be mispronounced.",
                    SuggestedFix: $"Set the Language property on screen '{node.Id}' to the appropriate ISO language code (e.g., 'en-us' for US English, 'es' for Spanish).",
                    WcagCriterion: WcagCriterion.LanguageOfPage_3_1_1
                );
            }
        }

        // Check for text controls with potentially different language
        if (IsTextControl(node) && context.Depth > 0)
        {
            var hasInlineLanguageChange = HasInlineLanguageChange(node);
            var hasLanguageOverride = HasLanguageProperty(node);
            
            if (hasInlineLanguageChange && !hasLanguageOverride)
            {
                yield return new Finding(
                    Id: $"{Id}:INLINE:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_INLINE_LANGUAGE",
                    Message: $"Control '{node.Id}' may contain text in a different language. Consider marking the language explicitly.",
                    WcagReference: "WCAG 2.2 § 3.1.2 Language of Parts (Level AA)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "When content is in a different language than the page default, it should be identified for proper screen reader pronunciation.",
                    SuggestedFix: $"If the text in '{node.Id}' is in a different language than the screen default, set the Language property on this control to the appropriate language code.",
                    WcagCriterion: WcagCriterion.LanguageOfParts_3_1_2
                );
            }
        }
    }

    private static bool HasLanguageProperty(UiNode node)
    {
        if (node.Properties == null) return false;

        // Check common language property names
        var languageProps = new[] { "Language", "Lang", "Locale", "Culture" };
        
        foreach (var prop in languageProps)
        {
            if (node.Properties.TryGetValue(prop, out var val))
            {
                var langValue = val?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(langValue))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsTextControl(UiNode node)
    {
        var textTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Label", "Text", "Classic/Label", "HtmlText", "RichTextEditor"
        };
        
        return textTypes.Contains(node.Type);
    }

    private static bool HasInlineLanguageChange(UiNode node)
    {
        // Heuristic: Check if text contains common non-English patterns
        // This is a simple heuristic and may have false positives
        var text = node.Text ?? GetTextProperty(node);
        
        if (string.IsNullOrWhiteSpace(text)) return false;

        // Check for common indicators of multilingual content
        // Note: This is a simplified check - real implementation would be more sophisticated
        
        // Check for explicit language markers
        if (text.Contains("lang=", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for common non-ASCII character ranges that might indicate different languages
        foreach (var c in text)
        {
            // Chinese/Japanese/Korean
            if (c >= 0x4E00 && c <= 0x9FFF) return true;
            // Cyrillic
            if (c >= 0x0400 && c <= 0x04FF) return true;
            // Arabic
            if (c >= 0x0600 && c <= 0x06FF) return true;
            // Hebrew
            if (c >= 0x0590 && c <= 0x05FF) return true;
        }

        return false;
    }

    private static string? GetTextProperty(UiNode node)
    {
        if (node.Properties == null) return null;

        var textProps = new[] { "Text", "Value", "Content", "Label" };
        foreach (var prop in textProps)
        {
            if (node.Properties.TryGetValue(prop, out var val))
            {
                return val?.ToString();
            }
        }

        return null;
    }
}
