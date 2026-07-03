using System.Collections.Generic;
using System.Linq;

namespace McPipeAdd
{
    public class PipeCandidate
    {
        public int PnPID = 0;
        public string PartFamilyId = string.Empty;
        public string PartFamilyLongDesc = string.Empty;
        public string PartSizeLongDesc = string.Empty;
        public string ShortDescription = string.Empty;
        public string ItemCode = string.Empty;
        public string NominalDiameter = string.Empty;
        public string NominalUnit = string.Empty;
        public string Schedule = string.Empty;
        public string MaterialCode = string.Empty;
        public string PortDetails = string.Empty;

        public int? Priority = null;
        public List<string> EndConditions = new List<string>();

        public bool MatchesExpectedEnd(string expectedEnd)
        {
            return EndConditions.Any(e => TextUtil.SameText(e, expectedEnd));
        }
    }
}