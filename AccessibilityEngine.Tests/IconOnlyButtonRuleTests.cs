using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;
using AccessibilityEngine.Rules;
using Xunit;

namespace AccessibilityEngine.Tests;

public class IconOnlyButtonRuleTests
{
    private readonly IconOnlyButtonRule _rule = new();

    [Fact]
    public void Evaluate_ButtonWithoutNameAndHasIconProperty_ReturnsFinding()
    {
        // Arrange
        var button = new UiNode("btn1", "button");
        button.Properties["hasIcon"] = "true";
        var root = new UiTree(button);
        var context = new RuleContext(root, button);

        // Act
        var findings = _rule.Evaluate(context).ToList();

        // Assert
        Assert.Single(findings);
        Assert.Equal("R003", findings[0].RuleId);
        Assert.Equal(Severity.Warning, findings[0].Severity);
    }

    [Fact]
    public void Evaluate_ButtonWithNameAndHasIcon_ReturnsNoFinding()
    {
        // Arrange
        var button = new UiNode("btn1", "button") { Name = "Menu" };
        button.Properties["hasIcon"] = "true";
        var root = new UiTree(button);
        var context = new RuleContext(root, button);

        // Act
        var findings = _rule.Evaluate(context).ToList();

        // Assert
        Assert.Empty(findings);
    }

    [Fact]
    public void Evaluate_ButtonWithoutIconProperty_ReturnsNoFinding()
    {
        // Arrange
        var button = new UiNode("btn1", "button");
        var root = new UiTree(button);
        var context = new RuleContext(root, button);

        // Act
        var findings = _rule.Evaluate(context).ToList();

        // Assert
        Assert.Empty(findings);
    }

    [Fact]
    public void Evaluate_ButtonWithDecorativeChildren_ReturnsFinding()
    {
        // Arrange
        var button = new UiNode("btn1", "button");
        var icon = new UiNode("icon1", "icon");
        icon.Properties["decorative"] = "true";
        button.Children.Add(icon);
        var root = new UiTree(button);
        var context = new RuleContext(root, button);

        // Act
        var findings = _rule.Evaluate(context).ToList();

        // Assert
        Assert.Single(findings);
    }

    [Fact]
    public void Evaluate_NonButtonRole_ReturnsNoFinding()
    {
        // Arrange
        var div = new UiNode("div1", "container");
        div.Properties["hasIcon"] = "true";
        var root = new UiTree(div);
        var context = new RuleContext(root, div);

        // Act
        var findings = _rule.Evaluate(context).ToList();

        // Assert
        Assert.Empty(findings);
    }
}
