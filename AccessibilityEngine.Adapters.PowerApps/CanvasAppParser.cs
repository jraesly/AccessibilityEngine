using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AccessibilityEngine.Core.Models;

namespace AccessibilityEngine.Adapters.PowerApps;

/// <summary>
/// Parses Power Apps canvas app JSON structures into UiTree format.
/// Supports multiple formats: Screens array, TopParent hierarchy, and EditorState controls.
/// </summary>
public class CanvasAppParser
{
    /// <summary>
    /// Parses a canvas app JSON document into a UiTree.
    /// </summary>
    public UiTree ParseCanvasApp(string appName, JsonDocument appJson)
    {
        if (appJson == null) throw new ArgumentNullException(nameof(appJson));

        var rootNodes = new List<UiNode>();
        var root = appJson.RootElement;

        // Strategy 1: Standard Screens array format
        if (root.TryGetProperty("Screens", out var screens))
        {
            ParseScreensArray(screens, rootNodes);
        }
        // Strategy 2: TopParent format (used in some .msapp exports)
        else if (root.TryGetProperty("TopParent", out var topParent))
        {
            ParseTopParent(topParent, rootNodes);
        }
        // Strategy 3: Direct Controls array
        else if (root.TryGetProperty("Controls", out var controls))
        {
            ParseControlsArray(controls, "Default", rootNodes);
        }
        // Strategy 4: EditorState format with ControlStates
        else if (root.TryGetProperty("ControlStates", out var controlStates))
        {
            ParseControlStates(controlStates, rootNodes);
        }
        // Strategy 5: Check if root itself is a control/screen definition
        else if (root.TryGetProperty("Name", out _) || root.TryGetProperty("ControlUniqueId", out _))
        {
            var node = ParseControlElement(root, "Default");
            if (node != null) rootNodes.Add(node);
        }

        return new UiTree(SurfaceType.CanvasApp, appName, rootNodes);
    }

    /// <summary>
    /// Parses the standard Screens array format.
    /// </summary>
    private void ParseScreensArray(JsonElement screens, List<UiNode> rootNodes)
    {
        foreach (var screen in screens.EnumerateArray())
        {
            var screenName = GetControlName(screen) ?? "Screen";
            
            // Add the screen itself as a node (children are parsed within ParseControlElement)
            var screenNode = ParseControlElement(screen, screenName);
            if (screenNode != null)
            {
                rootNodes.Add(screenNode);
            }
            // Note: Children are already added as nested nodes within screenNode by ParseControlElement
            // Do NOT add them again to rootNodes to avoid duplication
        }
    }

    /// <summary>
    /// Parses TopParent format used in some Power Apps exports.
    /// </summary>
    private void ParseTopParent(JsonElement topParent, List<UiNode> rootNodes)
    {
        var screenName = GetControlName(topParent) ?? "Screen";
        
        // TopParent itself is usually the screen (children are parsed within ParseControlElement)
        var screenNode = ParseControlElement(topParent, screenName);
        if (screenNode != null)
        {
            rootNodes.Add(screenNode);
        }
        // Note: Children are already added as nested nodes within screenNode by ParseControlElement
        // Do NOT add them again to rootNodes to avoid duplication
    }

    /// <summary>
    /// Parses a Controls/Children array into the provided list.
    /// Each control is added as a node with its children nested within.
    /// </summary>
    private void ParseControlsArray(JsonElement controls, string screenName, List<UiNode> rootNodes)
    {
        if (controls.ValueKind != JsonValueKind.Array) return;

        foreach (var control in controls.EnumerateArray())
        {
            // ParseControlElement handles children internally - no need for recursive flattening
            var node = ParseControlElement(control, screenName);
            if (node != null)
            {
                rootNodes.Add(node);
            }
        }
    }

    /// <summary>
    /// Parses EditorState ControlStates format.
    /// </summary>
    private void ParseControlStates(JsonElement controlStates, List<UiNode> rootNodes)
    {
        if (controlStates.ValueKind != JsonValueKind.Object) return;

        foreach (var prop in controlStates.EnumerateObject())
        {
            var controlId = prop.Name;
            var controlData = prop.Value;
            
            var node = ParseEditorStateControl(controlId, controlData);
            if (node != null)
            {
                rootNodes.Add(node);
            }
        }
    }

    /// <summary>
    /// Parses a single control element into a UiNode.
    /// Children are recursively parsed and nested within this node.
    /// </summary>
    private UiNode? ParseControlElement(JsonElement control, string screenName)
    {
        var id = GetControlId(control);
        if (id == null) return null;

        var type = GetControlType(control);
        var props = ExtractProperties(control);
        var accessible = GetAccessibleLabel(control, props);

        var children = new List<UiNode>();
        
        // Parse inline children into the node's Children collection
        // Power Apps uses both "Children" and "Controls" properties for nested controls
        if (control.TryGetProperty("Children", out var childrenArray) && childrenArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in childrenArray.EnumerateArray())
            {
                var childNode = ParseControlElement(child, screenName);
                if (childNode != null) children.Add(childNode);
            }
        }
        
        if (control.TryGetProperty("Controls", out var controlsArray) && controlsArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in controlsArray.EnumerateArray())
            {
                var childNode = ParseControlElement(child, screenName);
                if (childNode != null) children.Add(childNode);
            }
        }

        var meta = new UiMeta(SurfaceType.CanvasApp, screenName, null, null);
        return new UiNode(id, type, null, accessible, accessible, props, children, meta);
    }

    /// <summary>
    /// Parses an EditorState control entry.
    /// </summary>
    private UiNode? ParseEditorStateControl(string controlId, JsonElement controlData)
    {
        var type = "Control";
        if (controlData.TryGetProperty("ControlType", out var ct))
        {
            type = ct.GetString() ?? "Control";
        }
        else if (controlData.TryGetProperty("Template", out var template))
        {
            if (template.TryGetProperty("Name", out var templateName))
            {
                type = templateName.GetString() ?? "Control";
            }
        }

        var props = ExtractProperties(controlData);
        var screenName = "Default";
        
        if (controlData.TryGetProperty("ParentScreenName", out var psn))
        {
            screenName = psn.GetString() ?? "Default";
        }

        var accessible = GetAccessibleLabel(controlData, props);
        var meta = new UiMeta(SurfaceType.CanvasApp, screenName, null, null);

        return new UiNode(controlId, type, null, accessible, accessible, props, new List<UiNode>(), meta);
    }

    /// <summary>
    /// Gets the control ID from various possible properties.
    /// </summary>
    private static string? GetControlId(JsonElement control)
    {
        // Try various ID/Name properties used in different formats
        string[] idProps = ["Name", "ControlUniqueId", "ControlId", "Id", "UniqueId"];
        
        foreach (var prop in idProps)
        {
            if (control.TryGetProperty(prop, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var id = value.GetString();
                if (!string.IsNullOrWhiteSpace(id)) return id;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the control name for display purposes.
    /// </summary>
    private static string? GetControlName(JsonElement control)
    {
        if (control.TryGetProperty("Name", out var name) && name.ValueKind == JsonValueKind.String)
        {
            return name.GetString();
        }
        return GetControlId(control);
    }

    /// <summary>
    /// Gets the control type from various possible properties.
    /// Avoids returning generic types like "ControlInfo" by inferring from name if needed.
    /// </summary>
    private static string GetControlType(JsonElement control)
    {
        string? type = null;
        var controlName = GetControlId(control) ?? "";

        // Strategy 1: Template.Name (most reliable in Power Apps)
        if (control.TryGetProperty("Template", out var template))
        {
            if (template.TryGetProperty("Name", out var templateName) && templateName.ValueKind == JsonValueKind.String)
            {
                type = templateName.GetString();
                if (!string.IsNullOrEmpty(type) && !IsGenericType(type))
                    return type;
            }
            if (template.TryGetProperty("Id", out var templateId) && templateId.ValueKind == JsonValueKind.String)
            {
                type = templateId.GetString();
                if (!string.IsNullOrEmpty(type) && !IsGenericType(type))
                    return type;
            }
        }

        // Strategy 2: ControlType property
        if (control.TryGetProperty("ControlType", out var ct) && ct.ValueKind == JsonValueKind.String)
        {
            type = ct.GetString();
            if (!string.IsNullOrEmpty(type) && !IsGenericType(type))
                return type;
        }

        // Strategy 3: Type property
        if (control.TryGetProperty("Type", out var t) && t.ValueKind == JsonValueKind.String)
        {
            type = t.GetString();
            if (!string.IsNullOrEmpty(type) && !IsGenericType(type))
                return type;
        }

        // Strategy 4: TemplateName property
        if (control.TryGetProperty("TemplateName", out var tn) && tn.ValueKind == JsonValueKind.String)
        {
            type = tn.GetString();
            if (!string.IsNullOrEmpty(type) && !IsGenericType(type))
                return type;
        }

        // Strategy 5: Infer from control name patterns
        return InferControlTypeFromName(controlName);
    }

    /// <summary>
    /// Checks if a type is a generic/wrapper type that should be replaced.
    /// </summary>
    private static bool IsGenericType(string type)
    {
        return type.Equals("ControlInfo", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("Control", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("GroupControl", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("TypedDataCard", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Infers control type from the control name using Power Apps naming conventions.
    /// </summary>
    private static string InferControlTypeFromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Control";

        // Power Apps default naming: "Button1", "Label1", "TextInput1", "Icon1", etc.
        var patterns = new (string Pattern, string Type)[]
        {
            ("Button", "Button"), ("btn", "Button"),
            ("Label", "Label"), ("lbl", "Label"),
            ("TextInput", "TextInput"), ("txt", "TextInput"),
            ("Icon", "Icon"), ("ico", "Icon"),
            ("Image", "Image"), ("img", "Image"),
            ("Gallery", "Gallery"), ("gal", "Gallery"),
            ("Form", "Form"), ("frm", "Form"),
            ("Screen", "Screen"), ("scr", "Screen"),
            ("Dropdown", "Dropdown"), ("dd", "Dropdown"),
            ("ComboBox", "ComboBox"), ("cb", "ComboBox"),
            ("Checkbox", "Checkbox"), ("chk", "Checkbox"),
            ("Toggle", "Toggle"), ("tog", "Toggle"),
            ("Slider", "Slider"), ("DatePicker", "DatePicker"),
            ("Rating", "Rating"), ("Timer", "Timer"),
            ("Video", "Video"), ("Audio", "Audio"),
            ("Camera", "Camera"), ("Barcode", "Barcode"),
            ("PenInput", "PenInput"), ("RichTextEditor", "RichTextEditor"),
            ("HtmlText", "HtmlText"), ("Rectangle", "Rectangle"),
            ("Circle", "Circle"), ("Container", "Container")
        };

        foreach (var (pattern, type) in patterns)
        {
            if (name.StartsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
                name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return type;
            }
        }

        return "Control";
    }

    /// <summary>
    /// Gets the accessible label from control data.
    /// </summary>
    private static string? GetAccessibleLabel(JsonElement control, IReadOnlyDictionary<string, object?> props)
    {
        // Check direct AccessibleLabel property
        if (control.TryGetProperty("AccessibleLabel", out var al) && al.ValueKind == JsonValueKind.String)
        {
            return al.GetString();
        }

        // Check in Properties object
        if (props.TryGetValue("AccessibleLabel", out var alProp))
        {
            if (alProp is string s) return s;
            if (alProp is JsonElement je && je.ValueKind == JsonValueKind.String) return je.GetString();
        }

        // Fall back to Text property
        if (control.TryGetProperty("Text", out var text) && text.ValueKind == JsonValueKind.String)
        {
            return text.GetString();
        }

        if (props.TryGetValue("Text", out var textProp))
        {
            if (textProp is string s) return s;
            if (textProp is JsonElement je && je.ValueKind == JsonValueKind.String) return je.GetString();
        }

        // Fall back to Default property (common for input controls)
        if (control.TryGetProperty("Default", out var def) && def.ValueKind == JsonValueKind.String)
        {
            return def.GetString();
        }

        return null;
    }

    /// <summary>
    /// Extracts all properties from a control element.
    /// </summary>
    private static IReadOnlyDictionary<string, object?> ExtractProperties(JsonElement control)
    {
        var dict = new Dictionary<string, object?>();

        // If there's a Properties sub-object, extract from there
        if (control.TryGetProperty("Properties", out var propsElement) && propsElement.ValueKind == JsonValueKind.Object)
        {
            AddPropertiesToDictionary(propsElement, dict);
        }

        // Also add top-level properties that are relevant
        string[] relevantProps = [
            "AccessibleLabel", "Text", "Default", "Tooltip", "HintText",
            "X", "Y", "Width", "Height", "Visible", "DisplayMode",
            "Fill", "Color", "BorderColor", "FocusedBorderColor",
            "TabIndex", "ContentLanguage", "Role"
        ];

        foreach (var prop in relevantProps)
        {
            if (!dict.ContainsKey(prop) && control.TryGetProperty(prop, out var value))
            {
                dict[prop] = ConvertJsonValue(value);
            }
        }

        // Handle Rules array (Power Apps formula definitions)
        if (control.TryGetProperty("Rules", out var rules) && rules.ValueKind == JsonValueKind.Array)
        {
            foreach (var rule in rules.EnumerateArray())
            {
                if (rule.TryGetProperty("Property", out var propName) &&
                    rule.TryGetProperty("InvariantScript", out var script))
                {
                    var key = propName.GetString();
                    if (!string.IsNullOrEmpty(key) && !dict.ContainsKey(key))
                    {
                        dict[key] = script.GetString();
                    }
                }
            }
        }

        return dict;
    }

    /// <summary>
    /// Adds properties from a JSON object to a dictionary.
    /// </summary>
    private static void AddPropertiesToDictionary(JsonElement element, Dictionary<string, object?> dict)
    {
        if (element.ValueKind != JsonValueKind.Object) return;

        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = ConvertJsonValue(prop.Value);
        }
    }

    /// <summary>
    /// Converts a JsonElement to a .NET object.
    /// </summary>
    private static object? ConvertJsonValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.TryGetInt64(out var l) ? l : value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => value.ToString()
        };
    }
}
