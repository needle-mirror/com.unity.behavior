using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Unity.Behavior.GraphFramework
{
    internal static class GraphAssetUtility
    {
        public readonly struct EnumValueEntry
        {
            public readonly string Name;
            public readonly string NumericValue;

            public EnumValueEntry(string name, string numericValue)
            {
                Name = name;
                NumericValue = numericValue;
            }
        }

        public static void CleanupOrphanedFloatingPortNodes(GraphAsset asset)
        {
            if (asset?.Nodes == null)
            {
                return;
            }

            List<FloatingPortNodeModel> orphanedNodes = asset.Nodes
                .OfType<FloatingPortNodeModel>()
                .Where(floatingNode => IsOrphaned(asset, floatingNode))
                .ToList();

            DeleteNodes(asset, orphanedNodes);
        }

        public static void CleanupOrphanedFloatingPortNodesForParent(GraphAsset asset, SerializableGUID parentNodeId)
        {
            if (asset?.Nodes == null)
            {
                return;
            }

            List<FloatingPortNodeModel> orphanedNodes = asset.Nodes
                .OfType<FloatingPortNodeModel>()
                .Where(floatingNode => floatingNode.ParentNodeID == parentNodeId)
                .Where(floatingNode => IsOrphaned(asset, floatingNode))
                .ToList();

            DeleteNodes(asset, orphanedNodes);
        }

        public static bool AreEnumDependencyDictionariesEqual(
            IReadOnlyDictionary<string, string> current,
            IReadOnlyDictionary<string, string> next)
        {
            if (ReferenceEquals(current, next))
            {
                return true;
            }

            if (current == null || next == null || current.Count != next.Count)
            {
                return false;
            }

            foreach (var kvp in current)
            {
                if (!next.TryGetValue(kvp.Key, out string value)
                    || !string.Equals(value, kvp.Value, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool TryComputeEnumDependencySignature(Type enumType, out string signatureHash)
        {
            signatureHash = string.Empty;
            if (enumType == null || !enumType.IsEnum)
            {
                return false;
            }

            string typeId = GetEnumTypeId(enumType);
            if (string.IsNullOrEmpty(typeId))
            {
                return false;
            }

            var builder = new StringBuilder(256);
            builder.Append(typeId).Append('|');
            builder.Append(Enum.GetUnderlyingType(enumType).FullName).Append('|');
            foreach (EnumValueEntry entry in GetEnumValueEntries(enumType))
            {
                builder.Append(entry.Name).Append('=');
                builder.Append(entry.NumericValue).Append(';');
            }

            signatureHash = Hash128.Compute(builder.ToString()).ToString();
            return true;
        }

        public static bool TryComputeEnumDependencySignatureFromTypeId(string enumTypeId, out string signatureHash)
        {
            signatureHash = string.Empty;
            if (string.IsNullOrEmpty(enumTypeId))
            {
                return false;
            }

            if (!TryGetEnumTypeFromTypeId(enumTypeId, out Type enumType))
            {
                return false;
            }

            return TryComputeEnumDependencySignature(enumType, out signatureHash);
        }

        public static string GetEnumTypeId(Type enumType)
        {
            if (enumType == null || !enumType.IsEnum)
            {
                return string.Empty;
            }

            string typeName = enumType.FullName;
            string assemblySimpleName = enumType.Assembly.GetName().Name;
            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(assemblySimpleName))
            {
                return string.Empty;
            }

            return $"{typeName}, {assemblySimpleName}";
        }

        public static bool TryGetEnumTypeFromTypeId(string enumTypeId, out Type enumType)
        {
            enumType = null;
            if (string.IsNullOrEmpty(enumTypeId))
            {
                return false;
            }

            enumType = Type.GetType(enumTypeId);
            if (enumType != null && enumType.IsEnum)
            {
                return true;
            }

            int firstCommaIndex = enumTypeId.IndexOf(',');
            if (firstCommaIndex <= 0 || firstCommaIndex >= enumTypeId.Length - 1)
            {
                return false;
            }

            string typeName = enumTypeId.Substring(0, firstCommaIndex).Trim();
            string assemblySimpleName = enumTypeId.Substring(firstCommaIndex + 1).Trim();
            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(assemblySimpleName))
            {
                return false;
            }

#if UNITY_6000_5_OR_NEWER
            foreach (Assembly assembly in UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies())
#else
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
#endif
            {
                if (!string.Equals(assembly.GetName().Name, assemblySimpleName, StringComparison.Ordinal))
                {
                    continue;
                }

                Type candidate = assembly.GetType(typeName, throwOnError: false);
                if (candidate != null && candidate.IsEnum)
                {
                    enumType = candidate;
                    return true;
                }
            }

            return false;
        }

        public static IReadOnlyList<EnumValueEntry> GetEnumValueEntries(Type enumType)
        {
            if (enumType == null || !enumType.IsEnum)
            {
                return Array.Empty<EnumValueEntry>();
            }

            Type underlyingType = Enum.GetUnderlyingType(enumType);
            string[] names = Enum.GetNames(enumType);
            Array values = Enum.GetValues(enumType);
            var entries = new List<EnumValueEntry>(names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                object enumValue = values.GetValue(i);
                entries.Add(new EnumValueEntry(names[i], GetNumericEnumValue(enumValue, underlyingType)));
            }

            return entries;
        }

        private static bool IsOrphaned(GraphAsset asset, FloatingPortNodeModel floatingNode)
        {
            if (!floatingNode.HasIncomingConnections)
            {
                return true;
            }

            NodeModel parentNode = asset.Nodes.FirstOrDefault(node => node.ID == floatingNode.ParentNodeID);
            if (parentNode == null)
            {
                return true;
            }

            PortModel referencedPort = parentNode.FindPortModelByName(floatingNode.PortName);
            return referencedPort == null;
        }

        private static void DeleteNodes(GraphAsset asset, List<FloatingPortNodeModel> nodesToDelete)
        {
            foreach (FloatingPortNodeModel node in nodesToDelete)
            {
                asset.DeleteNode(node);
            }
        }

        private static string GetNumericEnumValue(object enumValue, Type underlyingType)
        {
            TypeCode typeCode = Type.GetTypeCode(underlyingType);
            ulong rawValue = typeCode switch
            {
                TypeCode.SByte => unchecked((ulong)Convert.ToSByte(enumValue)),
                TypeCode.Int16 => unchecked((ulong)Convert.ToInt16(enumValue)),
                TypeCode.Int32 => unchecked((ulong)Convert.ToInt32(enumValue)),
                TypeCode.Int64 => unchecked((ulong)Convert.ToInt64(enumValue)),
                TypeCode.Byte => Convert.ToByte(enumValue),
                TypeCode.UInt16 => Convert.ToUInt16(enumValue),
                TypeCode.UInt32 => Convert.ToUInt32(enumValue),
                TypeCode.UInt64 => Convert.ToUInt64(enumValue),
                _ => unchecked((ulong)Convert.ToInt64(enumValue))
            };

            return rawValue.ToString("X16"); // Convert to a hexadecimal string.
        }
    }
}
