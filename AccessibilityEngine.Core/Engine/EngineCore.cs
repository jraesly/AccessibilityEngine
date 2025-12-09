using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;

namespace AccessibilityEngine.Core.Engine;

/// <summary>
/// Pure rule engine that analyzes a UiTree with deterministic rules.
/// </summary>
public static class Engine
{
    public static ScanResult Analyze(UiTree uiTree, IEnumerable<IRule> rules)
    {
        if (uiTree is null) throw new ArgumentNullException(nameof(uiTree));
        if (rules is null) throw new ArgumentNullException(nameof(rules));

        var rulesList = rules.ToList();
        var findings = new List<Finding>();

        // Create initial context at root level
        var rootContext = new RuleContext(uiTree.Surface, uiTree.AppName);

        // Walk the tree with context
        foreach (var (node, context) in WalkWithContext(uiTree.Nodes, rootContext))
        {
            foreach (var rule in rulesList)
            {
                if (rule.AppliesTo != null && !Array.Exists(rule.AppliesTo, s => s == uiTree.Surface))
                    continue;

                var results = rule.Evaluate(node, context);
                if (results != null)
                {
                    findings.AddRange(results);
                }
            }
        }

        return ScanResult.FromFindings(findings);
    }

    /// <summary>
    /// Walks the tree and yields each node with its context (parent, siblings, depth, etc.)
    /// </summary>
    private static IEnumerable<(UiNode Node, RuleContext Context)> WalkWithContext(
        IReadOnlyList<UiNode> nodes,
        RuleContext parentContext)
    {
        if (nodes == null || nodes.Count == 0) yield break;

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            
            // Get siblings (all nodes except current)
            var siblings = nodes.Where((_, idx) => idx != i).ToList();

            // Determine if this is a screen (for tracking)
            var screen = IsScreen(node) ? node : parentContext.Screen;

            // Create context for this node
            var context = new RuleContext(
                parentContext.Surface,
                parentContext.AppName,
                parentContext.Parent,
                siblings,
                parentContext.Depth,
                i,
                parentContext.Ancestors,
                screen);

            yield return (node, context);

            // Process children with updated context
            if (node.Children != null && node.Children.Count > 0)
            {
                var childContext = context.ForChild(node, node.Children, 0, screen);
                
                foreach (var childResult in WalkWithContext(node.Children, childContext))
                {
                    yield return childResult;
                }
            }
        }
    }

    /// <summary>
    /// Determines if a node represents a screen.
    /// </summary>
    private static bool IsScreen(UiNode node)
    {
        return node.Type.Equals("Screen", StringComparison.OrdinalIgnoreCase) ||
               node.Type.Contains("Screen", StringComparison.OrdinalIgnoreCase);
    }
}
