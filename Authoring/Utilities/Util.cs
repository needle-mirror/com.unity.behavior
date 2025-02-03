using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if !UNITY_EDITOR
using System.Reflection;
#endif
using Unity.AppUI.UI;
using Unity.Behavior.GraphFramework;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Behavior
{
    internal static class Util
    {
        private static readonly List<Type> s_StaticallySupportedTypes = new()
        {
            typeof(RegularText),
            typeof(GameObject),
            typeof(string),
            typeof(int),
            typeof(float),
            typeof(double),
            typeof(bool),
            typeof(Vector2),
            typeof(Vector3),
            typeof(Vector4),
            typeof(Vector2Int),
            typeof(Vector3Int),
            typeof(Color),
            typeof(List<int>),
            typeof(List<float>),
            typeof(List<double>),
            typeof(List<bool>),
            typeof(List<string>),
            typeof(List<GameObject>),
            typeof(List<Vector2>),
            typeof(List<Vector3>),
            typeof(List<Vector4>),
            typeof(List<Vector2Int>),
            typeof(List<Vector3Int>),
            typeof(List<Color>)
        };

        // Todo: Check if we're using the correct icon here. Also this should be moved to a global UI Utils class later.
        public static Texture2D GetBehaviorGraphIcon()
        {
            return ResourceLoadAPI.Load<Texture2D>("Packages/com.unity.behavior/Editor/Icons/GraphAssetDark@2x.png");
        }

        public static IEnumerable<Type> GetSupportedTypes() =>
            s_StaticallySupportedTypes.Concat(GetEnumVariableTypes());

        private static bool IsCapitalCharacter(char character)
        {
            return character >= 'A' && character <= 'Z';
        }

        private static bool IsNumberCharacter(char character)
        {
            return character >= '0' && character <= '9';
        }

        public static string NicifyVariableName(string input, bool detectAbbreviation = false)
        {
            System.Text.StringBuilder output = new System.Text.StringBuilder();
            char[] inputArray = input.ToCharArray();
            int startIndex = 0;

            if (inputArray.Length > 1 && inputArray[0] == 'm' && input[1] == '_')
                startIndex += 2;

            if (inputArray.Length > 1 && inputArray[0] == 'k' && inputArray[1] >= 'A' && inputArray[1] <= 'Z')
                startIndex += 1;

            if (inputArray.Length > 0 && inputArray[0] >= 'a' && inputArray[0] <= 'z')
                inputArray[0] -= (char)('a' - 'A');

            for (int i = startIndex; i < inputArray.Length; ++i)
            {
                if (inputArray[i] == '_')
                {
                    output.Append(' ');
                    continue;
                }
                if (IsCapitalCharacter(inputArray[i]))
                {
                    bool IsAbbreviation()
                    {
                        return i > 0 && output[output.Length - 1] != ' ' &&
                            !(IsCapitalCharacter(inputArray[i - 1]) || IsNumberCharacter(inputArray[i - 1]));
                    }
                    if (!detectAbbreviation || IsAbbreviation())
                    {
                        output.Append(' ');
                    }
                }
                output.Append(inputArray[i]);
            }

            return output.ToString().TrimStart(' ');
        }

#if UNITY_EDITOR
        public static BehaviorAuthoringGraph[] GetBehaviorGraphAssets()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(BehaviorAuthoringGraph).FullName}");
            BehaviorAuthoringGraph[] assets = new BehaviorAuthoringGraph[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                assets[i] = AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(path);
            }

            return assets;
        }
#endif
        
#if UNITY_EDITOR
        public static BehaviorBlackboardAuthoringAsset[] GetNonGraphBlackboardAssets()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(BlackboardAsset).FullName}");
            BehaviorBlackboardAuthoringAsset[] assets = new BehaviorBlackboardAuthoringAsset[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                assets[i] = AssetDatabase.LoadAssetAtPath<BehaviorBlackboardAuthoringAsset>(path);
            }

            List<BehaviorBlackboardAuthoringAsset> blackboardAssets = new List<BehaviorBlackboardAuthoringAsset>();
            foreach (BehaviorBlackboardAuthoringAsset asset in assets)
            {
                if (AssetDatabase.IsMainAsset(asset))
                {
                    blackboardAssets.Add(asset);
                }
            }

            return blackboardAssets.ToArray();
        }
#endif
        
        public static Dictionary<string, Type> GetVariableSuggestions(GraphAsset asset, PlaceholderNodeModel placeholderNodeModel = null)
        {
            Dictionary<string, Type> variables = new Dictionary<string, Type>();
            List<Type> supportedTypes = GetSupportedTypes().ToList();
            foreach (VariableModel variableModel in asset.Blackboard.Variables.Where(variableModel => supportedTypes.Contains(variableModel.Type)))
            {
                variables[variableModel.Name.ToLower()] = variableModel.Type;
            }

            if (placeholderNodeModel?.Variables != null)
            {
                foreach (VariableInfo variable in placeholderNodeModel.Variables)
                {
                    if (variable.Type != null)
                    {
                        variables[variable.Name.ToLower()] = variable.Type;
                    }
                }
            }

            return variables;
        }

        public static DataType GetVariableValueCopy<DataType>(DataType variableData)
        {
            if (variableData is ValueType || variableData == null)
            {
                return variableData;
            }

            Type type = variableData.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type itemType = variableData.GetType().GetGenericArguments().First();
                IList copiedList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));

                foreach (var item in (IEnumerable)variableData)
                {
                    copiedList.Add(item);
                }
                return (DataType)copiedList;
            }
            return variableData;
        }

        public static bool IsPathRelativeToProjectAssets(string path)
        {
            path = path.Replace("\\", "/").Replace("//", "/");
            return path.StartsWith(Application.dataPath) && Directory.Exists(path);
        }

        public static string GetAbsolutePathToProjectAssets(string path, bool makeDirectories = true)
        {
            path = path.Replace("\\", "/").Replace("//", "/");
            if (!path.StartsWith(Application.dataPath))
            {
                path = $"{Application.dataPath}/{path}";
            }
            if (!Directory.Exists(path) && makeDirectories)
            {
                MakeFolderPath(path);
            }
            return Directory.Exists(path) ? path : Application.dataPath; // Making the directories may still fail. Make sure the path exists.
        }

        public static void MakeFolderPath(string path)
        {
#if UNITY_EDITOR
            path = MakePathRelativeToProject(path);
            string[] directories = path.Split('/');
            string currentDirectory = "Assets";
            foreach (string directory in directories)
            {
                if (!AssetDatabase.IsValidFolder($"{currentDirectory}/{directory}"))
                {
                    AssetDatabase.CreateFolder(currentDirectory, directory);
                }
                currentDirectory = $"{currentDirectory}/{directory}";
            }
#endif
        }

        public static string MakePathRelativeToProject(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "";
            }
            path = path.Replace("\\", "/").Replace("//", "/");
            bool statsWithDataPath = path.StartsWith(Application.dataPath);
            if (statsWithDataPath)
            {
                if (path.Length == Application.dataPath.Length) {

                    return "";
                }
                path = path.Remove(0, Application.dataPath.Length + 1);
            }
            return path;
        }

        public static bool IsNodeInPackageRuntimeAssembly(NodeInfo info)
        {
            return info.Type.Assembly == typeof(Action).Assembly;
        }

        public static IEnumerable<Type> GetEnumVariableTypes()
        {
#if UNITY_EDITOR
            return UnityEditor.TypeCache.GetTypesWithAttribute<BlackboardEnumAttribute>()
                .Where(type => type.IsEnum && Enum.GetValues(type).Length > 0);
#else
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    // we don't consider empty enum types
                    if (!type.IsEnum || type.GetCustomAttribute<BlackboardEnumAttribute>() == null || Enum.GetValues(type).Length == 0)
                    {
                        continue;
                    }

                    yield return type;
                }
            }
#endif
        }

        public static SearchMenuBuilder CreateBlackboardOptions(Dispatcher dispatcher, VisualElement referenceView, SerializableCommandBuffer buffer)
        {
            SearchMenuBuilder builder = new SearchMenuBuilder();

            void CreateVariableFromMenuAction(string variableTypeName, Type type)
            {
                dispatcher.DispatchImmediate(new CreateVariableCommand(variableTypeName, BlackboardUtils.GetVariableModelTypeForType(type)));
            }

            List<BlackboardOption> blackboardTypes = BlackboardRegistry.GetDefaultBlackboardOptions();

            foreach (BlackboardOption blackboardType in blackboardTypes)
            {
                builder.Add(blackboardType.Path, () => CreateVariableFromMenuAction(blackboardType.Name, blackboardType.Type), blackboardType.Icon, priority: blackboardType.Priority);
            }

            // Enums menu
#if UNITY_EDITOR
            builder.Add($"Enumeration/Create new enum type...", onSelected: () => OnCreateNewEnum(referenceView, buffer), priority: 1);
#endif
            foreach (Type enumType in GetEnumVariableTypes())
            {
                builder.Add($"Enumeration/{enumType.Name}", onSelected: () => dispatcher.DispatchImmediate(new CreateVariableCommand(enumType.Name, BlackboardUtils.GetVariableModelTypeForType(enumType))));
            }

            // Event channels menu
#if UNITY_EDITOR
            builder.Add($"Events/Create new event channel type...", onSelected: () => OnCreateNewEventChannel(referenceView, buffer), priority: 1);
#endif
            foreach (EventChannelUtility.EventChannelInfo channelInfo in EventChannelUtility.GetEventChannelTypes())
            {
                builder.Add($"Events/{channelInfo.Name}", iconName: "enum", onSelected: () => dispatcher.DispatchImmediate(new CreateVariableCommand(channelInfo.Name, channelInfo.VariableModelType)));
            }

            builder.DefaultTabName = "Common";

            List<BlackboardOption> customTypes = BlackboardRegistry.GetCustomTypes();
            builder.Add($"Other", priority: -1);
            foreach (var customType in customTypes)
            {
                builder.Add($"Other/{customType.Path}", () => CreateVariableFromMenuAction(customType.Name, customType.Type), customType.Icon, priority: 0);
            }

            return builder;
        }
        
        private static void OnCreateNewEventChannel(VisualElement target, SerializableCommandBuffer buffer)
        {
#if UNITY_EDITOR
            Dictionary<string, Type> variableSuggestions = null;
            // Only add suggestions when creating a new event from the graph editor.
            if (target is GraphEditor graphEditor)
            {
                variableSuggestions = GetVariableSuggestions(graphEditor.Asset);
            }
            EventChannelWizard wizard = EventChannelWizardWindow.GetAndShowWindow(target, variableSuggestions);
            wizard.OnEventChannelTypeCreated += data =>
            {
                OnEventChannelTypeCreated(data, buffer);
            };
#endif
        }

#if UNITY_EDITOR 
        private static void OnEventChannelTypeCreated(EventChannelGeneratorUtility.EventChannelData eventChannelData, SerializableCommandBuffer buffer)
        {
            buffer.SerializeDeferredCommand(new CreateVariableFromSerializedTypeCommand(eventChannelData.ClassName, true));
        }
#endif

        private static void OnCreateNewEnum(VisualElement target, SerializableCommandBuffer buffer)
        {
#if UNITY_EDITOR
            WizardStepper stepper = new WizardStepper();
            Modal modal = Modal.Build(target, stepper);
            stepper.WizardAppBar.title = "New Enum Type";
            stepper.CloseButton.clicked += modal.Dismiss;
            EnumWizard wizard = new EnumWizard(modal, stepper);
            wizard.OnEnumTypeCreated += enumClassName =>
            {
                buffer.SerializeDeferredCommand(new CreateVariableFromSerializedTypeCommand(enumClassName, true));
            };
            modal.shown += (_) =>
            {
                wizard.OnShow();
            };
            stepper.Add(wizard);
            modal.Show();
#endif
        }
        
        public static void UpdateLinkFieldBlackboardPrefixes(BaseLinkField linkField)
        {
            if (linkField.Model != null && linkField.Model.Asset != null)
            {
                linkField.LinkedLabelPrefix = GetBlackboardVariablePrefix(linkField.Model.Asset, linkField.LinkedVariable);   
            }
        }

        public static string GetBlackboardVariablePrefix(GraphAsset graphAsset, VariableModel variableModel)
        {
            if (variableModel == null || graphAsset == null || graphAsset is not BehaviorAuthoringGraph authoringGraph)
            {
                return string.Empty;
            }

            for (int index = 0; index < authoringGraph.m_Blackboards.Count; index++)
            {
                BehaviorBlackboardAuthoringAsset blackboard = authoringGraph.m_Blackboards[index];
                foreach (VariableModel variable in blackboard.Variables)
                {
                    if (variable.ID != variableModel.ID)
                    {
                        continue;
                    }

                    // If the variable can be found from a Blackboard, update the linked variable label prefix with the matched Blackboard asset name.
                    return $"{blackboard.name} {BlackboardUtils.GetArrowUnicode()} ";
                }
            }

            // If no matching blackboard is found, set the prefix back to empty.
            return string.Empty;
        }
    }
}