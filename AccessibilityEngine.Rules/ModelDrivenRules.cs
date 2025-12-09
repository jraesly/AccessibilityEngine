using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for missing or non-descriptive labels in Model-Driven App forms.
/// </summary>
public sealed class MdaFieldLabelRule : IRule
{
    public string Id => "MDA_FIELD_LABEL";
    public string Description => "Form fields should have descriptive labels.";
    public Severity Severity => Severity.High;
    public SurfaceType[]? AppliesTo => [SurfaceType.ModelDrivenApp];

    // Patterns that indicate auto-generated or non-descriptive names
    private static readonly string[] NonDescriptivePatterns =
    [
        "field", "column", "attribute", "new_", "cr???_", // Common schema prefixes
        "_base", "_date", "_state", "_status"
    ];

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;
        
        // Only check field controls
        if (!IsFieldControl(node.Type)) yield break;

        // Check if label is shown
        var showLabel = true;
        if (node.Properties.TryGetValue("showLabel", out var sl))
        {
            showLabel = sl is true || sl?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        }

        var label = node.Name?.Trim();
        var dataField = node.Properties.TryGetValue("datafieldname", out var df) ? df?.ToString() : null;

        // Check 1: Label is hidden
        if (!showLabel)
        {
            yield return new Finding(
                Id: $"{Id}:HIDDEN:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_HIDDEN",
                Message: $"Field '{node.Id}' has its label hidden. Ensure the field purpose is clear from context or provide an accessible description.",
                WcagReference: "WCAG 2.1 – 3.3.2 Labels or Instructions",
                Section508Reference: "Section 508 - Labels",
                Rationale: "Form fields need visible or programmatic labels for screen reader users.",
                EntityName: node.Meta?.EntityName,
                TabName: node.Meta?.TabName,
                SectionName: node.Meta?.SectionName,
                SuggestedFix: $"In the form editor, select field '{node.Id}' and enable 'Display label on the form', or add a Description that explains the field's purpose."
            );
        }

        // Check 2: Label matches schema name (not user-friendly)
        if (!string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(dataField))
        {
            if (label.Equals(dataField, StringComparison.OrdinalIgnoreCase) ||
                IsSchemaName(label))
            {
                yield return new Finding(
                    Id: $"{Id}:SCHEMA:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_SCHEMA_NAME",
                    Message: $"Field '{node.Id}' appears to use schema name '{label}' as label. Consider using a user-friendly display name.",
                    WcagReference: "WCAG 2.1 – 3.3.2 Labels or Instructions",
                    Section508Reference: "Section 508 - Labels",
                    Rationale: "Labels should be meaningful to end users, not technical schema names.",
                    EntityName: node.Meta?.EntityName,
                    TabName: node.Meta?.TabName,
                    SectionName: node.Meta?.SectionName,
                    SuggestedFix: $"Update the Display Name for column '{node.Id}' in the table settings to a user-friendly label (e.g., change 'new_accountid' to 'Account')."
                );
            }
        }

        // Check 3: Missing description for complex fields
        var hasDescription = node.Properties.TryGetValue("description", out var desc) && 
                            !string.IsNullOrWhiteSpace(desc?.ToString());
        
        if (!hasDescription && IsComplexFieldType(node.Type))
        {
            yield return new Finding(
                Id: $"{Id}:NO_DESC:{node.Id}",
                Severity: Severity.Low,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_NO_DESCRIPTION",
                Message: $"Complex field '{node.Id}' of type '{node.Type}' has no description/tooltip to help users understand its purpose.",
                WcagReference: "WCAG 2.1 – 3.3.5 Help",
                Section508Reference: "Section 508 - Help",
                Rationale: "Complex fields benefit from additional help text for all users.",
                EntityName: node.Meta?.EntityName,
                TabName: node.Meta?.TabName,
                SectionName: node.Meta?.SectionName,
                SuggestedFix: $"Add a Description to column '{node.Id}' in the table settings that explains what data should be entered and any format requirements."
            );
        }
    }

    private static bool IsFieldControl(string type)
    {
        return type.EndsWith("Field", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("Field", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("control", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSchemaName(string name)
    {
        // Check for common schema name patterns
        return name.Contains("_") || // Schema names typically have underscores
               name.StartsWith("new_", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("cr", StringComparison.OrdinalIgnoreCase) && name.Length > 5 && name[5] == '_' ||
               NonDescriptivePatterns.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsComplexFieldType(string type)
    {
        return type.Equals("LookupField", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("OptionSetField", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("MultiSelectOptionSetField", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("DateTimeField", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Rule that checks for required field indicators in Model-Driven App forms.
/// </summary>
public sealed class MdaRequiredFieldRule : IRule
{
    public string Id => "MDA_REQUIRED_FIELD";
    public string Description => "Required fields should be clearly indicated.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.ModelDrivenApp];

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check if field is required but label is hidden
        var isRequired = node.Properties.TryGetValue("isRequired", out var req) &&
                        (req is true || req?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true);

        if (!isRequired) yield break;

        var showLabel = true;
        if (node.Properties.TryGetValue("showLabel", out var sl))
        {
            showLabel = sl is true || sl?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        }

        if (!showLabel)
        {
            yield return new Finding(
                Id: $"{Id}:{node.Id}",
                Severity: Severity.High,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: Id,
                Message: $"Required field '{node.Id}' has its label hidden. Users may not know this field is mandatory.",
                WcagReference: "WCAG 2.1 – 3.3.2 Labels or Instructions",
                Section508Reference: "Section 508 - Required Fields",
                Rationale: "Required fields must be clearly indicated so users know what information is mandatory.",
                EntityName: node.Meta?.EntityName,
                TabName: node.Meta?.TabName,
                SectionName: node.Meta?.SectionName,
                SuggestedFix: $"Enable 'Display label on the form' for required field '{node.Id}' in the form editor so the required indicator (*) is visible to users."
            );
        }
    }
}

/// <summary>
/// Rule that checks for non-descriptive tab and section names.
/// </summary>
public sealed class MdaStructureNamingRule : IRule
{
    public string Id => "MDA_STRUCTURE_NAMING";
    public string Description => "Tabs and sections should have descriptive names.";
    public Severity Severity => Severity.Low;
    public SurfaceType[]? AppliesTo => [SurfaceType.ModelDrivenApp];

    // Generic names that don't describe content
    private static readonly HashSet<string> GenericNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "tab", "tab1", "tab2", "tab3", "tab4", "tab5",
        "section", "section1", "section2", "section3", "section4", "section5",
        "general", "details", "info", "data", "other", "misc", "default",
        "new", "untitled", "placeholder"
    };

    // Patterns that indicate auto-generated names (e.g., "SUMMARY_TAB_section_6", "tab_3")
    private static readonly System.Text.RegularExpressions.Regex AutoGeneratedPattern = 
        new(@"_(section|tab)_\d+$|^(tab|section)_?\d+$", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | 
            System.Text.RegularExpressions.RegexOptions.Compiled);

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Only check tabs and sections
        if (!node.Type.Equals("Tab", StringComparison.OrdinalIgnoreCase) &&
            !node.Type.Equals("Section", StringComparison.OrdinalIgnoreCase))
            yield break;

        var name = node.Name?.Trim();
        if (string.IsNullOrEmpty(name)) yield break;

        var isAutoGenerated = IsAutoGeneratedName(name);

        if (isAutoGenerated)
        {
            yield return new Finding(
                Id: $"{Id}:{node.Id}",
                Severity: Severity.Low,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: Id,
                Message: $"{node.Type} '{node.Id}' has a generic or auto-generated name. Consider using a descriptive name for better navigation.",
                WcagReference: "WCAG 2.1 – 2.4.6 Headings and Labels",
                Section508Reference: "Section 508 - Headings",
                Rationale: "Descriptive section names help users understand form structure and navigate efficiently.",
                EntityName: node.Meta?.EntityName,
                TabName: node.Meta?.TabName,
                SectionName: node.Meta?.SectionName,
                SuggestedFix: $"Rename {node.Type.ToLower()} '{node.Id}' in the form editor to a descriptive name that indicates its content (e.g., 'Contact Information' instead of 'tab_2')."
            );
        }
    }

    private static bool IsAutoGeneratedName(string name)
        {
            // Check for exact generic names
            if (GenericNames.Contains(name))
                return true;

            // Check for auto-generated patterns like "SUMMARY_TAB_section_6" or "tab_3"
            if (AutoGeneratedPattern.IsMatch(name))
                return true;

            // Check for names that are just "ref_pan_" prefixed (reference panel auto-names)
            if (name.StartsWith("ref_pan_", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
    }

    /// <summary>
    /// Rule that checks PCF (Power Apps Component Framework) controls for accessibility compliance.
    /// </summary>
    public sealed class MdaPcfControlRule : IRule
    {
        public string Id => "MDA_PCF_CONTROL";
        public string Description => "PCF controls should be reviewed for accessibility compliance.";
        public Severity Severity => Severity.Medium;
        public SurfaceType[]? AppliesTo => [SurfaceType.ModelDrivenApp];

        public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
        {
            if (node is null) yield break;

            // Check if this is a PCF control
            var isPcf = node.Properties.TryGetValue("isPcfControl", out var pcf) &&
                       (pcf is true || pcf?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true);

            if (!isPcf) yield break;

            var pcfName = node.Properties.TryGetValue("pcfControlName", out var name) ? name?.ToString() : "Unknown";

            yield return new Finding(
                Id: $"{Id}:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: Id,
                Message: $"PCF control '{pcfName}' detected on field '{node.Id}'. Custom controls should be reviewed for accessibility compliance including keyboard navigation, screen reader support, and ARIA attributes.",
                WcagReference: "WCAG 2.1 – 4.1.2 Name, Role, Value",
                Section508Reference: "Section 508 - Custom Controls",
                Rationale: "PCF controls are custom components that may not inherit default accessibility features. They require manual review.",
                EntityName: node.Meta?.EntityName,
                TabName: node.Meta?.TabName,
                SectionName: node.Meta?.SectionName,
                SuggestedFix: $"Review PCF control '{pcfName}' for: 1) Keyboard navigation support, 2) ARIA labels and roles, 3) Focus management, 4) Screen reader announcements. Consider using Microsoft's Accessibility Insights tool to test."
            );
        }
    }

    /// <summary>
    /// Rule that checks for read-only fields that may need accessibility considerations.
    /// </summary>
    public sealed class MdaReadOnlyFieldRule : IRule
    {
        public string Id => "MDA_READONLY_FIELD";
        public string Description => "Read-only fields should be clearly indicated to users.";
        public Severity Severity => Severity.Low;
        public SurfaceType[]? AppliesTo => [SurfaceType.ModelDrivenApp];

        public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
        {
            if (node is null) yield break;

            // Only check field controls
            if (!node.Type.EndsWith("Field", StringComparison.OrdinalIgnoreCase) &&
                !node.Type.Equals("Field", StringComparison.OrdinalIgnoreCase))
                yield break;

            var isReadOnly = node.Properties.TryGetValue("isReadOnly", out var ro) &&
                            (ro is true || ro?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true);

            var isDisabled = node.Properties.TryGetValue("disabled", out var dis) &&
                            (dis is true || dis?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true);

            if (!isReadOnly && !isDisabled) yield break;

            // Check if there's a description explaining why it's read-only
            var hasDescription = node.Properties.TryGetValue("description", out var desc) && 
                                !string.IsNullOrWhiteSpace(desc?.ToString());

            if (!hasDescription)
            {
                yield return new Finding(
                    Id: $"{Id}:{node.Id}",
                    Severity: Severity.Low,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: Id,
                    Message: $"Read-only field '{node.Id}' has no description explaining why it cannot be edited. Consider adding a tooltip.",
                    WcagReference: "WCAG 2.1 – 3.3.2 Labels or Instructions",
                    Section508Reference: "Section 508 - Labels",
                    Rationale: "Users benefit from understanding why a field is read-only, especially for assistive technology users.",
                    EntityName: node.Meta?.EntityName,
                    TabName: node.Meta?.TabName,
                    SectionName: node.Meta?.SectionName,
                    SuggestedFix: $"Add a Description to column '{node.Id}' explaining why it is read-only (e.g., 'This field is calculated automatically based on related records')."
                );
            }
        }
    }

    /// <summary>
    /// Rule that checks for conditionally visible fields that may cause accessibility issues.
    /// </summary>
    public sealed class MdaConditionalVisibilityRule : IRule
    {
        public string Id => "MDA_CONDITIONAL_VISIBILITY";
        public string Description => "Conditionally visible fields should announce state changes.";
        public Severity Severity => Severity.Medium;
        public SurfaceType[]? AppliesTo => [SurfaceType.ModelDrivenApp];

        public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
        {
            if (node is null) yield break;

            // Check if this field has conditional visibility
            var isConditionallyVisible = node.Properties.TryGetValue("isConditionallyVisible", out var cv) &&
                                        (cv is true || cv?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true);

            // Also check for event handlers that might toggle visibility
            var hasVisibilityEvents = node.Properties.TryGetValue("eventHandlers", out var handlers) &&
                                     handlers is IEnumerable<string> eventList &&
                                     eventList.Any(e => e.Contains("OnChange", StringComparison.OrdinalIgnoreCase));

            if (!isConditionallyVisible && !hasVisibilityEvents) yield break;

            yield return new Finding(
                Id: $"{Id}:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: Id,
                Message: $"Field '{node.Id}' has conditional visibility or events that may change form state. Ensure visibility changes are announced to screen readers.",
                WcagReference: "WCAG 2.1 – 4.1.3 Status Messages",
                Section508Reference: "Section 508 - Status Messages",
                Rationale: "Dynamic content changes should be announced to assistive technologies so users are aware of new fields or options.",
                EntityName: node.Meta?.EntityName,
                TabName: node.Meta?.TabName,
                SectionName: node.Meta?.SectionName,
                SuggestedFix: $"When field '{node.Id}' becomes visible, ensure focus is moved to it or use an aria-live region to announce the change. Consider adding instructional text that explains when and why this field appears."
            );
        }
    }

    /// <summary>
    /// Rule that checks for embedded web resources and iframes which may have accessibility issues.
    /// </summary>
    public sealed class MdaEmbeddedContentRule : IRule
    {
        public string Id => "MDA_EMBEDDED_CONTENT";
        public string Description => "Embedded web resources and iframes should be accessible.";
        public Severity Severity => Severity.High;
        public SurfaceType[]? AppliesTo => [SurfaceType.ModelDrivenApp];

        private static readonly HashSet<string> EmbeddedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "WebResource", "IFrame", "QuickViewForm"
        };

        public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
        {
            if (node is null) yield break;

            if (!EmbeddedTypes.Contains(node.Type)) yield break;

            var controlType = node.Type;
            var message = controlType switch
            {
                "WebResource" => $"Web resource '{node.Id}' embeds external content. Ensure the embedded content meets accessibility standards including proper heading structure, alt text, and keyboard navigation.",
                "IFrame" => $"IFrame '{node.Id}' embeds external content. Verify the iframe has a meaningful title attribute and the embedded content is accessible.",
                "QuickViewForm" => $"Quick view form '{node.Id}' embeds another form. Ensure the embedded form fields have proper labels and are keyboard accessible.",
                _ => $"Embedded content '{node.Id}' should be reviewed for accessibility compliance."
            };

            yield return new Finding(
                Id: $"{Id}:{node.Id}",
                Severity: Severity.High,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: Id,
                Message: message,
                WcagReference: "WCAG 2.1 – 4.1.2 Name, Role, Value",
                Section508Reference: "Section 508 - Frames",
                Rationale: "Embedded content must be independently accessible and should not create barriers for assistive technology users.",
                EntityName: node.Meta?.EntityName,
                TabName: node.Meta?.TabName,
                SectionName: node.Meta?.SectionName,
                SuggestedFix: $"Review embedded content '{node.Id}' for: 1) Proper title attribute on iframes, 2) Keyboard accessibility within the embedded content, 3) Screen reader compatibility, 4) Appropriate heading structure."
            );
        }
    }