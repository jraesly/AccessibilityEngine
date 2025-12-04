using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using AccessibilityEngine.Core.Models;

namespace AccessibilityEngine.Adapters.PowerApps;

public class ModelDrivenParser
{
    public IReadOnlyList<UiTree> ParseCustomizationsXml(XDocument xml)
    {
        var trees = new List<UiTree>();
        if (xml == null) return trees;

        var forms = xml.Descendants("form");
        foreach (var form in forms)
        {
            var formName = form.Attribute("name")?.Value ?? form.Attribute("id")?.Value ?? "form";
            var nodes = new List<UiNode>();

            // parse tabs
            foreach (var tab in form.Descendants("tab"))
            {
                var tabName = tab.Attribute("name")?.Value ?? "tab";
                var tabNode = new UiNode(tabName, "Tab", null, tabName, tabName, new Dictionary<string, object?>(), new List<UiNode>(), new UiMeta(SurfaceType.ModelDrivenApp, tabName, formName, null));
                nodes.Add(tabNode);

                foreach (var section in tab.Descendants("section"))
                {
                    var sectionName = section.Attribute("name")?.Value ?? "section";
                    var sectionNode = new UiNode(sectionName, "Section", null, sectionName, sectionName, new Dictionary<string, object?>(), new List<UiNode>(), new UiMeta(SurfaceType.ModelDrivenApp, tabName, formName, null));
                    nodes.Add(sectionNode);

                    foreach (var cell in section.Descendants("cell"))
                    {
                        foreach (var control in cell.Descendants("control"))
                        {
                            var ctrl = CreateNodeFromXml(control, formName, tabName);
                            if (ctrl != null) nodes.Add(ctrl);
                        }
                    }
                }
            }

            var tree = new UiTree(SurfaceType.ModelDrivenApp, formName, nodes);
            trees.Add(tree);
        }

        return trees;
    }

    public UiNode CreateNodeFromXml(XElement element, string? formName, string? screenName)
    {
        if (element == null) return null;
        var id = element.Attribute("id")?.Value ?? element.Attribute("name")?.Value ?? Guid.NewGuid().ToString();
        var type = element.Name.LocalName;
        var label = element.Attribute("label")?.Value ?? element.Attribute("name")?.Value;

        var props = new Dictionary<string, object?>();
        foreach (var a in element.Attributes())
        {
            props[a.Name.LocalName] = a.Value;
        }

        return new UiNode(id, type, null, label, label, props, new List<UiNode>(), new UiMeta(SurfaceType.ModelDrivenApp, screenName, formName, null));
    }
}
