using System.Collections;
using UnityEditor;
using UnityEngine;

namespace Unity.Behavior
{
    static internal class BehaviorGraphDropHandler
    {
        [InitializeOnLoadMethod]
        public static void HookUpDragAndDropHandlers()
        {
            DragAndDrop.AddDropHandler(InspectorDropHandler);
            DragAndDrop.AddDropHandler(HierarchyDropHandler);
        }

        static bool IsValidDragItem()
        {
            return DragAndDrop.objectReferences.Length == 1 && typeof(BehaviorAuthoringGraph).IsAssignableFrom(DragAndDrop.objectReferences[0].GetType());
        }

        static void HandleDropOnObject(GameObject gameObject)
        {
            Undo.RecordObject(gameObject, "Drop Behavior Graph Object");
            BehaviorAuthoringGraph behaviorAuthoringGraph = DragAndDrop.objectReferences[0] as BehaviorAuthoringGraph;
            var behaviorGraphAgent = gameObject.GetComponent<BehaviorGraphAgent>();
            if (behaviorGraphAgent == null)
            {
                behaviorGraphAgent = Undo.AddComponent<BehaviorGraphAgent>(gameObject);
            }
            var runtimeGraph = BehaviorAuthoringGraph.GetOrCreateGraph(behaviorAuthoringGraph);
            if (runtimeGraph.RootGraph == null)
            {
                behaviorAuthoringGraph.BuildRuntimeGraph();
                behaviorAuthoringGraph.SaveAsset();
            }
            if (PrefabUtility.IsPartOfPrefabThatCanBeAppliedTo(gameObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject);
            }
            EditorUtility.SetDirty(gameObject);
            SerializedObject serializedAgent = new SerializedObject(behaviorGraphAgent);
            serializedAgent.FindProperty("m_Graph").boxedValue = runtimeGraph;
            serializedAgent.ApplyModifiedProperties();
        }

        static DragAndDropVisualMode InspectorDropHandler(Object[] targets, bool perform)
        {
            if (!IsValidDragItem())
            {
                return DragAndDropVisualMode.None;
            }

            DragAndDropVisualMode visualMode = DragAndDropVisualMode.None;
            foreach (var target in targets)
            {
                if (target is GameObject gameObject)
                {
                    if (!perform)
                    {
                        return DragAndDropVisualMode.Generic;
                    }

                    // Drag performed. Add the graph to the agents.
                    visualMode = DragAndDropVisualMode.Generic;
                    HandleDropOnObject(gameObject);
                }
            }
            return visualMode;
        }

        static DragAndDropVisualMode HierarchyDropHandler(int dropTargetInstanceID, HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform)
        {
            GameObject targetGameObject = EditorUtility.InstanceIDToObject(dropTargetInstanceID) as GameObject;
            if (!dropMode.HasFlag(HierarchyDropFlags.DropUpon) || !IsValidDragItem() || targetGameObject == null)
            {
                return DragAndDropVisualMode.None;
            }
            if (perform)
            {
                HandleDropOnObject(targetGameObject);
            }
            return DragAndDropVisualMode.Generic;
        }

    }
}