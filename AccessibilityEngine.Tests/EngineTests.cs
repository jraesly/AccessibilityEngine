using AccessibilityEngine.Core.Engine;
using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;
using AccessibilityEngine.Rules;
using Xunit;

namespace AccessibilityEngine.Tests;

public class EngineTests
{
    [Fact]
    public void Analyze_WithNoRules_ReturnsEmptyScanResult()
    {
        // Arrange
        var root = new UiNode("root", "container");
        var tree = new UiTree(root);
        var rules = new List<IRule>();

        // Act
        var result = Engine.Analyze(tree, rules);

        // Assert
        Assert.Empty(result.Findings);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
        Assert.Equal(0, result.InfoCount);
    }

    [Fact]
    public void Analyze_WithMultipleRules_ReturnsCombinedFindings()
    {
        // Arrange
        var button = new UiNode("btn1", "button");
        var root = new UiTree(button);
        var rules = new List<IRule> { new MissingAccessibleNameRule(), new EmptyLabelRule() };

        // Act
        var result = Engine.Analyze(root, rules);

        // Assert
        Assert.NotEmpty(result.Findings);
        Assert.Equal(1, result.WarningCount);
    }

    [Fact]
    public void Analyze_TraversesAllNodes()
    {
        // Arrange
        var root = new UiNode("root", "container");
        var child1 = new UiNode("btn1", "button");
        var child2 = new UiNode("btn2", "button");
        root.Children.Add(child1);
        root.Children.Add(child2);
        var tree = new UiTree(root);
        var rules = new List<IRule> { new MissingAccessibleNameRule() };

        // Act
        var result = Engine.Analyze(tree, rules);

        // Assert
        // Should find 2 findings (one for each button without a name)
        Assert.Equal(2, result.Findings.Count);
    }

    [Fact]
    public void Analyze_CountsSeveritiesCorrectly()
    {
        // Arrange
        var button1 = new UiNode("btn1", "button");
        var button2 = new UiNode("btn2", "button") { Name = "OK" };
        var root = new UiTree(button1);
        root.Root.Children.Add(button1);
        root.Root.Children.Add(button2);
        var rules = new List<IRule> { new MissingAccessibleNameRule() };

        // Act
        var result = Engine.Analyze(root, rules);

        // Assert
        Assert.Single(result.Findings);
        Assert.Equal(1, result.WarningCount);
    }
}
