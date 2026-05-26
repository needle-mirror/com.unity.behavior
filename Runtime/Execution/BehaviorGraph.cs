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
    [HelpURL(DocumentationUrls.BehaviorGraphAsset)]
    public partial class BehaviorGraph : ScriptableObject
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
        /// Current execution status of the root node.
        /// Returns <see cref="Node.Status.Uninitialized"/> when the graph has no root node.
        /// </summary>
        internal Status CurrentStatus => RootGraph?.Root?.CurrentStatus ?? Status.Uninitialized;

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

        // Ensures SBBV callbacks are only active while the graph is running.
        private bool m_AreSharedCallbacksRegistered;

        /// <summary>
        /// Begins execution of the behavior graph.
        /// Shared blackboard variable callbacks are registered here so they are only
        /// active while the graph is running.
        /// </summary>
        public void Start()
        {
            if (RootGraph?.Root == null)
            {
                return;
            }
            RegisterSharedVariableCallbacks();
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
        /// Shared blackboard variable callbacks are unregistered here so stopped graphs
        /// do not react to shared variable changes.
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
            
            UnregisterSharedVariableCallbacks();
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

#region Graph Instance Lifecycle
        ////////////////////////////////////////////////////////////////////////////////////////
        // Keep BehaviorGraphAgent and RunSubgraphDynamic graph instance lifecycles in sync.
        ////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Acquires and initializes a new graph instance from the source graph.
        /// The instance is initialized but not yet running. Call <see cref="Start"/> to begin execution.
        /// The owner is responsible for releasing and nullifying its reference when the instance is no longer needed.
        /// </summary>
        /// <param name="owner">The game object that owns the graph instance.</param>
        /// <param name="sourceGraph">The source graph to clone (deep copy).</param>
        /// <returns>The newly instantiated graph instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the provided source graph is null.</exception>
        internal static BehaviorGraph AcquireInstance(GameObject owner, 
#if UNITY_EDITOR
            [System.Diagnostics.CodeAnalysis.DisallowNull] 
#endif
            BehaviorGraph sourceGraph)
        {
            if (sourceGraph == null)
            {
                throw new ArgumentNullException(nameof(sourceGraph), "Source graph cannot be null.");
            }

            var instance = ScriptableObject.Instantiate(sourceGraph);
            instance.InitializeInstance(owner);
            return instance;
        }

        /// <summary>
        /// Acquires and initializes a new graph instance from serialized data, restoring its runtime state.
        /// The instance is initialized but not yet running. Call <see cref="Start"/> to begin execution.
        /// The owner is responsible for releasing and nullifying its reference when the instance is no longer needed.
        /// </summary>
        /// <param name="owner">The game object that owns the graph instance.</param>
        /// <param name="serialized">The serialized data to restore the graph from.</param>
        /// <param name="serializer">The serializer used to deserialize the data.</param>
        /// <param name="resolver">The object resolver used during deserialization.</param>
        /// <typeparam name="TSerializedFormat">The type of the serialized data.</typeparam>
        /// <returns>The newly instantiated and restored graph instance.</returns>
        internal static BehaviorGraph AcquireDeserializedInstance<TSerializedFormat>(
            GameObject owner,
            TSerializedFormat serialized,
            RuntimeSerializationUtility.IBehaviorSerializer<TSerializedFormat> serializer,
            RuntimeSerializationUtility.IUnityObjectResolver<string> resolver)
        {
            var instance = ScriptableObject.CreateInstance<BehaviorGraph>();
            serializer.Deserialize(serialized, instance, resolver);
            instance.InitializeInstance(owner);
            instance.DeserializeGraphModules();
            return instance;
        }

        /// <summary>
        /// Ends execution of the graph instance and unregisters all internal callbacks.
        /// Calls <see cref="End"/> as a safety guarantee even if the caller already stopped execution.
        /// The owner is responsible for nullifying its reference after calling this.
        /// </summary>
        /// <param name="instance">The graph instance to release.</param>
        internal static void ReleaseInstance(BehaviorGraph instance)
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(instance))
            {
                return;
            }
#endif
            if (instance == null)
            {
                return;
            }

            instance.End();
            TeardownInstance(instance);
        }

        /// <summary>
        /// Initializes the graph instance by assigning the owner, creating metadata, initializing
        /// default event channels, and calling <see cref="Node.Setup"/> on all nodes.
        /// </summary>
        /// <param name="owner">The game object that owns the graph instance.</param>
        private void InitializeInstance(GameObject owner)
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(this))
            {
                Debug.LogError($"Cannot initialize a persistent graph instance: {name}. Please first create a new graph instance " +
                               $"from the source asset by calling ScriptableObject.Instantiate(SourceGraphAsset).", owner);
                return;
            }
#endif
            foreach (BehaviorGraphModule graphModule in Graphs)
            {
                graphModule.GameObject = owner;
                graphModule.InitializeDefaultEventChannels();
                graphModule.BlackboardReference?.Blackboard?.CreateMetadata();
                graphModule.InitializeNodes();
            }
        }

        /// <summary>
        /// Tears down a graph instance by invoking node teardown callbacks on all modules.
        /// This is called when the runtime instance is released and about to be returned to the pool.
        /// </summary>
        /// <param name="instance">The graph instance to tear down.</param>
        internal static void TeardownInstance(BehaviorGraph instance)
        {
            if (instance == null)
            {
                return;
            }

            foreach (BehaviorGraphModule graphModule in instance.Graphs)
            {
                graphModule.TeardownNodes();
            }
        }

        private void RegisterSharedVariableCallbacks()
        {
            if (m_AreSharedCallbacksRegistered)
            {
                return;
            }

            foreach (BehaviorGraphModule graphModule in Graphs)
            {
                graphModule.ForEachBlackboardVariable(variable =>
                {
                    if (variable is ISharedBlackboardVariable sharedVariable)
                    {
                        sharedVariable.RegisterValueChangedCallback();
                    }
                });
            }
            m_AreSharedCallbacksRegistered = true;
        }

        private void UnregisterSharedVariableCallbacks()
        {
            if (!m_AreSharedCallbacksRegistered)
            {
                return;
            }

            foreach (BehaviorGraphModule graphModule in Graphs)
            {
                graphModule.ForEachBlackboardVariable(variable =>
                {
                    if (variable is ISharedBlackboardVariable sharedVariable)
                    {
                        sharedVariable.UnregisterValueChangedCallback();
                    }
                });
            }
            m_AreSharedCallbacksRegistered = false;
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

#endregion Graph Instance Lifecycle

#if UNITY_EDITOR
#if DEBUG
        internal void RefreshModuleDebugInfo()
        {
            foreach (BehaviorGraphModule graph in Graphs)
            {
                graph.DebugInfo = m_DebugInfo;
            }
        }
#endif

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
