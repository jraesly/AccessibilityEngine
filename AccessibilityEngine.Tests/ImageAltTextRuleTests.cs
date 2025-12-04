using AccessibilityEngine.Core.Models;
using AccessibilityEngine.Core.Rules;
using AccessibilityEngine.Rules;
using Xunit;

namespace AccessibilityEngine.Tests;

public class ImageAltTextRuleTests
{
    private readonly ImageAltTextRule _rule = new();

    [Fact]
    public void Evaluate_ImageWithoutAltOrName_ReturnsFinding()
    {
        // Arrange
        var image = new UiNode("img1", "img");
        var root = new UiTree(image);
        var context = new RuleContext(root, image);

        // Act
        var findings = _rule.Evaluate(context).ToList();

        // Assert
        Assert.Single(findings);
        Assert.Equal("R002", findings[0].RuleId);
        Assert.Equal(Severity.Warning, findings[0].Severity);
    }

    [Fact]
    public void Evaluate_ImageWithAltText_ReturnsNoFinding()
    {
        // Arrange
        var image = new UiNode("img1", "img") { AltText = "A photo of a cat" };
        var root = new UiTree(image);
        var context = new RuleContext(root, image);

        // Act
        var findings = _rule.Evaluate(context).ToList();

        // Assert
        Assert.Empty(findings);
    }

    [Fact]
    public void Evaluate_ImageWithName_ReturnsNoFinding()
    {
        // Arrange
        var image = new UiNode("img1", "img") { Name = "Decorative logo" };
        var root = new UiTree(image);
        var context = new RuleContext(root, image);

        // Act
        var findings = _rule.Evaluate(context).ToList();

        // Assert
        Assert.Empty(findings);
    }

    [Theory]
    [InlineData("img")]
    [InlineData("image")]
    public void Evaluate_ImageRoleVariations_AreHandled(string role)
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
    public void Evaluate_NonImageRole_ReturnsNoFinding()
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
    public void Evaluate_ImageWithWhitespaceAltText_ReturnsFinding()
    {
        // Arrange
        var image = new UiNode("img1", "img") { AltText = "   " };
        var root = new UiTree(image);
        var context = new RuleContext(root, image);

        // Act
        var findings = _rule.Evaluate(context).ToList();

        // Assert
        Assert.Single(findings);
    }
}
