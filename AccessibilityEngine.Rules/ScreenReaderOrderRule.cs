using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for logical reading/navigation order based on control positions.
/// Ensures screen reader and keyboard navigation follows visual layout.
/// </summary>
public sealed class ScreenReaderOrderRule : IRule
{
    public string Id => "SCREEN_READER_ORDER";
    public string Description => "Controls should follow a logical reading order (left-to-right, top-to-bottom).";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp];

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Only check at the screen level (check all children at once)
        if (!node.Type.Equals("Screen", StringComparison.OrdinalIgnoreCase))
            yield break;

        // Get all focusable children
        var focusableControls = GetFocusableDescendants(node).ToList();
        
        if (focusableControls.Count < 2)
            yield break;

        // Check for ZIndex-based ordering issues
        var zIndexIssues = CheckZIndexOrdering(focusableControls, context);
        foreach (var finding in zIndexIssues)
            yield return finding;

        // Check for visual vs DOM order mismatches
        var orderIssues = CheckVisualVsDomOrder(focusableControls, context);
        foreach (var finding in orderIssues)
            yield return finding;
    }

    private static IEnumerable<UiNode> GetFocusableDescendants(UiNode node)
    {
        if (IsFocusable(node) && !node.Type.Equals("Screen", StringComparison.OrdinalIgnoreCase))
        {
            yield return node;
        }

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                foreach (var descendant in GetFocusableDescendants(child))
                {
                    yield return descendant;
                }
            }
        }
    }

    private static bool IsFocusable(UiNode node)
    {
        var focusableTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Button", "IconButton", "Classic/Button",
            "TextInput", "Text input", "Classic/TextInput",
            "ComboBox", "Combo box", "Classic/ComboBox",
            "Dropdown", "Drop down", "Classic/Dropdown",
            "DatePicker", "Date picker", "Classic/DatePicker",
            "Slider", "Classic/Slider",
            "Toggle", "Classic/Toggle",
            "Rating", "Classic/Rating",
            "ListBox", "List box", "Classic/ListBox",
            "Radio", "RadioGroup", "Classic/Radio",
            "Checkbox", "Check box", "Classic/Checkbox",
            "Link", "HyperLink"
        };

        return focusableTypes.Contains(node.Type);
    }

    private IEnumerable<Finding> CheckZIndexOrdering(List<UiNode> controls, RuleContext context)
    {
        // Group controls by approximate row (Y position within tolerance)
        const int rowTolerance = 20; // pixels
        
        var rows = new List<List<(UiNode Node, int X, int Y, int ZIndex)>>();
        
        foreach (var control in controls)
        {
            var x = GetPosition(control, "X");
            var y = GetPosition(control, "Y");
            var zIndex = GetZIndex(control);

            if (x < 0 || y < 0) continue;

            var item = (control, x, y, zIndex);
            
            // Find matching row
            var foundRow = false;
            foreach (var row in rows)
            {
                if (row.Count > 0 && Math.Abs(row[0].Y - y) <= rowTolerance)
                {
                    row.Add(item);
                    foundRow = true;
                    break;
                }
            }
            
            if (!foundRow)
            {
                rows.Add(new List<(UiNode, int, int, int)> { item });
            }
        }

        // Check each row for ZIndex issues
        foreach (var row in rows)
        {
            if (row.Count < 2) continue;

            // Sort by visual order (X position)
            var visualOrder = row.OrderBy(c => c.X).ToList();
            
            // Sort by ZIndex
            var zIndexOrder = row.OrderBy(c => c.ZIndex).ToList();

            // Check if ZIndex order matches visual order
            for (int i = 0; i < visualOrder.Count; i++)
            {
                var visualItem = visualOrder[i];
                var zIndexPosition = zIndexOrder.FindIndex(c => c.Node.Id == visualItem.Node.Id);

                if (Math.Abs(i - zIndexPosition) > 1)
                {
                    yield return new Finding(
                        Id: $"{Id}:ZINDEX:{visualItem.Node.Id}",
                        Severity: Severity.Medium,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: context.Screen?.Id ?? visualItem.Node.Meta?.ScreenName,
                        ControlId: visualItem.Node.Id,
                        ControlType: visualItem.Node.Type,
                        IssueType: $"{Id}_ZINDEX",
                        Message: $"Control '{visualItem.Node.Id}' has ZIndex ({visualItem.ZIndex}) that may cause keyboard navigation to differ from visual order.",
                        WcagReference: "WCAG 2.1 – 2.4.3 Focus Order",
                        Section508Reference: "Section 508 - Focus Order",
                        Rationale: "ZIndex affects tab order in some platforms. Ensure it matches the visual left-to-right, top-to-bottom order.",
                        SuggestedFix: $"Adjust the ZIndex property on '{visualItem.Node.Id}' so the tab order matches the visual layout, or restructure the controls so they appear in the correct DOM order."
                    );
                }
            }
        }
    }

    private IEnumerable<Finding> CheckVisualVsDomOrder(List<UiNode> controls, RuleContext context)
    {
        if (controls.Count < 2) yield break;

        // Get controls with positions
        var positionedControls = controls
            .Select(c => (Node: c, X: GetPosition(c, "X"), Y: GetPosition(c, "Y")))
            .Where(c => c.X >= 0 && c.Y >= 0)
            .ToList();

        if (positionedControls.Count < 2) yield break;

        // Sort by visual order (top-to-bottom, left-to-right)
        var visualOrder = positionedControls
            .OrderBy(c => c.Y / 20 * 20) // Group by rows (20px tolerance)
            .ThenBy(c => c.X)
            .Select(c => c.Node.Id)
            .ToList();

        // DOM order (as they appear in the tree)
        var domOrder = controls.Select(c => c.Id).ToList();

        // Find significant order mismatches
        for (int i = 0; i < visualOrder.Count; i++)
        {
            var visualId = visualOrder[i];
            var domPosition = domOrder.IndexOf(visualId);

            // Allow some tolerance for minor differences
            if (Math.Abs(i - domPosition) > 2)
            {
                var node = controls.First(c => c.Id == visualId);
                yield return new Finding(
                    Id: $"{Id}:ORDER:{visualId}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: context.Screen?.Id ?? node.Meta?.ScreenName,
                    ControlId: visualId,
                    ControlType: node.Type,
                    IssueType: $"{Id}_ORDER",
                    Message: $"Control '{visualId}' appears at visual position {i + 1} but DOM position {domPosition + 1}. Screen reader order may not match visual layout.",
                    WcagReference: "WCAG 2.1 – 1.3.2 Meaningful Sequence",
                    Section508Reference: "Section 508 - Reading Order",
                    Rationale: "Screen readers navigate in DOM order, which should match the visual reading order.",
                    SuggestedFix: $"Reorder control '{visualId}' in the control tree so its position in the editor matches the visual left-to-right, top-to-bottom reading order."
                );
            }
        }
    }

    private static int GetPosition(UiNode node, string property)
    {
        if (node.Properties?.TryGetValue(property, out var value) == true)
        {
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is double d) return (int)d;
            if (value is string s && int.TryParse(s, out var parsed)) return parsed;
        }
        return -1;
    }

    private static int GetZIndex(UiNode node)
    {
        if (node.Properties?.TryGetValue("ZIndex", out var value) == true)
        {
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is string s && int.TryParse(s, out var parsed)) return parsed;
        }
        return 0;
    }
}
