using System;

namespace McPipeAdd
{
    public class PortInfo
    {
        public string Name = string.Empty;
        public string EndCondition = string.Empty;
        public string NominalDiameter = string.Empty;
        public string Facing = string.Empty;
        public string PressureClass = string.Empty;

        public bool HasPosition = false;
        public double X = 0.0;
        public double Y = 0.0;
        public double Z = 0.0;
        public string PositionSource = string.Empty;

        public double DistanceTo(PortInfo other)
        {
            if (other == null || !HasPosition || !other.HasPosition)
            {
                return double.MaxValue;
            }

            double dx = X - other.X;
            double dy = Y - other.Y;
            double dz = Z - other.Z;

            return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        }

        public string FormatPosition()
        {
            if (!HasPosition)
            {
                return "<not available>";
            }

            return "(" +
                   X.ToString("0.###") + ", " +
                   Y.ToString("0.###") + ", " +
                   Z.ToString("0.###") + ")";
        }
    }
}
