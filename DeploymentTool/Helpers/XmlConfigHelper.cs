using System.IO;
using System.Xml.Linq;

namespace DeploymentTool.Helpers;

public static class XmlConfigHelper
{
    // ── Read ──────────────────────────────────────────────────────────────────

    public static Dictionary<string, string> ReadAppSettings(string filePath)
    {
        var doc = XDocument.Load(filePath);
        return doc
            .Descendants("appSettings")
            .FirstOrDefault()
            ?.Elements("add")
            .Where(e => e.Attribute("key") != null)
            .ToDictionary(
                e => e.Attribute("key")!.Value,
                e => e.Attribute("value")?.Value ?? string.Empty,
                StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    // ── ApplyChange (in-memory) ───────────────────────────────────────────────

    public static string ApplyChange(string xml, string key, string value)
    {
        try
        {
            var doc         = XDocument.Parse(xml);
            bool found = false;

            // 1) appSettings: <add key="..." value="..." />
            var appSettings = doc.Descendants("appSettings").FirstOrDefault();
            if (appSettings != null)
            {
                foreach (var el in appSettings.Nodes().OfType<XElement>().Where(n => n.Name.LocalName == "add"))
                {
                    var k = el.Attribute("key")?.Value;
                    if (k != null && string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                    {
                        el.SetAttributeValue("value", value);
                        found = true;
                    }
                }
            }

            // 2) connectionStrings: <add name="..." connectionString="..." />
            var connectionStrings = doc.Descendants("connectionStrings").FirstOrDefault();
            if (connectionStrings != null)
            {
                foreach (var el in connectionStrings.Nodes().OfType<XElement>().Where(n => n.Name.LocalName == "add"))
                {
                    var name = el.Attribute("name")?.Value;
                    if (name != null && string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                    {
                        el.SetAttributeValue("connectionString", value);
                        found = true;
                    }
                }
            }

            if (!found) return xml;

            using var sw = new StringWriter();
            doc.Save(sw);
            return sw.ToString();
        }
        catch
        {
            return xml;
        }
    }

    // ── Write ─────────────────────────────────────────────────────────────────
    // Called from a background thread (Task.Run); keeps XML declaration + indentation.

    public static void WriteSync(string filePath, Dictionary<string, string> updates)
    {
        var doc         = XDocument.Load(filePath);
        var appSettings = doc.Descendants("appSettings").FirstOrDefault();
        if (appSettings == null) return;

        foreach (var element in appSettings.Nodes().OfType<XElement>().Where(n => n.Name.LocalName == "add"))
        {
            var key = element.Attribute("key")?.Value;
            if (key != null && updates.TryGetValue(key, out var newValue))
                element.SetAttributeValue("value", newValue);
        }

        doc.Save(filePath);
    }
}
