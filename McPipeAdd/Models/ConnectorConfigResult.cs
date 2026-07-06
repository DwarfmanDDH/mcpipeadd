using System.Collections.Generic;

namespace McPipeAdd
{
    public class ConnectorConfigResult
    {
        public string ConfigPath = string.Empty;
        public List<ConnectorJointRule> Rules = new List<ConnectorJointRule>();
        public List<string> Errors = new List<string>();

        public bool HasRules
        {
            get { return Rules != null && Rules.Count > 0; }
        }
    }
}
