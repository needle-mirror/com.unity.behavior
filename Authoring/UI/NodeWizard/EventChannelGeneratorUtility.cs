#if UNITY_EDITOR
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using Unity.Behavior.GraphFramework;
using System.Collections.Generic;

namespace Unity.Behavior
{
    internal static class EventChannelGeneratorUtility
    {
        internal class EventChannelData
        {
            internal string Name { get; set; }
            internal string ClassName { get; set; }
            internal string Description { get; set; }
            internal string Message { get; set; }
            internal string Category { get; set; }
            internal (string, Type)[] Parameters { get; set; }
        }

        internal static bool CreateEventChannelAsset(EventChannelData data)
        {
            string fileName = GeneratorUtils.ToPascalCase(data.Name);
            string suggestedSavePath = Util.GetAbsolutePathToProjectAssets(BehaviorProjectSettings.instance.SaveFolderEventChannels);
            string path = EditorUtility.SaveFilePanel(
                "Create Event Channel ScriptableObject",
                suggestedSavePath,
                fileName,
                "cs");

            if (path.Length == 0)
            {
                return false;
            }
            if (BehaviorProjectSettings.instance.AutoSaveLastSaveLocation)
            {
                BehaviorProjectSettings.instance.SaveFolderEventChannels = Path.GetDirectoryName(path);
            }

            GenerateEventChannelFile(data, path);

            return true;
        }

        internal static void GenerateEventChannelFile(EventChannelData data, string path)
        {
            string eventChannelName = Path.GetFileNameWithoutExtension(path);
            string attributeString = GenerateAttributeString(data);
            data.ClassName = eventChannelName;

            Dictionary<string, Type> parametersDictionary = new Dictionary<string, Type>();
            foreach (var parameter in data.Parameters)
            {
                parametersDictionary[parameter.Item1] = parameter.Item2;
            }

            // Determine parent class based on number of parameters
            string genericTypes = GetGenericTypesString(data.Parameters);
            string baseClassName = GetEventChannelBaseClassName(genericTypes);

            using (StreamWriter outfile = new StreamWriter(path))
            {
                // Imports
                var namespaceStrings = GeneratorUtils.GetNamespaceStrings(parametersDictionary);
                foreach (var namespaceString in namespaceStrings)
                {
                    outfile.WriteLine($"using {namespaceString};");
                }
                outfile.WriteLine("using Unity.Properties;");

                // Event channel
                outfile.WriteLine();
                outfile.WriteLine("#if UNITY_EDITOR");
                outfile.WriteLine($"[CreateAssetMenu(menuName = \"Behavior/Event Channels/{data.Name}\")]");
                outfile.WriteLine("#endif");
                outfile.WriteLine("[Serializable, GeneratePropertyBag]");
                outfile.WriteLine(attributeString);
                outfile.WriteLine($"public sealed partial class {eventChannelName} : {baseClassName} {{ }}");
                outfile.WriteLine();
            }

            AssetDatabase.Refresh();
        }

        private static string GetEventChannelBaseClassName(string genericTypes)
        {
            return string.IsNullOrEmpty(genericTypes) ? "EventChannel" : $"EventChannel<{genericTypes}>";
        }

        private static string GetGenericTypesString(IReadOnlyCollection<(string, Type)> parameters)
        {
            if (parameters.Count == 0)
                return string.Empty;

            return string.Join(", ", parameters.Select(p => GeneratorUtils.GetStringForType(p.Item2)));
        }

        internal static string GenerateAttributeString(EventChannelData data)
        {
            string name = string.IsNullOrEmpty(data.Name) ? "" : $"name: \"{data.Name}\",";
            string message = string.IsNullOrEmpty(data.Message) ? "" : $" message: \"{data.Message}\",";
            string description = string.IsNullOrEmpty(data.Description) ? "" : $" description: \"{data.Description}\",";
            string category = string.IsNullOrEmpty(data.Category) ? "" : $" category: \"{data.Category}\",";

            string attributeString = "[EventChannelDescription(" + name + description + message + category +
                                     $" id: \"{SerializableGUID.Generate()}\")]";
            return attributeString;
        }
    }
}
#endif
