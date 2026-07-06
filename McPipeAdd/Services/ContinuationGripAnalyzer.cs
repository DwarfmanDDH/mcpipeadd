using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.EditorInput;

namespace McPipeAdd
{
    public static class ContinuationGripAnalyzer
    {
        public static void WriteContinuationAnalysis(Editor ed, List<string> log, PartInfo startPart)
        {
            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n----------------------------------------------------");
            ReportWriter.Write(ed, log, "\nCONTINUATION GRIP ANALYSIS");
            ReportWriter.Write(ed, log, "\n----------------------------------------------------");

            if (startPart == null)
            {
                ReportWriter.Write(ed, log, "\nNo start part was provided.");
                return;
            }

            if (startPart.Ports == null || startPart.Ports.Count == 0)
            {
                ReportWriter.Write(ed, log, "\nThe selected part has no readable ports.");
                return;
            }

            string specName = startPart.Spec;

            ReportWriter.Write(ed, log, "\nSelected part: " + TextUtil.NullText(startPart.PartFamilyLongDesc));
            ReportWriter.Write(ed, log, "\nClass: " + TextUtil.NullText(startPart.PnPClassName));
            ReportWriter.Write(ed, log, "\nSpec: " + TextUtil.NullText(specName));

            foreach (PortInfo port in startPart.Ports)
            {
                AnalyzePort(ed, log, startPart, port, specName);
            }
        }

        private static void AnalyzePort(
            Editor ed,
            List<string> log,
            PartInfo startPart,
            PortInfo port,
            string specName)
        {
            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\nContinuation port:");
            ReportWriter.Write(ed, log, "\n  " + startPart.Label + "." + port.Name);
            ReportWriter.Write(ed, log, "\n  EndCondition: " + TextUtil.NullText(port.EndCondition));
            ReportWriter.Write(ed, log, "\n  NominalDiameter: " + TextUtil.NullText(port.NominalDiameter));

            List<string> expectedPipeEnds = GetExpectedPipeEndsForContinuationPort(port.EndCondition);

            if (expectedPipeEnds.Count == 0)
            {
                ReportWriter.Write(ed, log, "\n  No direct pipe-end recommendation for this port end condition.");
                ReportWriter.Write(ed, log, "\n  This port may require a fitting, flange, gasket/bolt set, or another component before pipe.");
                return;
            }

            ReportWriter.Write(ed, log, "\n  Expected continuation pipe end(s): " + string.Join(", ", expectedPipeEnds));

            if (string.IsNullOrWhiteSpace(specName))
            {
                ReportWriter.Write(ed, log, "\n  Cannot inspect pipe candidates because the selected part has a blank spec.");
                return;
            }

            string primaryExpectedPipeEnd = expectedPipeEnds[0];

            SpecCandidateResult result =
                SpecPipeCandidateFinder.FindPipeCandidates(
                    specName,
                    port.NominalDiameter,
                    primaryExpectedPipeEnd);

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n  Spec pipe candidates:");
            ReportWriter.Write(ed, log, "\n    Spec: " + TextUtil.NullText(result.SpecName));
            ReportWriter.Write(ed, log, "\n    PSPC: " + TextUtil.NullText(result.PspcPath));
            ReportWriter.Write(ed, log, "\n    PSPX: " + TextUtil.NullText(result.PspxPath));

            if (result.Errors.Count > 0)
            {
                ReportWriter.Write(ed, log, "\n    Errors / Warnings:");

                foreach (string error in result.Errors)
                {
                    ReportWriter.Write(ed, log, "\n      " + error);
                }
            }

            if (result.Candidates.Count == 0)
            {
                ReportWriter.Write(ed, log, "\n    No pipe candidates found for this size.");
                return;
            }

            PipeCandidate firstByPriority = result.Candidates.FirstOrDefault();

            PipeCandidate firstMatching =
                result.Candidates.FirstOrDefault(c => CandidateMatchesAnyExpectedEnd(c, expectedPipeEnds));

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n  Candidate order check:");

            if (firstByPriority != null)
            {
                ReportWriter.Write(ed, log, "\n    First pipe by spec priority:");
                WriteCandidateSummary(ed, log, firstByPriority, expectedPipeEnds);
            }

            if (firstMatching != null)
            {
                ReportWriter.Write(ed, log, "\n");
                ReportWriter.Write(ed, log, "\n    First pipe matching expected continuation end:");
                WriteCandidateSummary(ed, log, firstMatching, expectedPipeEnds);
            }
            else
            {
                ReportWriter.Write(ed, log, "\n");
                ReportWriter.Write(ed, log, "\n    No pipe candidate matches expected continuation end(s): " + string.Join(", ", expectedPipeEnds));
            }

            if (firstByPriority != null &&
                firstMatching != null &&
                firstByPriority.PnPID != firstMatching.PnPID)
            {
                ReportWriter.Write(ed, log, "\n");
                ReportWriter.Write(ed, log, "\n  POTENTIAL CONTINUATION GRIP RISK:");
                ReportWriter.Write(ed, log, "\n    The first pipe by spec priority does not match the expected continuation end.");
                ReportWriter.Write(ed, log, "\n    A lower-priority pipe does match.");
                ReportWriter.Write(ed, log, "\n    This matches the suspected behavior: priority is being considered before connection suitability.");
            }

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n  Top pipe candidates:");

            foreach (PipeCandidate candidate in result.Candidates.Take(10))
            {
                WriteCandidateSummary(ed, log, candidate, expectedPipeEnds);
            }
        }

        private static void WriteCandidateSummary(
            Editor ed,
            List<string> log,
            PipeCandidate candidate,
            List<string> expectedPipeEnds)
        {
            bool matches = CandidateMatchesAnyExpectedEnd(candidate, expectedPipeEnds);

            ReportWriter.Write(ed, log, "\n    " + FormatPriority(candidate.Priority) + " " + TextUtil.NullText(candidate.PartFamilyLongDesc));
            ReportWriter.Write(ed, log, "\n      PnPID: " + candidate.PnPID);
            ReportWriter.Write(ed, log, "\n      FamilyId: " + TextUtil.NullText(candidate.PartFamilyId));
            ReportWriter.Write(ed, log, "\n      EndConditions: " + FormatEndConditions(candidate.EndConditions));
            ReportWriter.Write(ed, log, "\n      PortDetails: " + TextUtil.NullText(candidate.PortDetails));
            ReportWriter.Write(ed, log, "\n      Match for expected continuation end(s): " + (matches ? "YES" : "NO"));
        }

        private static bool CandidateMatchesAnyExpectedEnd(
            PipeCandidate candidate,
            List<string> expectedPipeEnds)
        {
            if (candidate == null || expectedPipeEnds == null)
            {
                return false;
            }

            foreach (string expectedPipeEnd in expectedPipeEnds)
            {
                if (candidate.MatchesExpectedEnd(expectedPipeEnd))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<string> GetExpectedPipeEndsForContinuationPort(string endCondition)
        {
            string end = TextUtil.Clean(endCondition);

            switch (end)
            {
                case "THDF":
                    return new List<string> { "THDM" };

                case "SW":
                    return new List<string> { "PL" };

                case "BV":
                    return new List<string> { "BV", "PL" };

                case "PL":
                    return new List<string> { "PL", "BV" };

                default:
                    return new List<string>();
            }
        }

        private static string FormatPriority(int? priority)
        {
            if (priority.HasValue)
            {
                return "[" + priority.Value + "]";
            }

            return "[no priority]";
        }

        private static string FormatEndConditions(List<string> endConditions)
        {
            if (endConditions == null || endConditions.Count == 0)
            {
                return "<none>";
            }

            return string.Join(", ", endConditions);
        }
    }
}