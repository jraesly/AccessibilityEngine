using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;
using AccessibilityEngine.Rules;
using Xunit;

namespace AccessibilityEngine.Tests;

public class MissingAccessibleNameRuleTests
{
    private readonly MissingAccessibleNameRule _rule = new();

    [Fact]
    public void Evaluate_ButtonWithoutName_ReturnsFinding()
    {
        // Arrange
        var button = new UiNode("btn1", "button");
        var root = new UiTree(button);
        var context = new RuleContext(root, button);

        // Act
        var findings = _rule.Evaluate(context).ToList();

        // Assert
        Assert.Single(findings);
        Assert.Equal("R001", findings[0].RuleId);
        Assert.Equal(Severity.Warning, findings[0].Severity);
    }

    [Fact]
    public void Evaluate_ButtonWithName_ReturnsNoFinding()
    {
        // Arrange
        var button = new UiNode("btn1", "button") { Name = "Click me" };
        var root = new UiTree(button);
        var context = new RuleContext(root, button);

        // Act
        var findings = _rule.Evaluate(context).ToList();

        // Assert
        Assert.Empty(findings);
    }

    [Fact]
    public void Evaluate_NonInteractiveRole_ReturnsNoFinding()
    {
        // Arrange
        var div = new UiNode("div1", "container");
        var root = new UiTree(div);
        var context = new RuleContext(root, div);

        // Act
        var findings = _rule.Evaluate(context).ToList();

        // Assert
        Assert.Empty(findings);
    }

    [Theory]
    [InlineData("link")]
    [InlineData("textbox")]
    [InlineData("input")]
    [InlineData("select")]
    [InlineData("combo")]
    public void Evaluate_InteractiveRolesWithoutName_ReturnsFinding(string role)
    {
        // Arrange
        var node = new UiNode("node1", role);
        var root = new UiTree(node);
        var context = new RuleContext(root, node);

        // Act
        var findings = _rule.Evaluate(context).ToList();

        // Assert
        Assert.Single(findings);
    }

    [Fact]
    public void Evaluate_ButtonWithWhitespaceOnlyName_ReturnsFinding()
    {
        // Arrange
        var button = new UiNode("btn1", "button") { Name = "   " };
        var root = new UiTree(button);
        var context = new RuleContext(root, button);

        // Act
        var findings = _rule.Evaluate(context).ToList();

        // Assert
        Assert.Single(findings);
    }
}
