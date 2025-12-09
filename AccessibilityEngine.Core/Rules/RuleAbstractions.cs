using System.Collections.Generic;
using AccessibilityEngine.Core.Models;

namespace AccessibilityEngine.Core.Rules;

/// <summary>
/// Context provided to each rule during evaluation.
/// Contains metadata about the current node's position in the tree.
/// </summary>
public sealed class RuleContext
{
    /// <summary>
    /// The surface type being scanned (CanvasApp, ModelDrivenApp, etc.)
    /// </summary>
    public SurfaceType Surface { get; }

    /// <summary>
    /// The name of the app being scanned.
    /// </summary>
    public string? AppName { get; }

    /// <summary>
    /// The parent node of the current node being evaluated, if any.
    /// </summary>
    public UiNode? Parent { get; }

    /// <summary>
    /// Sibling nodes at the same level (excluding the current node).
    /// </summary>
    public IReadOnlyList<UiNode> Siblings { get; }

    /// <summary>
    /// The depth of the current node in the tree (0 = root level).
    /// </summary>
    public int Depth { get; }

    /// <summary>
    /// The index of the current node among its siblings.
    /// </summary>
    public int SiblingIndex { get; }

    /// <summary>
    /// All ancestor nodes from root to parent.
    /// </summary>
    public IReadOnlyList<UiNode> Ancestors { get; }

    /// <summary>
    /// The screen this control belongs to, if applicable.
    /// </summary>
    public UiNode? Screen { get; }

    public RuleContext(
        SurfaceType surface,
        string? appName,
        UiNode? parent = null,
        IReadOnlyList<UiNode>? siblings = null,
        int depth = 0,
        int siblingIndex = 0,
        IReadOnlyList<UiNode>? ancestors = null,
        UiNode? screen = null)
    {
        Surface = surface;
        AppName = appName;
        Parent = parent;
        Siblings = siblings ?? [];
        Depth = depth;
        SiblingIndex = siblingIndex;
        Ancestors = ancestors ?? [];
        Screen = screen;
    }

    /// <summary>
    /// Creates a new context for a child node.
    /// </summary>
    public RuleContext ForChild(UiNode parent, IReadOnlyList<UiNode> siblings, int siblingIndex, UiNode? screen = null)
    {
        var newAncestors = new List<UiNode>(Ancestors) { parent };
        return new RuleContext(
            Surface,
            AppName,
            parent,
            siblings,
            Depth + 1,
            siblingIndex,
            newAncestors,
            screen ?? Screen);
    }
}

public interface IRule
{
    string Id { get; }
    string Description { get; }
    Severity Severity { get; }
    SurfaceType[]? AppliesTo { get; }

    IEnumerable<Finding>? Evaluate(UiNode node, RuleContext context);
}
