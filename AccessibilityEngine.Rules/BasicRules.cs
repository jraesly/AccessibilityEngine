using System.Collections.Generic;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

public static class BasicRules
{
    public static IReadOnlyList<IRule> All { get; } =
    [
        // General accessibility rules
        new AccessibleLabelRule(),
        new MissingAccessibleNameRule(),
        new ImageAltTextRule(),
        new IconOnlyControlRule(),
        new EmptyLabelFieldRule(),

        // Canvas App specific rules
        new TabIndexRule(),
        new ColorContrastRule(),
        new FocusIndicatorRule(),
        new TouchTargetSizeRule(),
        new ScreenReaderOrderRule(),

        // Model-Driven App specific rules
        new MdaFieldLabelRule(),
        new MdaRequiredFieldRule(),
        new MdaStructureNamingRule(),
        new MdaPcfControlRule(),
        new MdaReadOnlyFieldRule(),
        new MdaConditionalVisibilityRule(),
        new MdaEmbeddedContentRule(),

        // WCAG 2.2 comprehensive rules
        new KeyboardAccessRule(),           // WCAG 2.1.1, 2.1.2, 2.4.3
        new LanguageOfPageRule(),           // WCAG 3.1.1, 3.1.2
        new ErrorIdentificationRule(),      // WCAG 3.3.1, 3.3.2, 1.4.1
        new InputLabelAssociationRule(),    // WCAG 1.3.1, 3.3.2, 2.5.3
        new HeadingStructureRule(),         // WCAG 1.3.1, 2.4.6
        new StatusMessageRule(),            // WCAG 4.1.3
        new TimingControlRule(),            // WCAG 2.2.1, 2.2.2

        // WCAG 2.2 Input Modalities (2.5.x)
        new PointerGesturesRule(),          // WCAG 2.5.1, 2.5.2
        new MotionActuationRule(),          // WCAG 2.5.4
        new DraggingMovementsRule(),        // WCAG 2.5.7 (New in 2.2)

        // WCAG 2.2 Adaptable & Distinguishable (1.3.x, 1.4.x)
        new OrientationRule(),              // WCAG 1.3.4
        new ContentOnHoverFocusRule(),      // WCAG 1.4.13
        new SensoryCharacteristicsRule(),   // WCAG 1.3.3
        new IdentifyInputPurposeRule(),     // WCAG 1.3.5
        new ResizeTextRule(),               // WCAG 1.4.4
        new ImagesOfTextRule(),             // WCAG 1.4.5
        new ReflowRule(),                   // WCAG 1.4.10
        new NonTextContrastRule(),          // WCAG 1.4.11
        new TextSpacingRule(),              // WCAG 1.4.12

        // WCAG 2.2 Keyboard Accessible (2.1.x)
        new CharacterKeyShortcutsRule(),    // WCAG 2.1.4

        // WCAG 2.2 Seizures and Physical Reactions (2.3.x)
        new FlashingContentRule(),          // WCAG 2.3.1

        // WCAG 2.2 Navigable (2.4.x)
        new BypassBlocksRule(),             // WCAG 2.4.1
        new PageTitleRule(),                // WCAG 2.4.2
        new LinkPurposeRule(),              // WCAG 2.4.4
        new MultipleWaysRule(),             // WCAG 2.4.5
        new FocusNotObscuredRule(),         // WCAG 2.4.11 (New in 2.2)

        // WCAG 2.2 Predictable (3.2.x)
        new PredictableBehaviorRule(),      // WCAG 3.2.1, 3.2.2
        new ConsistentNavigationRule(),     // WCAG 3.2.3, 3.2.4
        new ConsistentHelpRule(),           // WCAG 3.2.6 (New in 2.2)

        // WCAG 2.2 Input Assistance (3.3.x)
        new ErrorSuggestionRule(),          // WCAG 3.3.3
        new ErrorPreventionRule(),          // WCAG 3.3.4
        new RedundantEntryRule(),           // WCAG 3.3.7 (New in 2.2)
        new AccessibleAuthenticationRule()  // WCAG 3.3.8 (New in 2.2)
    ];
}