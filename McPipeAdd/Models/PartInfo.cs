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
        public string PressureClass = string.Empty;
        public string DataLinksEndType = string.Empty;
        public string DataLinksFacing = string.Empty;
        public string DataLinksPortName = string.Empty;

        public bool HasExtents = false;
        public double ExtentsMinX = 0.0;
        public double ExtentsMinY = 0.0;
        public double ExtentsMinZ = 0.0;
        public double ExtentsMaxX = 0.0;
        public double ExtentsMaxY = 0.0;
        public double ExtentsMaxZ = 0.0;
        public string ExtentsSource = string.Empty;

        public List<PortInfo> Ports = new List<PortInfo>();
        public List<string> Errors = new List<string>();

        public string FormatExtentsMin()
        {
            if (!HasExtents)
            {
                return "<not available>";
            }

            return "(" +
                   ExtentsMinX.ToString("0.###") + ", " +
                   ExtentsMinY.ToString("0.###") + ", " +
                   ExtentsMinZ.ToString("0.###") + ")";
        }

        public string FormatExtentsMax()
        {
            if (!HasExtents)
            {
                return "<not available>";
            }

            return "(" +
                   ExtentsMaxX.ToString("0.###") + ", " +
                   ExtentsMaxY.ToString("0.###") + ", " +
                   ExtentsMaxZ.ToString("0.###") + ")";
        }

        public string FormatExtentsSize()
        {
            if (!HasExtents)
            {
                return "<not available>";
            }

            return "(" +
                   (ExtentsMaxX - ExtentsMinX).ToString("0.###") + ", " +
                   (ExtentsMaxY - ExtentsMinY).ToString("0.###") + ", " +
                   (ExtentsMaxZ - ExtentsMinZ).ToString("0.###") + ")";
        }
    }
}
