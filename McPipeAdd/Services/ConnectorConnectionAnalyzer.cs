using System.Collections.Generic;
using System.Linq;

namespace McPipeAdd
{
    public static class ConnectorConnectionAnalyzer
    {
        private static readonly string[] PipeEndPreferenceOrder =
        {
            "THDM",
            "PL",
            "BV"
        };

        public static bool IsValidConnection(
            ConnectorConfigResult connectorConfig,
            string endConditionA,
            string endConditionB)
        {
            if (connectorConfig != null && connectorConfig.HasRules)
            {
                return connectorConfig.Rules.Any(r => r.AllowsPair(endConditionA, endConditionB));
            }

            return IsValidConnectionFallback(endConditionA, endConditionB);
        }

        public static List<string> GetExpectedMatingEnds(
            ConnectorConfigResult connectorConfig,
            string endCondition)
        {
            if (connectorConfig != null && connectorConfig.HasRules)
            {
                List<string> matingEnds = new List<string>();

                foreach (ConnectorJointRule rule in connectorConfig.Rules)
                {
                    matingEnds.AddRange(rule.GetMatingEnds(endCondition));
                }

                return matingEnds
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Select(e => TextUtil.Clean(e))
                    .Distinct()
                    .OrderBy(e => e)
                    .ToList();
            }

            return GetExpectedMatingEndsFallback(endCondition);
        }

        public static string FormatExpectedMatingEnds(
            ConnectorConfigResult connectorConfig,
            string endCondition)
        {
            List<string> matingEnds = GetExpectedMatingEnds(connectorConfig, endCondition);

            if (matingEnds.Count == 0)
            {
                return "UNKNOWN";
            }

            return string.Join(", ", matingEnds);
        }

        public static List<string> GetExpectedPipeEndsForComponentPort(
            ConnectorConfigResult connectorConfig,
            string componentEndCondition)
        {
            List<string> matingEnds = GetExpectedMatingEnds(connectorConfig, componentEndCondition);

            List<string> pipeEnds = matingEnds
                .Where(IsPipeEndCondition)
                .Select(e => TextUtil.Clean(e))
                .Distinct()
                .ToList();

            pipeEnds = pipeEnds
                .OrderBy(e => GetPipeEndPreferenceIndex(e, componentEndCondition))
                .ThenBy(e => e)
                .ToList();

            return pipeEnds;
        }

        public static string GetPrimaryExpectedPipeEndForComponentPort(
            ConnectorConfigResult connectorConfig,
            string componentEndCondition)
        {
            List<string> pipeEnds = GetExpectedPipeEndsForComponentPort(connectorConfig, componentEndCondition);

            if (pipeEnds.Count == 0)
            {
                return string.Empty;
            }

            return pipeEnds[0];
        }

        public static string GetConnectorRulesForEndSummary(
            ConnectorConfigResult connectorConfig,
            string endCondition)
        {
            if (connectorConfig == null || !connectorConfig.HasRules)
            {
                return "<using fallback rules>";
            }

            string cleanEnd = TextUtil.Clean(endCondition);

            List<string> rules = connectorConfig.Rules
                .Where(r =>
                    r.EndConditions1.Any(e => TextUtil.SameText(e, cleanEnd)) ||
                    r.EndConditions2.Any(e => TextUtil.SameText(e, cleanEnd)))
                .Select(r => r.FormatSummary())
                .ToList();

            if (rules.Count == 0)
            {
                return "<no connector rules found for " + cleanEnd + ">";
            }

            return string.Join("; ", rules);
        }

        private static bool IsPipeEndCondition(string endCondition)
        {
            string end = TextUtil.Clean(endCondition);

            return end == "THDM" ||
                   end == "PL" ||
                   end == "BV";
        }

        private static int GetPipeEndPreferenceIndex(string pipeEndCondition, string componentEndCondition)
        {
            string pipeEnd = TextUtil.Clean(pipeEndCondition);
            string componentEnd = TextUtil.Clean(componentEndCondition);

            if (componentEnd == "THDF" && pipeEnd == "THDM")
            {
                return -100;
            }

            if (componentEnd == "SW" && pipeEnd == "PL")
            {
                return -100;
            }

            if (componentEnd == "BV" && pipeEnd == "BV")
            {
                return -100;
            }

            if (componentEnd == "PL" && pipeEnd == "PL")
            {
                return -100;
            }

            for (int i = 0; i < PipeEndPreferenceOrder.Length; i++)
            {
                if (TextUtil.SameText(PipeEndPreferenceOrder[i], pipeEnd))
                {
                    return i;
                }
            }

            return 999;
        }

        private static bool IsValidConnectionFallback(string end1, string end2)
        {
            string a = TextUtil.Clean(end1);
            string b = TextUtil.Clean(end2);

            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            {
                return false;
            }

            if (Pair(a, b, "THDM", "THDF"))
            {
                return true;
            }

            if (Pair(a, b, "FL", "FL"))
            {
                return true;
            }

            if (Pair(a, b, "PL", "SW"))
            {
                return true;
            }

            if ((a == "BV" || a == "PL") && (b == "BV" || b == "PL"))
            {
                return true;
            }

            return false;
        }

        private static List<string> GetExpectedMatingEndsFallback(string endCondition)
        {
            string end = TextUtil.Clean(endCondition);

            switch (end)
            {
                case "THDF":
                    return new List<string> { "THDM" };

                case "THDM":
                    return new List<string> { "THDF" };

                case "SW":
                    return new List<string> { "PL" };

                case "PL":
                    return new List<string> { "SW", "PL", "BV" };

                case "BV":
                    return new List<string> { "BV", "PL" };

                case "FL":
                    return new List<string> { "FL" };

                default:
                    return new List<string>();
            }
        }

        private static bool Pair(string a, string b, string x, string y)
        {
            return (a == x && b == y) || (a == y && b == x);
        }
    }
}
