using System;
using System.Collections.Generic;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for orientation restrictions.
/// WCAG 1.3.4: Content must not restrict view/operation to a single display orientation unless essential.
/// </summary>
public sealed class OrientationRule : IRule
{
    public string Id => "ORIENTATION";
    public string Description => "Content must not restrict view or operation to a single display orientation.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp];

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check at app/screen level for orientation settings
        foreach (var finding in CheckAppOrientation(node, context))
            yield return finding;

        // Check for controls with orientation-specific layouts
        foreach (var finding in CheckOrientationDependentLayout(node, context))
            yield return finding;

        // Check for media that requires specific orientation
        foreach (var finding in CheckMediaOrientation(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckAppOrientation(UiNode node, RuleContext context)
    {
        // Check if this is a screen or app-level node
        if (!node.Type.Equals("Screen", StringComparison.OrdinalIgnoreCase) &&
            !node.Type.Equals("App", StringComparison.OrdinalIgnoreCase))
            yield break;

        if (node.Properties == null) yield break;

        // Check for orientation lock properties
        var orientationLockProperties = new[]
        {
            "SupportedOrientations", "Orientation", "ScreenOrientation",
            "LockOrientation", "OrientationLock", "AllowedOrientations"
        };

        foreach (var prop in orientationLockProperties)
        {
            if (node.Properties.TryGetValue(prop, out var value))
            {
                var orientationValue = value?.ToString()?.ToLowerInvariant() ?? "";
                
                // Check for restricted orientation
                var isLocked = orientationValue.Contains("portrait") && !orientationValue.Contains("landscape") ||
                              orientationValue.Contains("landscape") && !orientationValue.Contains("portrait") ||
                              orientationValue.Contains("locked") ||
                              orientationValue.Equals("true", StringComparison.OrdinalIgnoreCase);

                if (isLocked)
                {
                    var lockedTo = orientationValue.Contains("portrait") ? "portrait" : 
                                   orientationValue.Contains("landscape") ? "landscape" : "specific orientation";
                    
                    yield return new Finding(
                        Id: $"{Id}:LOCKED:{node.Id}",
                        Severity: Severity.High,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_LOCKED",
                        Message: $"Screen/App '{node.Id}' restricts orientation to {lockedTo}. Ensure this is essential for the content.",
                        WcagReference: "WCAG 2.2 § 1.3.4 Orientation (Level AA)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Users who have mounted devices (e.g., on wheelchairs) or specific physical needs may require content in a particular orientation.",
                        SuggestedFix: $"Remove orientation lock from '{node.Id}' unless the orientation is essential (e.g., piano keyboard app, deposit check scanning). Ensure layout works in both portrait and landscape.",
                        WcagCriterion: WcagCriterion.Orientation_1_3_4
                    );
                }
            }
        }

        // Check for fixed dimensions that suggest single orientation
        if (node.Properties.TryGetValue("Width", out var width) &&
            node.Properties.TryGetValue("Height", out var height))
        {
            var w = ParseDimension(width);
            var h = ParseDimension(height);

            if (w > 0 && h > 0)
            {
                var aspectRatio = (double)w / h;
                var isExtremeAspectRatio = aspectRatio > 2.5 || aspectRatio < 0.4;

                if (isExtremeAspectRatio)
                {
                    yield return new Finding(
                        Id: $"{Id}:ASPECT_RATIO:{node.Id}",
                        Severity: Severity.Low,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_ASPECT_RATIO",
                        Message: $"Screen '{node.Id}' has extreme aspect ratio ({aspectRatio:F2}:1). Verify it works when device orientation changes.",
                        WcagReference: "WCAG 2.2 § 1.3.4 Orientation (Level AA)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Fixed dimensions optimized for one orientation may be unusable in the other orientation.",
                        SuggestedFix: $"Consider using responsive layout for '{node.Id}' that adapts to both portrait and landscape orientations.",
                        WcagCriterion: WcagCriterion.Orientation_1_3_4
                    );
                }
            }
        }
    }

    private IEnumerable<Finding> CheckOrientationDependentLayout(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check for formulas that change based on orientation
        var layoutProperties = new[] { "X", "Y", "Width", "Height", "Visible" };

        foreach (var prop in layoutProperties)
        {
            if (node.Properties.TryGetValue(prop, out var value))
            {
                var formula = value?.ToString() ?? "";
                
                // Check if layout depends on orientation detection
                if (formula.Contains("App.Width", StringComparison.OrdinalIgnoreCase) ||
                    formula.Contains("App.Height", StringComparison.OrdinalIgnoreCase))
                {
                    // This is actually good - responsive design
                    // But check if content is hidden in one orientation
                    if (prop == "Visible")
                    {
                        var hidesContent = formula.Contains(">") || formula.Contains("<");
                        if (hidesContent)
                        {
                            yield return new Finding(
                                Id: $"{Id}:HIDDEN_ORIENTATION:{node.Id}",
                                Severity: Severity.Medium,
                                Surface: context.Surface,
                                AppName: context.AppName,
                                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                                ControlId: node.Id,
                                ControlType: node.Type,
                                IssueType: $"{Id}_HIDDEN_ORIENTATION",
                                Message: $"Control '{node.Id}' visibility changes based on screen dimensions. Ensure essential content is available in all orientations.",
                                WcagReference: "WCAG 2.2 § 1.3.4 Orientation (Level AA)",
                                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                                Rationale: "Hiding content in certain orientations may make essential functionality unavailable to users who cannot change device orientation.",
                                SuggestedFix: $"Review visibility formula for '{node.Id}'. If this hides essential content in certain orientations, ensure an alternative way to access it.",
                                WcagCriterion: WcagCriterion.Orientation_1_3_4
                            );
                        }
                    }
                }
            }
        }
    }

    private IEnumerable<Finding> CheckMediaOrientation(UiNode node, RuleContext context)
    {
        // Check video/image controls that might require specific orientation
        var mediaTypes = new[] { "Video", "Image", "Camera", "PDF", "Document" };
        
        var isMediaType = false;
        foreach (var mediaType in mediaTypes)
        {
            if (node.Type.Contains(mediaType, StringComparison.OrdinalIgnoreCase))
            {
                isMediaType = true;
                break;
            }
        }

        if (!isMediaType) yield break;

        // Check for camera control (may legitimately need specific orientation)
        if (node.Type.Contains("Camera", StringComparison.OrdinalIgnoreCase))
        {
            if (node.Properties?.TryGetValue("Stream", out var stream) == true ||
                node.Properties?.ContainsKey("CameraMode") == true)
            {
                yield return new Finding(
                    Id: $"{Id}:CAMERA:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_CAMERA",
                    Message: $"Camera control '{node.Id}' detected. If used for document scanning (e.g., checks), specific orientation may be acceptable.",
                    WcagReference: "WCAG 2.2 § 1.3.4 Orientation (Level AA)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Document scanning that requires alignment (e.g., bank checks) is an exception where specific orientation may be essential.",
                    SuggestedFix: $"If '{node.Id}' is used for general photo capture, allow both orientations. If used for document/check scanning where orientation is essential, this is acceptable.",
                    WcagCriterion: WcagCriterion.Orientation_1_3_4
                );
            }
        }

        // Check for fullscreen video that might force orientation
        if (node.Type.Contains("Video", StringComparison.OrdinalIgnoreCase))
        {
            if (node.Properties?.TryGetValue("Fullscreen", out var fullscreen) == true)
            {
                var isFullscreen = fullscreen is true || 
                                  fullscreen?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
                if (isFullscreen)
                {
                    yield return new Finding(
                        Id: $"{Id}:VIDEO_FULLSCREEN:{node.Id}",
                        Severity: Severity.Low,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_VIDEO_FULLSCREEN",
                        Message: $"Fullscreen video '{node.Id}' detected. Ensure users can view video in either orientation without forced rotation.",
                        WcagReference: "WCAG 2.2 § 1.3.4 Orientation (Level AA)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Video should adapt to device orientation, potentially using letterboxing/pillarboxing rather than forcing rotation.",
                        SuggestedFix: $"Allow video '{node.Id}' to play in the current device orientation with appropriate letterboxing rather than forcing landscape mode.",
                        WcagCriterion: WcagCriterion.Orientation_1_3_4
                    );
                }
            }
        }
    }

    private static int ParseDimension(object? value)
    {
        if (value == null) return -1;
        if (value is int i) return i;
        if (value is long l) return (int)l;
        if (value is double d) return (int)d;
        if (value is string s)
        {
            // Check if it's a formula (contains function calls or references)
            if (s.Contains("(") || s.Contains("Self.") || s.Contains("Parent.") || s.Contains("App."))
                return -1; // Dynamic value

            if (int.TryParse(s, out var parsed))
                return parsed;
        }
        return -1;
    }
}
