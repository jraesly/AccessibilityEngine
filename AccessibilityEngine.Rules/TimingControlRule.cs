using System;
using System.Collections.Generic;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for proper timing control accessibility.
/// WCAG 2.2.1: For each time limit set by content, users can turn off, adjust, or extend it.
/// WCAG 2.2.2: For moving, blinking, scrolling, or auto-updating information, users can pause, stop, or hide it.
/// </summary>
public sealed class TimingControlRule : IRule
{
    public string Id => "TIMING_CONTROL";
    public string Description => "Time-limited content must provide ways for users to adjust or disable timing.";
    public Severity Severity => Severity.High;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage, SurfaceType.DomSnapshot];

    // Timer-related control types
    private static readonly HashSet<string> TimerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Timer", "timer", "Classic/Timer"
    };

    // Controls that may have auto-updating content
    private static readonly HashSet<string> AutoUpdateTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Gallery", "DataTable", "Chart", "Image", "Media", "Video"
    };

    // Animation-related patterns
    private static readonly HashSet<string> AnimationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Image", "Media", "Video", "Gif", "Animation"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check Timer controls
        if (TimerTypes.Contains(node.Type))
        {
            // Check for auto-start timers
            if (IsAutoStartTimer(node))
            {
                var hasUserControl = HasTimerUserControl(node, context);
                
                if (!hasUserControl)
                {
                    yield return new Finding(
                        Id: $"{Id}:TIMER_AUTO:{node.Id}",
                        Severity: Severity.High,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_AUTO_TIMER_NO_CONTROL",
                        Message: $"Timer '{node.Id}' starts automatically but does not appear to have user controls to pause, stop, or adjust it.",
                        WcagReference: "WCAG 2.2 § 2.2.1 Timing Adjustable (Level A)",
                        Section508Reference: "Section 508 E207.2 - Timing Adjustable",
                        Rationale: "Users must be able to turn off, adjust, or extend time limits. Automatic timers can cause problems for users who need more time.",
                        SuggestedFix: $"Add controls (buttons) that allow users to pause, stop, or reset timer '{node.Id}'. Alternatively, warn users before the timer ends and allow them to extend the time.",
                        WcagCriterion: WcagCriterion.TimingAdjustable_2_2_1
                    );
                }
            }

            // Check for session timeout timers
            if (IsSessionTimeout(node))
            {
                yield return new Finding(
                    Id: $"{Id}:SESSION:{node.Id}",
                    Severity: Severity.High,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_SESSION_TIMEOUT",
                    Message: $"Timer '{node.Id}' appears to control session timeout. Users must be warned before timeout and given the option to extend.",
                    WcagReference: "WCAG 2.2 § 2.2.1 Timing Adjustable (Level A)",
                    Section508Reference: "Section 508 E207.2 - Timing Adjustable",
                    Rationale: "Session timeouts must warn users at least 20 seconds before expiration and allow them to extend the session with a simple action.",
                    SuggestedFix: $"Before timer '{node.Id}' expires, show a warning dialog that allows users to extend the session. The warning should appear at least 20 seconds before timeout.",
                    WcagCriterion: WcagCriterion.TimingAdjustable_2_2_1
                );
            }

            // Check timer duration for very short durations
            var duration = GetTimerDuration(node);
            if (duration.HasValue && duration.Value < 5000) // Less than 5 seconds
            {
                yield return new Finding(
                    Id: $"{Id}:SHORT_TIMER:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_SHORT_DURATION",
                    Message: $"Timer '{node.Id}' has a very short duration ({duration}ms). This may not provide enough time for users with disabilities to respond.",
                    WcagReference: "WCAG 2.2 § 2.2.1 Timing Adjustable (Level A)",
                    Section508Reference: "Section 508 E207.2 - Timing Adjustable",
                    Rationale: "Very short time limits may not allow users with motor, visual, or cognitive disabilities enough time to read, understand, and respond.",
                    SuggestedFix: $"Consider increasing the duration of timer '{node.Id}' or providing users with options to adjust the timing. If timing is essential, document why.",
                    WcagCriterion: WcagCriterion.TimingAdjustable_2_2_1
                );
            }
        }

        // Check for auto-refreshing content
        if (AutoUpdateTypes.Contains(node.Type))
        {
            if (HasAutoRefresh(node))
            {
                var hasRefreshControl = HasRefreshControl(node, context);
                
                if (!hasRefreshControl)
                {
                    yield return new Finding(
                        Id: $"{Id}:AUTO_REFRESH:{node.Id}",
                        Severity: Severity.Medium,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_AUTO_REFRESH",
                        Message: $"Control '{node.Id}' appears to auto-refresh. Users should be able to pause or control the update frequency.",
                        WcagReference: "WCAG 2.2 § 2.2.2 Pause, Stop, Hide (Level A)",
                        Section508Reference: "Section 508 E207.2 - Pause, Stop, Hide",
                        Rationale: "Auto-updating information can be disorienting for screen reader users and distracting for users with cognitive disabilities.",
                        SuggestedFix: $"Add a pause/resume control for '{node.Id}', or allow users to manually trigger refresh instead of auto-refresh.",
                        WcagCriterion: WcagCriterion.PauseStopHide_2_2_2
                    );
                }
            }
        }

        // Check for animated content
        if (AnimationTypes.Contains(node.Type) || HasAnimation(node))
        {
            if (!HasAnimationControl(node, context))
            {
                yield return new Finding(
                    Id: $"{Id}:ANIMATION:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_ANIMATION_NO_CONTROL",
                    Message: $"Control '{node.Id}' contains animation. If the animation lasts more than 5 seconds, users must be able to pause, stop, or hide it.",
                    WcagReference: "WCAG 2.2 § 2.2.2 Pause, Stop, Hide (Level A)",
                    Section508Reference: "Section 508 E207.2 - Pause, Stop, Hide",
                    Rationale: "Moving or animated content can be distracting and may cause problems for users with attention disorders or motion sensitivity.",
                    SuggestedFix: $"If animation in '{node.Id}' lasts more than 5 seconds, add controls to pause or stop it. Alternatively, set the animation to stop after 5 seconds.",
                    WcagCriterion: WcagCriterion.PauseStopHide_2_2_2
                );
            }
        }

        // Check for carousel/slideshow patterns
        if (IsCarousel(node))
        {
            var hasCarouselControl = HasCarouselControls(node, context);
            
            if (!hasCarouselControl)
            {
                yield return new Finding(
                    Id: $"{Id}:CAROUSEL:{node.Id}",
                    Severity: Severity.High,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_CAROUSEL_NO_CONTROL",
                    Message: $"Carousel/slideshow '{node.Id}' auto-advances but lacks pause control. Users must be able to stop auto-advancing.",
                    WcagReference: "WCAG 2.2 § 2.2.2 Pause, Stop, Hide (Level A)",
                    Section508Reference: "Section 508 E207.2 - Pause, Stop, Hide",
                    Rationale: "Auto-advancing carousels must have a pause button so users can read content at their own pace.",
                    SuggestedFix: $"Add a pause/play button to carousel '{node.Id}'. The carousel should pause on keyboard focus and mouse hover. Provide previous/next buttons for manual navigation.",
                    WcagCriterion: WcagCriterion.PauseStopHide_2_2_2
                );
            }
        }
    }

    private static bool IsAutoStartTimer(UiNode node)
    {
        if (node.Properties == null) return false;

        // Check AutoStart property
        if (node.Properties.TryGetValue("AutoStart", out var autoStart))
        {
            var autoStartStr = autoStart?.ToString();
            return autoStartStr == "true" || autoStartStr == "True" || autoStart is bool b && b;
        }

        // Check Start property
        if (node.Properties.TryGetValue("Start", out var start))
        {
            return start?.ToString() == "true";
        }

        return false;
    }

    private static bool HasTimerUserControl(UiNode node, RuleContext context)
    {
        // Look for sibling controls that might control this timer
        foreach (var sibling in context.Siblings)
        {
            var siblingType = sibling.Type?.ToLowerInvariant() ?? "";
            if (siblingType.Contains("button"))
            {
                // Check if button controls this timer
                var onSelect = sibling.Properties?.GetValueOrDefault("OnSelect")?.ToString();
                if (onSelect != null && onSelect.Contains(node.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Check button text for pause/stop indicators
                var text = sibling.Text ?? sibling.Properties?.GetValueOrDefault("Text")?.ToString();
                if (text != null)
                {
                    var textLower = text.ToLowerInvariant();
                    if (textLower.Contains("pause") || textLower.Contains("stop") || 
                        textLower.Contains("cancel") || textLower.Contains("extend"))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsSessionTimeout(UiNode node)
    {
        var idLower = node.Id?.ToLowerInvariant() ?? "";
        var nameLower = node.Name?.ToLowerInvariant() ?? "";

        // Check for session/timeout patterns
        return idLower.Contains("session") || idLower.Contains("timeout") || idLower.Contains("idle") ||
               nameLower.Contains("session") || nameLower.Contains("timeout") || nameLower.Contains("idle");
    }

    private static int? GetTimerDuration(UiNode node)
    {
        if (node.Properties == null) return null;

        // Check Duration property
        if (node.Properties.TryGetValue("Duration", out var duration))
        {
            if (duration is int i) return i;
            if (duration is long l) return (int)l;
            if (duration is string s && int.TryParse(s, out var parsed)) return parsed;
        }

        return null;
    }

    private static bool HasAutoRefresh(UiNode node)
    {
        if (node.Properties == null) return false;

        // Check Items property for refresh patterns
        if (node.Properties.TryGetValue("Items", out var items))
        {
            var itemsStr = items?.ToString();
            if (itemsStr != null && 
                (itemsStr.Contains("Timer", StringComparison.OrdinalIgnoreCase) ||
                 itemsStr.Contains("Refresh(", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasRefreshControl(UiNode node, RuleContext context)
    {
        foreach (var sibling in context.Siblings)
        {
            var text = sibling.Text ?? sibling.Properties?.GetValueOrDefault("Text")?.ToString();
            if (text != null)
            {
                var textLower = text.ToLowerInvariant();
                if (textLower.Contains("refresh") || textLower.Contains("pause") || textLower.Contains("stop"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasAnimation(UiNode node)
    {
        // Check for GIF images or animation properties
        if (node.Properties == null) return false;

        if (node.Properties.TryGetValue("Image", out var image))
        {
            var imageStr = image?.ToString();
            if (imageStr != null && imageStr.Contains(".gif", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Check for transition/animation properties
        return node.Properties.ContainsKey("Transition") || 
               node.Properties.ContainsKey("Animation") ||
               node.Properties.ContainsKey("Animate");
    }

    private static bool HasAnimationControl(UiNode node, RuleContext context)
    {
        // Look for animation control buttons
        foreach (var sibling in context.Siblings)
        {
            var text = sibling.Text ?? sibling.Properties?.GetValueOrDefault("Text")?.ToString();
            if (text != null)
            {
                var textLower = text.ToLowerInvariant();
                if (textLower.Contains("pause") || textLower.Contains("stop") || textLower.Contains("play"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsCarousel(UiNode node)
    {
        var idLower = node.Id?.ToLowerInvariant() ?? "";
        var nameLower = node.Name?.ToLowerInvariant() ?? "";
        var typeLower = node.Type?.ToLowerInvariant() ?? "";

        return idLower.Contains("carousel") || idLower.Contains("slider") || idLower.Contains("slideshow") ||
               nameLower.Contains("carousel") || nameLower.Contains("slider") ||
               typeLower.Contains("carousel") || typeLower.Contains("slider");
    }

    private static bool HasCarouselControls(UiNode node, RuleContext context)
    {
        var hasPausePlay = false;
        var hasNavigation = false;

        foreach (var sibling in context.Siblings)
        {
            var text = sibling.Text ?? sibling.Properties?.GetValueOrDefault("Text")?.ToString() ?? sibling.Id;
            if (text != null)
            {
                var textLower = text.ToLowerInvariant();
                if (textLower.Contains("pause") || textLower.Contains("play") || textLower.Contains("stop"))
                    hasPausePlay = true;
                if (textLower.Contains("next") || textLower.Contains("prev") || textLower.Contains("previous"))
                    hasNavigation = true;
            }
        }

        return hasPausePlay || hasNavigation;
    }
}
