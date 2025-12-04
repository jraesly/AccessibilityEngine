using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AccessibilityEngine.Core.Models;

namespace AccessibilityEngine.Adapters.PowerApps;

public class CanvasAppParser
{
    public UiTree ParseCanvasApp(string appName, JsonDocument appJson)
    {
        if (appJson == null) throw new ArgumentNullException(nameof(appJson));

        var rootNodes = new List<UiNode>();

        if (!appJson.RootElement.TryGetProperty("Screens", out var screens))
            return new UiTree(SurfaceType.CanvasApp, appName, rootNodes);

        foreach (var screen in screens.EnumerateArray())
        {
            var screenName = screen.GetProperty("Name").GetString();
            if (!screen.TryGetProperty("Controls", out var controls)) continue;

            foreach (var control in controls.EnumerateArray())
            {
                var node = ParseControl(control, screenName);
                if (node != null)
                    rootNodes.Add(node);
            }
        }

        return new UiTree(SurfaceType.CanvasApp, appName, rootNodes);
    }

    private UiNode? ParseControl(JsonElement control, string screenName)
    {
        if (!control.TryGetProperty("Name", out var nameProp)) return null;
        var id = nameProp.GetString() ?? Guid.NewGuid().ToString();
        var type = control.TryGetProperty("ControlType", out var ct) ? ct.GetString() ?? "Control" : "Control";

        var props = ToPropertyDictionary(control.GetProperty("Properties"));

        string? accessible = null;
        if (props.TryGetValue("AccessibleLabel", out var al) && al is JsonElement jel && jel.ValueKind == JsonValueKind.String)
            accessible = jel.GetString();
        else if (props.TryGetValue("Text", out var t) && t is JsonElement jt && jt.ValueKind == JsonValueKind.String)
            accessible = jt.GetString();

        var children = new List<UiNode>();
        if (control.TryGetProperty("Children", out var ch))
        {
            foreach (var c in ch.EnumerateArray())
            {
                var child = ParseControl(c, screenName);
                if (child != null) children.Add(child);
            }
        }

        var meta = new UiMeta(SurfaceType.CanvasApp, screenName, null, null);

        return new UiNode(id, type, null, accessible, accessible, props, children, meta);
    }

    private static IReadOnlyDictionary<string, object?> ToPropertyDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object?>();
        if (element.ValueKind != JsonValueKind.Object) return dict;

        foreach (var prop in element.EnumerateObject())
        {
            switch (prop.Value.ValueKind)
            {
                case JsonValueKind.String:
                    dict[prop.Name] = prop.Value.GetString();
                    break;
                case JsonValueKind.Number:
                    if (prop.Value.TryGetInt64(out var l)) dict[prop.Name] = l;
                    else if (prop.Value.TryGetDouble(out var d)) dict[prop.Name] = d;
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    dict[prop.Name] = prop.Value.GetBoolean();
                    break;
                default:
                    dict[prop.Name] = prop.Value.ToString();
                    break;
            }
        }

        return dict;
    }
}
