using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace McPipeAdd
{
    public static class SpecPathResolver
    {
        public static SpecCandidateResult ResolveSpecFiles(string specName)
        {
            SpecCandidateResult result = new SpecCandidateResult();
            result.SpecName = specName ?? string.Empty;

            if (string.IsNullOrWhiteSpace(specName))
            {
                result.Errors.Add("Spec name is blank. Cannot locate spec files.");
                return result;
            }

            string cleanSpecName = specName.Trim();

            List<string> roots = GetSearchRoots();

            foreach (string root in roots)
            {
                TryCheckSpecFolder(result, root, cleanSpecName, "Spec Sheets");
                if (HasPspc(result)) return result;

                TryCheckSpecFolder(result, root, cleanSpecName, "Specs");
                if (HasPspc(result)) return result;

                TryCheckSpecFolder(result, root, cleanSpecName, "SpecSheets");
                if (HasPspc(result)) return result;

                TryCheckDirect(result, root, cleanSpecName);
                if (HasPspc(result)) return result;
            }

            string projectRoot = FindProjectRoot();

            if (!string.IsNullOrWhiteSpace(projectRoot))
            {
                try
                {
                    string found = Directory
                        .GetFiles(projectRoot, cleanSpecName + ".pspc", SearchOption.AllDirectories)
                        .FirstOrDefault();

                    if (!string.IsNullOrWhiteSpace(found))
                    {
                        result.PspcPath = found;
                        string pspx = Path.ChangeExtension(found, ".pspx");

                        if (File.Exists(pspx))
                        {
                            result.PspxPath = pspx;
                        }

                        return result;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add("Recursive project spec search failed: " + ex.Message);
                }
            }

            result.Errors.Add("Could not locate " + cleanSpecName + ".pspc. Expected it under the project Spec Sheets folder.");
            return result;
        }

        private static bool HasPspc(SpecCandidateResult result)
        {
            return !string.IsNullOrWhiteSpace(result.PspcPath) && File.Exists(result.PspcPath);
        }

        private static void TryCheckSpecFolder(SpecCandidateResult result, string root, string specName, string folderName)
        {
            string folder = Path.Combine(root, folderName);
            TryCheckDirect(result, folder, specName);
        }

        private static void TryCheckDirect(SpecCandidateResult result, string folder, string specName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                {
                    return;
                }

                string pspc = Path.Combine(folder, specName + ".pspc");
                string pspx = Path.Combine(folder, specName + ".pspx");

                if (File.Exists(pspc))
                {
                    result.PspcPath = pspc;

                    if (File.Exists(pspx))
                    {
                        result.PspxPath = pspx;
                    }
                }
            }
            catch
            {
                // Ignore resolver noise.
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