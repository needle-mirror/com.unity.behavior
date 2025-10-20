using System;
using System.Collections.Generic;
using Unity.Behavior.GraphFramework;
using Unity.Properties;
using UnityEngine;
using Status = Unity.Behavior.Node.Status;

namespace Unity.Behavior
{
    /// <summary>
    /// BehaviorGraph holds all the runtime graph instances linked together into a complete behaviour
    /// defined within a BehaviorAuthoringGraph.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    public partial class BehaviorGraph : ScriptableObject, ISerializationCallbackReceiver
    {
        internal static readonly SerializableGUID k_GraphSelfOwnerID = new SerializableGUID(1, 0);

        /// <summary>
        /// The blackboard reference used for accessing variables.
        /// </summary>
        public BlackboardReference BlackboardReference => RootGraph?.BlackboardReference;

        /// <summary>
        /// True if the graph is running, false otherwise.
        /// </summary>
        public bool IsRunning => RootGraph?.Root is { CurrentStatus: Status.Running or Status.Waiting };

        /// <summary>
        /// The set of linked graphs that make up the behaviour.
        /// </summary>
        [SerializeReference]
        internal List<BehaviorGraphModule> Graphs = new();

        /// <summary>
        /// The primary entry point for the behaviour defined by the BehaviorAuthoringGraph.
        /// </summary>
        internal BehaviorGraphModule RootGraph => Graphs.Count > 0 ? Graphs[0] : null;

        [SerializeField, DontCreateProperty]
        internal BehaviorGraphDebugInfo m_DebugInfo;

        [SerializeField]
        private bool m_WasCompileWithPlaceholderNode = false;

        internal bool CompiledWithPlaceholderNode
        {
            get => m_WasCompileWithPlaceholderNode;
            set => m_WasCompileWithPlaceholderNode = value;
        }

        /// <summary>
        /// Begins execution of the behavior graph.
        /// </summary>
        public void Start()
        {
            if (RootGraph?.Root == null)
            {
                return;
            }
            RootGraph.StartNode(RootGraph.Root);
        }

        /// <summary>
        /// Executes one step of the graph.
        /// </summary>
        public void Tick()
        {
            RootGraph?.Tick();
        }

        /// <summary>
        /// Ends the execution of the behavior graph.
        /// </summary>
        public void End()
        {
            if (RootGraph?.Root == null)
            {
                return;
            }
            RootGraph.EndNode(RootGraph.Root);
            foreach (BehaviorGraphModule graphModule in Graphs)
            {
                graphModule.Reset();
            }
        }

        /// <summary>
        /// Resets the execution state and restarts the graph.
        /// </summary>
        public void Restart()
        {
            End();
            Start();
        }

        /// <summary>
        /// Determines whether this <see cref="BehaviorGraph"/> instance and the specified <paramref name="other"/> graph
        /// originate from the same source asset.
        /// </summary>
        /// <param name="other">The other <see cref="BehaviorGraph"/> to compare against.</param>
        /// <returns>True if both graphs have a valid graph module and their AuthoringAssetID values are equal;
        /// False otherwise.</returns>
        public bool HasSameSourceAssetAs(BehaviorGraph other)
        {
            bool areAssetValid = this.RootGraph != null && other != null && other.RootGraph != null;
            return areAssetValid && this.RootGraph.AuthoringAssetID == other.RootGraph.AuthoringAssetID;
        }

        internal void AssignGameObjectToGraphModules(GameObject gameObject)
        {
            if (RootGraph == null)
            {
                return;
            }

            RootGraph.GameObject = gameObject;
            foreach (var graphModule in Graphs)
            {
                graphModule.GameObject = gameObject;
            }
        }

        /// <summary>
        /// Raise OnRuntimeSerialize in each BehaviorGraphModule to notify nodes.
        /// </summary>
        internal void SerializeGraphModules()
        {
            for (int i = Graphs.Count - 1; i >= 0; i--)
            {
                Graphs[i].Serialize();
            }
        }

        /// <summary>
        /// Raise OnRuntimeDeserialize in each BehaviorGraphModule to notify nodes.
        /// </summary>
        internal void DeserializeGraphModules()
        {
            for (int i = Graphs.Count - 1; i >= 0; i--)
            {
                Graphs[i].Deserialize();
            }
        }

        /// <inheritdoc cref="OnBeforeSerialize"/>
        public void OnBeforeSerialize()
        {
        }

        /// <inheritdoc cref="OnAfterDeserialize"/>
        public void OnAfterDeserialize()
        {
#if DEBUG && UNITY_EDITOR
            foreach (BehaviorGraphModule graph in Graphs)
            {
                graph.DebugInfo = m_DebugInfo;
            }
#endif
        }


#if UNITY_EDITOR
        internal bool ContainsPlaceholderNodes()
        {
            if (!UnityEditor.EditorUtility.IsPersistent(this))
            {
                return false;
            }

            bool foundPlaceholderNode = false;
            foreach (var graphModule in Graphs)
            {
                if (graphModule == null)
                {
                    continue;
                }

                ValidateRuntimeNode(graphModule.Root, ref foundPlaceholderNode);
            }

            return foundPlaceholderNode;
        }

        private void ValidateRuntimeNode(Node node, ref bool foundPlaceholderNode)
        {
            if (node == null)
            {
                return;
            }

            switch (node)
            {
                case Action:
                    break;
                case Modifier modifier:
                    ValidateRuntimeNode(modifier.Child, ref foundPlaceholderNode);
                    break;
                case Composite composite:
                    for (int c = composite.Children.Count - 1; c >= 0 ; c--)
                    {
                        if (composite.Children[c] == null)
                        {
                            foundPlaceholderNode = true;
                            return;
                        }
                        else
                        {
                            ValidateRuntimeNode(composite.Children[c], ref foundPlaceholderNode);
                        }
                    }
                    break;
                case Join join:
                    ValidateRuntimeNode(join.Child, ref foundPlaceholderNode);
                    break;
            }
        }
#endif
    }
}
