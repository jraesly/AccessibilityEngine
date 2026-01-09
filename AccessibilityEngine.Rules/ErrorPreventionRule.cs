using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for error prevention on legal, financial, and data transactions.
/// WCAG 3.3.4: For pages that cause legal commitments, financial transactions, or data modification,
/// at least one of the following is true: reversible, checked, or confirmed.
/// </summary>
public sealed class ErrorPreventionRule : IRule
{
    public string Id => "ERROR_PREVENTION";
    public string Description => "Legal, financial, and data-modifying actions must be reversible, checked, or confirmed.";
    public Severity Severity => Severity.High;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Form types that may contain important submissions
    private static readonly HashSet<string> FormTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Form", "EditForm", "DataForm", "SubmitForm", "PaymentForm",
        "CheckoutForm", "ApplicationForm", "RegistrationForm"
    };

    // Action types that modify data
    private static readonly HashSet<string> ActionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Button", "SubmitButton", "ActionButton", "Link"
    };

    // Keywords indicating sensitive operations
    private static readonly string[] LegalKeywords =
    [
        "agree", "accept", "consent", "sign", "contract", "terms",
        "legal", "binding", "confirm order", "place order"
    ];

    private static readonly string[] FinancialKeywords =
    [
        "pay", "purchase", "buy", "checkout", "payment", "credit card",
        "billing", "charge", "subscribe", "donation", "transfer", "amount"
    ];

    private static readonly string[] DataKeywords =
    [
        "delete", "remove", "submit", "send", "save", "update", "modify",
        "create", "post", "publish", "cancel", "terminate", "close account"
    ];

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check forms for error prevention mechanisms
        if (FormTypes.Contains(node.Type))
        {
            foreach (var finding in CheckFormErrorPrevention(node, context))
                yield return finding;
        }

        // Check submit/action buttons
        if (ActionTypes.Contains(node.Type))
        {
            foreach (var finding in CheckActionErrorPrevention(node, context))
                yield return finding;
        }

        // Check for delete/destructive operations
        foreach (var finding in CheckDestructiveOperations(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckFormErrorPrevention(UiNode node, RuleContext context)
    {
        var sensitivityType = DetermineFormSensitivity(node);
        if (sensitivityType == SensitivityType.None) yield break;

        // Check for error prevention mechanisms
        var hasReviewStep = HasReviewMechanism(node);
        var hasConfirmation = HasConfirmationMechanism(node);
        var hasUndo = HasUndoMechanism(node);
        var hasValidation = HasValidationMechanism(node);

        // At least one protection mechanism should be present
        if (!hasReviewStep && !hasConfirmation && !hasUndo)
        {
            var sensitivityDescription = sensitivityType switch
            {
                SensitivityType.Legal => "legal commitments",
                SensitivityType.Financial => "financial transactions",
                SensitivityType.Data => "data modifications",
                _ => "sensitive operations"
            };

            yield return new Finding(
                Id: $"{Id}:FORM:{node.Id}",
                Severity: Severity.High,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_NO_PROTECTION",
                Message: $"Form '{node.Id}' involves {sensitivityDescription} but lacks error prevention mechanisms.",
                WcagReference: "WCAG 2.2 – 3.3.4 Error Prevention (Legal, Financial, Data) (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Users must be able to review, confirm, or reverse submissions that have significant consequences.",
                SuggestedFix: $"Add at least one of: (1) Review step before final submission, (2) Confirmation dialog, (3) Ability to undo/cancel within a reasonable time period.",
                WcagCriterion: WcagCriterion.ErrorPreventionLegalFinancialData_3_3_4
            );
        }

        // Check if validation covers all required fields
        if (!hasValidation)
        {
            yield return new Finding(
                Id: $"{Id}:VALIDATION:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_NO_VALIDATION",
                Message: $"Form '{node.Id}' doesn't appear to have input validation before submission.",
                WcagReference: "WCAG 2.2 – 3.3.4 Error Prevention (Legal, Financial, Data) (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Validating user input before submission helps prevent errors in important transactions.",
                SuggestedFix: $"Add validation to '{node.Id}' to check user input before allowing submission.",
                WcagCriterion: WcagCriterion.ErrorPreventionLegalFinancialData_3_3_4
            );
        }
    }

    private IEnumerable<Finding> CheckActionErrorPrevention(UiNode node, RuleContext context)
    {
        var actionText = GetActionText(node);
        if (string.IsNullOrWhiteSpace(actionText)) yield break;

        var sensitivityType = DetermineActionSensitivity(actionText, node);
        if (sensitivityType == SensitivityType.None) yield break;

        // Check if action has confirmation
        var hasConfirmation = HasActionConfirmation(node);
        
        if (!hasConfirmation)
        {
            var sensitivityDescription = sensitivityType switch
            {
                SensitivityType.Legal => "a legal commitment",
                SensitivityType.Financial => "a financial transaction",
                SensitivityType.Data => "data modification/deletion",
                _ => "a sensitive operation"
            };

            yield return new Finding(
                Id: $"{Id}:ACTION:{node.Id}",
                Severity: sensitivityType == SensitivityType.Financial ? Severity.High : Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_ACTION_NO_CONFIRM",
                Message: $"Action '{actionText}' ({node.Id}) triggers {sensitivityDescription} but lacks a confirmation step.",
                WcagReference: "WCAG 2.2 – 3.3.4 Error Prevention (Legal, Financial, Data) (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Users should confirm significant actions to prevent accidental submissions.",
                SuggestedFix: $"Add a confirmation dialog or review step before '{actionText}' action completes.",
                WcagCriterion: WcagCriterion.ErrorPreventionLegalFinancialData_3_3_4
            );
        }
    }

    private IEnumerable<Finding> CheckDestructiveOperations(UiNode node, RuleContext context)
    {
        if (!ActionTypes.Contains(node.Type)) yield break;

        var actionText = GetActionText(node)?.ToLowerInvariant() ?? "";
        var isDestructive = actionText.Contains("delete") || 
                           actionText.Contains("remove") ||
                           actionText.Contains("clear all") ||
                           actionText.Contains("reset") ||
                           actionText.Contains("cancel subscription");

        if (!isDestructive) yield break;

        // Check for confirmation
        var hasConfirmation = HasActionConfirmation(node);
        
        if (!hasConfirmation)
        {
            yield return new Finding(
                Id: $"{Id}:DESTRUCTIVE:{node.Id}",
                Severity: Severity.High,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_DESTRUCTIVE_NO_CONFIRM",
                Message: $"Destructive action '{node.Id}' can delete/remove data without confirmation.",
                WcagReference: "WCAG 2.2 – 3.3.4 Error Prevention (Legal, Financial, Data) (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Destructive actions can cause irreversible data loss. Users must confirm before proceeding.",
                SuggestedFix: $"Add a confirmation dialog for '{node.Id}' that clearly states what will be deleted and asks the user to confirm.",
                WcagCriterion: WcagCriterion.ErrorPreventionLegalFinancialData_3_3_4
            );
        }

        // Check for undo option
        var hasUndo = node.Properties?.ContainsKey("UndoAction") == true ||
                     node.Properties?.ContainsKey("Reversible") == true;

        if (!hasUndo)
        {
            yield return new Finding(
                Id: $"{Id}:NO_UNDO:{node.Id}",
                Severity: Severity.Low,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_NO_UNDO",
                Message: $"Destructive action '{node.Id}' doesn't provide an undo option.",
                WcagReference: "WCAG 2.2 – 3.3.4 Error Prevention (Legal, Financial, Data) (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Even with confirmation, providing an undo/recovery option improves error prevention.",
                SuggestedFix: $"Consider implementing soft-delete or a grace period during which the action can be undone.",
                WcagCriterion: WcagCriterion.ErrorPreventionLegalFinancialData_3_3_4
            );
        }
    }

    private enum SensitivityType
    {
        None,
        Legal,
        Financial,
        Data
    }

    private SensitivityType DetermineFormSensitivity(UiNode node)
    {
        var formText = GetFormText(node).ToLowerInvariant();

        if (LegalKeywords.Any(k => formText.Contains(k)))
            return SensitivityType.Legal;

        if (FinancialKeywords.Any(k => formText.Contains(k)))
            return SensitivityType.Financial;

        // Check form type name
        var typeName = node.Type.ToLowerInvariant();
        if (typeName.Contains("payment") || typeName.Contains("checkout"))
            return SensitivityType.Financial;

        if (typeName.Contains("agreement") || typeName.Contains("contract"))
            return SensitivityType.Legal;

        // Check if form modifies data
        if (node.Properties?.ContainsKey("DataSource") == true ||
            node.Properties?.ContainsKey("SubmitForm") == true ||
            node.Properties?.ContainsKey("Patch") == true)
        {
            return SensitivityType.Data;
        }

        return SensitivityType.None;
    }

    private SensitivityType DetermineActionSensitivity(string actionText, UiNode node)
    {
        var lower = actionText.ToLowerInvariant();

        if (LegalKeywords.Any(k => lower.Contains(k)))
            return SensitivityType.Legal;

        if (FinancialKeywords.Any(k => lower.Contains(k)))
            return SensitivityType.Financial;

        if (DataKeywords.Any(k => lower.Contains(k)))
            return SensitivityType.Data;

        // Check OnSelect handler for data operations
        if (node.Properties?.TryGetValue("OnSelect", out var handler) == true)
        {
            var handlerStr = handler?.ToString()?.ToLowerInvariant() ?? "";
            if (handlerStr.Contains("patch") || handlerStr.Contains("remove") ||
                handlerStr.Contains("submit") || handlerStr.Contains("delete"))
            {
                return SensitivityType.Data;
            }
        }

        return SensitivityType.None;
    }

    private static string GetFormText(UiNode node)
    {
        var texts = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(node.Name))
            texts.Add(node.Name);
        
        if (!string.IsNullOrWhiteSpace(node.Text))
            texts.Add(node.Text);

        // Gather text from children
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                if (!string.IsNullOrWhiteSpace(child.Text))
                    texts.Add(child.Text);
                if (!string.IsNullOrWhiteSpace(child.Name))
                    texts.Add(child.Name);
            }
        }

        return string.Join(" ", texts);
    }

    private static string? GetActionText(UiNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.Text))
            return node.Text;

        if (!string.IsNullOrWhiteSpace(node.Name))
            return node.Name;

        if (node.Properties != null)
        {
            var textProps = new[] { "Text", "Label", "Content", "AccessibleLabel" };
            foreach (var prop in textProps)
            {
                if (node.Properties.TryGetValue(prop, out var value))
                {
                    var text = value?.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }
            }
        }

        return null;
    }

    private static bool HasReviewMechanism(UiNode node)
    {
        if (node.Properties == null) return false;

        return node.Properties.ContainsKey("ReviewStep") ||
               node.Properties.ContainsKey("PreviewMode") ||
               node.Properties.ContainsKey("Summary") ||
               HasChildByName(node, "review") ||
               HasChildByName(node, "summary");
    }

    private static bool HasConfirmationMechanism(UiNode node)
    {
        if (node.Properties == null) return false;

        return node.Properties.ContainsKey("ConfirmationDialog") ||
               node.Properties.ContainsKey("ConfirmBeforeSubmit") ||
               node.Properties.ContainsKey("RequireConfirmation");
    }

    private static bool HasUndoMechanism(UiNode node)
    {
        if (node.Properties == null) return false;

        return node.Properties.ContainsKey("UndoEnabled") ||
               node.Properties.ContainsKey("Reversible") ||
               node.Properties.ContainsKey("CancellationPeriod") ||
               node.Properties.ContainsKey("GracePeriod");
    }

    private static bool HasValidationMechanism(UiNode node)
    {
        if (node.Properties == null) return false;

        return node.Properties.ContainsKey("Validation") ||
               node.Properties.ContainsKey("ValidateOnSubmit") ||
               node.Properties.ContainsKey("Required");
    }

    private static bool HasActionConfirmation(UiNode node)
    {
        if (node.Properties == null) return false;

        // Check for confirmation-related properties
        if (node.Properties.ContainsKey("ConfirmationDialog") ||
            node.Properties.ContainsKey("Confirm") ||
            node.Properties.ContainsKey("RequireConfirmation"))
        {
            return true;
        }

        // Check OnSelect for confirmation patterns
        if (node.Properties.TryGetValue("OnSelect", out var handler))
        {
            var handlerStr = handler?.ToString()?.ToLowerInvariant() ?? "";
            return handlerStr.Contains("confirm") || 
                   handlerStr.Contains("dialog") ||
                   handlerStr.Contains("notify");
        }

        return false;
    }

    private static bool HasChildByName(UiNode node, string namePart)
    {
        if (node.Children == null) return false;

        foreach (var child in node.Children)
        {
            if (child.Name?.Contains(namePart, StringComparison.OrdinalIgnoreCase) == true ||
                child.Id?.Contains(namePart, StringComparison.OrdinalIgnoreCase) == true)
                return true;

            if (HasChildByName(child, namePart))
                return true;
        }

        return false;
    }
}
