using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for content that flashes more than three times per second.
/// WCAG 2.3.1: Content must not flash more than three times in any one second period,
/// or the flash must be below the general flash and red flash thresholds.
/// </summary>
public sealed class FlashingContentRule : IRule
{
    public string Id => "FLASHING_CONTENT";
    public string Description => "Content must not flash more than three times per second unless below flash thresholds.";
    public Severity Severity => Severity.Critical;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Control types that may contain flashing content
    private static readonly HashSet<string> MediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Video", "MediaPlayer", "Animation", "Gif", "Image", "Canvas",
        "Stream", "LiveVideo", "WebView"
    };

    // Control types that may have animated properties
    private static readonly HashSet<string> AnimatableTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Button", "Icon", "Indicator", "StatusLight", "Timer", "Progress",
        "Spinner", "Loading", "Alert", "Notification"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check for video/media content
        foreach (var finding in CheckMediaContent(node, context))
            yield return finding;

        // Check for animation properties
        foreach (var finding in CheckAnimationProperties(node, context))
            yield return finding;

        // Check for blinking/flashing indicators
        foreach (var finding in CheckBlinkingElements(node, context))
            yield return finding;

        // Check for auto-playing animated content
        foreach (var finding in CheckAutoPlayContent(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckMediaContent(UiNode node, RuleContext context)
    {
        if (!MediaTypes.Contains(node.Type)) yield break;
        if (node.Properties == null) yield break;

        // Check for video/animation content that might flash
        var hasFlashingWarning = false;

        // Check if there's an epilepsy/flashing warning or safe mode
        if (node.Properties.TryGetValue("FlashingContentWarning", out var warning) ||
            node.Properties.TryGetValue("PhotosensitivityWarning", out warning))
        {
            hasFlashingWarning = warning is bool w && w;
        }

        var hasReducedMotion = node.Properties.ContainsKey("ReducedMotionAlternative") ||
                               node.Properties.ContainsKey("SafeMode");

        // If it's video/animation without safety measures, flag for review
        if (node.Type.Equals("Video", StringComparison.OrdinalIgnoreCase) ||
            node.Type.Equals("Animation", StringComparison.OrdinalIgnoreCase))
        {
            if (!hasFlashingWarning && !hasReducedMotion)
            {
                yield return new Finding(
                    Id: $"{Id}:MEDIA:{node.Id}",
                    Severity: Severity.High,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_MEDIA_REVIEW",
                    Message: $"Media content '{node.Id}' should be reviewed for flashing content that could trigger seizures.",
                    WcagReference: "WCAG 2.2 – 2.3.1 Three Flashes or Below Threshold (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Content that flashes more than 3 times per second can trigger seizures in people with photosensitive epilepsy.",
                    SuggestedFix: $"Review '{node.Id}' for flashing content. If present, either reduce flash rate to below 3Hz, ensure flashes are below threshold, or provide a warning and alternative.",
                    WcagCriterion: WcagCriterion.ThreeFlashesOrBelowThreshold_2_3_1
                );
            }
        }

        // Check for GIF images which may be animated
        if (node.Type.Equals("Image", StringComparison.OrdinalIgnoreCase) ||
            node.Type.Equals("Gif", StringComparison.OrdinalIgnoreCase))
        {
            var source = GetImageSource(node);
            if (source != null && source.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            {
                yield return new Finding(
                    Id: $"{Id}:GIF:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_GIF",
                    Message: $"Animated GIF '{node.Id}' should be verified for flash rate compliance.",
                    WcagReference: "WCAG 2.2 – 2.3.1 Three Flashes or Below Threshold (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Animated GIFs can contain rapid flashing that may trigger seizures.",
                    SuggestedFix: $"Verify that '{node.Id}' does not flash more than 3 times per second. Consider providing pause control or using CSS animations with prefers-reduced-motion support.",
                    WcagCriterion: WcagCriterion.ThreeFlashesOrBelowThreshold_2_3_1
                );
            }
        }
    }

    private IEnumerable<Finding> CheckAnimationProperties(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check for animation duration that could result in rapid flashing
        if (node.Properties.TryGetValue("AnimationDuration", out var duration) ||
            node.Properties.TryGetValue("TransitionDuration", out duration))
        {
            var durationMs = ParseDuration(duration);
            
            // If animation cycles in less than 333ms (3Hz), it could be problematic
            if (durationMs > 0 && durationMs < 333)
            {
                // Check if it's a looping animation
                var isLooping = node.Properties.TryGetValue("AnimationLoop", out var loop) &&
                               (loop is bool l && l || loop?.ToString()?.ToLowerInvariant() == "true");

                var isRepeat = node.Properties.TryGetValue("AnimationRepeat", out var repeat) &&
                              repeat?.ToString() != "1" && repeat?.ToString() != "0";

                if (isLooping || isRepeat)
                {
                    yield return new Finding(
                        Id: $"{Id}:ANIMATION:{node.Id}",
                        Severity: Severity.High,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_FAST_ANIMATION",
                        Message: $"Animation on '{node.Id}' has a cycle time of {durationMs}ms which may exceed 3 flashes per second when looping.",
                        WcagReference: "WCAG 2.2 – 2.3.1 Three Flashes or Below Threshold (Level A)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Rapid looping animations can create a flashing effect that triggers seizures.",
                        SuggestedFix: $"Increase animation duration on '{node.Id}' to at least 333ms per cycle, or ensure the animation doesn't create high-contrast flashing.",
                        WcagCriterion: WcagCriterion.ThreeFlashesOrBelowThreshold_2_3_1
                    );
                }
            }
        }
    }

    private IEnumerable<Finding> CheckBlinkingElements(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check for blink effects
        var blinkProps = new[] { "Blink", "Blinking", "Flash", "Flashing", "Strobe", "Pulse" };

        foreach (var prop in blinkProps)
        {
            if (node.Properties.TryGetValue(prop, out var value))
            {
                var isEnabled = value is bool b && b || 
                               value?.ToString()?.ToLowerInvariant() == "true";

                if (isEnabled)
                {
                    yield return new Finding(
                        Id: $"{Id}:BLINK:{node.Id}",
                        Severity: Severity.Critical,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_BLINK_EFFECT",
                        Message: $"Control '{node.Id}' has a blinking/flashing effect enabled which may trigger seizures.",
                        WcagReference: "WCAG 2.2 – 2.3.1 Three Flashes or Below Threshold (Level A)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Blinking effects can cause seizures in people with photosensitive epilepsy and are distracting for users with attention disorders.",
                        SuggestedFix: $"Remove the {prop} effect from '{node.Id}' or ensure it blinks less than 3 times per second and respects prefers-reduced-motion.",
                        WcagCriterion: WcagCriterion.ThreeFlashesOrBelowThreshold_2_3_1
                    );
                }
            }
        }

        // Check for CSS text-decoration: blink or animation names suggesting blink
        if (node.Properties.TryGetValue("TextDecoration", out var textDecor))
        {
            if (textDecor?.ToString()?.ToLowerInvariant().Contains("blink") == true)
            {
                yield return new Finding(
                    Id: $"{Id}:TEXT_BLINK:{node.Id}",
                    Severity: Severity.Critical,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_TEXT_BLINK",
                    Message: $"Control '{node.Id}' uses blinking text decoration which is harmful and deprecated.",
                    WcagReference: "WCAG 2.2 – 2.3.1 Three Flashes or Below Threshold (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Blinking text can trigger seizures and is deprecated in web standards.",
                    SuggestedFix: $"Remove text-decoration: blink from '{node.Id}'. Use other visual indicators like color, icons, or subtle animations.",
                    WcagCriterion: WcagCriterion.ThreeFlashesOrBelowThreshold_2_3_1
                );
            }
        }
    }

    private IEnumerable<Finding> CheckAutoPlayContent(UiNode node, RuleContext context)
    {
        if (!MediaTypes.Contains(node.Type)) yield break;
        if (node.Properties == null) yield break;

        // Check for auto-playing animated content
        if (node.Properties.TryGetValue("AutoPlay", out var autoPlay) ||
            node.Properties.TryGetValue("AutoStart", out autoPlay))
        {
            var isAutoPlay = autoPlay is bool ap && ap ||
                            autoPlay?.ToString()?.ToLowerInvariant() == "true";

            if (isAutoPlay)
            {
                // Check if there's a reduced motion check
                var respectsReducedMotion = node.Properties.ContainsKey("PrefersReducedMotion") ||
                                           node.Properties.ContainsKey("ReducedMotionCheck");

                if (!respectsReducedMotion)
                {
                    yield return new Finding(
                        Id: $"{Id}:AUTOPLAY:{node.Id}",
                        Severity: Severity.Medium,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_AUTOPLAY",
                        Message: $"Media '{node.Id}' auto-plays without checking user motion preferences.",
                        WcagReference: "WCAG 2.2 – 2.3.1 Three Flashes or Below Threshold (Level A)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Auto-playing media should respect the user's prefers-reduced-motion setting to prevent triggering seizures or causing distraction.",
                        SuggestedFix: $"Add a check for prefers-reduced-motion on '{node.Id}' and disable autoplay when the user prefers reduced motion.",
                        WcagCriterion: WcagCriterion.ThreeFlashesOrBelowThreshold_2_3_1
                    );
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

    private static int ParseDuration(object? value)
    {
        if (value == null) return 0;
        
        var str = value.ToString()?.ToLowerInvariant() ?? "";
        
        // Handle milliseconds
        if (str.EndsWith("ms"))
        {
            str = str.Replace("ms", "").Trim();
            return int.TryParse(str, out var ms) ? ms : 0;
        }
        
        // Handle seconds
        if (str.EndsWith("s"))
        {
            str = str.Replace("s", "").Trim();
            return double.TryParse(str, out var s) ? (int)(s * 1000) : 0;
        }
        
        // Assume milliseconds if no unit
        return int.TryParse(str, out var result) ? result : 0;
    }
}
