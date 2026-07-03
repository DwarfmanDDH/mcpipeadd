using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.PlottingServices;
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
    }
}