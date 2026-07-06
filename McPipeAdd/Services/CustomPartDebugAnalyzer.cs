using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Autodesk.AutoCAD.EditorInput;

namespace McPipeAdd
{
    public static class CustomPartDebugAnalyzer
    {
        private const double PositionTolerance = 0.01;
        private const double AngleToleranceDegrees = 2.0;

        public static void WriteCustomPartDebug(
            Editor ed,
            List<string> log,
            List<PartInfo> parts)
        {
            WriteCustomPartDebug(ed, log, parts, new CustomPartDebugOptions());
        }

        public static void WriteCustomPartDebug(
            Editor ed,
            List<string> log,
            List<PartInfo> parts,
            CustomPartDebugOptions options)
        {
            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n----------------------------------------------------");
            ReportWriter.Write(ed, log, "\nCUSTOM PART GEOMETRY / PORT DEBUG");
            ReportWriter.Write(ed, log, "\n----------------------------------------------------");

            if (parts == null || parts.Count == 0)
            {
                ReportWriter.Write(ed, log, "\nNo parts were supplied to the custom part diagnostic.");
                return;
            }

            ReportWriter.Write(ed, log, "\nPurpose:");
            ReportWriter.Write(ed, log, "\n  This check is for custom part issues such as bad engagement, crooked insertion, wrong port location,");
            ReportWriter.Write(ed, log, "\n  non-collinear ports, and port direction vectors that do not match the part centerline.");

            foreach (PartInfo part in parts)
            {
                WriteSinglePartDebug(ed, log, part, options);
            }

            if (parts.Count >= 2)
            {
                WriteInterPartDebug(ed, log, parts);
            }
        }

        private static void WriteSinglePartDebug(Editor ed, List<string> log, PartInfo part, CustomPartDebugOptions options)
        {
            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\nCUSTOM PART CHECK: " + TextUtil.NullText(part.PartFamilyLongDesc));
            ReportWriter.Write(ed, log, "\n  Class: " + TextUtil.NullText(part.PnPClassName));
            ReportWriter.Write(ed, log, "\n  Spec: " + TextUtil.NullText(part.Spec));
            ReportWriter.Write(ed, log, "\n  Size: " + TextUtil.NullText(part.Size));
            ReportWriter.Write(ed, log, "\n  Port count: " + part.Ports.Count);

            if (part.Ports.Count == 0)
            {
                ReportWriter.Write(ed, log, "\n  No ports were found. Plant cannot connect this as a routed part without usable ports.");
                return;
            }

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n  PORT DETAIL:");

            foreach (PortInfo port in part.Ports)
            {
                ReportWriter.Write(ed, log, "\n    " + TextUtil.NullText(port.Name));
                ReportWriter.Write(ed, log, "\n      EndCondition: " + TextUtil.NullText(port.EndCondition));
                ReportWriter.Write(ed, log, "\n      NominalDiameter: " + TextUtil.NullText(port.NominalDiameter));
                ReportWriter.Write(ed, log, "\n      Facing: " + TextUtil.NullText(port.Facing));
                ReportWriter.Write(ed, log, "\n      PressureClass: " + TextUtil.NullText(port.PressureClass));
                ReportWriter.Write(ed, log, "\n      Position: " + port.FormatPosition());
                ReportWriter.Write(ed, log, "\n      PositionSource: " + TextUtil.NullText(port.PositionSource));
                ReportWriter.Write(ed, log, "\n      Direction: " + port.FormatDirection());
                ReportWriter.Write(ed, log, "\n      DirectionSource: " + TextUtil.NullText(port.DirectionSource));

                if (port.HasDirection)
                {
                    ReportWriter.Write(ed, log, "\n      DirectionLength: " + FormatNumber(port.DirectionLength()));
                }
            }

            WriteTwoPortGeometryCheck(ed, log, part);
            WriteExtentsEngagementCheck(ed, log, part);
            WriteQuantifiedEngagementCheck(ed, log, part, options);
            WriteClockingReferenceCheck(ed, log, part, options);
            WriteMultiPortSpacingCheck(ed, log, part);
        }

        private static void WriteTwoPortGeometryCheck(Editor ed, List<string> log, PartInfo part)
        {
            if (part.Ports.Count != 2)
            {
                return;
            }

            PortInfo p1 = part.Ports[0];
            PortInfo p2 = part.Ports[1];

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n  TWO-PORT INLINE CHECK:");

            if (!p1.HasPosition || !p2.HasPosition)
            {
                ReportWriter.Write(ed, log, "\n    Cannot check centerline because one or both port positions are unavailable.");
                return;
            }

            double lx = p2.X - p1.X;
            double ly = p2.Y - p1.Y;
            double lz = p2.Z - p1.Z;
            double length = VectorLength(lx, ly, lz);

            ReportWriter.Write(ed, log, "\n    " + p1.Name + " -> " + p2.Name + " centerline vector: " + FormatVector(lx, ly, lz));
            ReportWriter.Write(ed, log, "\n    Port-to-port distance / face-to-face length: " + FormatNumber(length));

            if (length <= PositionTolerance)
            {
                ReportWriter.Write(ed, log, "\n    WARNING: The two ports are almost on top of each other. Inline parts normally need separated ports.");
                return;
            }

            if (!p1.HasDirection && !p2.HasDirection)
            {
                ReportWriter.Write(ed, log, "\n    Port direction vectors are not available, so crooked/axis direction cannot be fully verified.");
                return;
            }

            if (p1.HasDirection)
            {
                double angleToLine = AngleBetween(
                    p1.DirectionX,
                    p1.DirectionY,
                    p1.DirectionZ,
                    lx,
                    ly,
                    lz);

                double nearestAxisAngle = NearestAxisAngle(angleToLine);

                ReportWriter.Write(ed, log, "\n    " + p1.Name + " direction angle to centerline axis: " + FormatNumber(nearestAxisAngle) + " deg");

                if (nearestAxisAngle > AngleToleranceDegrees)
                {
                    ReportWriter.Write(ed, log, "\n      WARNING: " + p1.Name + " direction is not aligned with the part centerline axis.");
                }
            }

            if (p2.HasDirection)
            {
                double angleToLine = AngleBetween(
                    p2.DirectionX,
                    p2.DirectionY,
                    p2.DirectionZ,
                    lx,
                    ly,
                    lz);

                double nearestAxisAngle = NearestAxisAngle(angleToLine);

                ReportWriter.Write(ed, log, "\n    " + p2.Name + " direction angle to centerline axis: " + FormatNumber(nearestAxisAngle) + " deg");

                if (nearestAxisAngle > AngleToleranceDegrees)
                {
                    ReportWriter.Write(ed, log, "\n      WARNING: " + p2.Name + " direction is not aligned with the part centerline axis.");
                }
            }

            if (p1.HasDirection && p2.HasDirection)
            {
                double directionAngle = AngleBetween(
                    p1.DirectionX,
                    p1.DirectionY,
                    p1.DirectionZ,
                    p2.DirectionX,
                    p2.DirectionY,
                    p2.DirectionZ);

                ReportWriter.Write(ed, log, "\n    Angle between " + p1.Name + " and " + p2.Name + " direction vectors: " + FormatNumber(directionAngle) + " deg");

                if (Math.Abs(directionAngle - 180.0) <= AngleToleranceDegrees)
                {
                    ReportWriter.Write(ed, log, "\n      Direction relationship: GOOD for an inline part. Port directions are opposite.");
                }
                else if (directionAngle <= AngleToleranceDegrees)
                {
                    ReportWriter.Write(ed, log, "\n      WARNING: Port directions point the same way. Inline part ports normally point opposite directions.");
                }
                else
                {
                    ReportWriter.Write(ed, log, "\n      WARNING: Port directions are neither opposite nor the same. This can make a custom part insert crooked.");
                }
            }
        }

        private static void WriteExtentsEngagementCheck(Editor ed, List<string> log, PartInfo part)
        {
            if (part.Ports.Count != 2 || !part.HasExtents)
            {
                return;
            }

            PortInfo p1 = part.Ports[0];
            PortInfo p2 = part.Ports[1];

            if (!p1.HasPosition || !p2.HasPosition)
            {
                return;
            }

            double lx = p2.X - p1.X;
            double ly = p2.Y - p1.Y;
            double lz = p2.Z - p1.Z;

            int axis = DominantAxis(lx, ly, lz);

            double min;
            double max;
            double p1Value;
            double p2Value;
            string axisName;

            GetAxisValues(part, p1, p2, axis, out min, out max, out p1Value, out p2Value, out axisName);

            PortInfo lowerPort = p1Value <= p2Value ? p1 : p2;
            PortInfo upperPort = p1Value <= p2Value ? p2 : p1;
            double lowerValue = Math.Min(p1Value, p2Value);
            double upperValue = Math.Max(p1Value, p2Value);

            double lowerOffset = lowerValue - min;
            double upperOffset = max - upperValue;

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n  ENGAGEMENT / PORT-TO-GEOMETRY CHECK:");
            ReportWriter.Write(ed, log, "\n    This compares port locations to the part's AutoCAD geometric extents.");
            ReportWriter.Write(ed, log, "\n    Dominant inline axis: " + axisName);
            ReportWriter.Write(ed, log, "\n    Extents min/max on " + axisName + ": " + FormatNumber(min) + " / " + FormatNumber(max));
            ReportWriter.Write(ed, log, "\n    Lower-side port: " + lowerPort.Name + " offset from extents min: " + FormatNumber(lowerOffset));
            ReportWriter.Write(ed, log, "\n    Upper-side port: " + upperPort.Name + " offset from extents max: " + FormatNumber(upperOffset));

            if (Math.Abs(lowerOffset) > PositionTolerance)
            {
                ReportWriter.Write(ed, log, "\n      CHECK: " + lowerPort.Name + " is not exactly on the lower geometry end along " + axisName + ".");
            }

            if (Math.Abs(upperOffset) > PositionTolerance)
            {
                ReportWriter.Write(ed, log, "\n      CHECK: " + upperPort.Name + " is not exactly on the upper geometry end along " + axisName + ".");
            }

            ReportWriter.Write(ed, log, "\n    Note: this extents check is strongest when the custom part is placed square to WCS.");
            ReportWriter.Write(ed, log, "\n    If the part is rotated in the drawing, use the port direction checks as the stronger crooked-part evidence.");
        }

        private static void WriteMultiPortSpacingCheck(Editor ed, List<string> log, PartInfo part)
        {
            List<PortInfo> portsWithPosition = part.Ports.Where(p => p.HasPosition).ToList();

            if (portsWithPosition.Count < 2)
            {
                return;
            }

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n  PORT-TO-PORT DISTANCES:");

            for (int i = 0; i < portsWithPosition.Count; i++)
            {
                for (int j = i + 1; j < portsWithPosition.Count; j++)
                {
                    PortInfo a = portsWithPosition[i];
                    PortInfo b = portsWithPosition[j];

                    ReportWriter.Write(ed, log,
                        "\n    " + a.Name + " to " + b.Name + ": " +
                        FormatNumber(a.DistanceTo(b)));
                }
            }
        }

        private static void WriteQuantifiedEngagementCheck(
            Editor ed,
            List<string> log,
            PartInfo part,
            CustomPartDebugOptions options)
        {
            if (options == null || !options.HasExpectedEngagementLength)
            {
                return;
            }

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n  QUANTIFIED ENGAGEMENT CHECK:");

            if (!part.HasExtents)
            {
                ReportWriter.Write(ed, log, "\n    Cannot quantify engagement because geometric extents are unavailable.");
                return;
            }

            PortInfo port = FindPort(part, options.EngagementPortName);

            if (port == null)
            {
                ReportWriter.Write(ed, log, "\n    Cannot find engagement port: " + TextUtil.NullText(options.EngagementPortName));
                return;
            }

            if (!port.HasPosition)
            {
                ReportWriter.Write(ed, log, "\n    Cannot quantify engagement because " + port.Name + " has no position.");
                return;
            }

            if (!port.HasDirection)
            {
                ReportWriter.Write(ed, log, "\n    Cannot quantify engagement because " + port.Name + " has no direction vector.");
                return;
            }

            double dx = port.DirectionX;
            double dy = port.DirectionY;
            double dz = port.DirectionZ;

            if (!Normalize(ref dx, ref dy, ref dz))
            {
                ReportWriter.Write(ed, log, "\n    Cannot quantify engagement because " + port.Name + " direction vector length is zero.");
                return;
            }

            double portProjection = Dot(port.X, port.Y, port.Z, dx, dy, dz);
            double faceProjection = MaxExtentsProjection(part, dx, dy, dz);
            double measuredSetback = faceProjection - portProjection;
            double expectedSetback = options.ExpectedEngagementLength;
            double error = expectedSetback - measuredSetback;

            ReportWriter.Write(ed, log, "\n    Port checked: " + port.Name);
            ReportWriter.Write(ed, log, "\n    Port outward direction: " + port.FormatDirection());
            ReportWriter.Write(ed, log, "\n    Expected setback / engagement length: " + FormatNumber(expectedSetback));
            ReportWriter.Write(ed, log, "\n    Measured port setback from outside geometry face: " + FormatNumber(measuredSetback));
            ReportWriter.Write(ed, log, "\n    Engagement error, expected minus measured: " + FormatNumber(error));

            if (Math.Abs(error) <= PositionTolerance)
            {
                ReportWriter.Write(ed, log, "\n    Result: GOOD. Port setback matches expected engagement within tolerance.");
            }
            else if (error > 0.0)
            {
                ReportWriter.Write(ed, log, "\n    Result: PORT TOO SHALLOW / TOO FLUSH BY " + FormatNumber(error) + ".");
                ReportWriter.Write(ed, log, "\n    Correction: move " + port.Name + " inward, opposite its outward direction, by " + FormatNumber(error) + " drawing units.");
                ReportWriter.Write(ed, log, "\n    Move vector: " + FormatVector(-dx * error, -dy * error, -dz * error));
            }
            else
            {
                double amount = Math.Abs(error);

                ReportWriter.Write(ed, log, "\n    Result: PORT TOO DEEP / TOO RECESSED BY " + FormatNumber(amount) + ".");
                ReportWriter.Write(ed, log, "\n    Correction: move " + port.Name + " outward, along its outward direction, by " + FormatNumber(amount) + " drawing units.");
                ReportWriter.Write(ed, log, "\n    Move vector: " + FormatVector(dx * amount, dy * amount, dz * amount));
            }
        }

        private static void WriteClockingReferenceCheck(
            Editor ed,
            List<string> log,
            PartInfo part,
            CustomPartDebugOptions options)
        {
            if (options == null || !options.HasClockingReferencePoint)
            {
                return;
            }

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n  QUANTIFIED CLOCKING / ROLL CHECK:");

            PortInfo axisPort = FindPort(part, options.ClockingPortName);

            if (axisPort == null)
            {
                ReportWriter.Write(ed, log, "\n    Cannot find clocking axis port: " + TextUtil.NullText(options.ClockingPortName));
                return;
            }

            if (!axisPort.HasPosition)
            {
                ReportWriter.Write(ed, log, "\n    Cannot quantify clocking because " + axisPort.Name + " has no position.");
                return;
            }

            if (!axisPort.HasDirection)
            {
                ReportWriter.Write(ed, log, "\n    Cannot quantify clocking because " + axisPort.Name + " has no direction vector.");
                return;
            }

            double outwardX = axisPort.DirectionX;
            double outwardY = axisPort.DirectionY;
            double outwardZ = axisPort.DirectionZ;

            if (!Normalize(ref outwardX, ref outwardY, ref outwardZ))
            {
                ReportWriter.Write(ed, log, "\n    Cannot quantify clocking because " + axisPort.Name + " direction vector length is zero.");
                return;
            }

            // Viewer is assumed to be standing at the selected port end, looking back toward the part.
            // The view direction points from the viewer into the part.
            double viewX = -outwardX;
            double viewY = -outwardY;
            double viewZ = -outwardZ;

            double upX = 0.0;
            double upY = 0.0;
            double upZ = 1.0;

            ProjectOntoPlane(ref upX, ref upY, ref upZ, viewX, viewY, viewZ);

            if (!Normalize(ref upX, ref upY, ref upZ))
            {
                upX = 1.0;
                upY = 0.0;
                upZ = 0.0;

                ProjectOntoPlane(ref upX, ref upY, ref upZ, viewX, viewY, viewZ);
                Normalize(ref upX, ref upY, ref upZ);
            }

            double rightX;
            double rightY;
            double rightZ;

            Cross(upX, upY, upZ, viewX, viewY, viewZ, out rightX, out rightY, out rightZ);
            Normalize(ref rightX, ref rightY, ref rightZ);

            double refX = options.ClockingReferenceX - axisPort.X;
            double refY = options.ClockingReferenceY - axisPort.Y;
            double refZ = options.ClockingReferenceZ - axisPort.Z;

            ProjectOntoPlane(ref refX, ref refY, ref refZ, viewX, viewY, viewZ);

            if (!Normalize(ref refX, ref refY, ref refZ))
            {
                ReportWriter.Write(ed, log, "\n    Cannot quantify clocking because the picked reference point lies on or too near the port axis.");
                return;
            }

            double screenRight = Dot(refX, refY, refZ, rightX, rightY, rightZ);
            double screenUp = Dot(refX, refY, refZ, upX, upY, upZ);
            double clockwiseDegrees = Math.Atan2(screenRight, screenUp) * 180.0 / Math.PI;

            string directionText = "on reference";

            if (clockwiseDegrees > AngleToleranceDegrees)
            {
                directionText = "CLOCKWISE";
            }
            else if (clockwiseDegrees < -AngleToleranceDegrees)
            {
                directionText = "COUNTER-CLOCKWISE";
            }

            ReportWriter.Write(ed, log, "\n    Axis port: " + axisPort.Name);
            ReportWriter.Write(ed, log, "\n    Axis port position: " + axisPort.FormatPosition());
            ReportWriter.Write(ed, log, "\n    Axis port outward direction: " + axisPort.FormatDirection());
            ReportWriter.Write(ed, log, "\n    View assumption: looking from " + axisPort.Name + " end back toward the part.");
            ReportWriter.Write(ed, log, "\n    Expected 12 o'clock reference: WCS +Z projected perpendicular to the port axis.");
            ReportWriter.Write(ed, log, "\n    Picked reference point: " + FormatVector(options.ClockingReferenceX, options.ClockingReferenceY, options.ClockingReferenceZ));
            ReportWriter.Write(ed, log, "\n    Clocking angle: " + FormatNumber(Math.Abs(clockwiseDegrees)) + " deg " + directionText + ".");
            ReportWriter.Write(ed, log, "\n    Signed angle: " + FormatNumber(clockwiseDegrees) + " deg. Positive means clockwise in this view.");

            if (Math.Abs(clockwiseDegrees) > AngleToleranceDegrees)
            {
                ReportWriter.Write(ed, log, "\n    Correction: rotate the custom block/solid geometry " +
                    FormatNumber(Math.Abs(clockwiseDegrees)) + " deg " +
                    (clockwiseDegrees > 0.0 ? "counter-clockwise" : "clockwise") +
                    " around the " + axisPort.Name + " port axis to bring the reference back to 12 o'clock.");
            }
            else
            {
                ReportWriter.Write(ed, log, "\n    Result: GOOD. Clocking is within tolerance relative to the picked reference point.");
            }
        }

        private static PortInfo FindPort(PartInfo part, string portName)
        {
            if (part == null || part.Ports == null)
            {
                return null;
            }

            foreach (PortInfo port in part.Ports)
            {
                if (TextUtil.SameText(port.Name, portName))
                {
                    return port;
                }
            }

            return null;
        }

        private static double MaxExtentsProjection(PartInfo part, double dx, double dy, double dz)
        {
            double[] xs = { part.ExtentsMinX, part.ExtentsMaxX };
            double[] ys = { part.ExtentsMinY, part.ExtentsMaxY };
            double[] zs = { part.ExtentsMinZ, part.ExtentsMaxZ };

            double max = double.MinValue;

            foreach (double x in xs)
            {
                foreach (double y in ys)
                {
                    foreach (double z in zs)
                    {
                        double projection = Dot(x, y, z, dx, dy, dz);

                        if (projection > max)
                        {
                            max = projection;
                        }
                    }
                }
            }

            return max;
        }

        private static void ProjectOntoPlane(
            ref double x,
            ref double y,
            ref double z,
            double normalX,
            double normalY,
            double normalZ)
        {
            double dot = Dot(x, y, z, normalX, normalY, normalZ);

            x = x - (dot * normalX);
            y = y - (dot * normalY);
            z = z - (dot * normalZ);
        }

        private static bool Normalize(ref double x, ref double y, ref double z)
        {
            double length = VectorLength(x, y, z);

            if (length <= 0.0000001)
            {
                return false;
            }

            x = x / length;
            y = y / length;
            z = z / length;

            return true;
        }

        private static double Dot(
            double ax,
            double ay,
            double az,
            double bx,
            double by,
            double bz)
        {
            return (ax * bx) + (ay * by) + (az * bz);
        }

        private static void Cross(
            double ax,
            double ay,
            double az,
            double bx,
            double by,
            double bz,
            out double cx,
            out double cy,
            out double cz)
        {
            cx = (ay * bz) - (az * by);
            cy = (az * bx) - (ax * bz);
            cz = (ax * by) - (ay * bx);
        }

        private static void WriteInterPartDebug(Editor ed, List<string> log, List<PartInfo> parts)
        {
            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n----------------------------------------------------");
            ReportWriter.Write(ed, log, "\nINTER-PART CONNECTION / ENGAGEMENT CHECK");
            ReportWriter.Write(ed, log, "\n----------------------------------------------------");

            for (int i = 0; i < parts.Count; i++)
            {
                for (int j = i + 1; j < parts.Count; j++)
                {
                    WriteClosestPairCheck(ed, log, parts[i], parts[j]);
                }
            }
        }

        private static void WriteClosestPairCheck(Editor ed, List<string> log, PartInfo part1, PartInfo part2)
        {
            PortInfo best1 = null;
            PortInfo best2 = null;
            double bestDistance = double.MaxValue;

            foreach (PortInfo p1 in part1.Ports)
            {
                foreach (PortInfo p2 in part2.Ports)
                {
                    double distance = p1.DistanceTo(p2);

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best1 = p1;
                        best2 = p2;
                    }
                }
            }

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n  Closest pair:");
            ReportWriter.Write(ed, log, "\n    Part 1: " + TextUtil.NullText(part1.PartFamilyLongDesc));
            ReportWriter.Write(ed, log, "\n    Part 2: " + TextUtil.NullText(part2.PartFamilyLongDesc));

            if (best1 == null || best2 == null || bestDistance == double.MaxValue)
            {
                ReportWriter.Write(ed, log, "\n    Could not find a closest port pair because port positions are unavailable.");
                return;
            }

            ReportWriter.Write(ed, log, "\n    " + best1.Name + " " + best1.FormatPosition() + " <-> " + best2.Name + " " + best2.FormatPosition());
            ReportWriter.Write(ed, log, "\n    Distance: " + FormatNumber(bestDistance));

            if (bestDistance > PositionTolerance)
            {
                ReportWriter.Write(ed, log, "\n      WARNING: Closest connected ports are not coincident. This can show engagement/offset problems.");
            }
            else
            {
                ReportWriter.Write(ed, log, "\n      Port positions are coincident within tolerance.");
            }

            if (best1.HasDirection && best2.HasDirection)
            {
                double directionAngle = AngleBetween(
                    best1.DirectionX,
                    best1.DirectionY,
                    best1.DirectionZ,
                    best2.DirectionX,
                    best2.DirectionY,
                    best2.DirectionZ);

                ReportWriter.Write(ed, log, "\n    Angle between closest port directions: " + FormatNumber(directionAngle) + " deg");

                if (Math.Abs(directionAngle - 180.0) <= AngleToleranceDegrees)
                {
                    ReportWriter.Write(ed, log, "\n      Direction relationship: GOOD. Connected ports point opposite directions.");
                }
                else
                {
                    ReportWriter.Write(ed, log, "\n      WARNING: Connected port directions are not opposite. This can show why a part inserts crooked.");
                }
            }
            else
            {
                ReportWriter.Write(ed, log, "\n    Port direction comparison unavailable because one or both ports did not expose direction vectors.");
            }
        }

        private static double VectorLength(double x, double y, double z)
        {
            return Math.Sqrt((x * x) + (y * y) + (z * z));
        }

        private static double AngleBetween(
            double ax,
            double ay,
            double az,
            double bx,
            double by,
            double bz)
        {
            double aLength = VectorLength(ax, ay, az);
            double bLength = VectorLength(bx, by, bz);

            if (aLength <= 0.0000001 || bLength <= 0.0000001)
            {
                return 0.0;
            }

            double dot = ((ax * bx) + (ay * by) + (az * bz)) / (aLength * bLength);

            if (dot > 1.0)
            {
                dot = 1.0;
            }

            if (dot < -1.0)
            {
                dot = -1.0;
            }

            return Math.Acos(dot) * 180.0 / Math.PI;
        }

        private static double NearestAxisAngle(double angle)
        {
            double direct = Math.Abs(angle);
            double opposite = Math.Abs(180.0 - angle);

            return Math.Min(direct, opposite);
        }

        private static int DominantAxis(double x, double y, double z)
        {
            double ax = Math.Abs(x);
            double ay = Math.Abs(y);
            double az = Math.Abs(z);

            if (ax >= ay && ax >= az)
            {
                return 0;
            }

            if (ay >= ax && ay >= az)
            {
                return 1;
            }

            return 2;
        }

        private static void GetAxisValues(
            PartInfo part,
            PortInfo p1,
            PortInfo p2,
            int axis,
            out double min,
            out double max,
            out double p1Value,
            out double p2Value,
            out string axisName)
        {
            if (axis == 0)
            {
                min = part.ExtentsMinX;
                max = part.ExtentsMaxX;
                p1Value = p1.X;
                p2Value = p2.X;
                axisName = "X";
                return;
            }

            if (axis == 1)
            {
                min = part.ExtentsMinY;
                max = part.ExtentsMaxY;
                p1Value = p1.Y;
                p2Value = p2.Y;
                axisName = "Y";
                return;
            }

            min = part.ExtentsMinZ;
            max = part.ExtentsMaxZ;
            p1Value = p1.Z;
            p2Value = p2.Z;
            axisName = "Z";
        }

        private static string FormatVector(double x, double y, double z)
        {
            return "(" +
                   FormatNumber(x) + ", " +
                   FormatNumber(y) + ", " +
                   FormatNumber(z) + ")";
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }
    }
}
