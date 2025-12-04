using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;
using AccessibilityEngine.Rules;
using Xunit;

namespace AccessibilityEngine.Tests;

public class EmptyLabelRuleTests
{
    private readonly EmptyLabelRule _rule = new();

    [Fact]
    public void Evaluate_LabelWithoutName_ReturnsFinding()
    {
        // Arrange
        var label = new UiNode("lbl1", "label");
        var root = new UiTree(label);
        var context = new RuleContext(root, label);

        // Act
        var findings = _rule.Evaluate(context).ToList();

        // Assert
        Assert.Single(findings);
        Assert.Equal("R004", findings[0].RuleId);
        Assert.Equal(Severity.Warning, findings[0].Severity);
    }

    [Fact]
    public void Evaluate_LabelWithName_ReturnsNoFinding()
    {
        // Arrange
        var label = new UiNode("lbl1", "label") { Name = "Username" };
        var root = new UiTree(label);
        var context = new RuleContext(root, label);

        // Act
        var findings = _rule.Evaluate(context).ToList();

        // Assert
        Assert.Empty(findings);
    }

    [Fact]
    public void Evaluate_NonLabelRole_ReturnsNoFinding()
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

    [Fact]
    public void Evaluate_LabelWithWhitespaceOnly_ReturnsFinding()
    {
        // Arrange
        var label = new UiNode("lbl1", "label") { Name = "   " };
        var root = new UiTree(label);
        var context = new RuleContext(root, label);

        // Act
        var findings = _rule.Evaluate(context).ToList();

        // Assert
        Assert.Single(findings);
    }

    [Theory]
    [InlineData("label")]
    [InlineData("LABEL")]
    [InlineData("Label")]
    public void Evaluate_LabelRoleCaseSensitivity_IsHandledCorrectly(string role)
    {
        // Arrange
        var label = new UiNode("lbl1", role);
        var root = new UiTree(label);
        var context = new RuleContext(root, label);

        // Act
        var findings = _rule.Evaluate(context).ToList();

        // Assert
        Assert.Single(findings);
    }
}
