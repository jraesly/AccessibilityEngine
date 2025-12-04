using System.Collections.Generic;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Rules;

public static class BasicRules
{
    public static IReadOnlyList<IRule> All { get; } = new IRule[]
    {
        new MissingAccessibleNameRule(),
        new ImageAltTextRule(),
        new IconOnlyControlRule(),
        new EmptyLabelFieldRule()
    };
}
