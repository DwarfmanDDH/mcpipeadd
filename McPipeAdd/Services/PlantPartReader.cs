using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Autodesk.AutoCAD.DatabaseServices;

using Autodesk.ProcessPower.PlantInstance;
using Autodesk.ProcessPower.ProjectManager;
using Autodesk.ProcessPower.P3dProjectParts;
using Autodesk.ProcessPower.DataLinks;

namespace McPipeAdd
{
    public static class PlantPartReader
    {
        public static PartInfo ReadPartInfo(ObjectId objectId)
        {
            PartInfo info = new PartInfo();
            info.ObjectIdText = objectId.ToString();

            ReadDataLinksProperties(objectId, info);
            ReadPorts(objectId, info);

            return info;
        }

        private static void ReadDataLinksProperties(ObjectId objectId, PartInfo info)
        {
            try
            {
                PlantProject currentProject = PlantApplication.CurrentProject;

                if (currentProject == null)
                {
                    info.Errors.Add("No active Plant 3D project found.");
                    return;
                }

                PipingProject pipingProject =
                    currentProject.ProjectParts["Piping"] as PipingProject;

                if (pipingProject == null)
                {
                    info.Errors.Add("Could not access Piping project part.");
                    return;
                }

                DataLinksManager dlm = pipingProject.DataLinksManager;

                int rowId = dlm.FindAcPpRowId(objectId);

                if (rowId <= 0)
                {
                    info.Errors.Add("Selected object is not linked to Plant 3D data.");
                    return;
                }

                info.RowId = rowId;

                List<KeyValuePair<string, string>> properties =
                    dlm.GetAllProperties(rowId, true);

                info.PnPClassName = GetPropertyValue(properties, "PnPClassName");
                info.Spec = GetPropertyValue(properties, "Spec");
                info.Size = GetPropertyValue(properties, "Size");
                info.NominalDiameter = GetPropertyValue(properties, "NominalDiameter");
                info.ShortDescription = GetPropertyValue(properties, "ShortDescription");
                info.PartFamilyLongDesc = GetPropertyValue(properties, "PartFamilyLongDesc");
                info.PartSizeLongDesc = GetPropertyValue(properties, "PartSizeLongDesc");
                info.ItemCode = GetPropertyValue(properties, "ItemCode");
                info.DataLinksEndType = GetPropertyValue(properties, "EndType");
                info.DataLinksFacing = GetPropertyValue(properties, "Facing");
                info.DataLinksPortName = GetPropertyValue(properties, "PortName");
            }
            catch (System.Exception ex)
            {
                info.Errors.Add("DataLinks read failed: " + ex.Message);
            }
        }

        private static void ReadPorts(ObjectId objectId, PartInfo info)
        {
            try
            {
                Database db = objectId.Database;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    DBObject obj = tr.GetObject(objectId, OpenMode.ForRead);

                    info.RuntimeType = obj.GetType().FullName;

                    object portCollection = GetPortsAll(obj);

                    if (portCollection == null)
                    {
                        info.Errors.Add("Could not read port collection.");
                        tr.Commit();
                        return;
                    }

                    foreach (object port in EnumerateCollection(portCollection))
                    {
                        string portName = TryGetStringProperty(port, "Name");

                        if (string.IsNullOrWhiteSpace(portName))
                        {
                            continue;
                        }

                        PortInfo portInfo = new PortInfo();
                        portInfo.Name = portName;

                        object specPort = GetSpecPort(obj, portName);

                        if (specPort != null)
                        {
                            portInfo.EndCondition = TryGetStringProperty(specPort, "EndCondition");
                            portInfo.NominalDiameter = TryGetStringProperty(specPort, "NominalDiameter");
                        }

                        if (string.IsNullOrWhiteSpace(portInfo.EndCondition))
                        {
                            portInfo.EndCondition = TryGetStringProperty(port, "EndType");
                        }

                        if (string.IsNullOrWhiteSpace(portInfo.NominalDiameter))
                        {
                            portInfo.NominalDiameter = TryGetStringProperty(port, "NominalDiameter");
                        }

                        info.Ports.Add(portInfo);
                    }

                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                info.Errors.Add("Port read failed: " + ex.Message);
            }
        }

        private static object GetPortsAll(object plantObject)
        {
            MethodInfo getPortsMethod =
                plantObject.GetType().GetMethod("GetPorts", BindingFlags.Public | BindingFlags.Instance);

            if (getPortsMethod == null)
            {
                return null;
            }

            ParameterInfo[] parameters = getPortsMethod.GetParameters();

            if (parameters.Length != 1 || !parameters[0].ParameterType.IsEnum)
            {
                return null;
            }

            Type portTypeEnum = parameters[0].ParameterType;

            object allValue = null;

            try
            {
                allValue = Enum.Parse(portTypeEnum, "All");
            }
            catch
            {
                try
                {
                    allValue = Enum.Parse(portTypeEnum, "Both");
                }
                catch
                {
                    allValue = Enum.GetValues(portTypeEnum).GetValue(0);
                }
            }

            return getPortsMethod.Invoke(plantObject, new object[] { allValue });
        }

        private static object GetSpecPort(object plantObject, string portName)
        {
            MethodInfo portPropertiesMethod =
                plantObject.GetType().GetMethod("PortProperties", BindingFlags.Public | BindingFlags.Instance);

            if (portPropertiesMethod == null)
            {
                return null;
            }

            return portPropertiesMethod.Invoke(plantObject, new object[] { portName });
        }

        private static IEnumerable<object> EnumerateCollection(object collection)
        {
            if (collection == null)
            {
                yield break;
            }

            IEnumerable enumerable = collection as IEnumerable;

            if (enumerable != null)
            {
                foreach (object item in enumerable)
                {
                    yield return item;
                }

                yield break;
            }

            Type type = collection.GetType();

            PropertyInfo countProperty = type.GetProperty("Count");

            if (countProperty == null)
            {
                yield break;
            }

            int count;
            object countValue = countProperty.GetValue(collection, null);

            if (!int.TryParse(Convert.ToString(countValue), out count))
            {
                yield break;
            }

            PropertyInfo indexer = type.GetProperties()
                .FirstOrDefault(p =>
                    p.GetIndexParameters().Length == 1 &&
                    p.GetIndexParameters()[0].ParameterType == typeof(int));

            if (indexer == null)
            {
                yield break;
            }

            for (int i = 0; i < count; i++)
            {
                yield return indexer.GetValue(collection, new object[] { i });
            }
        }

        private static string TryGetStringProperty(object obj, string propertyName)
        {
            if (obj == null)
            {
                return string.Empty;
            }

            try
            {
                PropertyInfo property = obj.GetType().GetProperty(propertyName);

                if (property == null)
                {
                    return string.Empty;
                }

                object value = property.GetValue(obj, null);

                return value == null ? string.Empty : Convert.ToString(value);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetPropertyValue(List<KeyValuePair<string, string>> properties, string key)
        {
            KeyValuePair<string, string> exact =
                properties.FirstOrDefault(p =>
                    string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(exact.Key))
            {
                return exact.Value;
            }

            string normalizedKey = TextUtil.Normalize(key);

            KeyValuePair<string, string> normalized =
                properties.FirstOrDefault(p =>
                    TextUtil.Normalize(p.Key) == normalizedKey);

            return normalized.Value;
        }
    }
}