using System;
using System.Collections.Generic;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for pointer gesture accessibility issues.
/// WCAG 2.5.1: All multipoint or path-based gestures must have single-pointer alternatives.
/// WCAG 2.5.2: Pointer cancellation must be supported (up-event completion, abort, or undo).
/// </summary>
public sealed class PointerGesturesRule : IRule
{
    public string Id => "POINTER_GESTURES";
    public string Description => "Functionality using multipoint or path-based gestures must have single-pointer alternatives.";
    public Severity Severity => Severity.High;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Control types that commonly use gestures
    private static readonly HashSet<string> GestureControlTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Canvas", "PenInput", "Pen input",
        "Map", "MapControl",
        "Slider", "RangeSlider", "Range slider",
        "Gallery", "DataTable",
        "Image", "Picture",
        "Component", "PCFControl", "Custom"
    };

    // Control types with potential down-event activation issues
    private static readonly HashSet<string> ClickableControlTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Button", "IconButton", "Classic/Button",
        "Image", "Icon",
        "Link", "HyperLink",
        "Gallery", "DataTable"
    };

    // Properties that indicate gesture handlers
    private static readonly string[] GestureProperties =
    [
        "OnSwipe", "OnPinch", "OnPan", "OnRotate",
        "OnDrag", "OnDragStart", "OnDragEnd",
        "OnMultiTouch", "OnGesture"
    ];

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check for multipoint/path-based gesture controls (2.5.1)
        if (GestureControlTypes.Contains(node.Type))
        {
            foreach (var finding in CheckMultipointGestures(node, context))
                yield return finding;
        }

        // Check for down-event activation issues (2.5.2)
        if (ClickableControlTypes.Contains(node.Type))
        {
            foreach (var finding in CheckPointerCancellation(node, context))
                yield return finding;
        }

        // Check for path-based gesture requirements
        foreach (var finding in CheckPathBasedGestures(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckMultipointGestures(UiNode node, RuleContext context)
    {
        // Check for pinch-to-zoom functionality
        if (HasPinchZoom(node))
        {
            var hasAlternative = HasZoomButtons(node, context);
            if (!hasAlternative)
            {
                yield return new Finding(
                    Id: $"{Id}:PINCH_ZOOM:{node.Id}",
                    Severity: Severity.High,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_PINCH_ZOOM",
                    Message: $"Control '{node.Id}' appears to support pinch-to-zoom. Ensure zoom/scale buttons are available as a single-pointer alternative.",
                    WcagReference: "WCAG 2.2 § 2.5.1 Pointer Gestures (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Users who cannot perform multipoint gestures (e.g., using a head pointer or single finger) need single-pointer alternatives.",
                    SuggestedFix: $"Add zoom in/out buttons near '{node.Id}' that provide the same zoom functionality as pinch gestures.",
                    WcagCriterion: WcagCriterion.PointerGestures_2_5_1
                );
            }
        }

        // Check for rotation gestures
        if (HasRotationGesture(node))
        {
            yield return new Finding(
                Id: $"{Id}:ROTATION:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_ROTATION",
                Message: $"Control '{node.Id}' may use rotation gestures. Ensure a single-pointer alternative is available (e.g., rotate buttons or slider).",
                WcagReference: "WCAG 2.2 § 2.5.1 Pointer Gestures (Level A)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Two-finger rotation gestures require single-pointer alternatives for users who cannot perform multipoint gestures.",
                SuggestedFix: $"Add rotation controls (buttons or slider) to '{node.Id}' as an alternative to two-finger rotation.",
                WcagCriterion: WcagCriterion.PointerGestures_2_5_1
            );
        }
    }

    private IEnumerable<Finding> CheckPointerCancellation(UiNode node, RuleContext context)
    {
        // Check for OnSelect with immediate action (potential down-event issue)
        if (node.Properties?.TryGetValue("OnSelect", out var onSelect) == true)
        {
            var selectAction = onSelect?.ToString() ?? "";
            
            // Check if action happens immediately without confirmation
            if (HasImmediateDestructiveAction(selectAction))
            {
                yield return new Finding(
                    Id: $"{Id}:IMMEDIATE_ACTION:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_IMMEDIATE_ACTION",
                    Message: $"Control '{node.Id}' may perform immediate actions without confirmation. Ensure users can cancel accidental activations.",
                    WcagReference: "WCAG 2.2 § 2.5.2 Pointer Cancellation (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Users should be able to abort functions before completion or undo them after, especially for destructive actions.",
                    SuggestedFix: $"For '{node.Id}', either: (1) Complete the action on up-event, not down-event; (2) Add a confirmation dialog; or (3) Provide an undo mechanism.",
                    WcagCriterion: WcagCriterion.PointerCancellation_2_5_2
                );
            }
        }

        // Check for PressedFill without corresponding release handling
        if (node.Properties?.TryGetValue("PressedFill", out _) == true)
        {
            var hasOnSelect = node.Properties?.ContainsKey("OnSelect") == true;
            if (hasOnSelect)
            {
                // Verify action completes on release (up-event), not press (down-event)
                // Power Apps OnSelect fires on release, which is correct behavior
                // But custom controls may differ
                if (node.Type.Equals("Component", StringComparison.OrdinalIgnoreCase) ||
                    node.Type.Equals("PCFControl", StringComparison.OrdinalIgnoreCase))
                {
                    yield return new Finding(
                        Id: $"{Id}:VERIFY_CANCELLATION:{node.Id}",
                        Severity: Severity.Low,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_VERIFY_CANCELLATION",
                        Message: $"Custom control '{node.Id}' should be verified for pointer cancellation support.",
                        WcagReference: "WCAG 2.2 § 2.5.2 Pointer Cancellation (Level A)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Custom controls should complete actions on pointer up-event, allowing users to cancel by moving pointer away.",
                        SuggestedFix: $"Verify that '{node.Id}' completes its action on pointer release (up-event), not on pointer press (down-event), so users can cancel by dragging away.",
                        WcagCriterion: WcagCriterion.PointerCancellation_2_5_2
                    );
                }
            }
        }
    }

    private IEnumerable<Finding> CheckPathBasedGestures(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check for swipe gesture handlers
        foreach (var gestureProperty in GestureProperties)
        {
            if (node.Properties.TryGetValue(gestureProperty, out var handler) && handler != null)
            {
                yield return new Finding(
                    Id: $"{Id}:PATH_GESTURE:{node.Id}:{gestureProperty}",
                    Severity: Severity.High,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_PATH_GESTURE",
                    Message: $"Control '{node.Id}' uses path-based gesture '{gestureProperty}'. Ensure single-pointer alternative exists.",
                    WcagReference: "WCAG 2.2 § 2.5.1 Pointer Gestures (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Path-based gestures (swipe, drag, etc.) must have alternatives that can be operated with a single click or tap.",
                    SuggestedFix: $"For gesture '{gestureProperty}' on '{node.Id}', add equivalent buttons or controls that perform the same action with a single click.",
                    WcagCriterion: WcagCriterion.PointerGestures_2_5_1
                );
            }
        }

        // Check for pen/drawing input
        if (node.Type.Equals("PenInput", StringComparison.OrdinalIgnoreCase) ||
            node.Type.Equals("Pen input", StringComparison.OrdinalIgnoreCase))
        {
            yield return new Finding(
                Id: $"{Id}:PEN_INPUT:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_PEN_INPUT",
                Message: $"Pen input control '{node.Id}' requires path-based input. If signature or drawing is required, consider if an alternative input method is needed.",
                WcagReference: "WCAG 2.2 § 2.5.1 Pointer Gestures (Level A)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "If drawing/signature is essential to the function, this is an exception. Otherwise, provide alternatives like text input or file upload.",
                SuggestedFix: $"If the drawing on '{node.Id}' is not essential, provide an alternative method (e.g., typed name for signatures, file upload for images).",
                WcagCriterion: WcagCriterion.PointerGestures_2_5_1
            );
        }
    }

    private static bool HasPinchZoom(UiNode node)
    {
        if (node.Properties == null) return false;

        // Check for zoom-related properties
        var zoomProperties = new[] { "Zoom", "EnableZoom", "ZoomLevel", "OnZoom" };
        foreach (var prop in zoomProperties)
        {
            if (node.Properties.ContainsKey(prop))
                return true;
        }

        // Map controls typically have pinch-to-zoom
        if (node.Type.Equals("Map", StringComparison.OrdinalIgnoreCase) ||
            node.Type.Equals("MapControl", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool HasZoomButtons(UiNode node, RuleContext context)
    {
        // Check siblings for zoom controls
        foreach (var sibling in context.Siblings)
        {
            var name = sibling.Name?.ToLowerInvariant() ?? "";
            var text = sibling.Text?.ToLowerInvariant() ?? "";
            
            if (name.Contains("zoom") || text.Contains("zoom") ||
                name.Contains("+") || name.Contains("-") ||
                text.Contains("+") || text.Contains("-"))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasRotationGesture(UiNode node)
    {
        if (node.Properties == null) return false;

        var rotationProperties = new[] { "Rotation", "OnRotate", "EnableRotation", "RotationAngle" };
        foreach (var prop in rotationProperties)
        {
            if (node.Properties.ContainsKey(prop))
                return true;
        }
        return false;
    }

    private static bool HasImmediateDestructiveAction(string action)
    {
        if (string.IsNullOrWhiteSpace(action)) return false;

        var destructivePatterns = new[]
        {
            "Remove(", "RemoveIf(", "Clear(", "ClearCollect(",
            "Delete", "Destroy", "Reset(", "Navigate(",
            "Launch(", "Exit(", "Submit("
        };

        foreach (var pattern in destructivePatterns)
        {
            if (action.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                // Check if there's a confirmation pattern
                if (action.Contains("Confirm", StringComparison.OrdinalIgnoreCase) ||
                    action.Contains("Dialog", StringComparison.OrdinalIgnoreCase) ||
                    action.Contains("Notify", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                return true;
            }
        }
        return false;
    }
}
