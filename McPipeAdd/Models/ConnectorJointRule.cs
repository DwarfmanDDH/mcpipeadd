using System.Collections.Generic;
using System.Linq;

namespace McPipeAdd
{
    public class ConnectorJointRule
    {
        public string Name = string.Empty;
        public string Description = string.Empty;
        public string MatchCondition = string.Empty;

        public List<string> EndConditions1 = new List<string>();
        public List<string> EndConditions2 = new List<string>();

        public bool AllowsPair(string endConditionA, string endConditionB)
        {
            string a = TextUtil.Clean(endConditionA);
            string b = TextUtil.Clean(endConditionB);

            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            {
                return false;
            }

            bool forward =
                EndConditions1.Any(e => TextUtil.SameText(e, a)) &&
                EndConditions2.Any(e => TextUtil.SameText(e, b));

            bool reverse =
                EndConditions1.Any(e => TextUtil.SameText(e, b)) &&
                EndConditions2.Any(e => TextUtil.SameText(e, a));

            return forward || reverse;
        }

        public List<string> GetMatingEnds(string endCondition)
        {
            string end = TextUtil.Clean(endCondition);
            List<string> matingEnds = new List<string>();

            if (string.IsNullOrWhiteSpace(end))
            {
                return matingEnds;
            }

            if (EndConditions1.Any(e => TextUtil.SameText(e, end)))
            {
                matingEnds.AddRange(EndConditions2);
            }

            if (EndConditions2.Any(e => TextUtil.SameText(e, end)))
            {
                matingEnds.AddRange(EndConditions1);
            }

            return matingEnds
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => TextUtil.Clean(e))
                .Distinct()
                .OrderBy(e => e)
                .ToList();
        }

        public string FormatSummary()
        {
            string side1 = EndConditions1.Count == 0 ? "<none>" : string.Join(", ", EndConditions1);
            string side2 = EndConditions2.Count == 0 ? "<none>" : string.Join(", ", EndConditions2);

            if (string.IsNullOrWhiteSpace(Name))
            {
                return side1 + " <-> " + side2;
            }

            return Name + ": " + side1 + " <-> " + side2;
        }
    }
}
