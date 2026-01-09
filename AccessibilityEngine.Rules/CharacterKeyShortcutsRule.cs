using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for character key shortcuts without modifier keys.
/// WCAG 2.1.4: If a keyboard shortcut uses only letter, punctuation, number, or symbol characters,
/// then users must be able to turn it off, remap it, or it should only be active when focused.
/// </summary>
public sealed class CharacterKeyShortcutsRule : IRule
{
    public string Id => "CHARACTER_KEY_SHORTCUTS";
    public string Description => "Character key shortcuts must be remappable, disableable, or only active on focus.";
    public Severity Severity => Severity.Medium;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Pattern to detect single character shortcuts (no modifier)
    private static readonly Regex SingleCharShortcut = new(
        @"^[a-zA-Z0-9!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]$",
        RegexOptions.Compiled);

    // Pattern to detect shortcuts with modifiers (Ctrl, Alt, Shift, Meta/Cmd)
    private static readonly Regex ModifierShortcut = new(
        @"(ctrl|control|alt|shift|meta|cmd|command)\s*[\+\-]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check for keyboard shortcut definitions
        foreach (var finding in CheckKeyboardShortcuts(node, context))
            yield return finding;

        // Check for access key definitions
        foreach (var finding in CheckAccessKeys(node, context))
            yield return finding;

        // Check for hotkey handlers
        foreach (var finding in CheckHotkeyHandlers(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckKeyboardShortcuts(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        var shortcutProps = new[] { "KeyboardShortcut", "Shortcut", "HotKey", "AccessKey", "KeyBinding" };

        foreach (var prop in shortcutProps)
        {
            if (!node.Properties.TryGetValue(prop, out var value)) continue;
            
            var shortcut = value?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(shortcut)) continue;

            // Check if it's a single character shortcut without modifiers
            if (IsSingleCharacterShortcut(shortcut))
            {
                // Check if there's a way to disable or remap
                var canDisable = node.Properties.ContainsKey("ShortcutEnabled") ||
                                node.Properties.ContainsKey("DisableShortcut") ||
                                node.Properties.ContainsKey("ShortcutRemappable");

                var isOnlyOnFocus = node.Properties.TryGetValue("ShortcutScope", out var scope) &&
                                   scope?.ToString()?.ToLowerInvariant() == "focus";

                if (!canDisable && !isOnlyOnFocus)
                {
                    yield return new Finding(
                        Id: $"{Id}:{prop}:{node.Id}",
                        Severity: Severity.High,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_SINGLE_CHAR",
                        Message: $"Control '{node.Id}' has single-character keyboard shortcut '{shortcut}' without modifier keys.",
                        WcagReference: "WCAG 2.2 – 2.1.4 Character Key Shortcuts (Level A)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Single-character shortcuts can be accidentally triggered by speech input users who may inadvertently speak characters.",
                        SuggestedFix: $"Either: (1) Add a modifier key (Ctrl, Alt, Shift) to the shortcut, (2) Allow users to disable or remap the shortcut, or (3) Make it only active when '{node.Id}' has focus.",
                        WcagCriterion: WcagCriterion.CharacterKeyShortcuts_2_1_4
                    );
                }
            }
        }
    }

    private IEnumerable<Finding> CheckAccessKeys(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Access keys (like HTML accesskey attribute) are typically single characters
        if (node.Properties.TryGetValue("AccessKey", out var accessKey))
        {
            var key = accessKey?.ToString() ?? "";
            
            // Access keys are usually single characters activated with Alt+key
            // This is acceptable as it requires a modifier, but check for global scope
            if (!string.IsNullOrWhiteSpace(key) && key.Length == 1)
            {
                // Check if it's globally active (not just on focus)
                var isGlobal = node.Properties.TryGetValue("AccessKeyScope", out var scope) &&
                              scope?.ToString()?.ToLowerInvariant() == "global";

                if (isGlobal)
                {
                    yield return new Finding(
                        Id: $"{Id}:ACCESSKEY:{node.Id}",
                        Severity: Severity.Medium,
                        Surface: context.Surface,
                        AppName: context.AppName,
                        Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                        ControlId: node.Id,
                        ControlType: node.Type,
                        IssueType: $"{Id}_GLOBAL_ACCESSKEY",
                        Message: $"Control '{node.Id}' has a globally-scoped access key '{key}' that may conflict with browser or assistive technology shortcuts.",
                        WcagReference: "WCAG 2.2 – 2.1.4 Character Key Shortcuts (Level A)",
                        Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                        Rationale: "Global access keys can interfere with browser shortcuts and assistive technology commands.",
                        SuggestedFix: $"Consider limiting the scope of access key '{key}' on '{node.Id}' to when the control or its container has focus.",
                        WcagCriterion: WcagCriterion.CharacterKeyShortcuts_2_1_4
                    );
                }
            }
        }
    }

    private IEnumerable<Finding> CheckHotkeyHandlers(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check for OnKeyPress or similar handlers that might capture single keys
        var keyHandlerProps = new[] { "OnKeyPress", "OnKeyDown", "OnKeyUp", "KeyHandler" };

        foreach (var prop in keyHandlerProps)
        {
            if (!node.Properties.TryGetValue(prop, out var handler)) continue;
            
            var handlerStr = handler?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(handlerStr)) continue;

            // Check if handler appears to handle single character keys
            // This is a heuristic check - look for patterns like key == "a" or keyCode == 65
            var hasSingleKeyCheck = Regex.IsMatch(handlerStr, 
                @"key\s*[=!]=\s*['""][a-zA-Z0-9]['""]|keyCode\s*[=!]=\s*\d{1,3}", 
                RegexOptions.IgnoreCase);

            var hasModifierCheck = Regex.IsMatch(handlerStr,
                @"(ctrl|alt|shift|meta)Key|getModifierState",
                RegexOptions.IgnoreCase);

            if (hasSingleKeyCheck && !hasModifierCheck)
            {
                yield return new Finding(
                    Id: $"{Id}:HANDLER:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_KEY_HANDLER",
                    Message: $"Control '{node.Id}' has a key handler that may respond to single character keys without checking for modifier keys.",
                    WcagReference: "WCAG 2.2 – 2.1.4 Character Key Shortcuts (Level A)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Key handlers that respond to single characters without modifiers can interfere with speech input and assistive technologies.",
                    SuggestedFix: $"Update the key handler in '{node.Id}' to either require modifier keys, only activate when focused, or provide a way to disable the shortcut.",
                    WcagCriterion: WcagCriterion.CharacterKeyShortcuts_2_1_4
                );
            }
        }
    }

    private static bool IsSingleCharacterShortcut(string shortcut)
    {
        // Check if shortcut is a single character without modifiers
        shortcut = shortcut.Trim();
        
        // If it contains a modifier, it's not a single-char shortcut
        if (ModifierShortcut.IsMatch(shortcut))
            return false;

        // Check if it's a single printable character
        return SingleCharShortcut.IsMatch(shortcut);
    }
}
