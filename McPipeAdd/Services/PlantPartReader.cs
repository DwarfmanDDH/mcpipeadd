using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
                info.PressureClass = GetPropertyValue(properties, "PressureClass");
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
                            portInfo.Facing = TryGetStringProperty(specPort, "Facing");
                            portInfo.PressureClass = TryGetStringProperty(specPort, "PressureClass");
                        }

                        if (string.IsNullOrWhiteSpace(portInfo.EndCondition))
                        {
                            portInfo.EndCondition = TryGetStringProperty(port, "EndType");
                        }

                        if (string.IsNullOrWhiteSpace(portInfo.EndCondition))
                        {
                            portInfo.EndCondition = TryGetStringProperty(specPort, "EndType");
                        }

                        if (string.IsNullOrWhiteSpace(portInfo.NominalDiameter))
                        {
                            portInfo.NominalDiameter = TryGetStringProperty(port, "NominalDiameter");
                        }

                        if (string.IsNullOrWhiteSpace(portInfo.Facing))
                        {
                            portInfo.Facing = TryGetStringProperty(port, "Facing");
                        }

                        if (string.IsNullOrWhiteSpace(portInfo.PressureClass))
                        {
                            portInfo.PressureClass = TryGetStringProperty(port, "PressureClass");
                        }

                        TryReadPortPosition(obj, port, specPort, portName, portInfo);

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

        private static void TryReadPortPosition(
            object plantObject,
            object runtimePort,
            object specPort,
            string portName,
            PortInfo portInfo)
        {
            double x;
            double y;
            double z;
            string source;

            if (TryReadPointFromObject(runtimePort, out x, out y, out z, out source))
            {
                SetPortPosition(portInfo, x, y, z, "runtime port " + source);
                return;
            }

            if (TryReadPointFromObject(specPort, out x, out y, out z, out source))
            {
                SetPortPosition(portInfo, x, y, z, "spec port " + source);
                return;
            }

            if (TryReadPointFromPlantObjectMethod(plantObject, portName, out x, out y, out z, out source))
            {
                SetPortPosition(portInfo, x, y, z, source);
                return;
            }
        }

        private static void SetPortPosition(
            PortInfo portInfo,
            double x,
            double y,
            double z,
            string source)
        {
            portInfo.HasPosition = true;
            portInfo.X = x;
            portInfo.Y = y;
            portInfo.Z = z;
            portInfo.PositionSource = source ?? string.Empty;
        }

        private static bool TryReadPointFromObject(
            object obj,
            out double x,
            out double y,
            out double z,
            out string source)
        {
            x = 0.0;
            y = 0.0;
            z = 0.0;
            source = string.Empty;

            if (obj == null)
            {
                return false;
            }

            if (TryReadXyzFromPointObject(obj, out x, out y, out z))
            {
                source = "object XYZ";
                return true;
            }

            string[] memberNames =
            {
                "Position",
                "Location",
                "Point",
                "Origin",
                "Center",
                "EndPoint",
                "StartPoint",
                "ConnectPoint",
                "ConnectionPoint",
                "PortPosition",
                "PortPoint"
            };

            foreach (string memberName in memberNames)
            {
                object propertyValue = TryGetPropertyValue(obj, memberName);

                if (TryReadXyzFromPointObject(propertyValue, out x, out y, out z))
                {
                    source = "property " + memberName;
                    return true;
                }

                object fieldValue = TryGetFieldValue(obj, memberName);

                if (TryReadXyzFromPointObject(fieldValue, out x, out y, out z))
                {
                    source = "field " + memberName;
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadPointFromPlantObjectMethod(
            object plantObject,
            string portName,
            out double x,
            out double y,
            out double z,
            out string source)
        {
            x = 0.0;
            y = 0.0;
            z = 0.0;
            source = string.Empty;

            if (plantObject == null || string.IsNullOrWhiteSpace(portName))
            {
                return false;
            }

            string[] methodNames =
            {
                "GetPortPosition",
                "GetPortLocation",
                "GetPortPoint",
                "GetConnectionPoint",
                "PortPosition",
                "PortLocation",
                "PortPoint"
            };

            Type type = plantObject.GetType();

            foreach (string methodName in methodNames)
            {
                MethodInfo[] methods = type
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == methodName)
                    .ToArray();

                foreach (MethodInfo method in methods)
                {
                    ParameterInfo[] parameters = method.GetParameters();

                    if (parameters.Length != 1 ||
                        parameters[0].ParameterType != typeof(string))
                    {
                        continue;
                    }

                    try
                    {
                        object value = method.Invoke(plantObject, new object[] { portName });

                        if (TryReadXyzFromPointObject(value, out x, out y, out z))
                        {
                            source = "plant object method " + methodName;
                            return true;
                        }
                    }
                    catch
                    {
                        // Try the next method.
                    }
                }
            }

            return false;
        }

        private static bool TryReadXyzFromPointObject(
            object pointObject,
            out double x,
            out double y,
            out double z)
        {
            x = 0.0;
            y = 0.0;
            z = 0.0;

            if (pointObject == null)
            {
                return false;
            }

            object xValue = TryGetPropertyValue(pointObject, "X");
            object yValue = TryGetPropertyValue(pointObject, "Y");
            object zValue = TryGetPropertyValue(pointObject, "Z");

            if (xValue == null || yValue == null || zValue == null)
            {
                xValue = TryGetFieldValue(pointObject, "X");
                yValue = TryGetFieldValue(pointObject, "Y");
                zValue = TryGetFieldValue(pointObject, "Z");
            }

            if (xValue == null || yValue == null || zValue == null)
            {
                return false;
            }

            return TryConvertDouble(xValue, out x) &&
                   TryConvertDouble(yValue, out y) &&
                   TryConvertDouble(zValue, out z);
        }

        private static bool TryConvertDouble(object value, out double result)
        {
            result = 0.0;

            if (value == null)
            {
                return false;
            }

            return double.TryParse(
                Convert.ToString(value, CultureInfo.InvariantCulture),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result);
        }

        private static object TryGetPropertyValue(object obj, string propertyName)
        {
            if (obj == null)
            {
                return null;
            }

            try
            {
                PropertyInfo property = obj.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Public | BindingFlags.Instance);

                if (property == null)
                {
                    return null;
                }

                return property.GetValue(obj, null);
            }
            catch
            {
                return null;
            }
        }

        private static object TryGetFieldValue(object obj, string fieldName)
        {
            if (obj == null)
            {
                return null;
            }

            try
            {
                FieldInfo field = obj.GetType().GetField(
                    fieldName,
                    BindingFlags.Public | BindingFlags.Instance);

                if (field == null)
                {
                    return null;
                }

                return field.GetValue(obj);
            }
            catch
            {
                return null;
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
