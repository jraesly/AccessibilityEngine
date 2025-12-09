using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using AccessibilityEngine.Core.Models;

namespace AccessibilityEngine.Adapters.PowerApps;

/// <summary>
/// Parses Model-Driven App customizations.xml to extract UI structure for accessibility scanning.
/// Groups findings by Table (Entity) for cleaner organization.
/// </summary>
public class ModelDrivenParser
{
    public IReadOnlyList<UiTree> ParseCustomizationsXml(XDocument xml, string? solutionName = null)
    {
        var trees = new List<UiTree>();
        if (xml == null) return trees;

        // Parse AppModules to get MDA app names
        var appModules = ParseAppModules(xml);
        
        // Parse AppModuleComponents to map entities to apps
        var entityToAppMap = ParseEntityToAppMapping(xml, appModules);

        // Parse each entity separately - group by Table
        var entities = xml.Descendants("Entity");
        foreach (var entity in entities)
        {
            var entityName = entity.Element("Name")?.Attribute("LocalizedName")?.Value 
                          ?? entity.Element("Name")?.Value
                          ?? entity.Attribute("Name")?.Value;
            
            if (string.IsNullOrEmpty(entityName)) continue;

            var displayEntityName = FormatDisplayName(entityName);
            var forms = entity.Descendants("form");
            
            // Get the MDA app name for this entity (if mapped)
            var mdaAppName = entityToAppMap.TryGetValue(entityName.ToLowerInvariant(), out var appName) 
                ? appName 
                : appModules.Values.FirstOrDefault(); // Fall back to first app if not mapped
            
            foreach (var form in forms)
            {
                var formName = form.Attribute("name")?.Value ?? form.Attribute("id")?.Value ?? "form";
                var formType = form.Attribute("type")?.Value;
                
                // Build display-friendly form name (e.g., "Main Form" instead of "main")
                var displayFormName = BuildFormDisplayName(formName, formType);
                
                // AppName is the Table name - findings grouped by table
                var tableName = displayEntityName;
                
                var nodes = new List<UiNode>();

                // Parse tabs with hierarchy preserved
                foreach (var tab in form.Elements("tabs")?.Elements("tab") ?? form.Descendants("tab"))
                {
                    var tabNode = ParseTab(tab, displayFormName, entityName);
                    if (tabNode != null)
                        nodes.Add(tabNode);
                }

                // Parse header if present
                var header = form.Element("header");
                if (header != null)
                {
                    var headerNodes = ParseFormSection(header, displayFormName, "Header", entityName);
                    nodes.AddRange(headerNodes);
                }

                // Parse footer if present
                var footer = form.Element("footer");
                if (footer != null)
                {
                    var footerNodes = ParseFormSection(footer, displayFormName, "Footer", entityName);
                    nodes.AddRange(footerNodes);
                }

                if (nodes.Count > 0)
                {
                    var tree = new UiTree(SurfaceType.ModelDrivenApp, tableName, nodes, mdaAppName);
                    trees.Add(tree);
                }
            }
        }

        return trees;
    }

    /// <summary>
    /// Parses AppModules section to extract MDA app names.
    /// Returns a dictionary of app unique name -> display name.
    /// </summary>
    private static Dictionary<string, string> ParseAppModules(XDocument xml)
    {
        var appModules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var appModule in xml.Descendants("AppModule"))
        {
            var uniqueName = appModule.Element("UniqueName")?.Value;
            if (string.IsNullOrEmpty(uniqueName)) continue;
            
            // Get the localized display name
            var localizedName = appModule.Descendants("LocalizedName")
                .FirstOrDefault(ln => ln.Attribute("languagecode")?.Value == "1033")?
                .Attribute("description")?.Value;
            
            var displayName = localizedName ?? FormatDisplayName(uniqueName);
            appModules[uniqueName] = displayName;
            
            Console.WriteLine($"[ModelDrivenParser] Found AppModule: {uniqueName} -> {displayName}");
        }
        
        return appModules;
    }

    /// <summary>
    /// Parses AppModuleComponents to map entities to their parent apps.
    /// Returns a dictionary of entity schema name -> app display name.
    /// </summary>
    private static Dictionary<string, string> ParseEntityToAppMapping(XDocument xml, Dictionary<string, string> appModules)
    {
        var entityToApp = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var component in xml.Descendants("AppModuleComponent"))
        {
            var type = component.Attribute("type")?.Value;
            
            // Type 1 = Entity
            if (type == "1")
            {
                var schemaName = component.Attribute("schemaName")?.Value;
                var appModuleUniqueName = component.Parent?.Parent?.Element("UniqueName")?.Value;
                
                if (!string.IsNullOrEmpty(schemaName) && !string.IsNullOrEmpty(appModuleUniqueName))
                {
                    if (appModules.TryGetValue(appModuleUniqueName, out var appDisplayName))
                    {
                        entityToApp[schemaName] = appDisplayName;
                        Console.WriteLine($"[ModelDrivenParser] Mapped entity {schemaName} -> app {appDisplayName}");
                    }
                }
            }
        }
        
        return entityToApp;
    }

    /// <summary>
    /// Extracts the entity name from customizations.xml.
    /// </summary>
    private static string? GetEntityName(XDocument xml)
    {
        // Try Entity element
        var entityElement = xml.Descendants("Entity").FirstOrDefault();
        if (entityElement != null)
        {
            var name = entityElement.Attribute("Name")?.Value;
            if (!string.IsNullOrEmpty(name)) return name;
        }

        // Try EntityInfo/entity
        var entityInfo = xml.Descendants("EntityInfo").FirstOrDefault();
        if (entityInfo != null)
        {
            var entity = entityInfo.Element("entity");
            var name = entity?.Attribute("Name")?.Value ?? entity?.Attribute("name")?.Value;
            if (!string.IsNullOrEmpty(name)) return name;
        }

        // Try from ImportExportXml/Entities/Entity
        var importEntity = xml.Descendants("Entities")?.Elements("Entity").FirstOrDefault();
        if (importEntity != null)
        {
            var name = importEntity.Attribute("Name")?.Value;
            if (!string.IsNullOrEmpty(name)) return name;
        }

        // Try LocalizedName
        var localizedName = xml.Descendants("LocalizedName").FirstOrDefault();
        if (localizedName != null)
        {
            var description = localizedName.Attribute("description")?.Value;
            if (!string.IsNullOrEmpty(description)) return description;
        }

        return null;
    }

    /// <summary>
    /// Builds a display-friendly form name from form info.
    /// </summary>
    private static string BuildFormDisplayName(string formName, string? formType)
    {
        // Clean up the form name if it's generic
        if (formName.Equals("form", StringComparison.OrdinalIgnoreCase) ||
            formName.Equals("main", StringComparison.OrdinalIgnoreCase))
        {
            return formType switch
            {
                "main" => "Main Form",
                "quick" => "Quick Create Form",
                "quickView" => "Quick View Form",
                "card" => "Card Form",
                _ => "Form"
            };
        }

        return FormatDisplayName(formName);
    }

    /// <summary>
    /// Formats a schema name into a display name (e.g., "account" -> "Account", "new_customentity" -> "Custom Entity").
    /// </summary>
    private static string FormatDisplayName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        // Remove common prefixes
        var cleanName = name;
        if (cleanName.StartsWith("new_", StringComparison.OrdinalIgnoreCase))
            cleanName = cleanName[4..];
        else if (cleanName.Length > 6 && cleanName[2..].StartsWith("_", StringComparison.OrdinalIgnoreCase) && 
                 cleanName.Substring(0, 2).All(char.IsLetter))
            cleanName = cleanName[3..]; // Remove publisher prefix like "cr_"

        // Convert underscores to spaces and capitalize words
        var words = cleanName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i][1..].ToLower();
            }
        }

        return string.Join(" ", words);
    }

    private UiNode? ParseTab(XElement tab, string formName, string? entityName)
    {
        var tabName = tab.Attribute("name")?.Value ?? "tab";
        var tabLabel = GetLabelText(tab) ?? tabName;
        var isVisible = tab.Attribute("visible")?.Value != "false";
        var expanded = tab.Attribute("expanded")?.Value != "false";

        var props = new Dictionary<string, object?>
        {
            ["visible"] = isVisible,
            ["expanded"] = expanded,
            ["showLabel"] = tab.Attribute("showlabel")?.Value != "false"
        };

        var children = new List<UiNode>();

        // Parse sections within tab
        foreach (var section in tab.Elements("columns")?.Elements("column")?.Elements("sections")?.Elements("section") 
                              ?? tab.Descendants("section"))
        {
            var sectionNode = ParseSection(section, formName, tabName, entityName);
            if (sectionNode != null)
                children.Add(sectionNode);
        }

        var meta = new UiMeta(
            Surface: SurfaceType.ModelDrivenApp, 
            ScreenName: tabLabel, 
            FormName: formName, 
            SourcePath: null,
            EntityName: entityName != null ? FormatDisplayName(entityName) : null,
            TabName: tabLabel,
            SectionName: null
        );
        return new UiNode(tabName, "Tab", "tabpanel", tabLabel, tabLabel, props, children, meta);
    }

    private UiNode? ParseSection(XElement section, string formName, string tabName, string? entityName)
    {
        var sectionName = section.Attribute("name")?.Value ?? "section";
        var sectionLabel = GetLabelText(section) ?? sectionName;
        var isVisible = section.Attribute("visible")?.Value != "false";
        var showLabel = section.Attribute("showlabel")?.Value != "false";

        var props = new Dictionary<string, object?>
        {
            ["visible"] = isVisible,
            ["showLabel"] = showLabel,
            ["columns"] = section.Attribute("columns")?.Value ?? "1"
        };

        var children = new List<UiNode>();

        // Parse rows/cells/controls
        foreach (var row in section.Elements("rows")?.Elements("row") ?? section.Descendants("row"))
        {
            foreach (var cell in row.Elements("cell") ?? Enumerable.Empty<XElement>())
            {
                var controlNode = ParseCell(cell, formName, tabName, sectionName, entityName);
                if (controlNode != null)
                    children.Add(controlNode);
            }
        }

        var meta = new UiMeta(
            Surface: SurfaceType.ModelDrivenApp, 
            ScreenName: tabName, 
            FormName: formName, 
            SourcePath: null,
            EntityName: entityName != null ? FormatDisplayName(entityName) : null,
            TabName: tabName,
            SectionName: sectionLabel
        );
        return new UiNode(sectionName, "Section", "group", sectionLabel, sectionLabel, props, children, meta);
    }

    private UiNode? ParseCell(XElement cell, string formName, string tabName, string sectionName, string? entityName)
    {
        var control = cell.Element("control");
        if (control == null) return null;

        var controlId = control.Attribute("id")?.Value ?? control.Attribute("datafieldname")?.Value;
        if (string.IsNullOrEmpty(controlId)) return null;

        var controlType = control.Attribute("classid")?.Value ?? "field";
        var dataFieldName = control.Attribute("datafieldname")?.Value;
        var isRequired = cell.Attribute("isrequired")?.Value == "true" || 
                        control.Attribute("isrequired")?.Value == "true";
        var isVisible = cell.Attribute("visible")?.Value != "false";
        var isDisabled = control.Attribute("disabled")?.Value == "true";
        var showLabel = cell.Attribute("showlabel")?.Value != "false";

        // Get label from cell or control
        var label = GetLabelText(cell) ?? GetLabelText(control) ?? dataFieldName ?? controlId;
        var description = control.Attribute("description")?.Value;

        var props = new Dictionary<string, object?>
        {
            ["classid"] = controlType,
            ["datafieldname"] = dataFieldName,
            ["isRequired"] = isRequired,
            ["visible"] = isVisible,
            ["disabled"] = isDisabled,
            ["showLabel"] = showLabel,
            ["description"] = description,
            ["rowspan"] = cell.Attribute("rowspan")?.Value,
            ["colspan"] = cell.Attribute("colspan")?.Value
        };

        // Extract parameters (often contain PCF control config)
        var parameters = control.Element("parameters");
        if (parameters != null)
        {
            foreach (var param in parameters.Elements())
            {
                props[$"param_{param.Name.LocalName}"] = param.Value;
            }
        }

        // Extract visibility expression (business rule-driven visibility)
        var visibilityExpression = cell.Attribute("availableforphone")?.Value;
        if (!string.IsNullOrEmpty(visibilityExpression))
        {
            props["availableForPhone"] = visibilityExpression;
        }

        // Check for conditionally visible/required fields
        var conditionalVisible = cell.Attribute("IsConditionallyVisible")?.Value;
        if (!string.IsNullOrEmpty(conditionalVisible))
        {
            props["isConditionallyVisible"] = conditionalVisible == "true";
        }

        // Check for read-only indicator
        var isReadOnly = control.Attribute("isunbound")?.Value == "true" ||
                        control.Attribute("disabled")?.Value == "true";
        props["isReadOnly"] = isReadOnly;

        // Detect PCF controls (Power Apps Component Framework)
        var isPcfControl = IsPcfControl(controlType, control);
        props["isPcfControl"] = isPcfControl;
        
        if (isPcfControl)
        {
            // Extract PCF control manifest name if available
            var pcfName = ExtractPcfControlName(control);
            if (!string.IsNullOrEmpty(pcfName))
            {
                props["pcfControlName"] = pcfName;
            }
        }

        // Extract events that might affect accessibility (e.g., OnChange hiding other fields)
        var events = control.Element("events");
        if (events != null)
        {
            var eventHandlers = new List<string>();
            foreach (var evt in events.Elements("event"))
            {
                var eventName = evt.Attribute("name")?.Value;
                var handlerName = evt.Element("handler")?.Attribute("functionName")?.Value;
                if (!string.IsNullOrEmpty(eventName))
                {
                    eventHandlers.Add($"{eventName}:{handlerName ?? "inline"}");
                }
            }
            if (eventHandlers.Count > 0)
            {
                props["eventHandlers"] = eventHandlers;
            }
        }

        // Extract auto-complete/input mask settings
        var autoComplete = control.Attribute("autocomplete")?.Value;
        if (!string.IsNullOrEmpty(autoComplete))
        {
            props["autoComplete"] = autoComplete;
        }

        // Map classid to friendly type name
        var friendlyType = MapClassIdToType(controlType);

        var meta = new UiMeta(
            Surface: SurfaceType.ModelDrivenApp, 
            ScreenName: tabName, 
            FormName: formName, 
            SourcePath: null,
            EntityName: entityName != null ? FormatDisplayName(entityName) : null,
            TabName: tabName,
            SectionName: sectionName
        );
        return new UiNode(controlId, friendlyType, null, label, label, props, new List<UiNode>(), meta);
    }

    /// <summary>
    /// Determines if a control is a PCF (Power Apps Component Framework) control.
    /// </summary>
    private static bool IsPcfControl(string? classId, XElement control)
    {
        // Known PCF class ID
        if (classId?.Equals("{5D68B988-0661-4DB2-BC3E-17598AD3BE6C}", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        // Check for customControlName attribute
        if (!string.IsNullOrEmpty(control.Attribute("customControlName")?.Value))
            return true;

        // Check for customControl element
        if (control.Element("customControl") != null)
            return true;

        // Check parameters for PCF indicators
        var parameters = control.Element("parameters");
        if (parameters != null)
        {
            // PCF controls often have specific parameter patterns
            if (parameters.Element("controlMode") != null ||
                parameters.Element("IsViewComponent") != null)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Extracts the PCF control name from control definition.
    /// </summary>
    private static string? ExtractPcfControlName(XElement control)
    {
        // Try customControlName attribute
        var customName = control.Attribute("customControlName")?.Value;
        if (!string.IsNullOrEmpty(customName))
            return customName;

        // Try customControl element
        var customControl = control.Element("customControl");
        if (customControl != null)
        {
            var name = customControl.Attribute("name")?.Value;
            if (!string.IsNullOrEmpty(name))
                return name;
        }

        return null;
    }

    private List<UiNode> ParseFormSection(XElement section, string formName, string sectionName, string? entityName)
    {
        var nodes = new List<UiNode>();
        
        foreach (var cell in section.Descendants("cell"))
        {
            var node = ParseCell(cell, formName, sectionName, sectionName, entityName);
            if (node != null)
                nodes.Add(node);
        }

        return nodes;
    }

    private void ParseEntityMetadata(XDocument xml, List<UiTree> trees)
    {
        // Extract entity-level field metadata for additional context
        var entities = xml.Descendants("Entity");
        foreach (var entity in entities)
        {
            var entityName = entity.Attribute("Name")?.Value;
            if (string.IsNullOrEmpty(entityName)) continue;

            // Look for attributes with display names and descriptions
            foreach (var attr in entity.Descendants("attribute"))
            {
                var attrName = attr.Attribute("PhysicalName")?.Value;
                var displayName = attr.Element("displaynames")?.Element("displayname")?.Attribute("description")?.Value;
                var description = attr.Element("Descriptions")?.Element("Description")?.Attribute("description")?.Value;

                // This metadata could be used to enhance field nodes
            }
        }
    }

    private static string? GetLabelText(XElement element)
    {
        // Try multiple label locations used in Dynamics forms
        var labels = element.Element("labels")?.Element("label");
        if (labels != null)
        {
            return labels.Attribute("description")?.Value;
        }

        return element.Attribute("label")?.Value;
    }

    private static string MapClassIdToType(string? classId)
    {
        if (string.IsNullOrEmpty(classId)) return "Field";

        // Common Dynamics 365 control class IDs
        return classId.ToUpperInvariant() switch
        {
            "{4273EDBD-AC1D-40D3-9FB2-095C621B552D}" => "TextField",
            "{C6D124CA-7EDA-4A60-AEA9-7FB8D318B68F}" => "LookupField",
            "{270BD3DB-D9AF-4782-9025-509E298DEC0A}" => "CheckboxField",
            "{5B773807-9FB2-42DB-97C3-7A91EFF8ADFF}" => "DateTimeField",
            "{67FAC785-CD58-4F9F-ABB3-4B7DDC6ED5ED}" => "OptionSetField",
            "{3EF39988-22BB-4F0B-BBBE-64B5A3748AEE}" => "MultiSelectOptionSetField",
            "{E0DECE4B-6FC8-4A8F-A065-082708572369}" => "WholeNumberField",
            "{C3EFE0C3-0EC6-42BE-8349-CBD9079DFD8E}" => "DecimalField",
            "{533B9E00-756B-4312-95A0-DC888637AC78}" => "MoneyField",
            "{06375649-C143-495E-A496-C962E5B4488E}" => "SubGrid",
            "{B0C6723A-8503-4FD7-BB28-C8A06AC933C2}" => "NotesControl",
            "{F9A8A302-114E-466A-B582-6771B2AE0D92}" => "EmailBodyField",
            "{62B0DF79-0571-4F8D-95C2-C61F6A1C2F94}" => "RichTextField",
            "{E7A81278-8635-4D9E-8D4D-59480B391C5B}" => "WebResource",
            "{9C5CA0A1-AB4D-4C60-8872-4E9B4A9DE22D}" => "IFrame",
            "{7C624A0B-F59E-493D-9583-638D34759266}" => "QuickViewForm",
            "{5D68B988-0661-4DB2-BC3E-17598AD3BE6C}" => "PCFControl",
            "{02D4264B-47E2-4B4C-AA95-F439F3F4D458}" => "SubGrid", // Another SubGrid variant
            _ => "Field"
        };
    }

    public UiNode? CreateNodeFromXml(XElement element, string? formName, string? screenName)
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
