using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Behavior.GraphFramework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Unity.Behavior
{
    [CustomEditor(typeof(BehaviorGraphAgent), editorForChildClasses: true)]
    [CanEditMultipleObjects]
    internal class BehaviorGraphAgentEditor : Editor
    {
        private readonly List<BehaviorGraphAgent> m_TargetAgents = new();
        private bool m_ShowBlackboard = true;
        private readonly Dictionary<SerializableGUID, bool> m_ListVariableFoldoutStates = new Dictionary<SerializableGUID, bool>();
        private readonly Dictionary<SerializableGUID, VariableModel> m_VariableGUIDToVariableModel = new Dictionary<SerializableGUID, VariableModel>();
        private long m_MappedBlackboardVersion = 0;
        private BehaviorGraph m_SharedGraph;
        bool m_ModificationsMade = false;

        private BehaviorGraphAgent m_TargetAgent;
        private SerializableGUID m_CurrentGraphID;
        private SerializedProperty m_GraphProperty;

        private BehaviorGraph SharedGraph
        {
            get => m_SharedGraph;
            set
            {
                if (ReferenceEquals(m_SharedGraph, value)) return;
                m_SharedGraph = value;
                if (m_SharedGraph == null)
                {
                    SharedAuthoringGraph = null;
                    return;
                }
                string assetPath = AssetDatabase.GetAssetPath(m_SharedGraph);
                if (string.IsNullOrEmpty(assetPath))
                {
                    if (BehaviorGraphAssetRegistry.TryGetAssetFromId(m_SharedGraph.RootGraph.AuthoringAssetID, out var sharedAuthoringGraph))
                    {
                        SharedAuthoringGraph = sharedAuthoringGraph;
                    }
                    return;
                }
                SharedAuthoringGraph = AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(assetPath);
            }
        }

        private BehaviorAuthoringGraph m_SharedAuthoringGraph;

        private BehaviorAuthoringGraph SharedAuthoringGraph
        {
            get => m_SharedAuthoringGraph;
            set
            {
                m_SharedAuthoringGraph = value;
                UpdateVariableModelMap();
            }
        }

        private void UpdateVariableModelMap()
        {
            if (SharedAuthoringGraph == null)
            {
                m_VariableGUIDToVariableModel.Clear();
                return;
            }

            if (m_VariableGUIDToVariableModel.Count > 0 && m_MappedBlackboardVersion == SharedAuthoringGraph.VersionTimestamp)
            {
                return;
            }

            m_VariableGUIDToVariableModel.Clear();
            m_MappedBlackboardVersion = SharedAuthoringGraph.VersionTimestamp;
            foreach (var variableModel in SharedAuthoringGraph.Blackboard.Variables)
            {
                m_VariableGUIDToVariableModel.Add(variableModel.ID, variableModel);
            }
        }

        private void OnEnable()
        {
            if (target == null)
            {
                return;
            }

            // Update the target agents and check for deleted runtime graph assets.
            m_TargetAgents.Clear();
            foreach (UnityEngine.Object objTarget in targets)
            {
                var targetAgent = objTarget as BehaviorGraphAgent;
                if (targetAgent == null) continue;

                m_TargetAgents.Add(targetAgent);
                UpdateBehaviorGraphIfNeeded(targetAgent);
            }

            // We need the agent in order to set the Graph property in case of playmode assignment. 
            m_TargetAgent = target as BehaviorGraphAgent;
            m_GraphProperty = serializedObject.FindProperty("m_Graph");
            Debug.Assert(m_GraphProperty != null);
            var graph = m_GraphProperty.objectReferenceValue as BehaviorGraph;
            m_CurrentGraphID = graph != null && graph.RootGraph != null ? graph.RootGraph.AuthoringAssetID : default;
        }

        private void FindSharedGraph()
        {
            // Use the first target to check for mixed values
            SharedGraph = m_GraphProperty.hasMultipleDifferentValues ? null : m_GraphProperty.objectReferenceValue as BehaviorGraph;
        }

        private DataType GetVariableDataCopy<DataType>(BlackboardVariable<DataType> blackboardVariable)
        {
            if (blackboardVariable.ObjectValue is ValueType)
            {
                return blackboardVariable.Value;
            }
            return Util.GetVariableValueCopy(blackboardVariable.Value);
        }

        private readonly string[] kPropertiesToExclude = new string[]
        {
            "m_Script",
            "m_Graph",
            "NetcodeRunOnlyOnOwner"
        };


        public override void OnInspectorGUI()
        {
            DrawPropertiesExcluding(serializedObject, kPropertiesToExclude);
            // Draw the graph field. If a new runtime graph is assigned, set the graph on the target and mark it dirty.
            EditorGUILayout.PropertyField(m_GraphProperty, new GUIContent("Behavior Graph"));
            DetectAssetDragDrop();

#if NETCODE_FOR_GAMEOBJECTS
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(BehaviorGraphAgent.NetcodeRunOnlyOnOwner)), new GUIContent("Netcode: Run only on Owner"));
#endif
            serializedObject.ApplyModifiedProperties();

            // If the change was made in playmode, make sure the affected agent are calling the proper callbacks.
            m_ModificationsMade |= DetectRuntimeGraphAssignment();

            FindSharedGraph();
            
            // Update overrides list before drawing the blackboard.
            foreach (BehaviorGraphAgent targetAgent in m_TargetAgents)
            {
                if (targetAgent.BlackboardReference != null)
                {
                    targetAgent.SynchronizeOverridesWithBlackboard();
                }
            }

            // Draw a blackboard only if all agents share the same graph and a blackboard for it exists.
            if (SharedGraph != null && SharedGraph.BlackboardReference?.Blackboard != null)
            {
                UpdateVariableModelMap();
                DrawBlackboard(SharedGraph.BlackboardReference.Blackboard.Variables);
            }

            if (m_ModificationsMade)
            {
                serializedObject.Update();
                m_ModificationsMade = false;
            }
        }

        private bool DetectRuntimeGraphAssignment()
        {
            if (!Application.isPlaying)
            {
                return false;
            }

            var graph = m_GraphProperty.objectReferenceValue as BehaviorGraph;
            if (graph != null && m_CurrentGraphID != graph.RootGraph.AuthoringAssetID)
            {
                m_CurrentGraphID = graph.RootGraph.AuthoringAssetID;
                // If we change the graph at runtime, there is a chain of callback that need to be triggered.
                m_TargetAgent.Graph = graph;
                return true;
            }
            else if (graph == null)
            {
                m_CurrentGraphID = default;
                m_TargetAgent.Graph = null;
                return true;
            }

            return false;
        }

        private bool DetectAssetDragDrop()
        {
            var lastRect = GUILayoutUtility.GetLastRect();
            EventType eventType = Event.current.type;
            if (lastRect.Contains(Event.current.mousePosition) &&
                (eventType == EventType.DragUpdated || eventType == EventType.DragPerform))
            {
                if (DragAndDrop.objectReferences.Length == 1 && typeof(BehaviorAuthoringGraph).IsAssignableFrom(DragAndDrop.objectReferences[0].GetType()))
                {
                    BehaviorAuthoringGraph authoringGraph = (BehaviorAuthoringGraph)DragAndDrop.objectReferences[0];
                    Event.current.Use();
                    DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
                    if (eventType == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        var runtimeGraph = BehaviorAuthoringGraph.GetOrCreateGraph(authoringGraph);
                        if (runtimeGraph?.RootGraph == null)
                        {
                            authoringGraph.BuildRuntimeGraph();
                        }
                        m_GraphProperty.boxedValue = runtimeGraph;
                        SharedGraph = runtimeGraph;
                        return true;
                    }
                }
            }
            return false;
        }

        private bool IsVariablePublic(BlackboardVariable variable)
        {
            if (m_VariableGUIDToVariableModel.TryGetValue(variable.GUID, out VariableModel variableModel))
            {
                return variableModel.IsExposed;
            }
            // Couldn't find the variable model! If the shared authoring graph is null, continue as if it's public.
            return SharedAuthoringGraph == null;
        }

        private void DrawBlackboard(IEnumerable<BlackboardVariable> variables)
        {
            m_ShowBlackboard = EditorGUILayout.Foldout(m_ShowBlackboard, "Blackboard Variables");
            if (!m_ShowBlackboard)
            {
                return;
            }

            EditorGUI.indentLevel++;
            foreach (BlackboardVariable variable in variables)
            {
                if (!IsVariablePublic(variable))
                {
                    continue;
                }
                EditorGUI.showMixedValue = false;
                bool isOverride = false;
                BlackboardVariable firstTargetVariable = null;
                foreach (BehaviorGraphAgent targetAgent in m_TargetAgents)
                {
                    BlackboardVariable targetVariable = GetTargetVariable(targetAgent, variable.GUID);
                    firstTargetVariable ??= targetVariable;
                    if (targetVariable == null)
                    {
                        return;
                    }

                    if (targetVariable.Name != variable.Name)
                    {
                        // The variable override exists, but its name may not be current if renamed in the asset.
                        targetVariable.Name = variable.Name;
                        if (PrefabUtility.IsPartOfPrefabThatCanBeAppliedTo(targetAgent))
                        {
                            PrefabUtility.RecordPrefabInstancePropertyModifications(targetAgent);
                        }
                        EditorUtility.SetDirty(targetAgent);
                        serializedObject.Update();
                    }

                    // If any target's variable value differs from the first target's, show a mixed value indicator.
                    EditorGUI.showMixedValue |= !firstTargetVariable.ValueEquals(targetVariable);

                    if (targetAgent.m_BlackboardOverrides.ContainsKey(variable.GUID))
                    {
                        // Is the variable we're checking the graph owner and is it set to the target agent?
                        bool isGraphOwnerVariableOverriden = variable.GUID == BehaviorGraph.k_GraphSelfOwnerID && ReferenceEquals(targetAgent.m_BlackboardOverrides[variable.GUID].ObjectValue, targetAgent.gameObject);
                        isOverride = !isGraphOwnerVariableOverriden;
                    }
                }

                DrawFieldForBlackboardVariable(firstTargetVariable, isOverride);
            }
            EditorGUI.indentLevel--;
        }

        private void DrawFieldForBlackboardVariable(BlackboardVariable variable, bool isOverride)
        {
            string varName = isOverride ? $"{variable.Name} (Override)" : variable.Name;
            GUIContent label = isOverride ? new GUIContent(varName, "The value of this variable has been changed from the value set on the graph asset.") : new GUIContent(varName);
            Type type = variable.Type;

            if (type == typeof(float) && variable is BlackboardVariable<float> floatVariable)
            {
                float value = floatVariable.Value;
                EditorGUI.BeginChangeCheck();
                value = EditorGUILayout.FloatField(label, value);
                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type == typeof(List<float>) && variable is BlackboardVariable<List<float>> floatListVariable)
            {
                List<float> value = GetVariableDataCopy(floatListVariable);
                EditorGUI.BeginChangeCheck();
                ReorderableList reorderableList = CreateVariableListElement(value, variable, varName);
                reorderableList.drawElementCallback = (rect, index, _, _) =>
                {
                    rect.height = EditorGUIUtility.singleLineHeight;
                    value[index] = EditorGUI.FloatField(rect, $"Element {index}", value[index]);
                    ShowContextMenuForVariable(variable.GUID, isOverride);
                };
                if (m_ListVariableFoldoutStates[variable.GUID])
                {
                    reorderableList.DoLayoutList();
                }

                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type == typeof(double) && variable is BlackboardVariable<double> doubleVariable)
            {
                double value = doubleVariable.Value;
                EditorGUI.BeginChangeCheck();
                value = EditorGUILayout.DoubleField(label, value);
                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type == typeof(List<double>) && variable is BlackboardVariable<List<double>> doubleListVariable)
            {
                List<double> value = GetVariableDataCopy(doubleListVariable);
                EditorGUI.BeginChangeCheck();
                ReorderableList reorderableList = CreateVariableListElement(value, variable, varName);
                reorderableList.drawElementCallback = (rect, index, _, _) =>
                {
                    rect.height = EditorGUIUtility.singleLineHeight;
                    value[index] = EditorGUI.DoubleField(rect, $"Element {index}", value[index]);
                    ShowContextMenuForVariable(variable.GUID, isOverride);
                };
                if (m_ListVariableFoldoutStates[variable.GUID])
                {
                    reorderableList.DoLayoutList();
                }

                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type == typeof(int) && variable is BlackboardVariable<int> intVariable)
            {
                int value = intVariable.Value;
                EditorGUI.BeginChangeCheck();
                value = EditorGUILayout.IntField(label, value);
                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type == typeof(List<int>) && variable is BlackboardVariable<List<int>> intListVariable)
            {
                List<int> value = GetVariableDataCopy(intListVariable);
                EditorGUI.BeginChangeCheck();

                ReorderableList reorderableList = CreateVariableListElement(value, variable, varName);
                reorderableList.drawElementCallback = (rect, index, _, _) =>
                {
                    rect.height = EditorGUIUtility.singleLineHeight;
                    value[index] = EditorGUI.IntField(rect, $"Element {index}", value[index]);
                    ShowContextMenuForVariable(variable.GUID, isOverride);
                };
                if (m_ListVariableFoldoutStates[variable.GUID])
                {
                    reorderableList.DoLayoutList();
                }

                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type == typeof(bool) && variable is BlackboardVariable<bool> boolVariable)
            {
                bool value = boolVariable.Value;
                EditorGUI.BeginChangeCheck();
                value = EditorGUILayout.Toggle(label, value);
                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type == typeof(List<bool>) && variable is BlackboardVariable<List<bool>> boolListVariable)
            {
                List<bool> value = GetVariableDataCopy(boolListVariable);
                EditorGUI.BeginChangeCheck();
                ReorderableList reorderableList = CreateVariableListElement(value, variable, varName);
                reorderableList.drawElementCallback = (rect, index, _, _) =>
                {
                    rect.height = EditorGUIUtility.singleLineHeight;
                    value[index] = EditorGUI.Toggle(rect, $"Element {index}", value[index]);
                    ShowContextMenuForVariable(variable.GUID, isOverride);
                };
                if (m_ListVariableFoldoutStates[variable.GUID])
                {
                    reorderableList.DoLayoutList();
                }

                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type == typeof(string) && variable is BlackboardVariable<string> stringVariable)
            {
                string value = stringVariable.Value;
                EditorGUI.BeginChangeCheck();
                value = EditorGUILayout.TextField(label, value);
                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type == typeof(List<string>) && variable is BlackboardVariable<List<string>> stringListVariable)
            {
                List<string> value = GetVariableDataCopy(stringListVariable);
                EditorGUI.BeginChangeCheck();
                ReorderableList reorderableList = CreateVariableListElement(value, variable, varName);
                reorderableList.drawElementCallback = (rect, index, _, _) =>
                {
                    rect.height = EditorGUIUtility.singleLineHeight;
                    value[index] = EditorGUI.TextField(rect, $"Element {index}", value[index]);
                    ShowContextMenuForVariable(variable.GUID, isOverride);
                };
                if (m_ListVariableFoldoutStates[variable.GUID])
                {
                    reorderableList.DoLayoutList();
                }

                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type == typeof(Color) && variable is BlackboardVariable<Color> colorVariable)
            {
                Color value = colorVariable.Value;
                EditorGUI.BeginChangeCheck();
                value = EditorGUILayout.ColorField(label, value);
                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type == typeof(List<Color>) && variable is BlackboardVariable<List<Color>> colorListVariable)
            {
                List<Color> value = GetVariableDataCopy(colorListVariable);
                EditorGUI.BeginChangeCheck();
                ReorderableList reorderableList = CreateVariableListElement(value, variable, varName);
                reorderableList.drawElementCallback = (rect, index, _, _) =>
                {
                    rect.height = EditorGUIUtility.singleLineHeight;
                    value[index] = EditorGUI.ColorField(rect, $"Element {index}", value[index]);
                    ShowContextMenuForVariable(variable.GUID, isOverride);
                };
                if (m_ListVariableFoldoutStates[variable.GUID])
                {
                    reorderableList.DoLayoutList();
                }

                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type == typeof(Vector4) && variable is BlackboardVariable<Vector4> vec4Variable)
            {
                Vector4 value = vec4Variable.Value;
                EditorGUI.BeginChangeCheck();
                value = EditorGUILayout.Vector4Field(label, value);
                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type == typeof(List<Vector4>) && variable is BlackboardVariable<List<Vector4>> vec4ListVariable)
            {
                List<Vector4> value = GetVariableDataCopy(vec4ListVariable);
                EditorGUI.BeginChangeCheck();
                ReorderableList reorderableList = CreateVariableListElement(value, variable, varName);
                reorderableList.drawElementCallback = (rect, index, _, _) =>
                {
                    rect.height = EditorGUIUtility.singleLineHeight;
                    value[index] = EditorGUI.Vector4Field(rect, $"Element {index}", value[index]);
                    ShowContextMenuForVariable(variable.GUID, isOverride);
                };
                if (m_ListVariableFoldoutStates[variable.GUID])
                {
                    reorderableList.DoLayoutList();
                }

                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type == typeof(Vector3) && variable is BlackboardVariable<Vector3> vec3Variable)
            {
                Vector3 value = vec3Variable.Value;
                EditorGUI.BeginChangeCheck();
                value = EditorGUILayout.Vector3Field(label, value);
                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type == typeof(List<Vector3>) && variable is BlackboardVariable<List<Vector3>> vec3ListVariable)
            {
                List<Vector3> value = GetVariableDataCopy(vec3ListVariable);
                EditorGUI.BeginChangeCheck();
                ReorderableList reorderableList = CreateVariableListElement(value, variable, varName);
                reorderableList.drawElementCallback = (rect, index, _, _) =>
                {
                    rect.height = EditorGUIUtility.singleLineHeight;
                    value[index] = EditorGUI.Vector3Field(rect, $"Element {index}", value[index]);
                    ShowContextMenuForVariable(variable.GUID, isOverride);
                };
                if (m_ListVariableFoldoutStates[variable.GUID])
                {
                    reorderableList.DoLayoutList();
                }

                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type == typeof(Vector2) && variable is BlackboardVariable<Vector2> vec2Variable)
            {
                Vector2 value = vec2Variable.Value;
                EditorGUI.BeginChangeCheck();
                value = EditorGUILayout.Vector2Field(label, value);
                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type == typeof(List<Vector4>) && variable is BlackboardVariable<List<Vector4>> vec2ListVariable)
            {
                List<Vector4> value = GetVariableDataCopy(vec2ListVariable);
                EditorGUI.BeginChangeCheck();
                ReorderableList reorderableList = CreateVariableListElement(value, variable, varName);
                reorderableList.drawElementCallback = (rect, index, _, _) =>
                {
                    rect.height = EditorGUIUtility.singleLineHeight;
                    value[index] = EditorGUI.Vector2Field(rect, $"Element {index}", value[index]);
                    ShowContextMenuForVariable(variable.GUID, isOverride);
                };
                if (m_ListVariableFoldoutStates[variable.GUID])
                {
                    reorderableList.DoLayoutList();
                }

                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type == typeof(GameObject) && variable is BlackboardVariable<GameObject> gameObjectVar)
            {
                GameObject value = gameObjectVar.Value;
                EditorGUI.BeginChangeCheck();
                value = EditorGUILayout.ObjectField(label, value, typeof(GameObject), true) as GameObject;
                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type == typeof(List<GameObject>) && variable is BlackboardVariable<List<GameObject>> gameObjectListVariable)
            {
                List<GameObject> value = GetVariableDataCopy(gameObjectListVariable);
                EditorGUI.BeginChangeCheck();
                ReorderableList reorderableList = CreateVariableListElement(value, variable, varName);
                reorderableList.drawElementCallback = (rect, index, _, _) =>
                {
                    rect.height = EditorGUIUtility.singleLineHeight;
                    value[index] = EditorGUI.ObjectField(rect, $"Element {index}", value[index], typeof(GameObject), true) as GameObject;
                    ShowContextMenuForVariable(variable.GUID, isOverride);
                };
                if (m_ListVariableFoldoutStates[variable.GUID])
                {
                    reorderableList.DoLayoutList();
                }

                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type.IsSubclassOf(typeof(ScriptableObject)))
            {
                ScriptableObject value = (ScriptableObject)variable.ObjectValue;
                EditorGUI.BeginChangeCheck();
                value = EditorGUILayout.ObjectField(label, value, type, false) as ScriptableObject;
                if (EditorGUI.EndChangeCheck())
                {
                    ValidateTypeAndUpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type == typeof(List<ScriptableObject>) && variable is BlackboardVariable<List<ScriptableObject>> scriptableObjectListVariable)
            {
                List<ScriptableObject> value = GetVariableDataCopy(scriptableObjectListVariable);
                EditorGUI.BeginChangeCheck();
                ReorderableList reorderableList = CreateVariableListElement(value, variable, varName);
                reorderableList.drawElementCallback = (rect, index, _, _) =>
                {
                    rect.height = EditorGUIUtility.singleLineHeight;
                    value[index] = EditorGUI.ObjectField(rect, $"Element {index}", value[index], typeof(GameObject), true) as ScriptableObject;
                    ShowContextMenuForVariable(variable.GUID, isOverride);
                };
                if (m_ListVariableFoldoutStates[variable.GUID])
                {
                    reorderableList.DoLayoutList();
                }

                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID);
                }
            }
            else if (type.IsSubclassOf(typeof(UnityEngine.Object)) || type == typeof(UnityEngine.Object))
            {
                UnityEngine.Object value = (UnityEngine.Object)variable.ObjectValue;
                EditorGUI.BeginChangeCheck();
                value = EditorGUILayout.ObjectField(label, value, type, true);
                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID, type);
                }
            }
            else if (typeof(Enum).IsAssignableFrom(type))
            {
                var value = (Enum)variable.ObjectValue;
                EditorGUI.BeginChangeCheck();
                value = EditorGUILayout.EnumPopup(label, value);
                if (EditorGUI.EndChangeCheck())
                {
                    UpdateValueIfChanged(value, variable.GUID, type);
                }
            }
            ShowContextMenuForVariable(variable.GUID, isOverride);
        }

        private ReorderableList CreateVariableListElement<T>(List<T> value, BlackboardVariable variable, string name)
        {
            m_ListVariableFoldoutStates.TryAdd(variable.GUID, false);
            m_ListVariableFoldoutStates[variable.GUID] = EditorGUILayout.Foldout(m_ListVariableFoldoutStates[variable.GUID], name);
            ReorderableList reorderableList = new ReorderableList(value, typeof(int))
            {
                draggable = false,
                displayAdd = true,
                displayRemove = true,
                elementHeight = EditorGUIUtility.singleLineHeight,
                headerHeight = 0
            };
            reorderableList.onAddCallback = _ =>
            {
                Undo.RecordObjects(targets, "Add List Element");
                value.Add(default);
            };
            reorderableList.onRemoveCallback = _ =>
            {
                Undo.RecordObjects(targets, "Remove List Element");
                value.RemoveAt(value.Count - 1);
            };

            return reorderableList;
        }

        // This method will update the value stored in the Blackboard if its changed (comparing using value), using the generic type of the value given.
        // This should be used for all value types whose data type matches their variable type.
        private void UpdateValueIfChanged<DataType>(DataType currentValue, SerializableGUID varID)
        {
            foreach (BehaviorGraphAgent targetAgent in m_TargetAgents)
            {
                BlackboardVariable<DataType> targetVariable = (BlackboardVariable<DataType>)GetTargetVariable(targetAgent, varID);
                if (EqualityComparer<DataType>.Default.Equals(currentValue, targetVariable.Value))
                {
                    continue; // this one
                }
                SetBlackboardVariableValue(targetAgent, targetVariable, currentValue);
            }
        }

        // This method will update the value stored in the Blackboard if its changed (comparing using value), using the generic type of the value given.
        // This can only be used for list types.
        private void UpdateValueIfChanged<DataType>(List<DataType> currentValue, SerializableGUID varID)
        {
            foreach (BehaviorGraphAgent targetAgent in m_TargetAgents)
            {
                BlackboardVariable<List<DataType>> targetVariable = (BlackboardVariable<List<DataType>>)GetTargetVariable(targetAgent, varID);
                // TODO: Check if LINQ usage here is generating allocs.
                if (currentValue != null && currentValue.SequenceEqual(targetVariable.Value))
                {
                    continue;
                }
                SetBlackboardVariableValue(targetAgent, targetVariable, currentValue);
            }
        }

        // This method will update the value stored in the Blackboard if its changed (comparing using value), using an explicit given type.
        // This should be used for all value types whose data type doesn't match their variable type, as is the case for Enums which Unity EnumField casts to Enum.
        private void UpdateValueIfChanged<DataType>(DataType currentValue, SerializableGUID varID, Type type)
        {
            foreach (BehaviorGraphAgent targetAgent in m_TargetAgents)
            {
                BlackboardVariable targetVariable = GetTargetVariable(targetAgent, varID);
                if (EqualityComparer<DataType>.Default.Equals(currentValue, (DataType)targetVariable.ObjectValue) ||
                             !type.IsInstanceOfType(currentValue))
                {
                    continue;
                }
                SetBlackboardVariableValue(targetAgent, targetVariable, currentValue);
            }
        }

        // This method will update the value stored in the Blackboard if its changed (comparing using reference check), using the variable's type.
        // This should be used for Objects.
        private void ValidateTypeAndUpdateValueIfChanged(object currentValue, SerializableGUID varID)
        {
            foreach (BehaviorGraphAgent targetAgent in m_TargetAgents)
            {
                BlackboardVariable targetVariable = GetTargetVariable(targetAgent, varID);
                if (ReferenceEquals(targetVariable.ObjectValue, currentValue) || !targetVariable.Type.IsInstanceOfType(currentValue) && currentValue != null)
                {
                    continue;
                }
                SetBlackboardVariableValue(targetAgent, targetVariable, currentValue);
            }
        }

        private void UpdateBehaviorGraphIfNeeded(BehaviorGraphAgent targetAgent)
        {
            if (Application.isPlaying)
            {
                return; // Don't update the graph if the application is playing, as the graph instance is a copy.
            }

            var isMainAssetValid = targetAgent.Graph != null;

            if (!isMainAssetValid)
            {
                return;
            }

            // Note: Target.Graph is set to a non-persistent copy when the agent is has been initialized at runtime.
            var isMainAssetPersistent = EditorUtility.IsPersistent(targetAgent.Graph);
            var isRuntimeGraphValid = targetAgent.Graph.RootGraph != null;
            if (isMainAssetPersistent && isRuntimeGraphValid)
            {
                return; // No change needed if the runtime graph of the persistent asset is still valid.
            }

            // At this point, we are dealing with a non persistent asset.
            BehaviorAuthoringGraph asset = null;
            // If root graph is still valid, let's try to get a hold of its referenced asset.
            if (isRuntimeGraphValid)
            {
                BehaviorGraphAssetRegistry.TryGetAssetFromId(targetAgent.Graph.RootGraph.AuthoringAssetID, out asset);
            }

            // If the graph isn't set, the asset contains no data and the asset link cannot be updated.
            // Likewise, if the asset reference is null, the asset has been deleted and the link cannot be updated.
            if (asset == null)
            {
                Debug.LogWarning($"Behavior graph reference lost on {targetAgent}.", targetAgent);
                targetAgent.Graph = null;
                if (PrefabUtility.IsPartOfPrefabThatCanBeAppliedTo(targetAgent))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(targetAgent);
                }

                EditorUtility.SetDirty(targetAgent);
                return;
            }

            // Destroy the temporary runtime graph instance.
            DestroyImmediate(targetAgent.Graph);

            // Try to update the reference through the authoring asset.
            // Note: asset.GetOrCreateGraph() would create a new runtime graph if one does not exist,
            // which is not desirable here. If no runtime graph exists, null should be assigned.
            string assetPath = AssetDatabase.GetAssetPath(asset);
            targetAgent.Graph = AssetDatabase.LoadAssetAtPath<BehaviorGraph>(assetPath);
            if (PrefabUtility.IsPartOfPrefabThatCanBeAppliedTo(targetAgent))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(targetAgent);
            }
            EditorUtility.SetDirty(targetAgent);
        }

        private BlackboardVariable GetTargetVariable(BehaviorGraphAgent agent, SerializableGUID variableID)
        {
            // If the application is playing, use the runtime graph's blackboard.
            agent.Graph.BlackboardReference.GetVariable(variableID, out BlackboardVariable runtimeVariableInstance);
            if (Application.isPlaying && agent.m_IsInitialised)
            {
                return runtimeVariableInstance;
            }

            // Otherwise, use the override variable if available.
            if (agent.m_BlackboardOverrides.TryGetValue(variableID, out BlackboardVariable overrideVariable))
            {
                return overrideVariable;
            }
            return runtimeVariableInstance;
        }

        private void SetBlackboardVariableValue<DataType>(BehaviorGraphAgent agent, BlackboardVariable refVariable, DataType newValue)
        {
            if (Application.isPlaying)
            {
                // Set behavior changes based on the agent state (init or not)
                agent.SetVariableValue(refVariable.GUID, newValue);
                return;
            }

            Undo.RecordObject(agent, "Set Blackboard Variable Value");
            if (agent.m_BlackboardOverrides.TryGetValue(refVariable.GUID, out BlackboardVariable overrideVariable))
            {
                overrideVariable.SetObjectValueWithoutNotify(newValue);
            }
            else
            {
                overrideVariable = refVariable.Duplicate();
                overrideVariable.SetObjectValueWithoutNotify(newValue);
                agent.m_BlackboardOverrides.Add(refVariable.GUID, overrideVariable);
                agent.m_BlackboardVariableOverridesList.Add(overrideVariable);
            }
            if (PrefabUtility.IsPartOfPrefabThatCanBeAppliedTo(agent))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(agent);
            }
            EditorUtility.SetDirty(agent);
            m_ModificationsMade = true;
        }

        private void ShowContextMenuForVariable(SerializableGUID guid, bool isOverride)
        {
            if (!isOverride) return;

            var lastRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.ContextClick)
            {
                if (lastRect.Contains(Event.current.mousePosition))
                {
                    Event.current.Use();
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Revert Variable"), false, () => ResetVariable(guid));
                    menu.ShowAsContext();
                }
            }
        }

        private void ResetVariable(SerializableGUID guid)
        {
            foreach (BehaviorGraphAgent targetAgent in m_TargetAgents)
            {
                if (targetAgent.m_BlackboardOverrides.ContainsKey(guid))
                {
                    Undo.RecordObject(targetAgent, "Reset Blackboard Variable");
                    if (guid == BehaviorGraph.k_GraphSelfOwnerID)
                    {
                        targetAgent.m_BlackboardOverrides[guid].ObjectValue = targetAgent.gameObject;
                    }
                    else
                    {
                        targetAgent.m_BlackboardOverrides.Remove(guid);
                    }
                    if (PrefabUtility.IsPartOfPrefabThatCanBeAppliedTo(targetAgent))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(targetAgent);
                    }
                    EditorUtility.SetDirty(targetAgent);
                }
            }
            serializedObject.Update();
        }
    }
}