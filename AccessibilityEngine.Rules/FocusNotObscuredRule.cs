using System;
using System.Collections.Generic;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for focus not obscured issues.
/// WCAG 2.4.11 (New in 2.2): When a component receives keyboard focus, it must not be entirely hidden by author-created content.
/// </summary>
public sealed class FocusNotObscuredRule : IRule
{
    public string Id => "FOCUS_NOT_OBSCURED";
    public string Description => "When a component receives keyboard focus, it must not be entirely hidden by other content.";
    public Severity Severity => Severity.High;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Control types that might obscure other content
    private static readonly HashSet<string> OverlayTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Header", "Footer", "Navigation", "Nav", "Navbar", "NavBar",
        "Sidebar", "SidePanel", "Panel", "Toolbar", "StatusBar",
        "Banner", "Notification", "Toast", "Alert", "Snackbar",
        "Modal", "Dialog", "Popup", "Overlay", "Drawer",
        "Cookie", "Chat", "ChatWidget", "Fab", "FloatingButton"
    };

    // Control types that are focusable
    private static readonly HashSet<string> FocusableTypes = new(StringComparer.OrdinalIgnoreCase)
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

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check for sticky/fixed positioned elements that might obscure content
        foreach (var finding in CheckStickyElements(node, context))
            yield return finding;

        // Check for high Z-index elements that might overlay
        foreach (var finding in CheckHighZIndexElements(node, context))
            yield return finding;

        // Check for modal/overlay patterns
        foreach (var finding in CheckModalOverlays(node, context))
            yield return finding;

        // Check for chat widgets and floating buttons
        foreach (var finding in CheckFloatingWidgets(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckStickyElements(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check for sticky/fixed positioning
        var hasFixedPosition = false;
        var positionProperty = "";

        var fixedPositionProps = new[] { "Position", "Sticky", "Fixed", "Docked" };
        foreach (var prop in fixedPositionProps)
        {
            if (node.Properties.TryGetValue(prop, out var value))
            {
                var posValue = value?.ToString()?.ToLowerInvariant() ?? "";
                if (posValue.Contains("fixed") || posValue.Contains("sticky") ||
                    posValue.Contains("docked") || posValue.Contains("true"))
                {
                    hasFixedPosition = true;
                    positionProperty = prop;
                    break;
                }
            }
        }

        // Also check common header/footer patterns
        if (!hasFixedPosition)
        {
            var name = node.Name?.ToLowerInvariant() ?? "";
            if (IsOverlayType(node.Type) || IsOverlayType(name))
            {
                // Check if positioned at top or bottom of screen
                if (node.Properties.TryGetValue("Y", out var y))
                {
                    var yValue = y?.ToString() ?? "";
                    if (yValue == "0" || yValue.Contains("App.Height", StringComparison.OrdinalIgnoreCase))
                    {
                        hasFixedPosition = true;
                        positionProperty = "position";
                    }
                }
            }
        }

        if (hasFixedPosition)
        {
            yield return new Finding(
                Id: $"{Id}:STICKY:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_STICKY",
                Message: $"Element '{node.Id}' appears to be sticky/fixed positioned. Ensure it doesn't entirely obscure focused elements.",
                WcagReference: "WCAG 2.2 § 2.4.11 Focus Not Obscured (Minimum) (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Sticky headers, footers, or banners may cover elements as users tab through the page. At minimum, focused elements must be partially visible.",
                SuggestedFix: $"For '{node.Id}': (1) Consider auto-scrolling content when focus moves behind sticky elements, (2) Use scroll-padding CSS equivalent to account for sticky element height, (3) Ensure focused element is at least partially visible.",
                WcagCriterion: WcagCriterion.FocusNotObscuredMinimum_2_4_11
            );
        }
    }

    private IEnumerable<Finding> CheckHighZIndexElements(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check for high Z-index that might cause overlapping
        if (node.Properties.TryGetValue("ZIndex", out var zIndex))
        {
            var zValue = 0;
            if (zIndex is int i) zValue = i;
            else if (zIndex is long l) zValue = (int)l;
            else if (zIndex is string s && int.TryParse(s, out var parsed)) zValue = parsed;

            // High Z-index indicates element may overlay others
            if (zValue > 100)
            {
                // Check if this is an overlay-type element
                var isOverlay = IsOverlayType(node.Type) || IsOverlayType(node.Name ?? "");

                if (isOverlay)
                {
                    yield return new Finding(
                        Id: $"{Id}:HIGH_ZINDEX:{node.Id}",
                        Severity: Severity.Medium,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_HIGH_ZINDEX",
                        Message: $"Element '{node.Id}' has high ZIndex ({zValue}) and may obscure focused content below it.",
                        WcagReference: "WCAG 2.2 § 2.4.11 Focus Not Obscured (Minimum) (Level AA)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "High Z-index elements like overlays, modals, and floating widgets may cover focusable elements during keyboard navigation.",
                        SuggestedFix: $"Ensure '{node.Id}' doesn't entirely cover focused elements. Consider: (1) Making the overlay smaller, (2) Positioning it to avoid main content area, (3) Auto-hiding when focus moves to covered content.",
                        WcagCriterion: WcagCriterion.FocusNotObscuredMinimum_2_4_11
                    );
                }
            }
        }
    }

    private IEnumerable<Finding> CheckModalOverlays(UiNode node, RuleContext context)
    {
        var modalTypes = new[] { "Modal", "Dialog", "Popup", "Overlay", "Lightbox" };
        
        var isModal = false;
        foreach (var modalType in modalTypes)
        {
            if (node.Type.Contains(modalType, StringComparison.OrdinalIgnoreCase) ||
                node.Name?.Contains(modalType, StringComparison.OrdinalIgnoreCase) == true)
            {
                isModal = true;
                break;
            }
        }

        if (!isModal) yield break;

        // Check if modal traps focus appropriately
        var trapsFocus = HasFocusTrap(node);
        
        if (!trapsFocus)
        {
            yield return new Finding(
                Id: $"{Id}:MODAL_FOCUS:{node.Id}",
                Severity: Severity.High,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_MODAL_FOCUS",
                Message: $"Modal/dialog '{node.Id}' may allow focus to move to obscured content behind it. Implement focus trapping.",
                WcagReference: "WCAG 2.2 § 2.4.11 Focus Not Obscured (Minimum) (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "When a modal is open, focus should be trapped within it. Allowing focus to move to obscured content behind the modal violates focus visibility.",
                SuggestedFix: $"For modal '{node.Id}': (1) Trap focus within the modal when open, (2) Set tabindex=-1 on background content, (3) Return focus to trigger element when modal closes.",
                WcagCriterion: WcagCriterion.FocusNotObscuredMinimum_2_4_11
            );
        }

        // Check for backdrop/overlay that might not be interactive
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                if (child.Name?.Contains("backdrop", StringComparison.OrdinalIgnoreCase) == true ||
                    child.Name?.Contains("overlay", StringComparison.OrdinalIgnoreCase) == true ||
                    child.Type.Contains("Rectangle", StringComparison.OrdinalIgnoreCase))
                {
                    // Check if backdrop is clickable to close
                    var hasCloseAction = child.Properties?.ContainsKey("OnSelect") == true;
                    
                    if (!hasCloseAction)
                    {
                        yield return new Finding(
                            Id: $"{Id}:BACKDROP:{child.Id}",
                            Severity: Severity.Low,
                            Surface: context.Surface,
                            AppName: context.AppName,
                            Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                            ControlId: child.Id,
                            ControlType: child.Type,
                            IssueType: $"{Id}_BACKDROP",
                            Message: $"Modal backdrop '{child.Id}' is not clickable to dismiss. Consider adding click-to-close for easier dismissal.",
                            WcagReference: "WCAG 2.2 § 2.4.11 Focus Not Obscured (Minimum) (Level AA)",
                            Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                            Rationale: "Allowing users to click outside a modal to close it provides an intuitive way to dismiss overlays that may be obscuring content.",
                            SuggestedFix: $"Add OnSelect handler to backdrop '{child.Id}' that closes the modal, in addition to the close button.",
                            WcagCriterion: WcagCriterion.FocusNotObscuredMinimum_2_4_11
                        );
                    }
                }
            }
        }
    }

    private IEnumerable<Finding> CheckFloatingWidgets(UiNode node, RuleContext context)
    {
        // Check for chat widgets, FABs, cookie notices, etc.
        var floatingTypes = new[] { "Chat", "ChatWidget", "Fab", "FloatingAction", "CookieBanner", "Cookie", "Notification" };
        
        var isFloating = false;
        var floatingType = "";
        
        foreach (var ft in floatingTypes)
        {
            if (node.Type.Contains(ft, StringComparison.OrdinalIgnoreCase) ||
                node.Name?.Contains(ft, StringComparison.OrdinalIgnoreCase) == true)
            {
                isFloating = true;
                floatingType = ft;
                break;
            }
        }

        if (!isFloating) yield break;

        // Check position - typically bottom-right corner
        var x = GetDimension(node, "X");
        var y = GetDimension(node, "Y");
        var width = GetDimension(node, "Width");
        var height = GetDimension(node, "Height");

        yield return new Finding(
            Id: $"{Id}:FLOATING:{node.Id}",
            Severity: Severity.Medium,
            Surface: context.Surface,
            AppName: context.AppName,
            Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
            ControlId: node.Id,
            ControlType: node.Type,
            IssueType: $"{Id}_FLOATING",
            Message: $"Floating widget '{node.Id}' ({floatingType}) may obscure focusable content. Ensure it can be minimized or doesn't cover interactive elements.",
            WcagReference: "WCAG 2.2 § 2.4.11 Focus Not Obscured (Minimum) (Level AA)",
            Section508Reference: "Section 508 E207.2 - WCAG Conformance",
            Rationale: "Floating widgets like chat buttons, cookie notices, and action buttons can cover content as users tab through the page.",
            SuggestedFix: $"For floating widget '{node.Id}': (1) Allow users to minimize/dismiss it, (2) Position it to avoid covering main interactive content, (3) Consider auto-hiding when focus moves to covered elements.",
            WcagCriterion: WcagCriterion.FocusNotObscuredMinimum_2_4_11
        );
    }

    private static bool IsOverlayType(string typeOrName)
    {
        if (string.IsNullOrEmpty(typeOrName)) return false;
        
        foreach (var overlayType in OverlayTypes)
        {
            if (typeOrName.Contains(overlayType, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool HasFocusTrap(UiNode node)
    {
        if (node.Properties == null) return false;

        // Check for focus trap indicators
        var focusTrapProps = new[] { "TrapFocus", "FocusTrap", "Modal", "InertBackground" };
        foreach (var prop in focusTrapProps)
        {
            if (node.Properties.TryGetValue(prop, out var value))
            {
                if (value is true || value?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
                    return true;
            }
        }

        // Check for OnKeyDown that might handle Tab key for trapping
        if (node.Properties.TryGetValue("OnKeyDown", out var keyHandler))
        {
            var handler = keyHandler?.ToString() ?? "";
            if (handler.Contains("Tab", StringComparison.OrdinalIgnoreCase) ||
                handler.Contains("Focus", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int GetDimension(UiNode node, string property)
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
}
