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

        var context = new RuleContext(uiTree.Surface, uiTree.AppName);

        var findings = new List<Finding>();

        foreach (var node in Walk(uiTree.Nodes))
        {
            foreach (var rule in rules)
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

    private static IEnumerable<UiNode> Walk(IEnumerable<UiNode> nodes)
    {
        if (nodes == null) yield break;

        var stack = new Stack<UiNode>(nodes.Reverse());

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            if (current.Children != null && current.Children.Count > 0)
            {
                // push in reverse so the first child is processed first
                foreach (var child in ((IEnumerable<UiNode>)current.Children).Reverse())
                {
                    stack.Push(child);
                }
            }
        }
    }
}
