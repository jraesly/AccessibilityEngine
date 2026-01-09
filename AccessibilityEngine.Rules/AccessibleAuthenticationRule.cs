using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

/// <summary>
/// Rule that checks for accessible authentication issues.
/// WCAG 3.3.8 (New in 2.2): Cognitive function tests must not be required for authentication unless alternatives exist.
/// </summary>
public sealed class AccessibleAuthenticationRule : IRule
{
    public string Id => "ACCESSIBLE_AUTHENTICATION";
    public string Description => "Authentication must not require cognitive function tests without alternatives.";
    public Severity Severity => Severity.High;
    public SurfaceType[]? AppliesTo => [SurfaceType.CanvasApp, SurfaceType.ModelDrivenApp, SurfaceType.PortalPage];

    // Patterns indicating authentication screens
    private static readonly string[] AuthScreenPatterns =
    [
        "login", "signin", "sign-in", "sign_in", "logon", "log-on",
        "authenticate", "auth", "password", "credentials",
        "register", "signup", "sign-up", "sign_up", "create_account"
    ];

    // CAPTCHA and puzzle-related patterns
    private static readonly string[] CaptchaPatterns =
    [
        "captcha", "recaptcha", "hcaptcha", "puzzle", "verify",
        "robot", "human", "challenge", "security_check"
    ];

    public IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context)
    {
        if (node is null) yield break;

        // Check if this is an authentication-related screen
        if (node.Type.Equals("Screen", StringComparison.OrdinalIgnoreCase))
        {
            if (IsAuthenticationScreen(node))
            {
                foreach (var finding in CheckAuthenticationScreenAccessibility(node, context))
                    yield return finding;
            }
        }

        // Check for CAPTCHA controls
        foreach (var finding in CheckCaptchaControls(node, context))
            yield return finding;

        // Check for password fields
        foreach (var finding in CheckPasswordFields(node, context))
            yield return finding;

        // Check for security question patterns
        foreach (var finding in CheckSecurityQuestions(node, context))
            yield return finding;

        // Check for OTP/verification code inputs
        foreach (var finding in CheckVerificationCodeFields(node, context))
            yield return finding;
    }

    private IEnumerable<Finding> CheckAuthenticationScreenAccessibility(UiNode node, RuleContext context)
    {
        // Look for cognitive function test indicators
        var hasPasswordField = HasPasswordField(node);
        var hasCaptcha = HasCaptcha(node);
        var hasSecurityQuestion = HasSecurityQuestion(node);
        var hasPasswordManager = SupportsPasswordManager(node);
        var hasCopyPaste = SupportsCopyPaste(node);
        var hasAlternativeAuth = HasAlternativeAuth(node);

        // Check password field accessibility
        if (hasPasswordField)
        {
            if (!hasPasswordManager && !hasCopyPaste)
            {
                yield return new Finding(
                    Id: $"{Id}:PASSWORD_MEMORY:{node.Id}",
                    Severity: Severity.High,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_PASSWORD_MEMORY",
                    Message: $"Authentication screen '{node.Id}' requires password entry. Ensure password managers work and copy/paste is enabled.",
                    WcagReference: "WCAG 2.2 § 3.3.8 Accessible Authentication (Minimum) (Level AA)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Remembering passwords is a cognitive function test. Users must be able to use password managers or copy/paste to avoid memorization.",
                    SuggestedFix: $"Ensure password fields on '{node.Id}': (1) Work with password manager autofill, (2) Allow paste from clipboard, (3) Have proper autocomplete attributes.",
                    WcagCriterion: WcagCriterion.AccessibleAuthenticationMinimum_3_3_8
                );
            }

            if (!hasAlternativeAuth)
            {
                yield return new Finding(
                    Id: $"{Id}:NO_ALTERNATIVE:{node.Id}",
                    Severity: Severity.Medium,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_NO_ALTERNATIVE",
                    Message: $"Authentication screen '{node.Id}' has no alternative authentication method (SSO, biometric, magic link).",
                    WcagReference: "WCAG 2.2 § 3.3.8 Accessible Authentication (Minimum) (Level AA)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Providing alternative authentication methods helps users who struggle with password-based authentication.",
                    SuggestedFix: $"Consider adding to '{node.Id}': SSO/OAuth login buttons, biometric authentication option, email magic link, or QR code scan for mobile app authentication.",
                    WcagCriterion: WcagCriterion.AccessibleAuthenticationMinimum_3_3_8
                );
            }
        }

        // Check for cognitive tests
        if (hasCaptcha || hasSecurityQuestion)
        {
            var testType = hasCaptcha ? "CAPTCHA" : "security question";
            
            yield return new Finding(
                Id: $"{Id}:COGNITIVE_TEST:{node.Id}",
                Severity: Severity.High,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_COGNITIVE_TEST",
                Message: $"Authentication screen '{node.Id}' appears to use {testType}, which is a cognitive function test.",
                WcagReference: "WCAG 2.2 § 3.3.8 Accessible Authentication (Minimum) (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: $"{testType}s require cognitive effort (memory, transcription, puzzle-solving) that creates barriers for users with cognitive disabilities.",
                SuggestedFix: $"For {testType} on '{node.Id}': (1) Provide an alternative method (audio CAPTCHA, object recognition), (2) Use invisible CAPTCHA that doesn't require user interaction, (3) Consider removing if not essential.",
                WcagCriterion: WcagCriterion.AccessibleAuthenticationMinimum_3_3_8
            );
        }
    }

    private IEnumerable<Finding> CheckCaptchaControls(UiNode node, RuleContext context)
    {
        var nodeName = node.Name?.ToLowerInvariant() ?? "";
        var nodeType = node.Type.ToLowerInvariant();

        var isCaptcha = CaptchaPatterns.Any(p => 
            nodeName.Contains(p) || nodeType.Contains(p));

        if (!isCaptcha) yield break;

        // Check for accessible alternatives
        var hasAudioAlternative = HasAudioCaptchaAlternative(node, context);
        var hasObjectRecognition = IsObjectRecognitionCaptcha(node);

        if (!hasAudioAlternative && !hasObjectRecognition)
        {
            yield return new Finding(
                Id: $"{Id}:CAPTCHA:{node.Id}",
                Severity: Severity.Critical,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_CAPTCHA",
                Message: $"CAPTCHA control '{node.Id}' detected without accessible alternatives (audio option or object recognition).",
                WcagReference: "WCAG 2.2 § 3.3.8 Accessible Authentication (Minimum) (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Visual CAPTCHAs requiring text transcription are cognitive tests that block users with cognitive disabilities. Object recognition is an acceptable alternative.",
                SuggestedFix: $"For CAPTCHA '{node.Id}': (1) Use reCAPTCHA v3 (invisible), (2) Provide audio CAPTCHA option, (3) Use object recognition ('select all images with cars'), (4) Use honeypot fields instead.",
                WcagCriterion: WcagCriterion.AccessibleAuthenticationMinimum_3_3_8
            );
        }
    }

    private IEnumerable<Finding> CheckPasswordFields(UiNode node, RuleContext context)
    {
        if (node.Properties == null) yield break;

        // Check if this is a password field
        var isPasswordField = node.Properties.TryGetValue("Mode", out var mode) &&
                             mode?.ToString()?.Contains("Password", StringComparison.OrdinalIgnoreCase) == true;

        if (!isPasswordField)
        {
            var nodeName = node.Name?.ToLowerInvariant() ?? "";
            isPasswordField = nodeName.Contains("password") || nodeName.Contains("pwd");
        }

        if (!isPasswordField) yield break;

        // Check for copy/paste blocking
        var hasDisablePaste = node.Properties.TryGetValue("DisablePaste", out var disablePaste);
        var hasOnPaste = node.Properties.TryGetValue("OnPaste", out var onPaste);
        
        if (hasDisablePaste || hasOnPaste)
        {
            var pasteBlocked = (hasDisablePaste && (disablePaste is true || 
                              disablePaste?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)) ||
                              (hasOnPaste && onPaste?.ToString()?.Contains("false", StringComparison.OrdinalIgnoreCase) == true);

            if (pasteBlocked)
            {
                yield return new Finding(
                    Id: $"{Id}:PASTE_BLOCKED:{node.Id}",
                    Severity: Severity.Critical,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_PASTE_BLOCKED",
                    Message: $"Password field '{node.Id}' blocks paste functionality, preventing password manager use.",
                    WcagReference: "WCAG 2.2 § 3.3.8 Accessible Authentication (Minimum) (Level AA)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Blocking paste forces users to memorize and type passwords, which is a cognitive function test. Password managers are a supported mechanism.",
                    SuggestedFix: $"Remove paste blocking on '{node.Id}'. Allow users to paste passwords from password managers or other sources.",
                    WcagCriterion: WcagCriterion.AccessibleAuthenticationMinimum_3_3_8
                );
            }
        }

        // Check for show/hide password toggle
        var hasShowPassword = CheckForShowPasswordToggle(node, context);
        if (!hasShowPassword)
        {
            yield return new Finding(
                Id: $"{Id}:NO_SHOW_PASSWORD:{node.Id}",
                Severity: Severity.Low,
                Surface: context.Surface,
                AppName: context.AppName,
                Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                ControlId: node.Id,
                ControlType: node.Type,
                IssueType: $"{Id}_NO_SHOW_PASSWORD",
                Message: $"Password field '{node.Id}' has no show/hide toggle to help users verify their input.",
                WcagReference: "WCAG 2.2 § 3.3.8 Accessible Authentication (Minimum) (Level AA)",
                Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                Rationale: "Show password toggles help users verify their input without relying solely on memory.",
                SuggestedFix: $"Add a show/hide password toggle next to '{node.Id}' that allows users to view their typed password.",
                WcagCriterion: WcagCriterion.AccessibleAuthenticationMinimum_3_3_8
            );
        }
    }

    private IEnumerable<Finding> CheckSecurityQuestions(UiNode node, RuleContext context)
    {
        var nodeName = node.Name?.ToLowerInvariant() ?? "";
        var nodeText = node.Text?.ToLowerInvariant() ?? "";
        
        var securityQuestionPatterns = new[]
        {
            "security question", "secret question", "mother's maiden",
            "first pet", "school name", "street you grew up", "favorite"
        };

        var isSecurityQuestion = securityQuestionPatterns.Any(p =>
            nodeName.Contains(p) || nodeText.Contains(p));

        if (!isSecurityQuestion) yield break;

        yield return new Finding(
            Id: $"{Id}:SECURITY_QUESTION:{node.Id}",
            Severity: Severity.High,
            Surface: context.Surface,
            AppName: context.AppName,
            Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
            ControlId: node.Id,
            ControlType: node.Type,
            IssueType: $"{Id}_SECURITY_QUESTION",
            Message: $"Security question '{node.Id}' requires users to remember personal information, which is a cognitive function test.",
            WcagReference: "WCAG 2.2 § 3.3.8 Accessible Authentication (Minimum) (Level AA)",
            Section508Reference: "Section 508 E207.2 - WCAG Conformance",
            Rationale: "Security questions require memorization of specific facts, creating barriers for users with memory impairments.",
            SuggestedFix: $"Replace security questions on '{node.Id}' with: (1) Email verification links, (2) SMS codes, (3) Authenticator apps, (4) Physical security keys.",
            WcagCriterion: WcagCriterion.AccessibleAuthenticationMinimum_3_3_8
        );
    }

    private IEnumerable<Finding> CheckVerificationCodeFields(UiNode node, RuleContext context)
    {
        var nodeName = node.Name?.ToLowerInvariant() ?? "";
        var hintText = node.Properties?.TryGetValue("HintText", out var hint) == true ? 
                      hint?.ToString()?.ToLowerInvariant() ?? "" : "";

        var isOtpField = nodeName.Contains("otp") || nodeName.Contains("code") ||
                        nodeName.Contains("verification") || nodeName.Contains("2fa") ||
                        hintText.Contains("code") || hintText.Contains("verification");

        if (!isOtpField) yield break;

        // OTP fields should allow paste
        if (node.Properties?.TryGetValue("DisablePaste", out var disablePaste) == true)
        {
            var pasteBlocked = disablePaste is true ||
                              disablePaste?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

            if (pasteBlocked)
            {
                yield return new Finding(
                    Id: $"{Id}:OTP_PASTE:{node.Id}",
                    Severity: Severity.High,
                    Surface: context.Surface,
                    AppName: context.AppName,
                    Screen: node.Meta?.ScreenName ?? context.Screen?.Id,
                    ControlId: node.Id,
                    ControlType: node.Type,
                    IssueType: $"{Id}_OTP_PASTE",
                    Message: $"Verification code field '{node.Id}' blocks paste, requiring manual transcription which is a cognitive test.",
                    WcagReference: "WCAG 2.2 § 3.3.8 Accessible Authentication (Minimum) (Level AA)",
                    Section508Reference: "Section 508 E207.2 - WCAG Conformance",
                    Rationale: "Users should be able to copy verification codes from email/SMS and paste them, reducing transcription errors.",
                    SuggestedFix: $"Enable paste on verification code field '{node.Id}' to allow copying codes from messages or authenticator apps.",
                    WcagCriterion: WcagCriterion.AccessibleAuthenticationMinimum_3_3_8
                );
            }
        }
    }

    private static bool IsAuthenticationScreen(UiNode node)
    {
        var screenName = node.Name?.ToLowerInvariant() ?? "";
        return AuthScreenPatterns.Any(p => screenName.Contains(p));
    }

    private static bool HasPasswordField(UiNode node) =>
        FindDescendant(node, n => 
            n.Properties?.TryGetValue("Mode", out var m) == true && 
            m?.ToString()?.Contains("Password", StringComparison.OrdinalIgnoreCase) == true ||
            n.Name?.Contains("password", StringComparison.OrdinalIgnoreCase) == true);

    private static bool HasCaptcha(UiNode node) =>
        FindDescendant(node, n => 
            CaptchaPatterns.Any(p => 
                n.Name?.Contains(p, StringComparison.OrdinalIgnoreCase) == true ||
                n.Type.Contains(p, StringComparison.OrdinalIgnoreCase)));

    private static bool HasSecurityQuestion(UiNode node) =>
        FindDescendant(node, n =>
            n.Name?.Contains("security", StringComparison.OrdinalIgnoreCase) == true ||
            n.Text?.Contains("security question", StringComparison.OrdinalIgnoreCase) == true);

    private static bool SupportsPasswordManager(UiNode node) =>
        FindDescendant(node, n =>
            n.Properties?.ContainsKey("AutoComplete") == true ||
            n.Properties?.ContainsKey("autocomplete") == true);

    private static bool SupportsCopyPaste(UiNode node) =>
        !FindDescendant(node, n =>
            n.Properties?.TryGetValue("DisablePaste", out var v) == true &&
            (v is true || v?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true));

    private static bool HasAlternativeAuth(UiNode node) =>
        FindDescendant(node, n =>
            n.Name?.Contains("sso", StringComparison.OrdinalIgnoreCase) == true ||
            n.Name?.Contains("oauth", StringComparison.OrdinalIgnoreCase) == true ||
            n.Name?.Contains("microsoft", StringComparison.OrdinalIgnoreCase) == true ||
            n.Name?.Contains("google", StringComparison.OrdinalIgnoreCase) == true ||
            n.Name?.Contains("biometric", StringComparison.OrdinalIgnoreCase) == true ||
            n.Name?.Contains("fingerprint", StringComparison.OrdinalIgnoreCase) == true ||
            n.Name?.Contains("face", StringComparison.OrdinalIgnoreCase) == true ||
            n.Text?.Contains("Sign in with", StringComparison.OrdinalIgnoreCase) == true ||
            n.Text?.Contains("magic link", StringComparison.OrdinalIgnoreCase) == true);

    private static bool HasAudioCaptchaAlternative(UiNode node, RuleContext context)
    {
        foreach (var sibling in context.Siblings)
        {
            var name = sibling.Name?.ToLowerInvariant() ?? "";
            var text = sibling.Text?.ToLowerInvariant() ?? "";
            if (name.Contains("audio") || text.Contains("audio") ||
                name.Contains("listen") || text.Contains("listen"))
                return true;
        }
        return false;
    }

    private static bool IsObjectRecognitionCaptcha(UiNode node)
    {
        var name = node.Name?.ToLowerInvariant() ?? "";
        return name.Contains("select") || name.Contains("image") ||
               name.Contains("picture") || name.Contains("photo");
    }

    private static bool CheckForShowPasswordToggle(UiNode node, RuleContext context)
    {
        foreach (var sibling in context.Siblings)
        {
            var name = sibling.Name?.ToLowerInvariant() ?? "";
            var text = sibling.Text?.ToLowerInvariant() ?? "";
            if (name.Contains("show") || text.Contains("show") ||
                name.Contains("reveal") || text.Contains("reveal") ||
                name.Contains("eye") || name.Contains("visibility"))
                return true;
        }
        return false;
    }

    private static bool FindDescendant(UiNode node, Func<UiNode, bool> predicate)
    {
        if (predicate(node)) return true;
        
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                if (FindDescendant(child, predicate))
                    return true;
            }
        }
        return false;
    }
}
