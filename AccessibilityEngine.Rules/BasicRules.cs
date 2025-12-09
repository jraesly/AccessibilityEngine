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
        new MdaEmbeddedContentRule()
    ];
}