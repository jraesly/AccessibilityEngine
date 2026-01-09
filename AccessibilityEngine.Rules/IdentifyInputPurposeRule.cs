using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks if input fields identify their purpose for autocomplete.
/// WCAG 1.3.5: The purpose of each input field collecting information about the user can be
/// programmatically determined when the input field serves a purpose identified in the Input Purposes list.
/// </summary>
public sealed class IdentifyInputPurposeRule : IRule
{
    public string Id => "IDENTIFY_INPUT_PURPOSE";
    public string Description => "Input fields collecting user information should identify their purpose for autocomplete support.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Input types that collect personal information
    private static readonly HashSet<string> PersonalInputTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "TextInput", "TextBox", "Input", "TextField", "EditText", "ComboBox",
        "DatePicker", "PhoneInput", "EmailInput", "AddressInput"
    };

    // Common field name patterns and their expected autocomplete values
    private static readonly Dictionary<string[], string> AutocompleteMappings = new()
    {
        { ["name", "fullname", "full_name"], "name" },
        { ["firstname", "first_name", "fname", "givenname"], "given-name" },
        { ["lastname", "last_name", "lname", "surname", "familyname"], "family-name" },
        { ["email", "emailaddress", "email_address"], "email" },
        { ["phone", "telephone", "phonenumber", "phone_number", "tel"], "tel" },
        { ["address", "streetaddress", "street_address", "address1"], "street-address" },
        { ["city", "locality"], "address-level2" },
        { ["state", "province", "region"], "address-level1" },
        { ["zip", "zipcode", "postalcode", "postal_code"], "postal-code" },
        { ["country", "countryname"], "country-name" },
        { ["birthday", "birthdate", "dob", "dateofbirth"], "bday" },
        { ["username", "user_name", "userid"], "username" },
        { ["password", "pwd", "pass"], "current-password" },
        { ["newpassword", "new_password"], "new-password" },
        { ["creditcard", "cardnumber", "cc_number"], "cc-number" },
        { ["ccname", "cardholder", "cardname"], "cc-name" },
        { ["ccexp", "expiry", "expiration"], "cc-exp" },
        { ["cvc", "cvv", "securitycode"], "cc-csc" },
        { ["organization", "company", "companyname"], "organization" },
        { ["jobtitle", "job_title", "title"], "organization-title" }
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Only check input controls
        if (!PersonalInputTypes.Contains(node.Type)) yield break;

        // Get the field identifier (name or id)
        var fieldName = GetFieldIdentifier(node);
        if (string.IsNullOrWhiteSpace(fieldName)) yield break;

        // Check if this field appears to collect personal information
        var expectedAutocomplete = GetExpectedAutocomplete(fieldName);
        if (expectedAutocomplete == null) yield break;

        // Check if autocomplete is properly set
        var hasAutocomplete = HasAutocompleteAttribute(node);

        if (!hasAutocomplete)
        {
            yield return new Finding(
                Id: $"{Id}:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_MISSING",
                Message: $"Input field '{node.Id}' appears to collect personal information but does not specify autocomplete purpose.",
                WcagReference: "WCAG 2.2 – 1.3.5 Identify Input Purpose (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Users with cognitive disabilities benefit from browser autofill. Identifying input purpose enables personalized icons and symbols.",
                SuggestedFix: $"Add autocomplete='{expectedAutocomplete}' attribute to the input field '{node.Id}'.",
                WcagCriterion: WcagCriterion.IdentifyInputPurpose_1_3_5
            );
        }
    }

    private static string? GetFieldIdentifier(UiNode node)
    {
        var name = node.Name?.ToLowerInvariant()?.Replace(" ", "").Replace("-", "").Replace("_", "") ?? "";
        
        if (!string.IsNullOrWhiteSpace(name)) return name;

        if (node.Properties != null)
        {
            if (node.Properties.TryGetValue("Name", out var propName))
                return propName?.ToString()?.ToLowerInvariant()?.Replace(" ", "").Replace("-", "").Replace("_", "");
            if (node.Properties.TryGetValue("FieldName", out var fieldName))
                return fieldName?.ToString()?.ToLowerInvariant()?.Replace(" ", "").Replace("-", "").Replace("_", "");
            if (node.Properties.TryGetValue("DataField", out var dataField))
                return dataField?.ToString()?.ToLowerInvariant()?.Replace(" ", "").Replace("-", "").Replace("_", "");
        }

        return node.Id?.ToLowerInvariant()?.Replace(" ", "").Replace("-", "").Replace("_", "");
    }

    private static string? GetExpectedAutocomplete(string fieldName)
    {
        var normalizedName = fieldName.ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");

        foreach (var mapping in AutocompleteMappings)
        {
            if (mapping.Key.Any(pattern => normalizedName.Contains(pattern)))
            {
                return mapping.Value;
            }
        }

        return null;
    }

    private static bool HasAutocompleteAttribute(UiNode node)
    {
        if (node.Properties == null) return false;

        var autocompleteProps = new[] { "autocomplete", "Autocomplete", "AutoComplete", "InputPurpose", "inputpurpose" };
        
        foreach (var prop in autocompleteProps)
        {
            if (node.Properties.TryGetValue(prop, out var value))
            {
                var autocompleteValue = value?.ToString();
                // "off" doesn't count as identifying purpose
                if (!string.IsNullOrWhiteSpace(autocompleteValue) && 
                    !autocompleteValue.Equals("off", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
