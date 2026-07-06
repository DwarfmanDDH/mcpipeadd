using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace McPipeAdd
{
    public static class ConnectorConfigResolver
    {
        public static ConnectorConfigResult ResolveDefaultConnectorsConfig()
        {
            ConnectorConfigResult result = new ConnectorConfigResult();

            string projectRoot = FindProjectRoot();

            if (!string.IsNullOrWhiteSpace(projectRoot))
            {
                string found = FindFileRecursive(projectRoot, "DefaultConnectorsConfig.xml", result);

                if (!string.IsNullOrWhiteSpace(found))
                {
                    result.ConfigPath = found;
                    return result;
                }
            }

            foreach (string root in GetSearchRoots())
            {
                string direct = Path.Combine(root, "DefaultConnectorsConfig.xml");

                if (File.Exists(direct))
                {
                    result.ConfigPath = direct;
                    return result;
                }

                string piping = Path.Combine(root, "Piping", "DefaultConnectorsConfig.xml");

                if (File.Exists(piping))
                {
                    result.ConfigPath = piping;
                    return result;
                }

                string plant3d = Path.Combine(root, "Plant 3D", "DefaultConnectorsConfig.xml");

                if (File.Exists(plant3d))
                {
                    result.ConfigPath = plant3d;
                    return result;
                }
            }

            result.Errors.Add("Could not locate DefaultConnectorsConfig.xml. Expected it somewhere under the active Plant 3D project folder.");
            return result;
        }

        private static string FindFileRecursive(string root, string fileName, ConnectorConfigResult result)
        {
            try
            {
                return Directory
                    .GetFiles(root, fileName, SearchOption.AllDirectories)
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                result.Errors.Add("Connector config recursive search failed: " + ex.Message);
                return string.Empty;
            }
        }

        private static List<string> GetSearchRoots()
        {
            List<string> roots = new List<string>();

            try
            {
                string dwgPath = AcadApp.DocumentManager.MdiActiveDocument.Database.Filename;

                if (!string.IsNullOrWhiteSpace(dwgPath))
                {
                    DirectoryInfo dir = new DirectoryInfo(Path.GetDirectoryName(dwgPath));

                    while (dir != null)
                    {
                        roots.Add(dir.FullName);
                        dir = dir.Parent;
                    }
                }
            }
            catch
            {
                // Ignore.
            }

            try
            {
                string pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                if (!string.IsNullOrWhiteSpace(pluginFolder))
                {
                    roots.Add(pluginFolder);
                }
            }
            catch
            {
                // Ignore.
            }

            return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string FindProjectRoot()
        {
            try
            {
                string dwgPath = AcadApp.DocumentManager.MdiActiveDocument.Database.Filename;

                if (string.IsNullOrWhiteSpace(dwgPath))
                {
                    return string.Empty;
                }

                DirectoryInfo dir = new DirectoryInfo(Path.GetDirectoryName(dwgPath));

                while (dir != null)
                {
                    if (File.Exists(Path.Combine(dir.FullName, "Project.xml")))
                    {
                        return dir.FullName;
                    }

                    dir = dir.Parent;
                }
            }
            catch
            {
                // Ignore.
            }

            return string.Empty;
        }
    }
}
