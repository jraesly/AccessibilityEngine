using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for redundant entry issues.
/// WCAG 3.3.7 (New in 2.2): Information previously entered should be auto-populated or available for selection.
/// </summary>
public sealed class RedundantEntryRule : IRule
{
    public string Id => "REDUNDANT_ENTRY";
    public string Description => "Information previously entered should be auto-populated or available for user selection.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Common field name patterns that often require repeated entry
    private static readonly Dictionary<string, string[]> RelatedFieldPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["email"] = ["email", "e-mail", "emailaddress", "email_address", "mail"],
        ["phone"] = ["phone", "telephone", "tel", "mobile", "cell", "phonenumber", "phone_number"],
        ["address"] = ["address", "street", "addr", "location", "street_address"],
        ["city"] = ["city", "town", "municipality"],
        ["state"] = ["state", "province", "region"],
        ["zip"] = ["zip", "zipcode", "zip_code", "postal", "postalcode", "postal_code"],
        ["country"] = ["country", "nation"],
        ["name"] = ["name", "fullname", "full_name"],
        ["firstname"] = ["firstname", "first_name", "fname", "givenname", "given_name"],
        ["lastname"] = ["lastname", "last_name", "lname", "surname", "familyname", "family_name"],
        ["company"] = ["company", "organization", "org", "business", "employer"],
        ["password"] = ["password", "pwd", "pass"],
        ["confirm"] = ["confirm", "verify", "retype", "repeat"]
    };

    // Input control types
    private static readonly HashSet<string> InputTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "TextInput", "Text input", "Classic/TextInput",
        "ComboBox", "Combo box", "Classic/ComboBox",
        "Dropdown", "Drop down", "Classic/Dropdown",
        "DatePicker", "Date picker", "Classic/DatePicker"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check at screen level for duplicate field patterns
        if (node.Type.Equals("Screen", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var finding in CheckScreenForRedundantFields(node, context))
                yield return finding;
        }

        // Check individual input fields for auto-population opportunities
        foreach (var finding in CheckAutoPopulationOpportunities(node, context))
            yield return finding;

        // Check for confirmation/verify fields
        foreach (var finding in CheckConfirmationFields(node, context))
            yield return finding;

        // Check for multi-step form patterns
        foreach (var finding in CheckMultiStepFormPatterns(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckScreenForRedundantFields(UiNode node, RuleContext context)
    {
        // Collect all input fields on the screen
        var inputFields = GetAllInputFields(node).ToList();
        
        if (inputFields.Count < 2) yield break;

        // Group fields by semantic category
        var fieldsByCategory = new Dictionary<string, List<UiNode>>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in inputFields)
        {
            var fieldName = GetFieldName(field).ToLowerInvariant();
            
            foreach (var (category, patterns) in RelatedFieldPatterns)
            {
                foreach (var pattern in patterns)
                {
                    if (fieldName.Contains(pattern))
                    {
                        if (!fieldsByCategory.ContainsKey(category))
                            fieldsByCategory[category] = new List<UiNode>();
                        
                        fieldsByCategory[category].Add(field);
                        break;
                    }
                }
            }
        }

        // Check for duplicate fields in the same category
        foreach (var (category, fields) in fieldsByCategory)
        {
            if (fields.Count > 1)
            {
                // Check if fields are in different sections (might be intentional - shipping vs billing)
                var sections = fields.Select(f => f.Meta?.SectionName ?? "").Distinct().ToList();
                
                if (sections.Count == 1)
                {
                    // Same section - likely redundant
                    var fieldIds = string.Join(", ", fields.Select(f => f.Id).Take(3));
                    
                    yield return new Finding(
                        Id: $"{Id}:DUPLICATE:{category}",
                        Severity: Severity.Medium,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: fields[0].Id,
                        ControlType: fields[0].Type,
                        IssueType: $"{Id}_DUPLICATE",
                        Message: $"Multiple {category} fields found ({fieldIds}). Consider auto-populating from previously entered data.",
                        WcagReference: "WCAG 2.2 § 3.3.7 Redundant Entry (Level A)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Requiring users to re-enter information they've already provided increases cognitive load and error risk, especially for users with cognitive disabilities.",
                        SuggestedFix: $"For {category} fields: (1) Auto-populate from User() profile data, (2) Store in a variable and pre-fill subsequent fields, (3) Use a lookup/dropdown to select previously entered values.",
                        WcagCriterion: WcagCriterion.RedundantEntry_3_3_7
                    );
                }
            }
        }
    }

    private IEnumerable<Finding> CheckAutoPopulationOpportunities(UiNode node, RuleContext context)
    {
        if (!InputTypes.Contains(node.Type)) yield break;
        if (node.Properties == null) yield break;

        var fieldName = GetFieldName(node).ToLowerInvariant();

        // Check if field could be auto-populated from User() function
        var userDataFields = new[] { "email", "fullname", "name" };
        foreach (var userField in userDataFields)
        {
            if (fieldName.Contains(userField))
            {
                // Check if Default property uses User() function
                var hasUserDefault = false;
                if (node.Properties.TryGetValue("Default", out var defaultValue))
                {
                    var defaultStr = defaultValue?.ToString() ?? "";
                    if (defaultStr.Contains("User()", StringComparison.OrdinalIgnoreCase) ||
                        defaultStr.Contains("User.Email", StringComparison.OrdinalIgnoreCase) ||
                        defaultStr.Contains("User.FullName", StringComparison.OrdinalIgnoreCase))
                    {
                        hasUserDefault = true;
                    }
                }

                if (!hasUserDefault)
                {
                    yield return new Finding(
                        Id: $"{Id}:USER_DATA:{node.Id}",
                        Severity: Severity.Low,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_USER_DATA",
                        Message: $"Field '{node.Id}' ({userField}) could be auto-populated from User() profile data.",
                        WcagReference: "WCAG 2.2 § 3.3.7 Redundant Entry (Level A)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Auto-populating user profile information reduces redundant data entry.",
                        SuggestedFix: $"Set Default property of '{node.Id}' to User().{(userField.Contains("email") ? "Email" : "FullName")} to pre-fill from signed-in user data.",
                        WcagCriterion: WcagCriterion.RedundantEntry_3_3_7
                    );
                }
            }
        }

        // Check for billing/shipping address patterns
        if (fieldName.Contains("billing") || fieldName.Contains("shipping"))
        {
            var hasCopyButton = CheckForCopyAddressButton(context);
            var hasDefaultFromOther = CheckForCrossFieldDefault(node, fieldName.Contains("shipping") ? "billing" : "shipping");

            if (!hasCopyButton && !hasDefaultFromOther)
            {
                yield return new Finding(
                    Id: $"{Id}:ADDRESS_COPY:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_ADDRESS_COPY",
                    Message: $"Address field '{node.Id}' could offer 'Same as billing/shipping' option to reduce redundant entry.",
                    WcagReference: "WCAG 2.2 § 3.3.7 Redundant Entry (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "When billing and shipping addresses are often the same, providing a copy option reduces redundant data entry.",
                    SuggestedFix: $"Add a checkbox or button like 'Same as {(fieldName.Contains("shipping") ? "billing" : "shipping")} address' that copies address fields automatically.",
                    WcagCriterion: WcagCriterion.RedundantEntry_3_3_7
                );
            }
        }
    }

    private IEnumerable<Finding> CheckConfirmationFields(UiNode node, RuleContext context)
    {
        if (!InputTypes.Contains(node.Type)) yield break;

        var fieldName = GetFieldName(node).ToLowerInvariant();

        // Check for confirmation fields (confirm email, verify password, etc.)
        var isConfirmField = fieldName.Contains("confirm") || fieldName.Contains("verify") ||
                            fieldName.Contains("retype") || fieldName.Contains("repeat") ||
                            fieldName.StartsWith("re_") || fieldName.Contains("_confirm") ||
                            fieldName.Contains("_verify");

        if (!isConfirmField) yield break;

        // Confirmation fields for password and email are acceptable for security
        if (fieldName.Contains("password") || fieldName.Contains("email"))
        {
            yield return new Finding(
                Id: $"{Id}:CONFIRM_SECURITY:{node.Id}",
                Severity: Severity.Low,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_CONFIRM_SECURITY",
                Message: $"Confirmation field '{node.Id}' requires re-entry. This is acceptable for security purposes.",
                WcagReference: "WCAG 2.2 § 3.3.7 Redundant Entry (Level A)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Confirmation fields for security-sensitive data (passwords, emails) are an acceptable exception per WCAG 3.3.7.",
                SuggestedFix: $"No action required. Consider allowing paste in confirmation field '{node.Id}' and supporting password managers.",
                WcagCriterion: WcagCriterion.RedundantEntry_3_3_7
            );
        }
        else
        {
            // Other confirmation fields should consider alternatives
            yield return new Finding(
                Id: $"{Id}:CONFIRM_OTHER:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_CONFIRM_OTHER",
                Message: $"Confirmation field '{node.Id}' requires redundant data entry. Consider if confirmation is essential.",
                WcagReference: "WCAG 2.2 § 3.3.7 Redundant Entry (Level A)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Requiring users to retype information is only acceptable when essential for security.",
                SuggestedFix: $"For '{node.Id}': Either remove the confirmation field, or use a 'show value' toggle instead of re-entry confirmation.",
                WcagCriterion: WcagCriterion.RedundantEntry_3_3_7
            );
        }
    }

    private IEnumerable<Finding> CheckMultiStepFormPatterns(UiNode node, RuleContext context)
    {
        // Check if screen appears to be part of a multi-step wizard
        if (!node.Type.Equals("Screen", StringComparison.OrdinalIgnoreCase)) yield break;

        var screenName = node.Name?.ToLowerInvariant() ?? "";
        var isWizardStep = screenName.Contains("step") || screenName.Contains("wizard") ||
                          screenName.Contains("page") || screenName.Contains("form");

        if (!isWizardStep) yield break;

        // Check if there's a context/state management pattern
        var inputFields = GetAllInputFields(node).ToList();
        var fieldsWithoutDefault = inputFields.Where(f => 
        {
            if (f.Properties == null) return true;
            return !f.Properties.ContainsKey("Default") || 
                   string.IsNullOrWhiteSpace(f.Properties["Default"]?.ToString());
        }).ToList();

        if (fieldsWithoutDefault.Count > 2)
        {
            yield return new Finding(
                Id: $"{Id}:WIZARD_STATE:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_WIZARD_STATE",
                Message: $"Multi-step form screen '{node.Id}' has {fieldsWithoutDefault.Count} fields without default values. Consider preserving data if user navigates back.",
                WcagReference: "WCAG 2.2 § 3.3.7 Redundant Entry (Level A)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "In multi-step forms, users should not need to re-enter data if they navigate between steps.",
                SuggestedFix: $"Store form data in variables or a collection. Set Default property on input fields to restore values when users navigate back to this step.",
                WcagCriterion: WcagCriterion.RedundantEntry_3_3_7
            );
        }
    }

    private static IEnumerable<UiNode> GetAllInputFields(UiNode node)
    {
        if (InputTypes.Contains(node.Type))
            yield return node;

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                foreach (var field in GetAllInputFields(child))
                    yield return field;
            }
        }
    }

    private static string GetFieldName(UiNode node)
    {
        var name = node.Name ?? "";
        if (string.IsNullOrEmpty(name) && node.Properties?.TryGetValue("HintText", out var hint) == true)
            name = hint?.ToString() ?? "";
        if (string.IsNullOrEmpty(name) && node.Properties?.TryGetValue("AccessibleLabel", out var label) == true)
            name = label?.ToString() ?? "";
        return name;
    }

    private static bool CheckForCopyAddressButton(RuleContext context)
    {
        foreach (var sibling in context.Siblings)
        {
            var name = sibling.Name?.ToLowerInvariant() ?? "";
            var text = sibling.Text?.ToLowerInvariant() ?? "";
            
            if (name.Contains("same") || text.Contains("same") ||
                name.Contains("copy") || text.Contains("copy") ||
                text.Contains("same as billing") || text.Contains("same as shipping"))
            {
                return true;
            }
        }
        return false;
    }

    private static bool CheckForCrossFieldDefault(UiNode node, string sourcePrefix)
    {
        if (node.Properties?.TryGetValue("Default", out var defaultValue) == true)
        {
            var defaultStr = defaultValue?.ToString() ?? "";
            return defaultStr.Contains(sourcePrefix, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}
