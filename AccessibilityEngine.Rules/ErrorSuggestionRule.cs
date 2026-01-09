using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for error suggestions to help users correct input errors.
/// WCAG 3.3.3: If an input error is automatically detected and suggestions for correction are known,
/// then the suggestions are provided to the user (unless it would jeopardize security or purpose).
/// </summary>
public sealed class ErrorSuggestionRule : IRule
{
    public string Id => "ERROR_SUGGESTION";
    public string Description => "Error messages must provide suggestions for correcting input errors when known.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Input control types that can have validation
    private static readonly HashSet<string> InputTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "TextInput", "TextBox", "ComboBox", "DropDown", "DatePicker",
        "NumberInput", "EmailInput", "PhoneInput", "PasswordInput",
        "Slider", "Rating", "Toggle", "Checkbox", "RadioButton"
    };

    // Form types
    private static readonly HashSet<string> FormTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Form", "EditForm", "DataForm", "FormContainer"
    };

    // Error message types
    private static readonly HashSet<string> ErrorTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ErrorMessage", "ValidationMessage", "ErrorText", "Alert", "Warning"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check input controls for validation with suggestions
        if (InputTypes.Contains(node.Type))
        {
            foreach (var finding in CheckInputValidation(node, context))
                yield return finding;
        }

        // Check forms for error handling
        if (FormTypes.Contains(node.Type))
        {
            foreach (var finding in CheckFormErrorHandling(node, context))
                yield return finding;
        }

        // Check error message controls
        if (ErrorTypes.Contains(node.Type))
        {
            foreach (var finding in CheckErrorMessageContent(node, context))
                yield return finding;
        }
    }

    private IEnumerable<Finding> CheckInputValidation(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check if there's validation on this input
        var hasValidation = node.Properties.ContainsKey("Validation") ||
                           node.Properties.ContainsKey("ValidationRule") ||
                           node.Properties.ContainsKey("Required") ||
                           node.Properties.ContainsKey("Pattern") ||
                           node.Properties.ContainsKey("Min") ||
                           node.Properties.ContainsKey("Max") ||
                           node.Properties.ContainsKey("MaxLength") ||
                           node.Properties.ContainsKey("MinLength");

        if (!hasValidation) yield break;

        // Check for error message configuration
        var hasErrorMessage = node.Properties.ContainsKey("ErrorMessage") ||
                             node.Properties.ContainsKey("ValidationErrorMessage") ||
                             node.Properties.ContainsKey("InvalidMessage");

        if (!hasErrorMessage)
        {
            yield return new Finding(
                Id: $"{Id}:NO_MESSAGE:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_NO_ERROR_MESSAGE",
                Message: $"Input '{node.Id}' has validation but no custom error message configured.",
                WcagReference: "WCAG 2.2 – 3.3.3 Error Suggestion (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Generic validation errors don't help users understand how to fix the problem.",
                SuggestedFix: $"Add an ErrorMessage property to '{node.Id}' that explains what valid input looks like.",
                WcagCriterion: WcagCriterion.ErrorSuggestion_3_3_3
            );
            yield break;
        }

        // Check if error message includes a suggestion
        var errorMessage = GetErrorMessage(node);
        if (!string.IsNullOrWhiteSpace(errorMessage) && !ContainsSuggestion(errorMessage))
        {
            yield return new Finding(
                Id: $"{Id}:NO_SUGGESTION:{node.Id}",
                Severity: Severity.Low,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_NO_SUGGESTION",
                Message: $"Error message for '{node.Id}' identifies the error but may not provide a correction suggestion.",
                WcagReference: "WCAG 2.2 – 3.3.3 Error Suggestion (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Error messages should not only identify what's wrong but also suggest how to fix it.",
                SuggestedFix: $"Update error message for '{node.Id}' to include correction suggestions (e.g., 'Email must include @ symbol. Example: user@example.com').",
                WcagCriterion: WcagCriterion.ErrorSuggestion_3_3_3
            );
        }

        // Check specific input types for appropriate suggestions
        foreach (var finding in CheckInputTypeSpecificSuggestions(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckInputTypeSpecificSuggestions(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check for format validation without format hint
        if (node.Properties.TryGetValue("Format", out var format) ||
            node.Properties.TryGetValue("Pattern", out format))
        {
            var hasFormatHint = node.Properties.ContainsKey("Placeholder") ||
                               node.Properties.ContainsKey("HintText") ||
                               node.Properties.ContainsKey("FormatExample");

            if (!hasFormatHint)
            {
                yield return new Finding(
                    Id: $"{Id}:FORMAT:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_NO_FORMAT_HINT",
                    Message: $"Input '{node.Id}' has format validation but no format hint or example.",
                    WcagReference: "WCAG 2.2 – 3.3.3 Error Suggestion (Level AA)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Users need to know the expected format before entering data, not just when they make an error.",
                    SuggestedFix: $"Add a placeholder or hint text to '{node.Id}' showing the expected format (e.g., 'MM/DD/YYYY' for dates).",
                    WcagCriterion: WcagCriterion.ErrorSuggestion_3_3_3
                );
            }
        }

        // Check for range validation without range display
        if ((node.Properties.ContainsKey("Min") || node.Properties.ContainsKey("Max")) &&
            !node.Properties.ContainsKey("RangeDisplay") &&
            !node.Properties.ContainsKey("HintText"))
        {
            var min = node.Properties.TryGetValue("Min", out var minVal) ? minVal?.ToString() : null;
            var max = node.Properties.TryGetValue("Max", out var maxVal) ? maxVal?.ToString() : null;

            yield return new Finding(
                Id: $"{Id}:RANGE:{node.Id}",
                Severity: Severity.Low,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_NO_RANGE_HINT",
                Message: $"Input '{node.Id}' has range validation (min: {min ?? "none"}, max: {max ?? "none"}) but doesn't display the valid range.",
                WcagReference: "WCAG 2.2 – 3.3.3 Error Suggestion (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Users should know the valid range before entering data.",
                SuggestedFix: $"Add hint text or error message to '{node.Id}' that specifies the valid range.",
                WcagCriterion: WcagCriterion.ErrorSuggestion_3_3_3
            );
        }
    }

    private IEnumerable<Finding> CheckFormErrorHandling(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check if form has error summary
        var hasErrorSummary = node.Properties.ContainsKey("ErrorSummary") ||
                             node.Properties.ContainsKey("ValidationSummary") ||
                             HasChildOfType(node, ["ErrorSummary", "ValidationSummary"]);

        // Check if form has individual field errors
        var hasFieldErrors = node.Properties.ContainsKey("ShowFieldErrors") ||
                            node.Properties.ContainsKey("InlineValidation");

        if (!hasErrorSummary && !hasFieldErrors)
        {
            yield return new Finding(
                Id: $"{Id}:FORM:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_NO_ERROR_DISPLAY",
                Message: $"Form '{node.Id}' doesn't appear to have error display configuration.",
                WcagReference: "WCAG 2.2 – 3.3.3 Error Suggestion (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Forms must display validation errors in a way that users can perceive and understand.",
                SuggestedFix: $"Add error summary and/or inline field validation to '{node.Id}' to display helpful error messages.",
                WcagCriterion: WcagCriterion.ErrorSuggestion_3_3_3
            );
        }
    }

    private IEnumerable<Finding> CheckErrorMessageContent(UiNode node, RuleContext context)
    {
        var errorText = node.Text ?? "";
        
        if (node.Properties != null)
        {
            if (node.Properties.TryGetValue("Text", out var text))
                errorText = text?.ToString() ?? "";
            else if (node.Properties.TryGetValue("Message", out var msg))
                errorText = msg?.ToString() ?? "";
        }

        if (string.IsNullOrWhiteSpace(errorText)) yield break;

        // Check for generic error messages
        var genericMessages = new[]
        {
            "invalid", "error", "invalid input", "validation failed",
            "required field", "field is required", "invalid value"
        };

        var normalizedError = errorText.ToLowerInvariant().Trim();
        
        if (genericMessages.Any(g => normalizedError == g || normalizedError.StartsWith(g + ".")))
        {
            yield return new Finding(
                Id: $"{Id}:GENERIC:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_GENERIC_ERROR",
                Message: $"Error message '{TruncateText(errorText, 40)}' is too generic to help users correct the error.",
                WcagReference: "WCAG 2.2 – 3.3.3 Error Suggestion (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Generic error messages like 'Invalid input' don't help users understand what they did wrong or how to fix it.",
                SuggestedFix: $"Replace generic error message with specific guidance. Instead of '{TruncateText(errorText, 20)}', use 'Please enter a valid email address (e.g., name@example.com)'.",
                WcagCriterion: WcagCriterion.ErrorSuggestion_3_3_3
            );
        }
    }

    private static string? GetErrorMessage(UiNode node)
    {
        if (node.Properties == null) return null;

        var errorProps = new[] { "ErrorMessage", "ValidationErrorMessage", "InvalidMessage", "HelpText" };
        
        foreach (var prop in errorProps)
        {
            if (node.Properties.TryGetValue(prop, out var value))
            {
                return value?.ToString();
            }
        }

        return null;
    }

    private static bool ContainsSuggestion(string errorMessage)
    {
        var suggestionIndicators = new[]
        {
            "example", "e.g.", "for example", "such as", "like",
            "should be", "must be", "format", "expected",
            "try", "please enter", "valid", "between"
        };

        var lower = errorMessage.ToLowerInvariant();
        return suggestionIndicators.Any(s => lower.Contains(s));
    }

    private static bool HasChildOfType(UiNode node, string[] types)
    {
        if (node.Children == null) return false;

        var typeSet = new HashSet<string>(types, StringComparer.OrdinalIgnoreCase);
        
        foreach (var child in node.Children)
        {
            if (typeSet.Contains(child.Type))
                return true;
            
            if (HasChildOfType(child, types))
                return true;
        }

        return false;
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}
