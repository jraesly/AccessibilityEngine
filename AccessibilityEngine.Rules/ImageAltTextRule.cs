using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

public sealed class ImageAltTextRule : IRule
{
    public string Id => "MISSING_ALT_TEXT";
    public string Description => "Images should have alternative text.";
    public SurfaceType[] AppliesTo => new[] { SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage, SurfaceType.DomSnapshot };

    private static readonly HashSet<string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Image", "img", "Picture", "Graphic"
    };

    public IEnumerable<Finding> Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;
        if (!ImageTypes.Contains(node.Type)) yield break;

        string? alt = null;
        if (node.Properties != null && node.Properties.TryGetValue("Alt", out var a) && a is string sa)
            alt = sa?.Trim();
        if (string.IsNullOrWhiteSpace(alt) && node.Properties != null && node.Properties.TryGetValue("AccessibleLabel", out var al) && al is string sal)
            alt = sal?.Trim();
        if (string.IsNullOrWhiteSpace(alt))
            alt = node.Name?.Trim();

        if (string.IsNullOrWhiteSpace(alt))
        {
            yield return new Finding(
                Id: $"{Id}:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: Id,
                Message: "Image is missing alternative text.",
                WcagReference: "WCAG 2.1 – 1.1.1",
                Section508Reference: "Section 508 - Text Alternatives",
                Rationale: "Alternative text ensures non-text content is accessible to assistive technologies."
            );
        }
        else
        {
            // simple heuristics for generic alt text
            var generic = new[] { "image", "photo", "picture", "graphic", "logo" };
            if (alt.Length < 3 || generic.Any(g => string.Equals(g, alt, StringComparison.OrdinalIgnoreCase)))
            {
                yield return new Finding(
                    Id: $"{Id}:DESC:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: "DESCRIPTIVE_ALT_TEXT",
                    Message: "Alternative text is too short or generic; consider a more descriptive text.",
                    WcagReference: "WCAG 2.1 – 1.1.1",
                    Section508Reference: "Section 508 - Text Alternatives",
                    Rationale: "Descriptive alt text provides meaningful context for assistive technology users."
                );
            }
        }
    }
}
