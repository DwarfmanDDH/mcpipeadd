using System;
using System.Collections.Generic;
using System.IO;

using Autodesk.AutoCAD.EditorInput;

namespace McPipeAdd
{
    public static class ReportWriter
    {
        public static void Write(Editor ed, List<string> log, string message)
        {
            ed.WriteMessage(message);
            log.Add(message.Replace("\n", Environment.NewLine));
        }

        public static void WritePartSummary(Editor ed, List<string> log, string title, PartInfo part)
        {
            Write(ed, log, "\n");
            Write(ed, log, "\n----------------------------------------------------");
            Write(ed, log, "\n" + title);
            Write(ed, log, "\n----------------------------------------------------");
            Write(ed, log, "\nObjectId: " + part.ObjectIdText);
            Write(ed, log, "\nPlant RowId: " + part.RowId);
            Write(ed, log, "\nRuntime Type: " + TextUtil.NullText(part.RuntimeType));
            Write(ed, log, "\nClass: " + TextUtil.NullText(part.PnPClassName));
            Write(ed, log, "\nSpec: " + TextUtil.NullText(part.Spec));
            Write(ed, log, "\nSize: " + TextUtil.NullText(part.Size));
            Write(ed, log, "\nNominalDiameter: " + TextUtil.NullText(part.NominalDiameter));
            Write(ed, log, "\nShortDescription: " + TextUtil.NullText(part.ShortDescription));
            Write(ed, log, "\nPartFamilyLongDesc: " + TextUtil.NullText(part.PartFamilyLongDesc));
            Write(ed, log, "\nPartSizeLongDesc: " + TextUtil.NullText(part.PartSizeLongDesc));
            Write(ed, log, "\nItemCode: " + TextUtil.NullText(part.ItemCode));
            Write(ed, log, "\nPressureClass: " + TextUtil.NullText(part.PressureClass));
            Write(ed, log, "\nDataLinks default PortName: " + TextUtil.NullText(part.DataLinksPortName));
            Write(ed, log, "\nDataLinks default EndType: " + TextUtil.NullText(part.DataLinksEndType));
            Write(ed, log, "\nDataLinks default Facing: " + TextUtil.NullText(part.DataLinksFacing));

            Write(ed, log, "\nGeometricExtents Min: " + part.FormatExtentsMin());
            Write(ed, log, "\nGeometricExtents Max: " + part.FormatExtentsMax());
            Write(ed, log, "\nGeometricExtents Size: " + part.FormatExtentsSize());
            Write(ed, log, "\nGeometricExtents Source: " + TextUtil.NullText(part.ExtentsSource));

            Write(ed, log, "\n");
            Write(ed, log, "\nPorts:");

            if (part.Ports.Count == 0)
            {
                Write(ed, log, "\n  No ports found.");
            }
            else
            {
                foreach (PortInfo port in part.Ports)
                {
                    Write(ed, log,
                        "\n  " + port.Name +
                        " | EndCondition: " + TextUtil.NullText(port.EndCondition) +
                        " | NominalDiameter: " + TextUtil.NullText(port.NominalDiameter) +
                        " | Facing: " + TextUtil.NullText(port.Facing) +
                        " | PressureClass: " + TextUtil.NullText(port.PressureClass) +
                        " | Position: " + port.FormatPosition() +
                        " | PositionSource: " + TextUtil.NullText(port.PositionSource) +
                        " | Direction: " + port.FormatDirection() +
                        " | DirectionSource: " + TextUtil.NullText(port.DirectionSource));
                }
            }

            if (part.Errors.Count > 0)
            {
                Write(ed, log, "\n");
                Write(ed, log, "\nErrors / Warnings:");

                foreach (string error in part.Errors)
                {
                    Write(ed, log, "\n  " + error);
                }
            }
        }

        public static void WriteLogFile(Editor ed, List<string> log, string prefix)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

                string fileName =
                    prefix + "_" +
                    DateTime.Now.ToString("yyyyMMdd_HHmmss") +
                    ".txt";

                string path = Path.Combine(desktop, fileName);

                File.WriteAllText(path, string.Join("", log));

                ed.WriteMessage("\nReport written to:");
                ed.WriteMessage("\n" + path);
            }
            catch
            {
                ed.WriteMessage("\nCould not write report file.");
            }
        }
    }
}
