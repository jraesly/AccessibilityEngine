using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for instructions relying solely on sensory characteristics.
/// WCAG 1.3.3: Instructions provided for understanding and operating content must not rely solely
/// on sensory characteristics of components such as shape, color, size, visual location, orientation, or sound.
/// </summary>
public sealed class SensoryCharacteristicsRule : IRule
{
    public string Id => "SENSORY_CHARACTERISTICS";
    public string Description => "Instructions must not rely solely on sensory characteristics like shape, color, size, or location.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Patterns that indicate reliance on shape/visual characteristics
    private static readonly Regex ShapePatterns = new(
        @"\b(round|square|circular|triangle|triangular|rectangular|oval|diamond|star[\s-]?shaped)\b.*\b(button|icon|control|link|item)\b|\b(button|icon|control|link|item)\b.*\b(round|square|circular|triangle|triangular|rectangular|oval|diamond|star[\s-]?shaped)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Patterns that indicate reliance on color alone
    private static readonly Regex ColorPatterns = new(
        @"\b(click|press|select|tap|choose|hit)\s+(the\s+)?(red|blue|green|yellow|orange|purple|pink|black|white|gray|grey|colored?)\s+(button|icon|link|control|item)\b|\b(red|blue|green|yellow|orange|purple|pink|black|white|gray|grey)\s+(button|icon|link|control|item)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Patterns that indicate reliance on visual location alone
    private static readonly Regex LocationPatterns = new(
        @"\b(click|press|select|tap|choose)\s+(the\s+)?(button|icon|link|control|item)\s+(on|at|in)\s+the\s+(left|right|top|bottom|corner|side|above|below)\b|\b(left|right|top|bottom|upper|lower)\s+(button|icon|link|control|item)\b|\b(button|icon|link|control|item)\s+(on|at|in)\s+the\s+(left|right|top|bottom|corner)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Patterns that indicate reliance on size alone
    private static readonly Regex SizePatterns = new(
        @"\b(click|press|select|tap|choose)\s+(the\s+)?(large|larger|big|bigger|small|smaller|tiny|huge)\s+(button|icon|link|control|item)\b|\b(large|larger|big|bigger|small|smaller|tiny|huge)\s+(button|icon|link|control|item)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Patterns indicating reliance on sound cues alone
    private static readonly Regex SoundPatterns = new(
        @"\b(when\s+you\s+hear|after\s+the\s+(beep|sound|chime|tone)|listen\s+for)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Control types that typically contain instructional text
    private static readonly HashSet<string> InstructionalControlTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Label", "Text", "HtmlText", "RichText", "TextBlock", "InfoText",
        "HelpText", "Tooltip", "Description", "Instructions", "Message"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check instructional text content
        foreach (var finding in CheckInstructionalText(node, context))
            yield return finding;

        // Check tooltip and help text properties
        foreach (var finding in CheckTooltipsAndHelp(node, context))
            yield return finding;

        // Check for visual-only indicators
        foreach (var finding in CheckVisualOnlyIndicators(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckInstructionalText(UiNode node, RuleContext context)
    {
        // Check controls that typically contain instructional text
        if (!InstructionalControlTypes.Contains(node.Type)) yield break;

        var textContent = node.Text ?? node.Name ?? "";
        
        // Also check common text properties
        if (string.IsNullOrWhiteSpace(textContent) && node.Properties != null)
        {
            if (node.Properties.TryGetValue("Text", out var text))
                textContent = text?.ToString() ?? "";
            else if (node.Properties.TryGetValue("Content", out var content))
                textContent = content?.ToString() ?? "";
            else if (node.Properties.TryGetValue("Value", out var value))
                textContent = value?.ToString() ?? "";
        }

        if (string.IsNullOrWhiteSpace(textContent)) yield break;

        // Check for shape-based instructions
        if (ShapePatterns.IsMatch(textContent))
        {
            yield return CreateFinding(
                node, context,
                "SHAPE",
                "Instructions reference shape characteristics (e.g., 'round button', 'square icon') without additional identifiers.",
                $"The text '{TruncateText(textContent)}' relies on shape to identify elements. Users who cannot perceive shapes won't understand the instruction.",
                "Add text labels, names, or other non-visual identifiers. For example, instead of 'Click the round button', use 'Click the Submit button (round icon)'.");
        }

        // Check for color-based instructions
        if (ColorPatterns.IsMatch(textContent))
        {
            yield return CreateFinding(
                node, context,
                "COLOR",
                "Instructions reference color alone (e.g., 'click the red button') without additional identifiers.",
                $"The text '{TruncateText(textContent)}' relies on color to identify elements. Color-blind users or those using screen readers won't understand the instruction.",
                "Add text labels alongside color references. For example, instead of 'Click the red button', use 'Click the Delete button (red)'.");
        }

        // Check for location-based instructions
        if (LocationPatterns.IsMatch(textContent))
        {
            yield return CreateFinding(
                node, context,
                "LOCATION",
                "Instructions reference visual location alone (e.g., 'button on the left') without additional identifiers.",
                $"The text '{TruncateText(textContent)}' relies on visual location to identify elements. Screen reader users cannot perceive spatial position.",
                "Add text labels alongside location references. For example, instead of 'Click the button on the left', use 'Click the Previous button (on the left)'.");
        }

        // Check for size-based instructions
        if (SizePatterns.IsMatch(textContent))
        {
            yield return CreateFinding(
                node, context,
                "SIZE",
                "Instructions reference size alone (e.g., 'click the large button') without additional identifiers.",
                $"The text '{TruncateText(textContent)}' relies on size to identify elements. Users who cannot perceive relative sizes won't understand the instruction.",
                "Add text labels alongside size references. For example, instead of 'Click the large button', use 'Click the Main Menu button (large)'.");
        }

        // Check for sound-based instructions
        if (SoundPatterns.IsMatch(textContent))
        {
            yield return CreateFinding(
                node, context,
                "SOUND",
                "Instructions rely on sound cues alone without visual alternatives.",
                $"The text '{TruncateText(textContent)}' relies on sound cues. Deaf users or those in silent environments won't receive the instruction.",
                "Provide visual indicators alongside audio cues. For example, 'When you hear the confirmation sound (or see the green checkmark), proceed to the next step'.");
        }
    }

    private IEnumerable<Finding> CheckTooltipsAndHelp(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        var helpProperties = new[] { "Tooltip", "HelpText", "Description", "AccessibleDescription", "Hint" };

        foreach (var prop in helpProperties)
        {
            if (!node.Properties.TryGetValue(prop, out var value)) continue;
            
            var text = value?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(text)) continue;

            if (ShapePatterns.IsMatch(text) || ColorPatterns.IsMatch(text) || 
                LocationPatterns.IsMatch(text) || SizePatterns.IsMatch(text) ||
                SoundPatterns.IsMatch(text))
            {
                yield return new Finding(
                    Id: $"{Id}:HELP:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_HELP_TEXT",
                    Message: $"Help text or tooltip for '{node.Id}' relies on sensory characteristics to convey information.",
                    WcagReference: "WCAG 2.2 – 1.3.3 Sensory Characteristics (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Help text that relies on shape, color, location, size, or sound excludes users who cannot perceive these characteristics.",
                    SuggestedFix: $"Update the {prop} property of '{node.Id}' to include text-based identifiers alongside any sensory references.",
                    WcagCriterion: WcagCriterion.SensoryCharacteristics_1_3_3
                );
            }
        }
    }

    private IEnumerable<Finding> CheckVisualOnlyIndicators(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check for visual-only required field indicators (asterisks, colors)
        if (node.Properties.TryGetValue("Required", out var required) && 
            required is bool isRequired && isRequired)
        {
            // Check if there's only a visual indicator (like color change or asterisk)
            var hasAccessibleIndicator = 
                node.Properties.ContainsKey("AccessibleLabel") ||
                node.Properties.ContainsKey("AriaRequired") ||
                node.Properties.ContainsKey("aria-required");

            var hasVisualOnlyIndicator = 
                node.Properties.TryGetValue("RequiredIndicator", out var indicator) &&
                indicator?.ToString()?.Contains("*") == true;

            if (hasVisualOnlyIndicator && !hasAccessibleIndicator)
            {
                yield return new Finding(
                    Id: $"{Id}:REQUIRED:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_VISUAL_INDICATOR",
                    Message: $"Required field '{node.Id}' uses only visual indicator (asterisk/color). The required state should be programmatically exposed.",
                    WcagReference: "WCAG 2.2 – 1.3.3 Sensory Characteristics (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Visual-only indicators for required fields are not perceivable by screen reader users.",
                    SuggestedFix: $"Set aria-required='true' or include 'required' in the accessible label for '{node.Id}'.",
                    WcagCriterion: WcagCriterion.SensoryCharacteristics_1_3_3
                );
            }
        }

        // Check for icon-only status indicators without accessible alternatives
        var iconTypes = new[] { "Icon", "Glyph", "Symbol", "StatusIcon", "IndicatorIcon" };
        if (iconTypes.Any(t => node.Type.Contains(t, StringComparison.OrdinalIgnoreCase)))
        {
            // Check if icon conveys meaning without text alternative
            var hasAccessibleName = !string.IsNullOrWhiteSpace(node.Name) || 
                                    !string.IsNullOrWhiteSpace(node.Text) ||
                                    node.Properties.ContainsKey("AccessibleLabel") ||
                                    node.Properties.ContainsKey("aria-label");

            if (!hasAccessibleName && node.Properties.TryGetValue("Icon", out var iconName))
            {
                var meaningfulIcons = new[] { "warning", "error", "success", "info", "check", "cross", "alert" };
                var iconStr = iconName?.ToString()?.ToLowerInvariant() ?? "";
                
                if (meaningfulIcons.Any(m => iconStr.Contains(m)))
                {
                    yield return new Finding(
                        Id: $"{Id}:ICON:{node.Id}",
                        Severity: Severity.High,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_ICON_ONLY",
                        Message: $"Icon '{node.Id}' conveys status information but lacks accessible text alternative.",
                        WcagReference: "WCAG 2.2 – 1.3.3 Sensory Characteristics (Level A)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Icons that convey meaning must have text alternatives for users who cannot see them.",
                        SuggestedFix: $"Add an AccessibleLabel property to '{node.Id}' describing the status it represents (e.g., 'Error', 'Success', 'Warning').",
                        WcagCriterion: WcagCriterion.SensoryCharacteristics_1_3_3
                    );
                }
            }
        }
    }

    private Finding CreateFinding(UiNode node, RuleContext context, string subType, string issueDescription, string rationale, string suggestedFix)
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
            Message: issueDescription,
            WcagReference: "WCAG 2.2 – 1.3.3 Sensory Characteristics (Level A)",
            Section508Reference: "Section 508 E207.2 - WCAG Conformance",
            Rationale: rationale,
            SuggestedFix: suggestedFix,
            WcagCriterion: WcagCriterion.SensoryCharacteristics_1_3_3
        );
    }

    private static string TruncateText(string text, int maxLength = 50)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}
