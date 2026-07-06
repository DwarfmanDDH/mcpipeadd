using System;
using System.Linq;
using System.Xml.Linq;

namespace McPipeAdd
{
    public static class ConnectorConfigReader
    {
        public static ConnectorConfigResult LoadProjectConnectorConfig()
        {
            ConnectorConfigResult result = ConnectorConfigResolver.ResolveDefaultConnectorsConfig();

            if (string.IsNullOrWhiteSpace(result.ConfigPath))
            {
                return result;
            }

            try
            {
                XDocument document = XDocument.Load(result.ConfigPath);

                foreach (XElement joint in document.Descendants("Joint"))
                {
                    ConnectorJointRule rule = new ConnectorJointRule();

                    XAttribute nameAttribute = joint.Attribute("Name");

                    if (nameAttribute != null)
                    {
                        rule.Name = nameAttribute.Value.Trim();
                    }

                    rule.Description = GetElementValue(joint, "Description");
                    rule.MatchCondition = GetElementValue(joint, "MatchCondition");

                    XElement endConditions1 = joint.Element("EndConditions1");
                    XElement endConditions2 = joint.Element("EndConditions2");

                    if (endConditions1 != null)
                    {
                        rule.EndConditions1 = endConditions1
                            .Elements("EndCondition")
                            .Select(e => TextUtil.Clean(e.Value))
                            .Where(e => !string.IsNullOrWhiteSpace(e))
                            .Distinct()
                            .ToList();
                    }

                    if (endConditions2 != null)
                    {
                        rule.EndConditions2 = endConditions2
                            .Elements("EndCondition")
                            .Select(e => TextUtil.Clean(e.Value))
                            .Where(e => !string.IsNullOrWhiteSpace(e))
                            .Distinct()
                            .ToList();
                    }

                    if (rule.EndConditions1.Count > 0 && rule.EndConditions2.Count > 0)
                    {
                        result.Rules.Add(rule);
                    }
                }

                if (result.Rules.Count == 0)
                {
                    result.Errors.Add("DefaultConnectorsConfig.xml was found, but no usable Joint end-condition rules were loaded.");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add("Connector config read failed: " + ex.Message);
            }

            return result;
        }

        private static string GetElementValue(XElement parent, string elementName)
        {
            XElement element = parent.Element(elementName);
            return element == null ? string.Empty : element.Value.Trim();
        }
    }
}
