using System.Collections.Generic;

namespace McPipeAdd
{
    public class PartInfo
    {
        public string Label
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(PnPClassName))
                {
                    return PnPClassName;
                }

                return "Part";
            }
        }

        public string ObjectIdText = string.Empty;
        public int RowId = 0;
        public string RuntimeType = string.Empty;
        public string PnPClassName = string.Empty;
        public string Spec = string.Empty;
        public string Size = string.Empty;
        public string NominalDiameter = string.Empty;
        public string ShortDescription = string.Empty;
        public string PartFamilyLongDesc = string.Empty;
        public string PartSizeLongDesc = string.Empty;
        public string ItemCode = string.Empty;
        public string DataLinksEndType = string.Empty;
        public string DataLinksFacing = string.Empty;
        public string DataLinksPortName = string.Empty;

        public List<PortInfo> Ports = new List<PortInfo>();
        public List<string> Errors = new List<string>();
    }
}