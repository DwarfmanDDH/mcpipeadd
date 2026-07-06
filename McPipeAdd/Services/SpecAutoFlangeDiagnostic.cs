using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Autodesk.AutoCAD.EditorInput;

using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace McPipeAdd
{
    public static class SpecAutoFlangeDiagnostic
    {
        private static bool _sqliteInitialized = false;

        public static void WriteAutoFlangeCandidateCheck(
            Editor ed,
            List<string> log,
            PartInfo startPart,
            PortInfo flangePort)
        {
            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\nAUTOFLANGE CANDIDATE CHECK");

            if (startPart == null || flangePort == null)
            {
                ReportWriter.Write(ed, log, "\n  Cannot run AutoFlange check because the part or port is missing.");
                return;
            }

            if (!TextUtil.SameText(flangePort.EndCondition, "FL"))
            {
                ReportWriter.Write(ed, log, "\n  Skipped because selected port is not FL.");
                return;
            }

            string specName = startPart.Spec;

            if (string.IsNullOrWhiteSpace(specName))
            {
                ReportWriter.Write(ed, log, "\n  Cannot run AutoFlange check because the selected part has a blank spec.");
                return;
            }

            double nominalDiameter;

            if (!TryParseNominalDiameter(flangePort.NominalDiameter, out nominalDiameter))
            {
                nominalDiameter = 0.0;

                if (!TryParseNominalDiameter(startPart.NominalDiameter, out nominalDiameter))
                {
                    ReportWriter.Write(ed, log, "\n  Cannot parse nominal diameter: " + TextUtil.NullText(flangePort.NominalDiameter));
                    return;
                }
            }

            string expectedFacing = FirstNonBlank(flangePort.Facing, startPart.DataLinksFacing);
            string expectedPressureClass = FirstNonBlank(flangePort.PressureClass, startPart.PressureClass);

            ReportWriter.Write(ed, log, "\n  Start part: " + TextUtil.NullText(startPart.PartFamilyLongDesc));
            ReportWriter.Write(ed, log, "\n  Start port: " + TextUtil.NullText(flangePort.Name));
            ReportWriter.Write(ed, log, "\n  Required end condition: FL");
            ReportWriter.Write(ed, log, "\n  Required nominal diameter: " + TextUtil.NullText(flangePort.NominalDiameter));
            ReportWriter.Write(ed, log, "\n  Required facing: " + TextUtil.NullText(expectedFacing));
            ReportWriter.Write(ed, log, "\n  Required pressure class: " + TextUtil.NullText(expectedPressureClass));

            SpecCandidateResult specFiles = SpecPathResolver.ResolveSpecFiles(specName);

            ReportWriter.Write(ed, log, "\n  Spec: " + TextUtil.NullText(specFiles.SpecName));
            ReportWriter.Write(ed, log, "\n  PSPC: " + TextUtil.NullText(specFiles.PspcPath));
            ReportWriter.Write(ed, log, "\n  PSPX: " + TextUtil.NullText(specFiles.PspxPath));

            if (specFiles.Errors.Count > 0)
            {
                ReportWriter.Write(ed, log, "\n  Spec path warnings:");

                foreach (string error in specFiles.Errors)
                {
                    ReportWriter.Write(ed, log, "\n    " + error);
                }
            }

            if (string.IsNullOrWhiteSpace(specFiles.PspcPath))
            {
                return;
            }

            try
            {
                InitializeSqlite();

                using (SqliteConnection connection = OpenReadOnlyConnection(specFiles.PspcPath))
                {
                    List<SpecPartCandidate> flangeCandidates =
                        ReadCandidates(connection, "Flange", nominalDiameter);

                    List<SpecPartCandidate> gasketCandidates =
                        ReadCandidates(connection, "Gasket", nominalDiameter);

                    List<SpecPartCandidate> boltCandidates =
                        ReadCandidates(connection, "BoltSet", nominalDiameter);

                    WriteFlangeCandidates(ed, log, flangeCandidates, expectedFacing, expectedPressureClass);
                    WriteHardwareCandidates(ed, log, "GASKET CANDIDATES", gasketCandidates, expectedFacing, expectedPressureClass, false);
                    WriteHardwareCandidates(ed, log, "BOLT SET CANDIDATES", boltCandidates, expectedFacing, expectedPressureClass, true);
                }
            }
            catch (Exception ex)
            {
                ReportWriter.Write(ed, log, "\n  AutoFlange candidate check failed: " + ex.Message);
            }
        }

        private static void WriteFlangeCandidates(
            Editor ed,
            List<string> log,
            List<SpecPartCandidate> candidates,
            string expectedFacing,
            string expectedPressureClass)
        {
            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n  MATCHING FLANGE CANDIDATES IN SPEC:");

            if (candidates.Count == 0)
            {
                ReportWriter.Write(ed, log, "\n    No flange candidates found at this nominal diameter.");
                return;
            }

            int exactAutoFlangeMatches = 0;
            int normalizedAutoFlangeMatches = 0;

            foreach (SpecPartCandidate candidate in candidates.Take(20))
            {
                bool endMatches = TextUtil.SameText(candidate.EndType, "FL");
                bool facingMatches = string.IsNullOrWhiteSpace(expectedFacing) ||
                                     TextUtil.SameText(candidate.Facing, expectedFacing);
                bool pressureExact = string.IsNullOrWhiteSpace(expectedPressureClass) ||
                                     TextUtil.SameText(candidate.PressureClass, expectedPressureClass);
                bool pressureNormalized = string.IsNullOrWhiteSpace(expectedPressureClass) ||
                                          SamePressureClassNormalized(candidate.PressureClass, expectedPressureClass);
                bool hasPipeSidePort = CandidateHasPipeSidePort(candidate);

                bool exactAutoFlangeMatch =
                    endMatches &&
                    facingMatches &&
                    pressureExact &&
                    hasPipeSidePort;

                bool normalizedAutoFlangeMatch =
                    endMatches &&
                    facingMatches &&
                    pressureNormalized &&
                    hasPipeSidePort;

                if (exactAutoFlangeMatch)
                {
                    exactAutoFlangeMatches++;
                }

                if (normalizedAutoFlangeMatch)
                {
                    normalizedAutoFlangeMatches++;
                }

                ReportWriter.Write(ed, log, "\n");
                ReportWriter.Write(ed, log, "\n    " + candidate.PartFamilyLongDesc);
                ReportWriter.Write(ed, log, "\n      PnPID: " + candidate.PnPID);
                ReportWriter.Write(ed, log, "\n      SizeDesc: " + TextUtil.NullText(candidate.PartSizeLongDesc));
                ReportWriter.Write(ed, log, "\n      EndType: " + TextUtil.NullText(candidate.EndType));
                ReportWriter.Write(ed, log, "\n      Facing: " + TextUtil.NullText(candidate.Facing));
                ReportWriter.Write(ed, log, "\n      PressureClass: " + TextUtil.NullText(candidate.PressureClass));
                ReportWriter.Write(ed, log, "\n      PortDetails: " + TextUtil.NullText(candidate.PortDetails));
                ReportWriter.Write(ed, log, "\n      Has pipe-side port BV/PL/Universal_ET: " + YesNo(hasPipeSidePort));
                ReportWriter.Write(ed, log, "\n      EndType FL match: " + YesNo(endMatches));
                ReportWriter.Write(ed, log, "\n      Facing match: " + YesNo(facingMatches));
                ReportWriter.Write(ed, log, "\n      Pressure exact match: " + YesNo(pressureExact));
                ReportWriter.Write(ed, log, "\n      Pressure normalized match: " + YesNo(pressureNormalized));
                ReportWriter.Write(ed, log, "\n      AutoFlange candidate match using exact pressure: " + YesNo(exactAutoFlangeMatch));
                ReportWriter.Write(ed, log, "\n      AutoFlange candidate match ignoring #/LB pressure formatting: " + YesNo(normalizedAutoFlangeMatch));
            }

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n    Summary:");
            ReportWriter.Write(ed, log, "\n      Total flange candidates at size: " + candidates.Count);
            ReportWriter.Write(ed, log, "\n      Exact AutoFlange-style matches: " + exactAutoFlangeMatches);
            ReportWriter.Write(ed, log, "\n      Normalized pressure AutoFlange-style matches: " + normalizedAutoFlangeMatches);
        }

        private static void WriteHardwareCandidates(
            Editor ed,
            List<string> log,
            string title,
            List<SpecPartCandidate> candidates,
            string expectedFacing,
            string expectedPressureClass,
            bool requireNonLug)
        {
            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n  " + title + ":");

            if (candidates.Count == 0)
            {
                ReportWriter.Write(ed, log, "\n    No candidates found at this nominal diameter.");
                return;
            }

            int exactMatches = 0;
            int normalizedMatches = 0;

            foreach (SpecPartCandidate candidate in candidates.Take(20))
            {
                bool facingMatches = string.IsNullOrWhiteSpace(expectedFacing) ||
                                     TextUtil.SameText(candidate.Facing, expectedFacing);
                bool pressureExact = string.IsNullOrWhiteSpace(expectedPressureClass) ||
                                     TextUtil.SameText(candidate.PressureClass, expectedPressureClass);
                bool pressureNormalized = string.IsNullOrWhiteSpace(expectedPressureClass) ||
                                          SamePressureClassNormalized(candidate.PressureClass, expectedPressureClass);
                bool lugMatches = !requireNonLug || candidate.IsLugSetText == "0" || string.IsNullOrWhiteSpace(candidate.IsLugSetText);

                bool exactMatch = facingMatches && pressureExact && lugMatches;
                bool normalizedMatch = facingMatches && pressureNormalized && lugMatches;

                if (exactMatch)
                {
                    exactMatches++;
                }

                if (normalizedMatch)
                {
                    normalizedMatches++;
                }

                ReportWriter.Write(ed, log, "\n");
                ReportWriter.Write(ed, log, "\n    " + candidate.PartFamilyLongDesc);
                ReportWriter.Write(ed, log, "\n      PnPID: " + candidate.PnPID);
                ReportWriter.Write(ed, log, "\n      SizeDesc: " + TextUtil.NullText(candidate.PartSizeLongDesc));
                ReportWriter.Write(ed, log, "\n      Facing: " + TextUtil.NullText(candidate.Facing));
                ReportWriter.Write(ed, log, "\n      PressureClass: " + TextUtil.NullText(candidate.PressureClass));
                ReportWriter.Write(ed, log, "\n      IsLugSet: " + TextUtil.NullText(candidate.IsLugSetText));
                ReportWriter.Write(ed, log, "\n      PortDetails: " + TextUtil.NullText(candidate.PortDetails));
                ReportWriter.Write(ed, log, "\n      Facing match: " + YesNo(facingMatches));
                ReportWriter.Write(ed, log, "\n      Pressure exact match: " + YesNo(pressureExact));
                ReportWriter.Write(ed, log, "\n      Pressure normalized match: " + YesNo(pressureNormalized));

                if (requireNonLug)
                {
                    ReportWriter.Write(ed, log, "\n      Non-lug bolt set match: " + YesNo(lugMatches));
                }

                ReportWriter.Write(ed, log, "\n      Hardware match using exact pressure: " + YesNo(exactMatch));
                ReportWriter.Write(ed, log, "\n      Hardware match ignoring #/LB pressure formatting: " + YesNo(normalizedMatch));
            }

            ReportWriter.Write(ed, log, "\n");
            ReportWriter.Write(ed, log, "\n    Summary:");
            ReportWriter.Write(ed, log, "\n      Total candidates at size: " + candidates.Count);
            ReportWriter.Write(ed, log, "\n      Exact hardware matches: " + exactMatches);
            ReportWriter.Write(ed, log, "\n      Normalized pressure hardware matches: " + normalizedMatches);
        }

        private static List<SpecPartCandidate> ReadCandidates(
            SqliteConnection connection,
            string tableName,
            double nominalDiameter)
        {
            List<SpecPartCandidate> candidates = new List<SpecPartCandidate>();

            using (SqliteCommand command = connection.CreateCommand())
            {
                string lugColumn =
                    TextUtil.SameText(tableName, "BoltSet")
                        ? "CAST(part.IsLugSet AS TEXT) AS IsLugSetText,"
                        : "'' AS IsLugSetText,";

                command.CommandText = @"
SELECT
    ei.PnPID,
    ei.PartFamilyLongDesc,
    ei.PartSizeLongDesc,
    ei.ShortDescription,
    ei.ItemCode,
    ei.NominalDiameter,
    ei.NominalUnit,
    ei.EndType,
    ei.Facing,
    ei.PressureClass,
    " + lugColumn + @"
    GROUP_CONCAT(
        DISTINCT
        COALESCE(pp.Name, '') || ':' ||
        COALESCE(port.EndType, '') || ':' ||
        COALESCE(port.Facing, '') || ':' ||
        COALESCE(port.PressureClass, '')
    ) AS PortDetails
FROM " + tableName + @" part
JOIN EngineeringItems ei
    ON ei.PnPID = part.PnPID
LEFT JOIN PartPort pp
    ON pp.Part = ei.PnPID
LEFT JOIN Port port
    ON port.PnPID = pp.Port
WHERE ABS(ei.NominalDiameter - @nd) < 0.0001
GROUP BY
    ei.PnPID,
    ei.PartFamilyLongDesc,
    ei.PartSizeLongDesc,
    ei.ShortDescription,
    ei.ItemCode,
    ei.NominalDiameter,
    ei.NominalUnit,
    ei.EndType,
    ei.Facing,
    ei.PressureClass
ORDER BY
    ei.PnPID;";

                command.Parameters.AddWithValue("@nd", nominalDiameter);

                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        SpecPartCandidate candidate = new SpecPartCandidate();

                        candidate.PnPID = GetInt(reader, "PnPID");
                        candidate.PartFamilyLongDesc = GetString(reader, "PartFamilyLongDesc");
                        candidate.PartSizeLongDesc = GetString(reader, "PartSizeLongDesc");
                        candidate.ShortDescription = GetString(reader, "ShortDescription");
                        candidate.ItemCode = GetString(reader, "ItemCode");
                        candidate.NominalDiameter = GetString(reader, "NominalDiameter");
                        candidate.NominalUnit = GetString(reader, "NominalUnit");
                        candidate.EndType = GetString(reader, "EndType");
                        candidate.Facing = GetString(reader, "Facing");
                        candidate.PressureClass = GetString(reader, "PressureClass");
                        candidate.IsLugSetText = GetString(reader, "IsLugSetText");
                        candidate.PortDetails = GetString(reader, "PortDetails");

                        candidates.Add(candidate);
                    }
                }
            }

            return candidates;
        }

        private static SqliteConnection OpenReadOnlyConnection(string pspcPath)
        {
            SqliteConnectionStringBuilder builder = new SqliteConnectionStringBuilder();
            builder.DataSource = pspcPath;
            builder.Mode = SqliteOpenMode.ReadOnly;

            SqliteConnection connection = new SqliteConnection(builder.ToString());
            connection.Open();
            return connection;
        }

        private static void InitializeSqlite()
        {
            if (_sqliteInitialized)
            {
                return;
            }

            Batteries_V2.Init();
            _sqliteInitialized = true;
        }

        private static bool CandidateHasPipeSidePort(SpecPartCandidate candidate)
        {
            string details = TextUtil.Clean(candidate.PortDetails);

            return details.Contains(":BV:") ||
                   details.Contains(":PL:") ||
                   details.Contains(":UNIVERSAL_ET:");
        }

        private static bool SamePressureClassNormalized(string a, string b)
        {
            return NormalizePressureClass(a) == NormalizePressureClass(b);
        }

        private static string NormalizePressureClass(string value)
        {
            return (value ?? string.Empty)
                .Replace("#", string.Empty)
                .Replace("LB", string.Empty)
                .Replace("CLASS", string.Empty)
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Trim()
                .ToUpperInvariant();
        }

        private static string FirstNonBlank(string first, string second)
        {
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }

            if (!string.IsNullOrWhiteSpace(second))
            {
                return second;
            }

            return string.Empty;
        }

        private static string YesNo(bool value)
        {
            return value ? "YES" : "NO";
        }

        private static bool TryParseNominalDiameter(string text, out double value)
        {
            string clean = (text ?? string.Empty)
                .Replace("\"", "")
                .Replace("in", "")
                .Replace("IN", "")
                .Trim();

            return double.TryParse(
                clean,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static string GetString(SqliteDataReader reader, string column)
        {
            object value = reader[column];

            if (value == null || value == DBNull.Value)
            {
                return string.Empty;
            }

            return Convert.ToString(value);
        }

        private static int GetInt(SqliteDataReader reader, string column)
        {
            object value = reader[column];

            if (value == null || value == DBNull.Value)
            {
                return 0;
            }

            int result;

            if (int.TryParse(Convert.ToString(value), out result))
            {
                return result;
            }

            return 0;
        }

        private class SpecPartCandidate
        {
            public int PnPID = 0;
            public string PartFamilyLongDesc = string.Empty;
            public string PartSizeLongDesc = string.Empty;
            public string ShortDescription = string.Empty;
            public string ItemCode = string.Empty;
            public string NominalDiameter = string.Empty;
            public string NominalUnit = string.Empty;
            public string EndType = string.Empty;
            public string Facing = string.Empty;
            public string PressureClass = string.Empty;
            public string IsLugSetText = string.Empty;
            public string PortDetails = string.Empty;
        }
    }
}
