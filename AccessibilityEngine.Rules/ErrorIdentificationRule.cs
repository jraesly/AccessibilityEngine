using System;
using System.Collections.Generic;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for proper error identification and messaging in forms.
/// WCAG 3.3.1: If an input error is automatically detected, the item is identified and described in text.
/// WCAG 3.3.3: If an error is detected and suggestions are known, they are provided to the user.
/// </summary>
public sealed class ErrorIdentificationRule : IRule
{
    public string Id => "ERROR_IDENTIFICATION";
    public string Description => "Input errors must be identified and described in text accessible to all users.";
    public Severity Severity => Severity.High;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage, SurfaceType.DomSnapshot];

    // Input control types that can have validation errors
    private static readonly HashSet<string> InputTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "TextInput", "Text input", "Classic/TextInput",
        "ComboBox", "Combo box", "Classic/ComboBox",
        "Dropdown", "Drop down", "Classic/Dropdown",
        "DatePicker", "Date picker", "Classic/DatePicker",
        "Slider", "Classic/Slider",
        "Rating", "Classic/Rating",
        "Checkbox", "Check box", "Classic/Checkbox",
        "Radio", "RadioGroup", "Classic/Radio",
        "Toggle", "Classic/Toggle",
        "PenInput", "Pen input",
        "RichTextEditor", "Rich text editor",
        "Attachments", "Attachment"
    };

    // Form container types
    private static readonly HashSet<string> FormTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "EditForm", "Edit form", "DisplayForm", "Display form", "Form"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check input controls for error handling
        if (InputTypes.Contains(node.Type))
        {
            // Check for validation/error handling
            var hasErrorHandling = HasErrorHandling(node);
            var hasAccessibleErrorMessage = HasAccessibleErrorMessage(node);

            if (hasErrorHandling && !hasAccessibleErrorMessage)
            {
                yield return new Finding(
                    Id: $"{Id}:INPUT:{node.Id}",
                    Severity: Severity.High,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_MISSING_ERROR_TEXT",
                    Message: $"Input control '{node.Id}' has validation but may not provide accessible error messages. Screen reader users may not be informed of errors.",
                    WcagReference: "WCAG 2.2 § 3.3.1 Error Identification (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "When an input error is automatically detected, the item that is in error must be identified and the error described to the user in text.",
                    SuggestedFix: $"Add a Label control near '{node.Id}' that displays error messages, and ensure errors are announced to screen readers (e.g., using Live property or AccessibleLabel updates).",
                    WcagCriterion: WcagCriterion.ErrorIdentification_3_3_1
                );
            }

            // Check for color-only error indication
            if (HasColorOnlyError(node))
            {
                yield return new Finding(
                    Id: $"{Id}:COLOR_ONLY:{node.Id}",
                    Severity: Severity.High,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_COLOR_ONLY_ERROR",
                    Message: $"Control '{node.Id}' may indicate errors using only color changes. Add text or icons for users who cannot perceive color.",
                    WcagReference: "WCAG 2.2 § 1.4.1 Use of Color (Level A)",
                    Section508Reference: "Section 508 E207.2 - Use of Color",
                    Rationale: "Color must not be the only visual means of conveying information, indicating an action, or distinguishing elements.",
                    SuggestedFix: $"Add an error icon (like a warning symbol) and/or text message alongside the color change on '{node.Id}' to indicate errors.",
                    WcagCriterion: WcagCriterion.UseOfColor_1_4_1
                );
            }

            // Check for required field indicators
            if (IsRequiredField(node) && !HasRequiredIndicator(node))
            {
                yield return new Finding(
                    Id: $"{Id}:REQUIRED:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_REQUIRED_NOT_INDICATED",
                    Message: $"Required field '{node.Id}' may not clearly indicate it is required to screen reader users.",
                    WcagReference: "WCAG 2.2 § 3.3.2 Labels or Instructions (Level A)",
                    Section508Reference: "Section 508 E207.2 - Labels or Instructions",
                    Rationale: "Required fields must be identifiable to all users, including those using screen readers.",
                    SuggestedFix: $"Include 'required' or '(required)' in the label text or AccessibleLabel for '{node.Id}'. Do not rely solely on asterisks (*) or color to indicate required status.",
                    WcagCriterion: WcagCriterion.LabelsOrInstructions_3_3_2
                );
            }
        }

        // Check form containers for overall error summary
        if (FormTypes.Contains(node.Type))
        {
            var hasErrorSummary = HasErrorSummary(node, context);
            
            if (!hasErrorSummary)
            {
                yield return new Finding(
                    Id: $"{Id}:FORM:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_NO_ERROR_SUMMARY",
                    Message: $"Form '{node.Id}' does not appear to have an error summary. Consider adding one to help users identify all errors at once.",
                    WcagReference: "WCAG 2.2 § 3.3.1 Error Identification (Level A)",
                    Section508Reference: "Section 508 E207.2 - Error Identification",
                    Rationale: "An error summary helps users quickly understand what needs to be corrected, especially in long forms.",
                    SuggestedFix: $"Add a Label at the top or bottom of form '{node.Id}' that lists all validation errors when the form fails validation. This label should be announced to screen readers.",
                    WcagCriterion: WcagCriterion.ErrorIdentification_3_3_1
                );
            }
        }
    }

    private static bool HasErrorHandling(UiNode node)
    {
        if (node.Properties == null) return false;

        // Check for validation-related properties
        var validationProps = new[] { "OnChange", "Valid", "IsValid", "Validate", "Required", "MaxLength", "MinLength", "Pattern" };
        
        foreach (var prop in validationProps)
        {
            if (node.Properties.TryGetValue(prop, out var val))
            {
                var strVal = val?.ToString();
                if (!string.IsNullOrWhiteSpace(strVal))
                {
                    // Check if it contains validation logic
                    if (strVal.Contains("If(", StringComparison.OrdinalIgnoreCase) ||
                        strVal.Contains("IsBlank", StringComparison.OrdinalIgnoreCase) ||
                        strVal.Contains("IsMatch", StringComparison.OrdinalIgnoreCase) ||
                        strVal.Contains("Required", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool HasAccessibleErrorMessage(UiNode node)
    {
        if (node.Properties == null) return false;

        // Check for error message properties
        var errorProps = new[] { "ErrorMessage", "ValidationMessage", "HintText", "AccessibleLabel" };
        
        foreach (var prop in errorProps)
        {
            if (node.Properties.TryGetValue(prop, out var val))
            {
                var strVal = val?.ToString();
                if (!string.IsNullOrWhiteSpace(strVal) && 
                    (strVal.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                     strVal.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
                     strVal.Contains("required", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasColorOnlyError(UiNode node)
    {
        if (node.Properties == null) return false;

        // Check for conditional color that might indicate errors
        var colorProps = new[] { "Color", "Fill", "BorderColor" };
        
        foreach (var prop in colorProps)
        {
            if (node.Properties.TryGetValue(prop, out var val))
            {
                var strVal = val?.ToString();
                if (strVal != null && 
                    strVal.Contains("If(", StringComparison.OrdinalIgnoreCase) &&
                    (strVal.Contains("Red", StringComparison.OrdinalIgnoreCase) ||
                     strVal.Contains("255,0,0", StringComparison.OrdinalIgnoreCase) ||
                     strVal.Contains("error", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsRequiredField(UiNode node)
    {
        if (node.Properties == null) return false;

        // Check Required property
        if (node.Properties.TryGetValue("Required", out var req))
        {
            var reqStr = req?.ToString();
            return reqStr == "true" || reqStr == "True" || req is bool b && b;
        }

        return false;
    }

    private static bool HasRequiredIndicator(UiNode node)
    {
        // Check if the control's accessible label or name indicates required status
        var accessibleLabel = node.Properties?.GetValueOrDefault("AccessibleLabel")?.ToString();
        var label = node.Properties?.GetValueOrDefault("Label")?.ToString();
        var name = node.Name;

        var textsToCheck = new[] { accessibleLabel, label, name };
        
        foreach (var text in textsToCheck)
        {
            if (text != null && 
                (text.Contains("required", StringComparison.OrdinalIgnoreCase) ||
                 text.Contains("mandatory", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasErrorSummary(UiNode node, RuleContext context)
    {
        // Check siblings for error summary labels
        foreach (var sibling in context.Siblings)
        {
            if (sibling.Type.Contains("Label", StringComparison.OrdinalIgnoreCase))
            {
                var text = sibling.Text ?? sibling.Properties?.GetValueOrDefault("Text")?.ToString();
                if (text != null && 
                    (text.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                     text.Contains("fix", StringComparison.OrdinalIgnoreCase) ||
                     text.Contains("correct", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
