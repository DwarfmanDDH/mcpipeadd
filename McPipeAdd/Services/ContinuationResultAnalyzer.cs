using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.EditorInput;

namespace McPipeAdd
{
    public static class ContinuationResultAnalyzer
    {
        public static void WriteContinuationAnalysis(
            Editor ed,
            List<string> log,
            PartInfo part1,
            PartInfo part2)
        {
            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n----------------------------------------------------");
            ReportWriter.Write(ed, log, "\nCONTINUATION ANALYSIS");
            ReportWriter.Write(ed, log, "\n----------------------------------------------------");

            if (part1 == null)
            {
                ReportWriter.Write(ed, log, "\nNo part was provided.");
                return;
            }

            if (part2 == null)
            {
                WriteStartOnlyAnalysis(ed, log, part1);
                return;
            }

            WriteTwoPartResultAnalysis(ed, log, part1, part2);
        }

        private static void WriteStartOnlyAnalysis(Editor ed, List<string> log, PartInfo startPart)
        {
            ReportWriter.Write(ed, log, "\nMode: START-ONLY CONTINUATION PRE-CHECK");
            ReportWriter.Write(ed, log, "\nUse this mode before or without inserting anything with the Plant continuation grip.");
            ReportWriter.Write(ed, log, "\nSelected part: " + TextUtil.NullText(startPart.PartFamilyLongDesc));
            ReportWriter.Write(ed, log, "\nSpec: " + TextUtil.NullText(startPart.Spec));

            if (startPart.Ports == null || startPart.Ports.Count == 0)
            {
                ReportWriter.Write(ed, log, "\nThe selected part has no readable ports.");
                return;
            }

            foreach (PortInfo port in startPart.Ports)
            {
                WritePortExpectation(ed, log, startPart, port, true);
            }
        }

        private static void WriteTwoPartResultAnalysis(
            Editor ed,
            List<string> log,
            PartInfo part1,
            PartInfo part2)
        {
            ReportWriter.Write(ed, log, "\nMode: TWO-PART CONTINUATION RESULT CHECK");
            ReportWriter.Write(ed, log, "\nSelected parts are checked by closest port position. Selection order does not matter.");

            PortPairSelection selection = SelectClosestPortPair(part1, part2);

            PortInfo part1Port = null;
            PortInfo part2Port = null;

            if (selection.Success)
            {
                part1Port = selection.Part1Port;
                part2Port = selection.Part2Port;

                ReportWriter.Write(ed, log, "\n");
                ReportWriter.Write(ed, log, "\nPort selection method: AUTO - closest port pair by 3D position");
                ReportWriter.Write(ed, log, "\nClosest distance: " + selection.Distance.ToString("0.###"));
                ReportWriter.Write(ed, log, "\nSelected Part 1 port: " + part1Port.Name + " " + part1Port.FormatPosition());
                ReportWriter.Write(ed, log, "\nSelected Part 2 port: " + part2Port.Name + " " + part2Port.FormatPosition());
            }
            else
            {
                ReportWriter.Write(ed, log, "\n");
                ReportWriter.Write(ed, log, "\nPort selection method: MANUAL FALLBACK");
                ReportWriter.Write(ed, log, "\nReason: " + selection.Error);
                ReportWriter.Write(ed, log, "\nThe command could not read enough port positions to choose automatically.");

                part1Port = AskForPort(ed, log, "Selected Part 1", part1);

                if (part1Port == null)
                {
                    return;
                }

                part2Port = AskForPort(ed, log, "Selected Part 2", part2);

                if (part2Port == null)
                {
                    return;
                }
            }

            WriteSelectedPortPair(ed, log, part1, part1Port, part2, part2Port);

            ConnectionCheck connectionCheck = CheckPortConnection(part1Port, part2Port);

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\nAUTO-DETECTED CONNECTION RESULT:");
            ReportWriter.Write(ed, log, "\n  " + connectionCheck.StatusText);

            if (!string.IsNullOrWhiteSpace(connectionCheck.Detail))
            {
                ReportWriter.Write(ed, log, "\n  " + connectionCheck.Detail);
            }

            WriteFlangeContinuationInterpretation(
                ed,
                log,
                part1,
                part1Port,
                part2,
                part2Port,
                connectionCheck);

            WritePipeCandidateFollowUp(
                ed,
                log,
                part1,
                part1Port,
                part2,
                part2Port,
                connectionCheck);
        }

        private static void WriteSelectedPortPair(
            Editor ed,
            List<string> log,
            PartInfo part1,
            PortInfo part1Port,
            PartInfo part2,
            PortInfo part2Port)
        {
            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\nAUTO-DETECTED / SELECTED CONNECTION:");
            ReportWriter.Write(ed, log,
                "\n  Selected Part 1 " + part1.Label + "." + part1Port.Name +
                " [" + TextUtil.NullText(part1Port.EndCondition) +
                ", " + TextUtil.NullText(part1Port.NominalDiameter) +
                "] at " + part1Port.FormatPosition());

            ReportWriter.Write(ed, log,
                "\n  Selected Part 2 " + part2.Label + "." + part2Port.Name +
                " [" + TextUtil.NullText(part2Port.EndCondition) +
                ", " + TextUtil.NullText(part2Port.NominalDiameter) +
                "] at " + part2Port.FormatPosition());
        }

        private static void WriteFlangeContinuationInterpretation(
            Editor ed,
            List<string> log,
            PartInfo part1,
            PortInfo part1Port,
            PartInfo part2,
            PortInfo part2Port,
            ConnectionCheck connectionCheck)
        {
            bool part1IsFlangeFace = TextUtil.SameText(part1Port.EndCondition, "FL");
            bool part2IsFlangeFace = TextUtil.SameText(part2Port.EndCondition, "FL");

            if (!part1IsFlangeFace && !part2IsFlangeFace)
            {
                return;
            }

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\nRF/FL CONTINUATION INTERPRETATION:");

            if (part1IsFlangeFace && part2IsFlangeFace)
            {
                ReportWriter.Write(ed, log, "\n  Correct first-step behavior detected.");
                ReportWriter.Write(ed, log, "\n  Plant inserted or connected a mating flanged component at the RF/FL face.");
                ReportWriter.Write(ed, log, "\n  Next step should be pipe continuation from the inserted flange's pipe-side port, not from the RF face.");

                WriteOtherPortPipeContinuationGuidance(ed, log, "Selected Part 1 remaining continuation port(s)", part1, part1Port);
                WriteOtherPortPipeContinuationGuidance(ed, log, "Selected Part 2 remaining continuation port(s)", part2, part2Port);
                return;
            }

            ReportWriter.Write(ed, log, "\n  Invalid RF/FL continuation behavior detected.");
            ReportWriter.Write(ed, log, "\n  A flange RF/FL face should not directly receive a non-FL pipe/fitting end.");
            ReportWriter.Write(ed, log, "\n  Expected behavior: Plant should insert a mating flange/flanged component first.");
            ReportWriter.Write(ed, log, "\n  After that, pipe should continue from the new flange's pipe-side port.");

            if (part1IsFlangeFace)
            {
                SpecAutoFlangeDiagnostic.WriteAutoFlangeCandidateCheck(ed, log, part1, part1Port);
            }

            if (part2IsFlangeFace)
            {
                SpecAutoFlangeDiagnostic.WriteAutoFlangeCandidateCheck(ed, log, part2, part2Port);
            }
        }

        private static void WritePipeCandidateFollowUp(
            Editor ed,
            List<string> log,
            PartInfo part1,
            PortInfo part1Port,
            PartInfo part2,
            PortInfo part2Port,
            ConnectionCheck connectionCheck)
        {
            if (TextUtil.SameText(part1Port.EndCondition, "FL") ||
                TextUtil.SameText(part2Port.EndCondition, "FL"))
            {
                return;
            }

            List<string> expectedPart2PipeEnds =
                GetExpectedPipeEndsForContinuationPort(part1Port.EndCondition);

            if (expectedPart2PipeEnds.Count > 0 && IsPipe(part2))
            {
                WriteAvailablePipeCandidates(
                    ed,
                    log,
                    "Pipe candidate check from Selected Part 1 continuation port",
                    GetSpecName(part1, part2),
                    part1Port.NominalDiameter,
                    expectedPart2PipeEnds,
                    part2);
            }

            List<string> expectedPart1PipeEnds =
                GetExpectedPipeEndsForContinuationPort(part2Port.EndCondition);

            if (expectedPart1PipeEnds.Count > 0 && IsPipe(part1))
            {
                WriteAvailablePipeCandidates(
                    ed,
                    log,
                    "Pipe candidate check from Selected Part 2 continuation port",
                    GetSpecName(part2, part1),
                    part2Port.NominalDiameter,
                    expectedPart1PipeEnds,
                    part1);
            }
        }

        private static void WriteOtherPortPipeContinuationGuidance(
            Editor ed,
            List<string> log,
            string title,
            PartInfo part,
            PortInfo alreadyConnectedPort)
        {
            if (part == null || part.Ports == null)
            {
                return;
            }

            List<PortInfo> remainingPorts = part.Ports
                .Where(p => !TextUtil.SameText(p.Name, alreadyConnectedPort.Name))
                .ToList();

            if (remainingPorts.Count == 0)
            {
                return;
            }

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n  " + title + ":");

            foreach (PortInfo port in remainingPorts)
            {
                WritePortExpectation(ed, log, part, port, false);
            }
        }

        private static void WritePortExpectation(
            Editor ed,
            List<string> log,
            PartInfo part,
            PortInfo port,
            bool includeCandidateSearch)
        {
            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log,
                "  " + part.Label + "." + TextUtil.NullText(port.Name) +
                " [" + TextUtil.NullText(port.EndCondition) +
                ", " + TextUtil.NullText(port.NominalDiameter) +
                "] at " + port.FormatPosition());

            if (TextUtil.SameText(port.EndCondition, "FL"))
            {
                ReportWriter.Write(ed, log, "\n    Direct pipe continuation: NO");
                ReportWriter.Write(ed, log, "\n    Expected next component: mating FL/RF flange face with gasket/bolt behavior.");
                ReportWriter.Write(ed, log, "\n    After the mating flange is inserted, continue pipe from that flange's pipe-side port.");

                if (includeCandidateSearch)
                {
                    SpecAutoFlangeDiagnostic.WriteAutoFlangeCandidateCheck(ed, log, part, port);
                }

                return;
            }

            List<string> expectedPipeEnds =
                GetExpectedPipeEndsForContinuationPort(port.EndCondition);

            if (expectedPipeEnds.Count == 0)
            {
                ReportWriter.Write(ed, log, "\n    Direct pipe continuation: UNKNOWN");
                ReportWriter.Write(ed, log, "\n    No direct pipe-end rule is currently coded for this end condition.");
                return;
            }

            ReportWriter.Write(ed, log, "\n    Direct pipe continuation: YES");
            ReportWriter.Write(ed, log, "\n    Expected pipe end(s): " + string.Join(", ", expectedPipeEnds));

            if (includeCandidateSearch)
            {
                WriteAvailablePipeCandidates(
                    ed,
                    log,
                    "Available pipe candidates for " + part.Label + "." + port.Name,
                    part.Spec,
                    port.NominalDiameter,
                    expectedPipeEnds,
                    null);
            }
        }

        private static void WriteAvailablePipeCandidates(
            Editor ed,
            List<string> log,
            string title,
            string specName,
            string nominalDiameter,
            List<string> expectedPipeEnds,
            PartInfo insertedPipe)
        {
            if (expectedPipeEnds == null || expectedPipeEnds.Count == 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(specName))
            {
                ReportWriter.Write(ed, log, "\n    Cannot inspect pipe candidates because the spec name is blank.");
                return;
            }

            SpecCandidateResult result =
                SpecPipeCandidateFinder.FindPipeCandidates(
                    specName,
                    nominalDiameter,
                    expectedPipeEnds[0]);

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "    " + title + ":");
            ReportWriter.Write(ed, log, "\n      Spec: " + TextUtil.NullText(result.SpecName));
            ReportWriter.Write(ed, log, "\n      PSPC: " + TextUtil.NullText(result.PspcPath));
            ReportWriter.Write(ed, log, "\n      PSPX: " + TextUtil.NullText(result.PspxPath));

            if (result.Errors.Count > 0)
            {
                ReportWriter.Write(ed, log, "\n      Errors / Warnings:");

                foreach (string error in result.Errors)
                {
                    ReportWriter.Write(ed, log, "\n        " + error);
                }
            }

            if (result.Candidates.Count == 0)
            {
                ReportWriter.Write(ed, log, "\n      No pipe candidates found for this size.");
                return;
            }

            PipeCandidate firstBySpecOrder = result.Candidates.FirstOrDefault();

            PipeCandidate firstMatchingExpected =
                result.Candidates.FirstOrDefault(c => CandidateMatchesAnyExpectedEnd(c, expectedPipeEnds));

            PipeCandidate matchingInsertedPipe = null;

            if (insertedPipe != null)
            {
                matchingInsertedPipe =
                    result.Candidates.FirstOrDefault(c => IsSamePipeCandidate(c, insertedPipe));
            }

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n      First pipe by spec order:");
            WriteCandidateSummary(ed, log, firstBySpecOrder, insertedPipe, expectedPipeEnds);

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n      First pipe matching expected end:");
            WriteCandidateSummary(ed, log, firstMatchingExpected, insertedPipe, expectedPipeEnds);

            if (matchingInsertedPipe != null)
            {
                ReportWriter.Write(ed, log, "\n");
                ReportWriter.Write(ed, log, "\n      Inserted pipe matched in spec list:");
                WriteCandidateSummary(ed, log, matchingInsertedPipe, insertedPipe, expectedPipeEnds);
            }

            if (firstBySpecOrder != null &&
                firstMatchingExpected != null &&
                firstBySpecOrder.PnPID != firstMatchingExpected.PnPID)
            {
                ReportWriter.Write(ed, log, "\n");
                ReportWriter.Write(ed, log, "\n      SPEC ORDER RISK:");
                ReportWriter.Write(ed, log, "\n        The first pipe by spec order is not the first pipe matching the expected end condition.");
            }
        }

        private static PortInfo AskForPort(Editor ed, List<string> log, string label, PartInfo part)
        {
            if (part == null || part.Ports == null || part.Ports.Count == 0)
            {
                ReportWriter.Write(ed, log, "\n" + label + " has no readable ports.");
                return null;
            }

            ReportWriter.Write(ed, log, "\nReadable ports on " + label + ":");

            foreach (PortInfo port in part.Ports)
            {
                ReportWriter.Write(ed, log,
                    "\n  " + TextUtil.NullText(port.Name) +
                    " | EndCondition: " + TextUtil.NullText(port.EndCondition) +
                    " | NominalDiameter: " + TextUtil.NullText(port.NominalDiameter) +
                    " | Position: " + port.FormatPosition());
            }

            PromptStringOptions portOptions = new PromptStringOptions(
                "\nEnter port name for " + label + ", for example S1 or S2: ");

            portOptions.AllowSpaces = false;

            PromptResult portResult = ed.GetString(portOptions);

            if (portResult.Status != PromptStatus.OK)
            {
                ReportWriter.Write(ed, log, "\nNo port entered for " + label + ".");
                return null;
            }

            string portName = portResult.StringResult;

            PortInfo selectedPort = part.Ports
                .FirstOrDefault(p => TextUtil.SameText(p.Name, portName));

            if (selectedPort == null)
            {
                ReportWriter.Write(ed, log, "\nCould not find port " + TextUtil.NullText(portName) + " on " + label + ".");
                return null;
            }

            return selectedPort;
        }

        private static PortPairSelection SelectClosestPortPair(PartInfo part1, PartInfo part2)
        {
            PortPairSelection result = new PortPairSelection();

            if (part1 == null || part2 == null)
            {
                result.Error = "Missing one or both selected parts.";
                return result;
            }

            if (part1.Ports == null || part2.Ports == null)
            {
                result.Error = "Missing port list on one or both selected parts.";
                return result;
            }

            List<PortInfo> part1PortsWithPosition =
                part1.Ports.Where(p => p.HasPosition).ToList();

            List<PortInfo> part2PortsWithPosition =
                part2.Ports.Where(p => p.HasPosition).ToList();

            if (part1PortsWithPosition.Count == 0)
            {
                result.Error = "No readable 3D port positions were found on Selected Part 1.";
                return result;
            }

            if (part2PortsWithPosition.Count == 0)
            {
                result.Error = "No readable 3D port positions were found on Selected Part 2.";
                return result;
            }

            double bestDistance = double.MaxValue;
            PortInfo bestPart1Port = null;
            PortInfo bestPart2Port = null;

            foreach (PortInfo part1Port in part1PortsWithPosition)
            {
                foreach (PortInfo part2Port in part2PortsWithPosition)
                {
                    double distance = part1Port.DistanceTo(part2Port);

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestPart1Port = part1Port;
                        bestPart2Port = part2Port;
                    }
                }
            }

            if (bestPart1Port == null || bestPart2Port == null)
            {
                result.Error = "Could not determine a closest port pair.";
                return result;
            }

            result.Success = true;
            result.Part1Port = bestPart1Port;
            result.Part2Port = bestPart2Port;
            result.Distance = bestDistance;

            return result;
        }

        private static ConnectionCheck CheckPortConnection(PortInfo part1Port, PortInfo part2Port)
        {
            ConnectionCheck result = new ConnectionCheck();

            if (part1Port == null || part2Port == null)
            {
                result.StatusText = "INVALID";
                result.Detail = "Missing one or both ports.";
                return result;
            }

            bool sameSize =
                TextUtil.SameNominalDiameter(part1Port.NominalDiameter, part2Port.NominalDiameter);

            if (!sameSize)
            {
                result.StatusText = "INVALID";
                result.Detail = "The closest ports are not the same nominal size.";
                return result;
            }

            string a = TextUtil.Clean(part1Port.EndCondition);
            string b = TextUtil.Clean(part2Port.EndCondition);

            if (Pair(a, b, "FL", "FL"))
            {
                result.IsValid = true;
                result.StatusText = "VALID - FL/RF flange face to FL/RF flange face.";
                result.Detail = "This matches the expected first step when continuing from an RF flange face.";
                return result;
            }

            if (Pair(a, b, "THDM", "THDF"))
            {
                result.IsValid = true;
                result.StatusText = "VALID - threaded male to threaded female.";
                return result;
            }

            if (Pair(a, b, "PL", "SW"))
            {
                result.IsValid = true;
                result.StatusText = "VALID - plain end to socket weld.";
                return result;
            }

            if ((a == "BV" || a == "PL") &&
                (b == "BV" || b == "PL"))
            {
                result.IsValid = true;
                result.StatusText = "VALID - buttweld-compatible pipe-side connection.";
                return result;
            }

            result.StatusText = "INVALID - " + TextUtil.NullText(a) + " cannot directly connect to " + TextUtil.NullText(b) + ".";
            result.Detail = GetInvalidConnectionDetail(a, b);

            return result;
        }

        private static string GetInvalidConnectionDetail(string a, string b)
        {
            if ((a == "FL" && b != "FL") ||
                (b == "FL" && a != "FL"))
            {
                return "A flange RF/FL face expects another FL face, not a direct pipe end.";
            }

            if ((a == "THDF" && b != "THDM") ||
                (b == "THDF" && a != "THDM"))
            {
                return "A threaded female end expects THDM.";
            }

            return "No valid connector rule is currently coded for this end-condition pair.";
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

        private static void WriteCandidateSummary(
            Editor ed,
            List<string> log,
            PipeCandidate candidate,
            PartInfo insertedPipe,
            List<string> expectedPipeEnds)
        {
            if (candidate == null)
            {
                ReportWriter.Write(ed, log, "\n        <none>");
                return;
            }

            bool isInserted = IsSamePipeCandidate(candidate, insertedPipe);
            bool matchesExpected = CandidateMatchesAnyExpectedEnd(candidate, expectedPipeEnds);

            ReportWriter.Write(ed, log, "\n        " + FormatPriority(candidate.Priority) + " " + TextUtil.NullText(candidate.PartFamilyLongDesc));

            if (isInserted)
            {
                ReportWriter.Write(ed, log, "  <-- INSERTED PIPE");
            }

            ReportWriter.Write(ed, log, "\n          PnPID: " + candidate.PnPID);
            ReportWriter.Write(ed, log, "\n          FamilyId: " + TextUtil.NullText(candidate.PartFamilyId));
            ReportWriter.Write(ed, log, "\n          EndConditions: " + FormatEndConditions(candidate.EndConditions));
            ReportWriter.Write(ed, log, "\n          PortDetails: " + TextUtil.NullText(candidate.PortDetails));
            ReportWriter.Write(ed, log, "\n          Match for expected continuation end(s): " + (matchesExpected ? "YES" : "NO"));
        }

        private static bool IsSamePipeCandidate(PipeCandidate candidate, PartInfo pipe)
        {
            if (candidate == null || pipe == null)
            {
                return false;
            }

            return TextUtil.SameText(candidate.PartFamilyLongDesc, pipe.PartFamilyLongDesc) ||
                   TextUtil.SameText(candidate.PartSizeLongDesc, pipe.PartSizeLongDesc) ||
                   TextUtil.SameText(candidate.ItemCode, pipe.ItemCode);
        }

        private static string GetSpecName(PartInfo startPart, PartInfo pipe)
        {
            if (startPart != null && !string.IsNullOrWhiteSpace(startPart.Spec))
            {
                return startPart.Spec;
            }

            if (pipe != null && !string.IsNullOrWhiteSpace(pipe.Spec))
            {
                return pipe.Spec;
            }

            return string.Empty;
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

        private static bool IsPipe(PartInfo part)
        {
            if (part == null)
            {
                return false;
            }

            return TextUtil.SameText(part.PnPClassName, "Pipe") ||
                   TextUtil.Contains(part.PartFamilyLongDesc, "PIPE") ||
                   TextUtil.Contains(part.ShortDescription, "PIPE");
        }

        private static bool Pair(string a, string b, string x, string y)
        {
            return (a == x && b == y) || (a == y && b == x);
        }

        private class PortPairSelection
        {
            public bool Success = false;
            public PortInfo Part1Port = null;
            public PortInfo Part2Port = null;
            public double Distance = 0.0;
            public string Error = string.Empty;
        }

        private class ConnectionCheck
        {
            public bool IsValid = false;
            public string StatusText = string.Empty;
            public string Detail = string.Empty;
        }
    }
}
