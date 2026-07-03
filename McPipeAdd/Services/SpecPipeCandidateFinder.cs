using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using SQLitePCL;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace McPipeAdd
{

    public static class SpecPipeCandidateFinder
    {
        private static bool _sqliteInitialized = false;

        private static void InitializeSqlite()
        {
            if (_sqliteInitialized)
            {
                return;
            }

            Batteries_V2.Init();
            _sqliteInitialized = true;
        }
        public static SpecCandidateResult FindPipeCandidates(
            string specName,
            string nominalDiameterText,
            string expectedPipeEnd)
        {
            SpecCandidateResult result = SpecPathResolver.ResolveSpecFiles(specName);

            double nominalDiameter;

            

            if (!TryParseNominalDiameter(nominalDiameterText, out nominalDiameter))
            {
                result.Errors.Add("Could not parse nominal diameter: " + nominalDiameterText);
                return result;
            }

            if (string.IsNullOrWhiteSpace(result.PspcPath))
            {
                return result;
            }

            Dictionary<string, int> priorities =
                LoadPipePriorities(result.PspxPath, nominalDiameter);

            try
            {
                InitializeSqlite();

                SqliteConnectionStringBuilder builder = new SqliteConnectionStringBuilder();
                builder.DataSource = result.PspcPath;
                builder.Mode = SqliteOpenMode.ReadOnly;

                using (SqliteConnection connection = new SqliteConnection(builder.ToString()))
                {
                    connection.Open();

                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
SELECT
    ei.PnPID,
    ei.PartFamilyId,
    ei.PartFamilyLongDesc,
    ei.PartSizeLongDesc,
    ei.ShortDescription,
    ei.ItemCode,
    ei.NominalDiameter,
    ei.NominalUnit,
    ei.Schedule,
    ei.MaterialCode,
    GROUP_CONCAT(DISTINCT port.EndType) AS EndConditions,
    GROUP_CONCAT(DISTINCT port.PortName || ':' || port.EndType) AS PortDetails
FROM Pipe pipe
JOIN EngineeringItems ei
    ON ei.PnPID = pipe.PnPID
LEFT JOIN PartPort pp
    ON pp.Part = ei.PnPID
LEFT JOIN Port port
    ON port.PnPID = pp.Port
WHERE ABS(ei.NominalDiameter - @nd) < 0.0001
GROUP BY
    ei.PnPID,
    ei.PartFamilyId,
    ei.PartFamilyLongDesc,
    ei.PartSizeLongDesc,
    ei.ShortDescription,
    ei.ItemCode,
    ei.NominalDiameter,
    ei.NominalUnit,
    ei.Schedule,
    ei.MaterialCode
ORDER BY ei.PnPID;";

                        command.Parameters.AddWithValue("@nd", nominalDiameter);

                        using (SqliteDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                PipeCandidate candidate = new PipeCandidate();

                                candidate.PnPID = GetInt(reader, "PnPID");
                                candidate.PartFamilyId = GetGuid(reader, "PartFamilyId");
                                candidate.PartFamilyLongDesc = GetString(reader, "PartFamilyLongDesc");
                                candidate.PartSizeLongDesc = GetString(reader, "PartSizeLongDesc");
                                candidate.ShortDescription = GetString(reader, "ShortDescription");
                                candidate.ItemCode = GetString(reader, "ItemCode");
                                candidate.NominalDiameter = GetString(reader, "NominalDiameter");
                                candidate.NominalUnit = GetString(reader, "NominalUnit");
                                candidate.Schedule = GetString(reader, "Schedule");
                                candidate.MaterialCode = GetString(reader, "MaterialCode");
                                candidate.PortDetails = GetString(reader, "PortDetails");

                                candidate.EndConditions = SplitCsv(GetString(reader, "EndConditions"));

                                int priority;

                                if (!string.IsNullOrWhiteSpace(candidate.PartFamilyId) &&
                                    priorities.TryGetValue(candidate.PartFamilyId.ToLowerInvariant(), out priority))
                                {
                                    candidate.Priority = priority;
                                }

                                result.Candidates.Add(candidate);
                            }
                        }
                    }
                }

                result.Candidates = result.Candidates
                    .OrderBy(c => c.Priority.HasValue ? c.Priority.Value : 9999)
                    .ThenBy(c => c.PnPID)
                    .ToList();
            }
            catch (Exception ex)
            {
                result.Errors.Add("Spec database read failed: " + ex.Message);
            }

            return result;
        }

        private static Dictionary<string, int> LoadPipePriorities(string pspxPath, double nominalDiameter)
        {
            Dictionary<string, int> priorities = new Dictionary<string, int>();

            if (string.IsNullOrWhiteSpace(pspxPath))
            {
                return priorities;
            }

            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(pspxPath))
                {
                    ZipArchiveEntry entry = archive.GetEntry("content/PartUsePriorities.xml");

                    if (entry == null)
                    {
                        return priorities;
                    }

                    using (var stream = entry.Open())
                    {
                        XDocument document = XDocument.Load(stream);

                        foreach (XElement partTypeUsePriority in document.Descendants("PartTypeUsePriority"))
                        {
                            string partType = GetElementValue(partTypeUsePriority, "PartType");

                            if (!TextUtil.SameText(partType, "Pipe"))
                            {
                                continue;
                            }

                            XElement ndElement = partTypeUsePriority.Element("ND");

                            if (ndElement == null)
                            {
                                continue;
                            }

                            XAttribute valueAttribute = ndElement.Attribute("Value");

                            if (valueAttribute == null)
                            {
                                continue;
                            }

                            double ndValue;

                            if (!double.TryParse(
                                    valueAttribute.Value,
                                    NumberStyles.Float,
                                    CultureInfo.InvariantCulture,
                                    out ndValue))
                            {
                                continue;
                            }

                            if (Math.Abs(ndValue - nominalDiameter) > 0.0001)
                            {
                                continue;
                            }

                            XElement familyOrder = partTypeUsePriority.Element("PartFamilyUseOrder");

                            if (familyOrder == null)
                            {
                                continue;
                            }

                            int order = 1;

                            foreach (XElement family in familyOrder.Elements("PartFamilyName"))
                            {
                                XAttribute idAttribute = family.Attribute("PartFamilyId");

                                if (idAttribute != null)
                                {
                                    string id = idAttribute.Value.Trim().ToLowerInvariant();

                                    if (!priorities.ContainsKey(id))
                                    {
                                        priorities.Add(id, order);
                                    }
                                }

                                order++;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Priority is helpful but not mandatory.
            }

            return priorities;
        }

        private static string GetElementValue(XElement parent, string elementName)
        {
            XElement element = parent.Element(elementName);
            return element == null ? string.Empty : element.Value;
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

        private static List<string> SplitCsv(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new List<string>();
            }

            return value
                .Split(',')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
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

        private static string GetGuid(SqliteDataReader reader, string column)
        {
            object value = reader[column];

            if (value == null || value == DBNull.Value)
            {
                return string.Empty;
            }

            byte[] bytes = value as byte[];

            if (bytes != null && bytes.Length == 16)
            {
                return new Guid(bytes).ToString();
            }

            return Convert.ToString(value);
        }
    }
}