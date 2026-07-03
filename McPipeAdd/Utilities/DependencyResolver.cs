using System;
using System.IO;
using System.Reflection;

namespace McPipeAdd
{
    public static class DependencyResolver
    {
        private static bool _registered = false;

        public static void Register()
        {
            if (_registered)
            {
                return;
            }

            AppDomain.CurrentDomain.AssemblyResolve += ResolveFromPluginFolder;
            _registered = true;
        }

        private static Assembly ResolveFromPluginFolder(object sender, ResolveEventArgs args)
        {
            string requestedFileName = new AssemblyName(args.Name).Name + ".dll";

            string pluginFolder = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);

            if (string.IsNullOrWhiteSpace(pluginFolder))
            {
                return null;
            }

            string directPath = Path.Combine(pluginFolder, requestedFileName);

            if (File.Exists(directPath))
            {
                return Assembly.LoadFrom(directPath);
            }

            try
            {
                string[] matches = Directory.GetFiles(
                    pluginFolder,
                    requestedFileName,
                    SearchOption.AllDirectories);

                if (matches.Length > 0)
                {
                    return Assembly.LoadFrom(matches[0]);
                }
            }
            catch
            {
                // Do not crash while resolving dependencies.
            }

            return null;
        }
    }
}