using System.Linq;
using System.Collections.Generic;

using Autodesk.AutoCAD.EditorInput;

namespace McPipeAdd
{
    public static class ConnectionAnalyzer
    {
        public static void WritePairAnalysis(Editor ed, List<string> log, PartInfo part1, PartInfo part2)
        {
            ConnectorConfigResult connectorConfig = ConnectorConfigReader.LoadProjectConnectorConfig();

            WriteConnectorConfigSummary(ed, log, connectorConfig);

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n----------------------------------------------------");
            ReportWriter.Write(ed, log, "\nCONNECTION ANALYSIS");
            ReportWriter.Write(ed, log, "\n----------------------------------------------------");

            bool anyValid = false;
            bool anySameSize = false;

            foreach (PortInfo p1 in part1.Ports)
            {
                foreach (PortInfo p2 in part2.Ports)
                {
                    bool sameSize = TextUtil.SameNominalDiameter(p1.NominalDiameter, p2.NominalDiameter);
                    bool valid =
                        sameSize &&
                        ConnectorConnectionAnalyzer.IsValidConnection(
                            connectorConfig,
                            p1.EndCondition,
                            p2.EndCondition);

                    if (sameSize)
                    {
                        anySameSize = true;
                    }

                    if (valid)
                    {
                        anyValid = true;
                    }

                    ReportWriter.Write(ed, log,
                        "\n" +
                        part1.Label + "." + p1.Name + " [" + TextUtil.NullText(p1.EndCondition) + ", " + TextUtil.NullText(p1.NominalDiameter) + "]" +
                        "  <->  " +
                        part2.Label + "." + p2.Name + " [" + TextUtil.NullText(p2.EndCondition) + ", " + TextUtil.NullText(p2.NominalDiameter) + "]" +
                        "  =  " +
                        (valid ? "VALID" : "INVALID"));
                }
            }

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\nResult:");

            if (anyValid)
            {
                ReportWriter.Write(ed, log, "\n  At least one valid port pairing was found between the selected parts.");
            }
            else if (!anySameSize)
            {
                ReportWriter.Write(ed, log, "\n  No valid connection found. The selected ports also do not appear to have matching nominal diameters.");
            }
            else
            {
                ReportWriter.Write(ed, log, "\n  No valid connection found, even though at least one nominal diameter appears to match.");
            }

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\nExpected mating ends from connector configuration:");

            foreach (PortInfo port in part1.Ports)
            {
                WriteExpectedMate(ed, log, connectorConfig, part1.Label, port);
            }

            foreach (PortInfo port in part2.Ports)
            {
                WriteExpectedMate(ed, log, connectorConfig, part2.Label, port);
            }

            WritePipeSpecificRecommendation(ed, log, connectorConfig, part1, part2);
        }

        private static void WriteConnectorConfigSummary(
            Editor ed,
            List<string> log,
            ConnectorConfigResult connectorConfig)
        {
            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n----------------------------------------------------");
            ReportWriter.Write(ed, log, "\nCONNECTOR CONFIGURATION");
            ReportWriter.Write(ed, log, "\n----------------------------------------------------");

            if (connectorConfig == null)
            {
                ReportWriter.Write(ed, log, "\nConnector config result was null. Fallback rules will be used.");
                return;
            }

            ReportWriter.Write(ed, log, "\nPath: " + TextUtil.NullText(connectorConfig.ConfigPath));
            ReportWriter.Write(ed, log, "\nLoaded joint rules: " + connectorConfig.Rules.Count);

            if (connectorConfig.Errors.Count > 0)
            {
                ReportWriter.Write(ed, log, "\nErrors / Warnings:");

                foreach (string error in connectorConfig.Errors)
                {
                    ReportWriter.Write(ed, log, "\n  " + error);
                }

                ReportWriter.Write(ed, log, "\nFallback rules will be used where connector rules are unavailable.");
            }
        }

        private static void WriteExpectedMate(
            Editor ed,
            List<string> log,
            ConnectorConfigResult connectorConfig,
            string partLabel,
            PortInfo port)
        {
            string expected =
                ConnectorConnectionAnalyzer.FormatExpectedMatingEnds(
                    connectorConfig,
                    port.EndCondition);

            ReportWriter.Write(ed, log,
                "\n  " + partLabel + "." + port.Name +
                " [" + TextUtil.NullText(port.EndCondition) + "] expects: " +
                expected);

            ReportWriter.Write(ed, log,
                "\n    Rule source: " +
                ConnectorConnectionAnalyzer.GetConnectorRulesForEndSummary(
                    connectorConfig,
                    port.EndCondition));
        }

        private static void WritePipeSpecificRecommendation(
            Editor ed,
            List<string> log,
            ConnectorConfigResult connectorConfig,
            PartInfo part1,
            PartInfo part2)
        {
            PartInfo pipe = null;
            PartInfo other = null;

            if (IsPipe(part1) && !IsPipe(part2))
            {
                pipe = part1;
                other = part2;
            }
            else if (IsPipe(part2) && !IsPipe(part1))
            {
                pipe = part2;
                other = part1;
            }

            if (pipe == null || other == null)
            {
                return;
            }

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\nPipe selection check:");

            foreach (PortInfo otherPort in other.Ports)
            {
                List<string> expectedPipeEnds =
                    ConnectorConnectionAnalyzer.GetExpectedPipeEndsForComponentPort(
                        connectorConfig,
                        otherPort.EndCondition);

                if (expectedPipeEnds.Count == 0)
                {
                    continue;
                }

                string primaryExpectedPipeEnd =
                    ConnectorConnectionAnalyzer.GetPrimaryExpectedPipeEndForComponentPort(
                        connectorConfig,
                        otherPort.EndCondition);

                bool pipeHasAllowedEnd =
                    pipe.Ports.Any(p =>
                        TextUtil.SameNominalDiameter(p.NominalDiameter, otherPort.NominalDiameter) &&
                        expectedPipeEnds.Any(expected => TextUtil.SameText(p.EndCondition, expected)));

                bool pipeHasDifferentEndSameSize =
                    pipe.Ports.Any(p =>
                        TextUtil.SameNominalDiameter(p.NominalDiameter, otherPort.NominalDiameter) &&
                        !expectedPipeEnds.Any(expected => TextUtil.SameText(p.EndCondition, expected)));

                ReportWriter.Write(ed, log,
                    "\n  Component port " + otherPort.Name +
                    " [" + otherPort.EndCondition + ", " + otherPort.NominalDiameter + "]" +
                    " allows pipe end(s): " + string.Join(", ", expectedPipeEnds));

                if (pipeHasAllowedEnd)
                {
                    ReportWriter.Write(ed, log, "\n    Selected pipe has an allowed end condition.");
                }
                else if (pipeHasDifferentEndSameSize)
                {
                    ReportWriter.Write(ed, log, "\n    Selected pipe does NOT have an allowed end condition.");
                    ReportWriter.Write(ed, log, "\n    Selected pipe: " + TextUtil.NullText(pipe.PartFamilyLongDesc));
                    ReportWriter.Write(ed, log, "\n    Recommendation: choose a pipe with EndCondition = " + primaryExpectedPipeEnd);
                }

                WriteSpecPipeCandidates(ed, log, other, pipe, otherPort, primaryExpectedPipeEnd);
            }
        }

        private static void WriteSpecPipeCandidates(
            Editor ed,
            List<string> log,
            PartInfo component,
            PartInfo selectedPipe,
            PortInfo componentPort,
            string expectedPipeEnd)
        {
            string specName = component.Spec;

            if (string.IsNullOrWhiteSpace(specName))
            {
                specName = selectedPipe.Spec;
            }

            SpecCandidateResult result =
                SpecPipeCandidateFinder.FindPipeCandidates(
                    specName,
                    componentPort.NominalDiameter,
                    expectedPipeEnd);

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\nSpec pipe candidates:");
            ReportWriter.Write(ed, log, "\n  Spec: " + TextUtil.NullText(result.SpecName));
            ReportWriter.Write(ed, log, "\n  PSPC: " + TextUtil.NullText(result.PspcPath));
            ReportWriter.Write(ed, log, "\n  PSPX: " + TextUtil.NullText(result.PspxPath));

            if (result.Errors.Count > 0)
            {
                ReportWriter.Write(ed, log, "\n  Errors / Warnings:");

                foreach (string error in result.Errors)
                {
                    ReportWriter.Write(ed, log, "\n    " + error);
                }
            }

            if (result.Candidates.Count == 0)
            {
                ReportWriter.Write(ed, log, "\n  No pipe candidates found in the spec database.");
                return;
            }

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n  Available pipe candidates for " + componentPort.NominalDiameter + ":");

            foreach (PipeCandidate candidate in result.Candidates)
            {
                bool matchesExpected = candidate.MatchesExpectedEnd(expectedPipeEnd);
                bool isSelectedPipe =
                    TextUtil.SameText(candidate.PartFamilyLongDesc, selectedPipe.PartFamilyLongDesc) ||
                    TextUtil.SameText(candidate.PartSizeLongDesc, selectedPipe.PartSizeLongDesc) ||
                    TextUtil.SameText(candidate.ItemCode, selectedPipe.ItemCode);

                ReportWriter.Write(ed, log, "\n");
                ReportWriter.Write(ed, log, "  " + FormatPriority(candidate.Priority) + " " + candidate.PartFamilyLongDesc);

                if (isSelectedPipe)
                {
                    ReportWriter.Write(ed, log, "  <-- SELECTED PIPE");
                }

                ReportWriter.Write(ed, log, "\n    PnPID: " + candidate.PnPID);
                ReportWriter.Write(ed, log, "\n    FamilyId: " + TextUtil.NullText(candidate.PartFamilyId));
                ReportWriter.Write(ed, log, "\n    EndConditions: " + string.Join(", ", candidate.EndConditions));
                ReportWriter.Write(ed, log, "\n    PortDetails: " + TextUtil.NullText(candidate.PortDetails));
                ReportWriter.Write(ed, log, "\n    Schedule: " + TextUtil.NullText(candidate.Schedule));
                ReportWriter.Write(ed, log, "\n    Match for expected " + expectedPipeEnd + ": " + (matchesExpected ? "YES" : "NO"));
            }

            PipeCandidate recommended =
                result.Candidates.FirstOrDefault(c => c.MatchesExpectedEnd(expectedPipeEnd));

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n  Recommended spec candidate:");

            if (recommended == null)
            {
                ReportWriter.Write(ed, log, "\n    No pipe candidate with EndCondition = " + expectedPipeEnd + " was found.");
            }
            else
            {
                ReportWriter.Write(ed, log, "\n    " + recommended.PartFamilyLongDesc);
                ReportWriter.Write(ed, log, "\n    EndConditions: " + string.Join(", ", recommended.EndConditions));
                ReportWriter.Write(ed, log, "\n    Priority: " + (recommended.Priority.HasValue ? recommended.Priority.Value.ToString() : "<not listed>"));
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

        private static bool IsPipe(PartInfo part)
        {
            return TextUtil.SameText(part.PnPClassName, "Pipe") ||
                   TextUtil.Contains(part.PartFamilyLongDesc, "PIPE") ||
                   TextUtil.Contains(part.ShortDescription, "PIPE");
        }
    }
}
