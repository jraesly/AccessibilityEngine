using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for consistent help placement.
/// WCAG 3.2.6 (New in 2.2): Help mechanisms must appear in the same relative order across pages.
/// </summary>
public sealed class ConsistentHelpRule : IRule
{
    public string Id => "CONSISTENT_HELP";
    public string Description => "Help mechanisms must appear in the same relative order across pages.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Help mechanism patterns
    private static readonly string[] HelpPatterns =
    [
        "help", "support", "contact", "chat", "faq", "assistance",
        "phone", "email", "call", "message", "question"
    ];

    // Control types commonly used for help
    private static readonly HashSet<string> HelpControlTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Link", "HyperLink", "Button", "IconButton", "Icon",
        "Label", "Text", "ChatWidget", "Component"
    };

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check at screen level for help mechanism patterns
        if (node.Type.Equals("Screen", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var finding in CheckScreenForHelpMechanisms(node, context))
                yield return finding;
        }

        // Check individual controls for help-related functionality
        foreach (var finding in CheckHelpControlPlacement(node, context))
            yield return finding;

        // Check for help form/contact patterns
        foreach (var finding in CheckHelpFormPatterns(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckScreenForHelpMechanisms(UiNode node, RuleContext context)
    {
        // Find all help-related controls on this screen
        var helpControls = FindHelpControls(node).ToList();

        if (helpControls.Count == 0)
        {
            yield return new Finding(
                Id: $"{Id}:NO_HELP:{node.Id}",
                Severity: Severity.Low,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_NO_HELP",
                Message: $"Screen '{node.Id}' has no visible help mechanism (help link, contact info, FAQ, chat widget).",
                WcagReference: "WCAG 2.2 § 3.2.6 Consistent Help (Level A)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Users should have access to help on every screen. If help exists elsewhere, ensure it's consistently placed across all screens.",
                SuggestedFix: $"Add help mechanisms to '{node.Id}': (1) Help link/button in header/footer, (2) Contact information, (3) Chat widget, (4) FAQ link. Place in same location as other screens.",
                WcagCriterion: WcagCriterion.ConsistentHelp_3_2_6
            );
        }

        // Check for multiple types of help (redundancy is good for accessibility)
        var helpTypes = CategorizeHelpControls(helpControls);
        
        if (helpTypes.Count == 1 && helpControls.Count == 1)
        {
            yield return new Finding(
                Id: $"{Id}:LIMITED_HELP:{node.Id}",
                Severity: Severity.Low,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: helpControls[0].Id,
                ControlType: helpControls[0].Type,
                IssueType: $"{Id}_LIMITED_HELP",
                Message: $"Screen '{node.Id}' has limited help options. Consider providing multiple ways to get help.",
                WcagReference: "WCAG 2.2 § 3.2.6 Consistent Help (Level A)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Different users prefer different help methods (self-service FAQ, email, phone, chat). Multiple options improve accessibility.",
                SuggestedFix: $"Consider adding to '{node.Id}': (1) Self-help FAQ, (2) Contact email, (3) Phone number, (4) Live chat option.",
                WcagCriterion: WcagCriterion.ConsistentHelp_3_2_6
            );
        }

        // Check help control positioning (should be consistent - typically header/footer)
        foreach (var helpControl in helpControls)
        {
            var y = GetPosition(helpControl, "Y");
            var screenHeight = GetPosition(node, "Height");

            // Help should be in header area (top 15%) or footer area (bottom 15%)
            if (y > 0 && screenHeight > 0)
            {
                var relativePosition = (double)y / screenHeight;
                var isInHeaderOrFooter = relativePosition < 0.15 || relativePosition > 0.85;

                if (!isInHeaderOrFooter)
                {
                    yield return new Finding(
                        Id: $"{Id}:PLACEMENT:{helpControl.Id}",
                        Severity: Severity.Medium,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: helpControl.Id,
                        ControlType: helpControl.Type,
                        IssueType: $"{Id}_PLACEMENT",
                        Message: $"Help control '{helpControl.Id}' is placed in main content area. For consistency, place help in header or footer.",
                        WcagReference: "WCAG 2.2 § 3.2.6 Consistent Help (Level A)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Help mechanisms should be in predictable locations (header/footer) so users can find them consistently across all screens.",
                        SuggestedFix: $"Move help control '{helpControl.Id}' to a header or footer component that appears consistently on all screens.",
                        WcagCriterion: WcagCriterion.ConsistentHelp_3_2_6
                    );
                }
            }
        }
    }

    private IEnumerable<Finding> CheckHelpControlPlacement(UiNode node, RuleContext context)
    {
        if (!IsHelpControl(node)) yield break;

        // Check if this help control is in a consistent container (header, footer, nav)
        var isInConsistentContainer = IsInConsistentContainer(node, context);

        if (!isInConsistentContainer)
        {
            yield return new Finding(
                Id: $"{Id}:NOT_IN_CONTAINER:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_NOT_IN_CONTAINER",
                Message: $"Help control '{node.Id}' is not in a shared header/footer/nav component. May not appear consistently across screens.",
                WcagReference: "WCAG 2.2 § 3.2.6 Consistent Help (Level A)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Help mechanisms should be in shared components to ensure they appear in the same relative position on all screens.",
                SuggestedFix: $"Move help control '{node.Id}' into a reusable header or footer component used across all screens in the app.",
                WcagCriterion: WcagCriterion.ConsistentHelp_3_2_6
            );
        }

        // Check if help link is accessible
        if (node.Type.Contains("Link", StringComparison.OrdinalIgnoreCase) ||
            node.Type.Contains("Button", StringComparison.OrdinalIgnoreCase))
        {
            var hasAccessibleName = !string.IsNullOrWhiteSpace(node.Name) ||
                                   !string.IsNullOrWhiteSpace(node.Text) ||
                                   node.Properties?.ContainsKey("AccessibleLabel") == true;

            if (!hasAccessibleName)
            {
                yield return new Finding(
                    Id: $"{Id}:NO_LABEL:{node.Id}",
                    Severity: Severity.High,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_NO_LABEL",
                    Message: $"Help control '{node.Id}' has no accessible name. Screen reader users won't know its purpose.",
                    WcagReference: "WCAG 2.2 § 3.2.6 Consistent Help (Level A)",
                    Section508Reference: "Section 508 - Name, Role, Value",
                    Rationale: "Help controls must have clear labels so all users can identify and use them.",
                    SuggestedFix: $"Add AccessibleLabel (e.g., 'Get help', 'Contact support', 'Open FAQ') to help control '{node.Id}'.",
                    WcagCriterion: WcagCriterion.ConsistentHelp_3_2_6
                );
            }
        }
    }

    private IEnumerable<Finding> CheckHelpFormPatterns(UiNode node, RuleContext context)
    {
        // Check if this is a contact/help form
        var nodeName = node.Name?.ToLowerInvariant() ?? "";
        var isHelpForm = nodeName.Contains("contact") || nodeName.Contains("help") ||
                        nodeName.Contains("support") || nodeName.Contains("feedback");

        if (!isHelpForm || !node.Type.Equals("Screen", StringComparison.OrdinalIgnoreCase))
            yield break;

        // Help forms should be reachable from all screens
        yield return new Finding(
            Id: $"{Id}:HELP_FORM:{node.Id}",
            Severity: Severity.Low,
            Surface: context.Surface,
            AppName: context.AppName,
            Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
            ControlId: node.Id,
            ControlType: node.Type,
            IssueType: $"{Id}_HELP_FORM",
            Message: $"Help/contact form screen '{node.Id}' detected. Ensure it's accessible from a consistent location on all screens.",
            WcagReference: "WCAG 2.2 § 3.2.6 Consistent Help (Level A)",
            Section508Reference: "Section 508 E207.2 - WCAG Conformance",
            Rationale: "Users should be able to reach help from any screen in the app through a consistently placed link.",
            SuggestedFix: $"Add a 'Help' or 'Contact' link to the app's header/footer that navigates to '{node.Id}' from any screen.",
            WcagCriterion: WcagCriterion.ConsistentHelp_3_2_6
        );

        // Check if form has required contact information
        var hasContactInfo = HasContactInformation(node);
        if (!hasContactInfo)
        {
            yield return new Finding(
                Id: $"{Id}:NO_CONTACT_INFO:{node.Id}",
                Severity: Severity.Medium,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_NO_CONTACT_INFO",
                Message: $"Help form '{node.Id}' should display human contact details (phone, email) not just a form submission.",
                WcagReference: "WCAG 2.2 § 3.2.6 Consistent Help (Level A)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Per WCAG 3.2.6, help should include human contact details and/or self-help options, not just forms.",
                SuggestedFix: $"Add to '{node.Id}': (1) Support phone number, (2) Support email address, (3) Business hours, (4) Expected response time.",
                WcagCriterion: WcagCriterion.ConsistentHelp_3_2_6
            );
        }
    }

    private static IEnumerable<UiNode> FindHelpControls(UiNode node)
    {
        if (IsHelpControl(node))
            yield return node;

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                foreach (var helpControl in FindHelpControls(child))
                    yield return helpControl;
            }
        }
    }

    private static bool IsHelpControl(UiNode node)
    {
        if (!HelpControlTypes.Contains(node.Type))
            return false;

        var name = node.Name?.ToLowerInvariant() ?? "";
        var text = node.Text?.ToLowerInvariant() ?? "";

        return HelpPatterns.Any(p => name.Contains(p) || text.Contains(p));
    }

    private static Dictionary<string, int> CategorizeHelpControls(List<UiNode> helpControls)
    {
        var categories = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var control in helpControls)
        {
            var name = control.Name?.ToLowerInvariant() ?? "";
            var text = control.Text?.ToLowerInvariant() ?? "";
            var combined = name + " " + text;

            if (combined.Contains("chat"))
                IncrementCategory(categories, "chat");
            else if (combined.Contains("phone") || combined.Contains("call"))
                IncrementCategory(categories, "phone");
            else if (combined.Contains("email") || combined.Contains("mail"))
                IncrementCategory(categories, "email");
            else if (combined.Contains("faq") || combined.Contains("question"))
                IncrementCategory(categories, "faq");
            else if (combined.Contains("help") || combined.Contains("support"))
                IncrementCategory(categories, "general_help");
            else
                IncrementCategory(categories, "other");
        }

        return categories;
    }

    private static void IncrementCategory(Dictionary<string, int> dict, string key)
    {
        if (dict.ContainsKey(key))
            dict[key]++;
        else
            dict[key] = 1;
    }

    private static bool IsInConsistentContainer(UiNode node, RuleContext context)
    {
        // Check if parent is a header, footer, or nav component
        var consistentContainers = new[] { "header", "footer", "nav", "navigation", "topbar", "bottombar", "toolbar" };

        // Check parent information from context or node metadata
        var parentInfo = node.Meta?.SectionName?.ToLowerInvariant() ?? "";
        
        return consistentContainers.Any(c => parentInfo.Contains(c));
    }

    private static bool HasContactInformation(UiNode node)
    {
        var hasPhone = FindDescendantWithPattern(node, new[] { "phone", "tel", "call" });
        var hasEmail = FindDescendantWithPattern(node, new[] { "email", "mail", "@" });
        
        return hasPhone || hasEmail;
    }

    private static bool FindDescendantWithPattern(UiNode node, string[] patterns)
    {
        var name = node.Name?.ToLowerInvariant() ?? "";
        var text = node.Text?.ToLowerInvariant() ?? "";

        if (patterns.Any(p => name.Contains(p) || text.Contains(p)))
            return true;

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                if (FindDescendantWithPattern(child, patterns))
                    return true;
            }
        }

        return false;
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
}
