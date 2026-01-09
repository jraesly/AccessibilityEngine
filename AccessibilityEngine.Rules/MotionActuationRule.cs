using System;
using System.Collections.Generic;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for motion actuation accessibility issues.
/// WCAG 2.5.4: Functionality operated by device motion or user motion must have UI alternatives and motion can be disabled.
/// </summary>
public sealed class MotionActuationRule : IRule
{
    public string Id => "MOTION_ACTUATION";
    public string Description => "Functionality operated by motion must have UI component alternatives and motion can be disabled.";
    public Severity Severity => Severity.High;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp];

    // Properties that indicate motion-based functionality
    private static readonly string[] MotionProperties =
    [
        "OnShake", "OnTilt", "OnRotation", "OnDeviceMotion",
        "Accelerometer", "Gyroscope", "Compass", "Orientation"
    ];

    // Control types that may use device sensors
    private static readonly HashSet<string> SensorControlTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Accelerometer", "Gyroscope", "Compass",
        "LocationSensor", "OrientationSensor",
        "Component", "PCFControl", "Custom"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check for motion-activated functionality
        foreach (var finding in CheckMotionActivation(node, context))
            yield return finding;

        // Check for sensor-based controls
        foreach (var finding in CheckSensorControls(node, context))
            yield return finding;

        // Check for shake-to-undo or similar patterns
        foreach (var finding in CheckShakePatterns(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckMotionActivation(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        foreach (var motionProperty in MotionProperties)
        {
            if (node.Properties.TryGetValue(motionProperty, out var handler) && handler != null)
            {
                var hasAlternative = HasUIAlternative(node, context, motionProperty);
                var canBeDisabled = CanMotionBeDisabled(node, context);

                if (!hasAlternative)
                {
                    yield return new Finding(
                        Id: $"{Id}:NO_ALTERNATIVE:{node.Id}:{motionProperty}",
                        Severity: Severity.High,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_NO_ALTERNATIVE",
                        Message: $"Control '{node.Id}' uses motion-based trigger '{motionProperty}' without a visible UI alternative.",
                        WcagReference: "WCAG 2.2 § 2.5.4 Motion Actuation (Level A)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Users who cannot move devices (e.g., wheelchair-mounted tablets) or have tremors need UI alternatives to motion-based functions.",
                        SuggestedFix: $"Add a button or other UI control that performs the same function as the '{motionProperty}' gesture on '{node.Id}'.",
                        WcagCriterion: WcagCriterion.MotionActuation_2_5_4
                    );
                }

                if (!canBeDisabled)
                {
                    yield return new Finding(
                        Id: $"{Id}:CANNOT_DISABLE:{node.Id}:{motionProperty}",
                        Severity: Severity.Medium,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_CANNOT_DISABLE",
                        Message: $"Motion-based functionality '{motionProperty}' on '{node.Id}' cannot be disabled. Users with tremors may trigger it accidentally.",
                        WcagReference: "WCAG 2.2 § 2.5.4 Motion Actuation (Level A)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Motion actuation should be disableable to prevent accidental activation by users with motor impairments.",
                        SuggestedFix: $"Add a setting or toggle that allows users to disable motion-based functionality for '{motionProperty}' on '{node.Id}'.",
                        WcagCriterion: WcagCriterion.MotionActuation_2_5_4
                    );
                }
            }
        }
    }

    private IEnumerable<Finding> CheckSensorControls(UiNode node, RuleContext context)
    {
        if (!SensorControlTypes.Contains(node.Type)) yield break;

        // Check accelerometer/gyroscope sensors
        if (node.Type.Equals("Accelerometer", StringComparison.OrdinalIgnoreCase) ||
            node.Type.Equals("Gyroscope", StringComparison.OrdinalIgnoreCase))
        {
            yield return new Finding(
                Id: $"{Id}:SENSOR:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_SENSOR",
                Message: $"Motion sensor '{node.Id}' ({node.Type}) detected. Ensure motion-triggered functionality has UI alternatives and can be disabled.",
                WcagReference: "WCAG 2.2 § 2.5.4 Motion Actuation (Level A)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Functionality triggered by tilting, shaking, or device orientation must have conventional control alternatives.",
                SuggestedFix: $"For any functionality triggered by '{node.Id}': (1) Add a button/UI control as alternative, (2) Allow users to disable motion activation in app settings.",
                WcagCriterion: WcagCriterion.MotionActuation_2_5_4
            );
        }

        // Check orientation sensor for responsive layout
        if (node.Type.Equals("OrientationSensor", StringComparison.OrdinalIgnoreCase))
        {
            yield return new Finding(
                Id: $"{Id}:ORIENTATION_SENSOR:{node.Id}",
                Severity: Severity.Low,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_ORIENTATION_SENSOR",
                Message: $"Orientation sensor '{node.Id}' detected. If used for functional changes (beyond responsive layout), provide alternatives.",
                WcagReference: "WCAG 2.2 § 2.5.4 Motion Actuation (Level A)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Device orientation for responsive layout is acceptable, but using orientation for functions (e.g., tilt to navigate) needs alternatives.",
                SuggestedFix: $"Review uses of '{node.Id}'. Layout changes are acceptable, but functional changes triggered by orientation need UI alternatives.",
                WcagCriterion: WcagCriterion.MotionActuation_2_5_4
            );
        }
    }

    private IEnumerable<Finding> CheckShakePatterns(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Look for shake-to-refresh or shake-to-undo patterns
        foreach (var prop in node.Properties)
        {
            var value = prop.Value?.ToString() ?? "";
            if (value.Contains("Shake", StringComparison.OrdinalIgnoreCase))
            {
                yield return new Finding(
                    Id: $"{Id}:SHAKE:{node.Id}",
                    Severity: Severity.High,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_SHAKE",
                    Message: $"Control '{node.Id}' appears to use shake gesture. Provide a button alternative and allow disabling shake functionality.",
                    WcagReference: "WCAG 2.2 § 2.5.4 Motion Actuation (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Shake gestures are inaccessible to users who cannot physically shake a device and may be triggered accidentally by users with tremors.",
                    SuggestedFix: $"Add a visible button that performs the same action as shaking. Add a setting to disable shake-triggered functions.",
                    WcagCriterion: WcagCriterion.MotionActuation_2_5_4
                );
            }

            // Check for tilt-based navigation or controls
            if (value.Contains("Tilt", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("Inclination", StringComparison.OrdinalIgnoreCase))
            {
                yield return new Finding(
                    Id: $"{Id}:TILT:{node.Id}",
                    Severity: Severity.High,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_TILT",
                    Message: $"Control '{node.Id}' appears to use tilt-based functionality. Provide UI alternatives for tilt controls.",
                    WcagReference: "WCAG 2.2 § 2.5.4 Motion Actuation (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Tilt-based controls are inaccessible to users with mounted devices or motor impairments.",
                    SuggestedFix: $"Replace or supplement tilt-based controls on '{node.Id}' with on-screen buttons, sliders, or other standard UI controls.",
                    WcagCriterion: WcagCriterion.MotionActuation_2_5_4
                );
            }
        }
    }

    private static bool HasUIAlternative(UiNode node, RuleContext context, string motionProperty)
    {
        // Check siblings for buttons that might be alternatives
        foreach (var sibling in context.Siblings)
        {
            if (sibling.Type.Contains("Button", StringComparison.OrdinalIgnoreCase))
            {
                // Look for related naming
                var siblingName = sibling.Name?.ToLowerInvariant() ?? "";
                var siblingText = sibling.Text?.ToLowerInvariant() ?? "";
                var motionLower = motionProperty.ToLowerInvariant();

                // Check for common alternatives
                if (motionLower.Contains("shake"))
                {
                    if (siblingName.Contains("undo") || siblingText.Contains("undo") ||
                        siblingName.Contains("refresh") || siblingText.Contains("refresh"))
                        return true;
                }

                if (motionLower.Contains("tilt"))
                {
                    if (siblingName.Contains("navigate") || siblingText.Contains("navigate") ||
                        siblingName.Contains("scroll") || siblingText.Contains("scroll"))
                        return true;
                }
            }
        }

        return false;
    }

    private static bool CanMotionBeDisabled(UiNode node, RuleContext context)
    {
        // Look for settings or toggle controls on the screen or in siblings
        foreach (var sibling in context.Siblings)
        {
            var siblingName = sibling.Name?.ToLowerInvariant() ?? "";
            var siblingText = sibling.Text?.ToLowerInvariant() ?? "";

            if (siblingName.Contains("motion") || siblingText.Contains("motion") ||
                siblingName.Contains("setting") || siblingText.Contains("setting") ||
                siblingName.Contains("disable") || siblingText.Contains("disable"))
            {
                return true;
            }
        }

        // Check if there's a conditional check for motion
        if (node.Properties != null)
        {
            foreach (var prop in node.Properties)
            {
                var value = prop.Value?.ToString() ?? "";
                if (value.Contains("EnableMotion", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("MotionEnabled", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("UseMotion", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
