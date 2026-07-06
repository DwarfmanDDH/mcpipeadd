using System;
using System.Collections.Generic;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(McPipeAdd.Commands))]

namespace McPipeAdd
{
    public class Commands
    {
        [CommandMethod("MCCHECKPAIR")]
        public static void McCheckPair()
        {
            DependencyResolver.Register();

            Editor ed = AcadApp.DocumentManager.MdiActiveDocument.Editor;
            List<string> log = new List<string>();

            ReportWriter.Write(ed, log, "\nMCCHECKPAIR - Plant 3D two-part connection diagnostic started.");

            try
            {
                PromptSelectionOptions options = new PromptSelectionOptions();
                options.MessageForAdding = "\nSelect exactly 2 Plant 3D parts, then press Enter: ";
                options.AllowDuplicates = false;
                options.SingleOnly = false;

                PromptSelectionResult selectionResult = ed.GetSelection(options);

                if (selectionResult.Status != PromptStatus.OK)
                {
                    ReportWriter.Write(ed, log, "\nNothing selected.");
                    return;
                }

                ObjectId[] objectIds = selectionResult.Value.GetObjectIds();

                if (objectIds.Length != 2)
                {
                    ReportWriter.Write(ed, log, "\nYou selected " + objectIds.Length + " object(s).");
                    ReportWriter.Write(ed, log, "\nPlease run MCCHECKPAIR again and select exactly 2 Plant 3D parts.");
                    return;
                }

                PartInfo part1 = PlantPartReader.ReadPartInfo(objectIds[0]);
                PartInfo part2 = PlantPartReader.ReadPartInfo(objectIds[1]);

                ReportWriter.Write(ed, log, "\n");
                ReportWriter.Write(ed, log, "\n================ MCCHECKPAIR REPORT ================");

                ReportWriter.WritePartSummary(ed, log, "PART 1", part1);
                ReportWriter.WritePartSummary(ed, log, "PART 2", part2);

                ConnectionAnalyzer.WritePairAnalysis(ed, log, part1, part2);

                ReportWriter.Write(ed, log, "\n================ END MCCHECKPAIR REPORT ================");
                ReportWriter.WriteLogFile(ed, log, "McPipeAdd_MCCHECKPAIR");
            }
            catch (System.Exception ex)
            {
                ReportWriter.Write(ed, log, "\nMCCHECKPAIR failed.");
                ReportWriter.Write(ed, log, "\nError: " + ex.Message);
                ReportWriter.Write(ed, log, "\nStack trace: " + ex.StackTrace);

                ReportWriter.WriteLogFile(ed, log, "McPipeAdd_MCCHECKPAIR_ERROR");
            }
        }

        [CommandMethod("MCCHECKCONTINUE")]
        public static void McCheckContinue()
        {
            DependencyResolver.Register();

            Editor ed = AcadApp.DocumentManager.MdiActiveDocument.Editor;
            List<string> log = new List<string>();

            ReportWriter.Write(ed, log, "\nMCCHECKCONTINUE - Plant 3D continuation diagnostic started.");

            try
            {
                PromptSelectionOptions options = new PromptSelectionOptions();
                options.MessageForAdding =
                    "\nSelect 1 part for a continuation pre-check, or select 2 parts for a continuation result check.\n" +
                    "For a result check, select the original continuation start part first, then the component or pipe Plant inserted: ";

                options.AllowDuplicates = false;
                options.SingleOnly = false;

                PromptSelectionResult selectionResult = ed.GetSelection(options);

                if (selectionResult.Status != PromptStatus.OK)
                {
                    ReportWriter.Write(ed, log, "\nNothing selected.");
                    return;
                }

                ObjectId[] objectIds = selectionResult.Value.GetObjectIds();

                if (objectIds.Length != 1 && objectIds.Length != 2)
                {
                    ReportWriter.Write(ed, log, "\nYou selected " + objectIds.Length + " object(s).");
                    ReportWriter.Write(ed, log, "\nPlease run MCCHECKCONTINUE again and select either 1 or 2 Plant 3D parts.");
                    return;
                }

                PartInfo part1 = PlantPartReader.ReadPartInfo(objectIds[0]);
                PartInfo part2 = null;

                if (objectIds.Length == 2)
                {
                    part2 = PlantPartReader.ReadPartInfo(objectIds[1]);
                }

                ReportWriter.Write(ed, log, "\n");
                ReportWriter.Write(ed, log, "\n================ MCCHECKCONTINUE REPORT ================");

                ReportWriter.WritePartSummary(ed, log, "SELECTED PART 1", part1);

                if (part2 != null)
                {
                    ReportWriter.WritePartSummary(ed, log, "SELECTED PART 2", part2);
                }

                ContinuationResultAnalyzer.WriteContinuationAnalysis(ed, log, part1, part2);

                ReportWriter.Write(ed, log, "\n================ END MCCHECKCONTINUE REPORT ================");
                ReportWriter.WriteLogFile(ed, log, "McPipeAdd_MCCHECKCONTINUE");
            }
            catch (System.Exception ex)
            {
                ReportWriter.Write(ed, log, "\nMCCHECKCONTINUE failed.");
                ReportWriter.Write(ed, log, "\nError: " + ex.Message);
                ReportWriter.Write(ed, log, "\nStack trace: " + ex.StackTrace);

                ReportWriter.WriteLogFile(ed, log, "McPipeAdd_MCCHECKCONTINUE_ERROR");
            }
        }

        [CommandMethod("MCCHECKCUSTOMPART")]
        public static void McCheckCustomPart()
        {
            DependencyResolver.Register();

            Editor ed = AcadApp.DocumentManager.MdiActiveDocument.Editor;
            List<string> log = new List<string>();

            ReportWriter.Write(ed, log, "\nMCCHECKCUSTOMPART - Plant 3D custom part geometry / port diagnostic started.");

            try
            {
                PromptSelectionOptions options = new PromptSelectionOptions();
                options.MessageForAdding =
                    "\nSelect 1 to 4 custom Plant 3D parts, then press Enter.\n" +
                    "Use 1 part for block/port debugging, or 2+ parts to compare connected engagement/alignment: ";

                options.AllowDuplicates = false;
                options.SingleOnly = false;

                PromptSelectionResult selectionResult = ed.GetSelection(options);

                if (selectionResult.Status != PromptStatus.OK)
                {
                    ReportWriter.Write(ed, log, "\nNothing selected.");
                    return;
                }

                ObjectId[] objectIds = selectionResult.Value.GetObjectIds();

                if (objectIds.Length < 1 || objectIds.Length > 4)
                {
                    ReportWriter.Write(ed, log, "\nYou selected " + objectIds.Length + " object(s).");
                    ReportWriter.Write(ed, log, "\nPlease run MCCHECKCUSTOMPART again and select between 1 and 4 Plant 3D parts.");
                    return;
                }

                CustomPartDebugOptions debugOptions = PromptForCustomPartDebugOptions(ed, log);

                List<PartInfo> parts = new List<PartInfo>();

                for (int i = 0; i < objectIds.Length; i++)
                {
                    parts.Add(PlantPartReader.ReadPartInfo(objectIds[i]));
                }

                ReportWriter.Write(ed, log, "\n");
                ReportWriter.Write(ed, log, "\n================ MCCHECKCUSTOMPART REPORT ================");

                for (int i = 0; i < parts.Count; i++)
                {
                    ReportWriter.WritePartSummary(ed, log, "SELECTED PART " + (i + 1), parts[i]);
                }

                CustomPartDebugAnalyzer.WriteCustomPartDebug(ed, log, parts, debugOptions);

                ReportWriter.Write(ed, log, "\n================ END MCCHECKCUSTOMPART REPORT ================");
                ReportWriter.WriteLogFile(ed, log, "McPipeAdd_MCCHECKCUSTOMPART");
            }
            catch (System.Exception ex)
            {
                ReportWriter.Write(ed, log, "\nMCCHECKCUSTOMPART failed.");
                ReportWriter.Write(ed, log, "\nError: " + ex.Message);
                ReportWriter.Write(ed, log, "\nStack trace: " + ex.StackTrace);

                ReportWriter.WriteLogFile(ed, log, "McPipeAdd_MCCHECKCUSTOMPART_ERROR");
            }
        }


        private static CustomPartDebugOptions PromptForCustomPartDebugOptions(Editor ed, List<string> log)
        {
            CustomPartDebugOptions options = new CustomPartDebugOptions();

            PromptKeywordOptions engagementQuestion =
                new PromptKeywordOptions("\nQuantify expected engagement length? [No/Yes] <No>: ");

            engagementQuestion.Keywords.Add("No");
            engagementQuestion.Keywords.Add("Yes");
            engagementQuestion.Keywords.Default = "No";
            engagementQuestion.AllowNone = true;

            PromptResult engagementAnswer = ed.GetKeywords(engagementQuestion);

            if (engagementAnswer.Status == PromptStatus.OK &&
                TextUtil.SameText(engagementAnswer.StringResult, "Yes"))
            {
                PromptStringOptions portPrompt =
                    new PromptStringOptions("\nEngagement port name <S2>: ");

                portPrompt.AllowSpaces = false;

                PromptResult portAnswer = ed.GetString(portPrompt);

                if (portAnswer.Status == PromptStatus.OK &&
                    !string.IsNullOrWhiteSpace(portAnswer.StringResult))
                {
                    options.EngagementPortName = portAnswer.StringResult.Trim();
                }

                PromptDoubleOptions lengthPrompt =
                    new PromptDoubleOptions("\nExpected engagement/setback length in drawing units: ");

                lengthPrompt.AllowNegative = false;
                lengthPrompt.AllowZero = true;
                lengthPrompt.AllowNone = false;

                PromptDoubleResult lengthAnswer = ed.GetDouble(lengthPrompt);

                if (lengthAnswer.Status == PromptStatus.OK)
                {
                    options.HasExpectedEngagementLength = true;
                    options.ExpectedEngagementLength = lengthAnswer.Value;

                    ReportWriter.Write(ed, log, "\nExpected engagement check enabled.");
                    ReportWriter.Write(ed, log, "\n  Port: " + options.EngagementPortName);
                    ReportWriter.Write(ed, log, "\n  Expected engagement/setback: " + options.ExpectedEngagementLength.ToString("0.######"));
                }
            }

            PromptKeywordOptions clockingQuestion =
                new PromptKeywordOptions("\nQuantify clocking/roll angle with a picked reference point? [No/Yes] <No>: ");

            clockingQuestion.Keywords.Add("No");
            clockingQuestion.Keywords.Add("Yes");
            clockingQuestion.Keywords.Default = "No";
            clockingQuestion.AllowNone = true;

            PromptResult clockingAnswer = ed.GetKeywords(clockingQuestion);

            if (clockingAnswer.Status == PromptStatus.OK &&
                TextUtil.SameText(clockingAnswer.StringResult, "Yes"))
            {
                PromptStringOptions portPrompt =
                    new PromptStringOptions("\nClocking axis port name <S2>: ");

                portPrompt.AllowSpaces = false;

                PromptResult portAnswer = ed.GetString(portPrompt);

                if (portAnswer.Status == PromptStatus.OK &&
                    !string.IsNullOrWhiteSpace(portAnswer.StringResult))
                {
                    options.ClockingPortName = portAnswer.StringResult.Trim();
                }

                PromptPointOptions pointPrompt =
                    new PromptPointOptions(
                        "\nPick a clocking reference point on the custom part.\n" +
                        "Use a point that should represent 12 o'clock / top when viewed from the selected port end: ");

                PromptPointResult pointAnswer = ed.GetPoint(pointPrompt);

                if (pointAnswer.Status == PromptStatus.OK)
                {
                    Point3d point = pointAnswer.Value;

                    options.HasClockingReferencePoint = true;
                    options.ClockingReferenceX = point.X;
                    options.ClockingReferenceY = point.Y;
                    options.ClockingReferenceZ = point.Z;

                    ReportWriter.Write(ed, log, "\nClocking reference check enabled.");
                    ReportWriter.Write(ed, log, "\n  Axis port: " + options.ClockingPortName);
                    ReportWriter.Write(ed, log, "\n  Reference point: (" +
                        point.X.ToString("0.###") + ", " +
                        point.Y.ToString("0.###") + ", " +
                        point.Z.ToString("0.###") + ")");
                }
            }

            return options;
        }

    }
}
