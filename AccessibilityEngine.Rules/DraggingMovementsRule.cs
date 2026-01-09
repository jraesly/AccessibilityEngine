using System;
using System.Collections.Generic;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for dragging movement accessibility issues.
/// WCAG 2.5.7 (New in 2.2): Functionality that uses dragging must have single-pointer alternatives.
/// </summary>
public sealed class DraggingMovementsRule : IRule
{
    public string Id => "DRAGGING_MOVEMENTS";
    public string Description => "Functionality using dragging must have single-pointer alternatives.";
    public Severity Severity => Severity.High;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Control types that commonly use drag operations
    private static readonly HashSet<string> DraggableControlTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Slider", "RangeSlider", "Range slider",
        "Gallery", "DataTable", "DataGrid",
        "Canvas", "Shape", "Rectangle", "Circle", "Icon",
        "Image", "Component", "PCFControl", "Custom",
        "TreeView", "Tree view", "SortableList"
    };

    // Properties that indicate drag functionality
    private static readonly string[] DragProperties =
    [
        "OnDrag", "OnDragStart", "OnDragEnd", "OnDragOver", "OnDrop",
        "Draggable", "EnableDrag", "AllowDrag", "IsDraggable",
        "OnReorder", "OnSort", "Sortable"
    ];

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check for drag-enabled controls
        foreach (var finding in CheckDragControls(node, context))
            yield return finding;

        // Check for slider controls
        foreach (var finding in CheckSliderControls(node, context))
            yield return finding;

        // Check for reorderable lists
        foreach (var finding in CheckReorderableControls(node, context))
            yield return finding;

        // Check for drag-to-select functionality
        foreach (var finding in CheckDragToSelect(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckDragControls(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        foreach (var dragProperty in DragProperties)
        {
            if (node.Properties.TryGetValue(dragProperty, out var handler) && handler != null)
            {
                var hasAlternative = HasDragAlternative(node, context);

                if (!hasAlternative)
                {
                    yield return new Finding(
                        Id: $"{Id}:DRAG:{node.Id}:{dragProperty}",
                        Severity: Severity.High,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_NO_ALTERNATIVE",
                        Message: $"Control '{node.Id}' uses drag functionality ('{dragProperty}') without a single-pointer alternative.",
                        WcagReference: "WCAG 2.2 § 2.5.7 Dragging Movements (Level AA)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Users who operate with a single pointer (e.g., head wand, eye tracking, single switch) cannot perform drag operations.",
                        SuggestedFix: $"For '{node.Id}', add alternatives: (1) Click-to-select then click-to-place, (2) Arrow key movement, (3) Input field for position/value, (4) Up/down buttons.",
                        WcagCriterion: WcagCriterion.DraggingMovements_2_5_7
                    );
                }
            }
        }
    }

    private IEnumerable<Finding> CheckSliderControls(UiNode node, RuleContext context)
    {
        var isSlider = node.Type.Contains("Slider", StringComparison.OrdinalIgnoreCase) ||
                       node.Type.Contains("Range", StringComparison.OrdinalIgnoreCase);

        if (!isSlider) yield break;

        // Sliders inherently require dragging - check for alternatives
        var hasValueInput = HasValueInputAlternative(node, context);
        var hasStepButtons = HasStepButtonsAlternative(node, context);

        if (!hasValueInput && !hasStepButtons)
        {
            yield return new Finding(
                Id: $"{Id}:SLIDER:{node.Id}",
                Severity: Severity.High,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_SLIDER",
                Message: $"Slider control '{node.Id}' requires dragging. Provide an alternative input method (text input or step buttons).",
                WcagReference: "WCAG 2.2 § 2.5.7 Dragging Movements (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Sliders require precise drag movements that are difficult or impossible for users with motor impairments.",
                SuggestedFix: $"Add to '{node.Id}': (1) A text input showing the current value that users can edit directly, OR (2) +/- buttons or increment/decrement buttons to step through values.",
                WcagCriterion: WcagCriterion.DraggingMovements_2_5_7
            );
        }

        // Even with alternatives, verify keyboard support
        var hasKeyboardSupport = HasKeyboardSupport(node);
        if (!hasKeyboardSupport)
        {
            yield return new Finding(
                Id: $"{Id}:SLIDER_KEYBOARD:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_SLIDER_KEYBOARD",
                Message: $"Verify slider '{node.Id}' supports keyboard operation (arrow keys to adjust value).",
                WcagReference: "WCAG 2.2 § 2.5.7 Dragging Movements (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Sliders should be operable via arrow keys as a standard alternative to dragging.",
                SuggestedFix: $"Ensure '{node.Id}' responds to left/right (or up/down) arrow keys when focused. Add OnKeyDown handler if needed.",
                WcagCriterion: WcagCriterion.DraggingMovements_2_5_7
            );
        }
    }

    private IEnumerable<Finding> CheckReorderableControls(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check for reorderable/sortable lists
        var isReorderable = node.Properties.ContainsKey("OnReorder") ||
                           node.Properties.ContainsKey("Sortable") ||
                           node.Properties.TryGetValue("AllowReorder", out var reorder) &&
                           (reorder is true || reorder?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true);

        if (!isReorderable) yield break;

        var hasMoveButtons = HasMoveButtonsAlternative(node, context);

        if (!hasMoveButtons)
        {
            yield return new Finding(
                Id: $"{Id}:REORDER:{node.Id}",
                Severity: Severity.High,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_REORDER",
                Message: $"Reorderable control '{node.Id}' uses drag-to-reorder. Provide move up/down buttons as alternatives.",
                WcagReference: "WCAG 2.2 § 2.5.7 Dragging Movements (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Drag-to-reorder requires continuous pointer movement that is inaccessible to many users with motor disabilities.",
                SuggestedFix: $"For '{node.Id}', add 'Move Up' and 'Move Down' buttons for each item, or provide a mechanism to select an item and then choose its new position.",
                WcagCriterion: WcagCriterion.DraggingMovements_2_5_7
            );
        }
    }

    private IEnumerable<Finding> CheckDragToSelect(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check for drag selection (marquee select, etc.)
        var hasDragSelect = node.Properties.ContainsKey("OnDragSelect") ||
                           node.Properties.ContainsKey("EnableMarqueeSelect") ||
                           node.Properties.ContainsKey("SelectionMode") &&
                           node.Properties["SelectionMode"]?.ToString()?.Contains("Drag", StringComparison.OrdinalIgnoreCase) == true;

        if (!hasDragSelect) yield break;

        yield return new Finding(
            Id: $"{Id}:DRAG_SELECT:{node.Id}",
            Severity: Severity.Medium,
            Surface: context.Surface,
            AppName: context.AppName,
            Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
            ControlId: node.Id,
            ControlType: node.Type,
            IssueType: $"{Id}_DRAG_SELECT",
            Message: $"Control '{node.Id}' may use drag-to-select (marquee selection). Ensure click-to-select and Shift+click range selection are available.",
            WcagReference: "WCAG 2.2 § 2.5.7 Dragging Movements (Level AA)",
            Section508Reference: "Section 508 E207.2 - WCAG Conformance",
            Rationale: "Marquee/drag selection must have alternatives like click-to-select individual items and Shift+click for range selection.",
            SuggestedFix: $"Ensure '{node.Id}' supports: (1) Click to select single items, (2) Shift+click for range selection, (3) Ctrl+click for multi-select, (4) 'Select All' option.",
            WcagCriterion: WcagCriterion.DraggingMovements_2_5_7
        );
    }

    private static bool HasDragAlternative(UiNode node, RuleContext context)
    {
        // Check siblings for alternative controls
        foreach (var sibling in context.Siblings)
        {
            var siblingName = sibling.Name?.ToLowerInvariant() ?? "";
            var siblingText = sibling.Text?.ToLowerInvariant() ?? "";
            var siblingType = sibling.Type.ToLowerInvariant();

            // Look for buttons that might be alternatives
            if (siblingType.Contains("button"))
            {
                if (siblingName.Contains("move") || siblingText.Contains("move") ||
                    siblingName.Contains("up") || siblingText.Contains("up") ||
                    siblingName.Contains("down") || siblingText.Contains("down") ||
                    siblingName.Contains("left") || siblingText.Contains("left") ||
                    siblingName.Contains("right") || siblingText.Contains("right") ||
                    siblingName.Contains("position") || siblingText.Contains("position"))
                {
                    return true;
                }
            }

            // Look for text input for value/position
            if (siblingType.Contains("input") || siblingType.Contains("text"))
            {
                if (siblingName.Contains("value") || siblingName.Contains("position") ||
                    siblingName.Contains("coord") || siblingName.Contains("location"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasValueInputAlternative(UiNode node, RuleContext context)
    {
        foreach (var sibling in context.Siblings)
        {
            var siblingType = sibling.Type.ToLowerInvariant();
            var siblingName = sibling.Name?.ToLowerInvariant() ?? "";
            var nodeName = node.Name?.ToLowerInvariant() ?? "";

            // Look for text input that might be linked to the slider
            if (siblingType.Contains("input") || siblingType.Contains("text"))
            {
                // Check if names are related
                if (!string.IsNullOrEmpty(nodeName) && siblingName.Contains(nodeName.Replace("slider", "")))
                    return true;

                // Check for common value input patterns
                if (siblingName.Contains("value") || siblingName.Contains("amount") ||
                    siblingName.Contains("number") || siblingName.Contains("quantity"))
                    return true;
            }
        }

        return false;
    }

    private static bool HasStepButtonsAlternative(UiNode node, RuleContext context)
    {
        var hasIncrease = false;
        var hasDecrease = false;

        foreach (var sibling in context.Siblings)
        {
            var siblingType = sibling.Type.ToLowerInvariant();
            var siblingName = sibling.Name?.ToLowerInvariant() ?? "";
            var siblingText = sibling.Text?.ToLowerInvariant() ?? "";

            if (siblingType.Contains("button") || siblingType.Contains("icon"))
            {
                // Check for increase/plus buttons
                if (siblingName.Contains("plus") || siblingText.Contains("+") ||
                    siblingName.Contains("increase") || siblingText.Contains("increase") ||
                    siblingName.Contains("add") || siblingName.Contains("up"))
                {
                    hasIncrease = true;
                }

                // Check for decrease/minus buttons
                if (siblingName.Contains("minus") || siblingText.Contains("-") ||
                    siblingName.Contains("decrease") || siblingText.Contains("decrease") ||
                    siblingName.Contains("subtract") || siblingName.Contains("down"))
                {
                    hasDecrease = true;
                }
            }
        }

        return hasIncrease && hasDecrease;
    }

    private static bool HasMoveButtonsAlternative(UiNode node, RuleContext context)
    {
        var hasMoveUp = false;
        var hasMoveDown = false;

        foreach (var sibling in context.Siblings)
        {
            var siblingType = sibling.Type.ToLowerInvariant();
            var siblingName = sibling.Name?.ToLowerInvariant() ?? "";
            var siblingText = sibling.Text?.ToLowerInvariant() ?? "";

            if (siblingType.Contains("button") || siblingType.Contains("icon"))
            {
                if ((siblingName.Contains("move") && siblingName.Contains("up")) ||
                    siblingText.Contains("move up") ||
                    siblingName.Contains("moveup"))
                {
                    hasMoveUp = true;
                }

                if ((siblingName.Contains("move") && siblingName.Contains("down")) ||
                    siblingText.Contains("move down") ||
                    siblingName.Contains("movedown"))
                {
                    hasMoveDown = true;
                }
            }
        }

        return hasMoveUp && hasMoveDown;
    }

    private static bool HasKeyboardSupport(UiNode node)
    {
        if (node.Properties == null) return false;

        // Check for keyboard event handlers
        var keyboardHandlers = new[] { "OnKeyDown", "OnKeyUp", "OnKeyPress" };
        foreach (var handler in keyboardHandlers)
        {
            if (node.Properties.ContainsKey(handler))
                return true;
        }

        // Standard slider controls typically support keyboard by default
        if (node.Type.Contains("Slider", StringComparison.OrdinalIgnoreCase) &&
            !node.Type.Contains("Custom", StringComparison.OrdinalIgnoreCase) &&
            !node.Type.Contains("PCF", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
