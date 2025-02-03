using System.Collections.Generic;
using System.Linq;
using Unity.Behavior.GraphFramework;
using UnityEngine;

#if NETCODE_FOR_GAMEOBJECTS
using Unity.Netcode;
#endif

namespace Unity.Behavior
{
    /// <summary>
    /// Behavior agent component.
    /// </summary>
    [AddComponentMenu("AI/Behavior Agent")]
#if NETCODE_FOR_GAMEOBJECTS
    public class BehaviorGraphAgent : NetworkBehaviour, ISerializationCallbackReceiver
#else
    public class BehaviorGraphAgent : MonoBehaviour, ISerializationCallbackReceiver
#endif
    {
        [SerializeReference] private BehaviorGraph m_Graph;

        /// <summary>
        /// The graph of behaviours to be executed by the agent.
        /// </summary>
        public BehaviorGraph Graph
        {
            get => m_Graph;
            set
            {
                m_Graph = value;
                m_IsInitialised = false;
                m_IsStarted = false;

#if UNITY_TEST_FRAMEWORK
                m_InitialisedFromAssetProcessor = false;
#endif
                
                OnAssignBehaviorGraph();
            }
        }

        /// <summary>
        /// The blackboard associated with the agent's graph.
        /// </summary>
        public BlackboardReference BlackboardReference => m_Graph ? m_Graph.BlackboardReference : null;

        /// <summary>
        /// UnityEngine.Object references, serialized separately from other variable types.
        /// </summary>
        [SerializeReference, HideInInspector]
        private List<BlackboardVariable> m_BlackboardVariableOverridesList = new ();
        internal Dictionary<SerializableGUID, BlackboardVariable> m_BlackboardOverrides = new ();

#if UNITY_EDITOR
        /// <summary>
        /// Events used to notify to the BehaviorGraphEditor that it needs to refresh its graph reference.
        /// </summary>
        internal System.Action OnRuntimeDeserializationEvent;
#endif

        [SerializeField][HideInInspector] 
        internal bool m_IsInitialised = false;

        [SerializeField][HideInInspector]
        internal bool m_IsStarted = false;

#if UNITY_TEST_FRAMEWORK
        internal bool m_InitialisedFromAssetProcessor = false;
#endif

#if NETCODE_FOR_GAMEOBJECTS
        public bool NetcodeRunOnlyOnOwner = false;
        public override void OnNetworkSpawn()
        {
            if (!IsOwner && NetcodeRunOnlyOnOwner)
            {
                enabled = false;
                return;
            }
        }
#endif


        private void Awake()
        {
            Init();
        }

        private void OnAssignBehaviorGraph()
        {
            // If the graph or blackboard are null, we can't sync the overrides, so return.
            if (m_Graph == null || m_Graph.BlackboardReference?.Blackboard == null)
            {
                m_BlackboardOverrides.Clear();
                m_BlackboardVariableOverridesList.Clear();
                return;
            }

            SynchronizeOverridesWithBlackboard();
        }

        internal void SynchronizeOverridesWithBlackboard()
        {
            RemoveOverridesWithNoMatchInBlackboard();
            UpdateBlackboardOverridesToMatchBlackboard(); 
            CreateOrUpdateSelfOverride();
            ApplySelfOverrideToBlackboard();
        }

        private void CreateOrUpdateSelfOverride()
        {
            SerializableGUID graphOwnerID = BehaviorGraph.k_GraphSelfOwnerID;
            if (m_BlackboardOverrides.TryGetValue(graphOwnerID, out BlackboardVariable ownerVariableOverride))
            {
                if (ownerVariableOverride.ObjectValue != null)
                {
                    return;
                }
                // An override already exists, so set its value to this GameObject.
                ownerVariableOverride.ObjectValue = gameObject;
                // Set the blackboard owner variable value to this GameObject.
                if (BlackboardReference.GetVariable(graphOwnerID, out BlackboardVariable ownerVariable))
                {
                    ownerVariable.ObjectValue = ownerVariableOverride.ObjectValue;
                }
            }
            else if (BlackboardReference.GetVariable(graphOwnerID, out BlackboardVariable ownerVariable))
            {
                // No override exists, but a blackboard variable for the graph owner exists, so add an override.
                m_BlackboardOverrides.Add(graphOwnerID, new BlackboardVariable<GameObject>(gameObject)
                {
                    GUID = graphOwnerID,
                    Name = ownerVariable.Name
                });
            }
        }

        private void RemoveOverridesWithNoMatchInBlackboard()
        {
            // A new instance of a runtime graph has been assigned. Remove any out-of-date variable overrides.
            foreach ((SerializableGUID guid, _) in m_BlackboardOverrides.ToList())
            {
                if (m_Graph.BlackboardReference.Blackboard.Variables.Any(bbVariable => bbVariable.GUID == guid))
                {
                    continue;
                }

                m_BlackboardOverrides.Remove(guid);
                var indexToRemove = m_BlackboardVariableOverridesList.FindIndex((item) => item.GUID == guid);
                if (indexToRemove != -1)
                {
                    m_BlackboardVariableOverridesList.RemoveAt(indexToRemove);
                }
            }
        }

        private void UpdateBlackboardOverridesToMatchBlackboard()
        {
            foreach (BlackboardVariable variable in m_Graph.BlackboardReference.Blackboard.Variables)
            {
#if UNITY_EDITOR
                // This strange case sometimes happens when the inspector is open during a domain reload.
                if (variable == null) continue;
#endif
                if (m_BlackboardOverrides.TryGetValue(variable.GUID, out var overrideVariable))
                {
                    // An override already exists, so update its name if necessary.
                    if (overrideVariable.Name != variable.Name)
                    {
                        overrideVariable.Name = variable.Name;
                    }
                }
                else
                {
                    foreach (var blackboardOverride in m_BlackboardOverrides)
                    {
                        if (blackboardOverride.Value.Name == variable.Name)
                        {
                            m_BlackboardOverrides.Remove(blackboardOverride.Key);
                            blackboardOverride.Value.GUID = variable.GUID;
                            m_BlackboardOverrides.Add(variable.GUID, blackboardOverride.Value);
                        }
                    }
                }
            }
        }

        private void ApplySelfOverrideToBlackboard()
        {
            SerializableGUID graphOwnerID = BehaviorGraph.k_GraphSelfOwnerID;
            if (m_BlackboardOverrides.TryGetValue(graphOwnerID, out BlackboardVariable ownerVariableOverride))
            {
                if (ownerVariableOverride.ObjectValue != null)
                {
                    return;
                }

                ownerVariableOverride.ObjectValue = gameObject;
            }
        }

        /// <summary>
        /// Initializes a new instance of the agent's behavior graph.
        /// </summary>
        public void Init()
        {
            if (m_Graph == null)
            {
                return;
            }

            if (m_IsInitialised)
            {
                m_Graph.AssignGameObjectToGraphModules(gameObject);
                return;
            }

            m_Graph = ScriptableObject.Instantiate(m_Graph);
            m_Graph.AssignGameObjectToGraphModules(gameObject);
            InitChannelsAndMetadata();
            m_IsInitialised = true;
        }

        /// <summary>
        /// Gets a variable associated with the specified name and value type. For values of type subclassed from
        /// UnityEngine.Object, use the non-generic method.
        /// As blackboard variables are instantiated per agent instance on initialization,
        /// this function will return false in edit mode.
        /// </summary>
        /// <param name="variableName">The name of the variable</param> 
        /// <param name="variable">The blackboard variable matching the name and value type</param>
        /// <typeparam name="TValue">The type of value stored by the variable</typeparam>
        /// <returns>Returns true if a variable matching the name and type is found. Returns false otherwise (or if in edit mode).</returns>
        public bool GetVariable<TValue>(string variableName, out BlackboardVariable<TValue> variable)
        {
            if (!Application.isPlaying || !m_IsInitialised)
            {
                variable = null;
                Debug.LogWarning($"Failed to get blackboard variable with name '{variableName}'. Agent is not yet initialized.", this);
                return false;
            }

            if (m_Graph.RootGraph.GetVariable(variableName, out variable))
            {
                return true;
            }

            foreach (var behaviorGraphModule in m_Graph.Graphs)
            {
                if (behaviorGraphModule.GetVariable(variableName, out variable))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the variable associated with the specified name.
        /// As blackboard variables are instantiated per agent instance on initialization,
        /// this function will return false in edit mode.
        /// </summary>
        /// <param name="variableName">The name of the variable</param> 
        /// <param name="variable">Contains the value associated with the specified name, if the named variable is found;
        /// otherwise, the default value is assigned.</param>
        /// <returns>Returns true if a variable matching the name and type is found. Returns false otherwise (or if in edit mode).</returns>
        public bool GetVariable(string variableName, out BlackboardVariable variable)
        {
            if (!Application.isPlaying || !m_IsInitialised)
            {
                variable = null;
                Debug.LogWarning($"Failed to get blackboard variable with name '{variableName}'. Agent is not yet initialized.", this);
                return false;
            }

            if (m_Graph.RootGraph.GetVariable(variableName, out variable))
            {
                return true;
            }

            foreach (var behaviorGraphModule in m_Graph.Graphs)
            {
                if (behaviorGraphModule.GetVariable(variableName, out variable))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets a variable associated with the specified GUID.
        /// As blackboard variables are instantiated per agent instance on initialization,
        /// this function will return false in edit mode.
        /// </summary>
        /// <param name="guid">The GUID of the variable to get</param>
        /// <param name="variable">The variable associated with the specified GUID.</param>
        /// <returns>Returns true if a variable with a matching GUID was found and false otherwise (or if in edit mode).</returns>
        public bool GetVariable(SerializableGUID guid, out BlackboardVariable variable)
        {
            if (!Application.isPlaying || !m_IsInitialised)
            {
                variable = null;
                Debug.LogWarning($"Failed to get blackboard variable with id '{guid}'. Agent is not yet initialized.", this);
                return false;
            }

            if (m_Graph.RootGraph.GetVariable(guid, out variable))
            {
                return true;
            }

            foreach (var behaviorGraphModule in m_Graph.Graphs)
            {
                if (behaviorGraphModule.GetVariable(guid, out variable))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets a variable associated with the specified GUID and value type.
        /// As blackboard variables are instantiated per agent instance on initialization,
        /// this function will return false in edit mode.
        /// </summary>
        /// <param name="guid">The GUID of the variable to get</param>
        /// <param name="variable">The variable associated with the specified GUID.</param>
        /// <typeparam name="TValue">The value type of the variable</typeparam>
        /// <returns>Returns true if a variable with a matching GUID and type was found and false otherwise (or if in edit mode).</returns>
        public bool GetVariable<TValue>(SerializableGUID guid, out BlackboardVariable<TValue> variable)
        {
            if (!Application.isPlaying || !m_IsInitialised)
            {
                variable = null;
                Debug.LogWarning($"Failed to get blackboard variable with id '{guid}'. Agent is not yet initialized.", this);
                return false;
            }

            if (m_Graph.RootGraph.GetVariable(guid, out variable))
            {
                return true;
            }

            foreach (var behaviorGraphModule in m_Graph.Graphs)
            {
                if (behaviorGraphModule.GetVariable(guid, out variable))
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc cref="Behavior.BlackboardReference.GetVariableID"/>
        public bool GetVariableID(string variableName, out SerializableGUID id)
        {
            if (!Application.isPlaying || !m_IsInitialised)
            {
                return TryGetBlackboardVariableGUIDOverride(variableName, out id);
            }
            
            if (m_Graph.RootGraph.GetVariableID(variableName, out id))
            {
                return true;
            }

            foreach (var behaviorGraphModule in m_Graph.Graphs)
            {
                if (behaviorGraphModule.GetVariableID(variableName, out id))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Sets the value of a blackboard variable matching the specified name and value type.
        /// </summary>
        /// <param name="variableName">The name of the variable</param>
        /// <param name="value">The value to assign to the variable</param>
        /// <typeparam name="TValue">The type of value stored by the variable</typeparam>
        /// <returns>Returns true if a variable matching the name and type is found and set. Returns false otherwise.</returns>
        public bool SetVariableValue<TValue>(string variableName, TValue value)
        {
            if (!Application.isPlaying || !m_IsInitialised)
            {
                return TrySetBlackboardVariableOverride(variableName, value);
            }

            m_Graph.RootGraph?.SetVariableValue(variableName, value);

            foreach (var behaviorGraphModule in m_Graph.Graphs)
            {
                if (behaviorGraphModule.SetVariableValue(variableName, value))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Sets the value of the variable associated with the specified GUID.
        /// </summary>
        /// <param name="guid">The guid associated with the variable</param>
        /// <param name="value">The value to assign to the variable</param>
        /// <typeparam name="TValue">The value type of the variable</typeparam>
        /// <returns>Returns true if the value was set successfully and false otherwise.</returns>
        public bool SetVariableValue<TValue>(SerializableGUID guid, TValue value)
        {
            if (!Application.isPlaying || !m_IsInitialised)
            {
                return TrySetBlackboardVariableOverride(guid, value);
            }

            m_Graph.RootGraph?.SetVariableValue(guid, value);

            foreach (var behaviorGraphModule in m_Graph.Graphs)
            {
                if (behaviorGraphModule.SetVariableValue(guid, value))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Deserializes data on to the associated BehaviorGraph.
        /// </summary>
        /// <param name="serialized">Serialized data.</param>
        /// <param name="serializer">Serializer to use.</param>
        /// <param name="resolver">Object resolver to use.</param>
        /// <typeparam name="TSerializedFormat">Type of serialized data.</typeparam>
        public void Deserialize<TSerializedFormat>(TSerializedFormat serialized,
            RuntimeSerializationUtility.IBehaviorSerializer<TSerializedFormat> serializer,
            RuntimeSerializationUtility.IUnityObjectResolver<string> resolver)
        {
            m_Graph = ScriptableObject.CreateInstance<BehaviorGraph>();
            serializer.Deserialize(serialized, m_Graph, resolver);
            InitChannelsAndMetadata(applyOverride: false);
            m_Graph.DeserializeGraphModules();
#if UNITY_EDITOR
            OnRuntimeDeserializationEvent?.Invoke();
#endif
            m_IsInitialised = true;
            m_IsStarted = m_Graph.IsRunning;
        }

        /// <summary>
        /// Serializes the associated BehaviorGraph to data of TSerializedFormat type.
        /// </summary>
        /// <param name="serializer">Serializer to use.</param>
        /// <param name="resolver">Object resolver to use.</param>
        /// <typeparam name="TSerializedFormat">Type of serialized output.</typeparam>
        /// <returns>Serialized data.</returns>
        public TSerializedFormat Serialize<TSerializedFormat>(
            RuntimeSerializationUtility.IBehaviorSerializer<TSerializedFormat> serializer,
            RuntimeSerializationUtility.IUnityObjectResolver<string> resolver)
        {
            m_Graph.SerializeGraphModules();
            return serializer.Serialize(m_Graph, resolver);
        }

        private void InitChannelsAndMetadata(bool applyOverride = true)
        {
            if (applyOverride)
            {
                ApplyBlackboardOverrides();
            }

            // Initialize default event channels for unassigned channel variables.
            foreach (BehaviorGraphModule graph in Graph.Graphs)
            {
                foreach (BlackboardVariable variable in graph.Blackboard.Variables)
                {
                    if (typeof(EventChannelBase).IsAssignableFrom(variable.Type) && variable.ObjectValue == null)
                    {
                        ScriptableObject channel = ScriptableObject.CreateInstance(variable.Type);
                        channel.name = $"Default {variable.Name} Channel";
                        variable.ObjectValue = channel;
                    }
                }

                foreach (var bbref in graph.BlackboardGroupReferences)
                {
                    foreach (BlackboardVariable variable in bbref.Blackboard.Variables)
                    {
                        if (typeof(EventChannelBase).IsAssignableFrom(variable.Type) && variable.ObjectValue == null)
                        {
                            ScriptableObject channel = ScriptableObject.CreateInstance(variable.Type);
                            channel.name = $"Default {variable.Name} Channel";
                            variable.ObjectValue = channel;
                        }
                    }
                }

                foreach (Node node in graph.Nodes())
                {
                    node.Graph = graph;
                }
            }

            BlackboardReference.Blackboard.CreateMetadata();
        }

        /// <summary>
        /// Begins execution of the agent's behavior graph.
        /// </summary>
        public void Start()
        {
            if (m_Graph == null) return;
#if NETCODE_FOR_GAMEOBJECTS
            if (!IsOwner && NetcodeRunOnlyOnOwner) return;
#endif

            if (!isActiveAndEnabled)
            {
                if (!m_IsInitialised)
                {
                    return;
                }

                if (m_Graph.IsRunning)
                {
                    return;
                }
                m_Graph.End();
                m_IsStarted = false;
                return;
            }

            if (!m_IsInitialised)
            {
                Init();
            }
            if (m_Graph.IsRunning)
            {
                return;
            }
            m_Graph.Start();
            m_IsStarted = true;
        }

        /// <summary>
        /// Ends the execution of the agent's behavior graph.
        /// </summary>
        public void End()
        {
            if (m_Graph == null) return;
#if NETCODE_FOR_GAMEOBJECTS
            if (!IsOwner && NetcodeRunOnlyOnOwner) return;
#endif
            m_Graph.End();
        }

        /// <summary>
        /// Restarts the execution of the agent's behavior graph.
        /// </summary>
        public void Restart()
        {
#if NETCODE_FOR_GAMEOBJECTS
            if (!IsOwner && NetcodeRunOnlyOnOwner) return;
#endif
            if (m_Graph == null)
            {
                Debug.LogError("Can't restart the agent because no graph has been assigned.", this);
                return;
            }

            if (!isActiveAndEnabled)
            {
                if (m_IsInitialised)
                {
                    m_Graph.End();
                }
                m_IsStarted = false;
                return;
            }

            if (!m_IsInitialised)
            {
                // The graph needs initialising and then starting. The user asked to do it this frame so we do it here
                // instead of waiting for Update().
                Init();
                m_Graph.Start();
                m_IsStarted = true;
                return;
            }
            m_Graph.Restart();
            m_IsStarted = true;
        }

        /// <summary>
        /// Ticks the agent's behavior graph and initializes and starts the graph if necessary.
        /// </summary>
        public void Update()
        {
            if (m_Graph == null)
                return;

#if NETCODE_FOR_GAMEOBJECTS
            if (!IsOwner && NetcodeRunOnlyOnOwner) return;
#endif
            
            if (!m_IsInitialised)
            {
                Init();
            }

            if (!m_IsStarted)
            {
                m_Graph.Start();
                m_IsStarted = true;
            }
            m_Graph.Tick();
        }

#if NETCODE_FOR_GAMEOBJECTS
        public override void OnDestroy()
        {
            base.OnDestroy();
#else
        private void OnDestroy()
        {
#endif
            if (m_Graph)
            {
                m_Graph.End();
            }
        }

        /// <summary>
        /// Applies the agent's variable overrides to the blackboard.
        /// </summary>
        private void ApplyBlackboardOverrides()
        {
            foreach (var varOverride in m_BlackboardOverrides)
            {
                if (varOverride.Key == BehaviorGraph.k_GraphSelfOwnerID &&
                    varOverride.Value is BlackboardVariable<GameObject> gameObjectBlackboardVariable &&
                    gameObjectBlackboardVariable.Value == null)
                {
                    gameObjectBlackboardVariable.Value = gameObject;
                }

                if (BlackboardReference != null &&
                    BlackboardReference.GetVariable(varOverride.Key, out BlackboardVariable var))
                {
                    var.ObjectValue = varOverride.Value.ObjectValue;
                }

                foreach (var graphModule in Graph.Graphs)
                {
                    if (graphModule.Blackboard != null &&
                        graphModule.BlackboardReference.GetVariable(varOverride.Key,
                            out BlackboardVariable subGraphVariable))
                    {
                        subGraphVariable.ObjectValue = varOverride.Value.ObjectValue;
                    }

                    foreach (var blackboardReference in graphModule.BlackboardGroupReferences)
                    {
                        if (blackboardReference.GetVariable(varOverride.Key,
                                out BlackboardVariable blackboardReferenceVar))
                        {
                            blackboardReferenceVar.ObjectValue = varOverride.Value.ObjectValue;
                        }
                    }
                }
            }
        }

        /// <inheritdoc cref="OnBeforeSerialize"/>
        public void OnBeforeSerialize()
        {
            if (!m_Graph)
            {
                return;
            }

            m_BlackboardVariableOverridesList.Clear();
            foreach (BlackboardVariable variable in m_BlackboardOverrides.Values)
            {
                m_BlackboardVariableOverridesList.Add(variable);
            }
        }

        /// <inheritdoc cref="OnAfterDeserialize"/>
        public void OnAfterDeserialize()
        {
            if (!Graph)
            {
                return;
            }

            m_BlackboardOverrides = new Dictionary<SerializableGUID, BlackboardVariable>();
            foreach (BlackboardVariable variable in m_BlackboardVariableOverridesList)
            {
                m_BlackboardOverrides.Add(variable.GUID, variable);
            }
        }

        private bool TrySetBlackboardVariableOverride<TValue>(string variableName, TValue value)
        {
            foreach (var bbvo in m_BlackboardOverrides)
            {
                if (bbvo.Value.Name != variableName)
                {
                    continue;
                }

                bbvo.Value.ObjectValue = value; // implicit checks under ObjectValue
                return true;
            }

            if (m_Graph == null)
            {
                return false;
            }

            // At this point, the variable either don't exist or is not overriden yet. 
            // We check for the source blackboard and override it if needed.
            // TODO: If we want to support override of linked blackboard asset in the future, we will need to also add support for it here.
            var candidate = m_Graph.BlackboardReference.Blackboard.Variables.Where(bbVariable => bbVariable.Name == variableName).First();
            if (candidate is null)
            {
                return false;
            }

            var newOverride = candidate.Duplicate();
            newOverride.SetObjectValueWithoutNotify(value);
            m_BlackboardOverrides.Add(newOverride.GUID, newOverride);
            m_BlackboardVariableOverridesList.Add(newOverride);
            return true;
        }

        private bool TrySetBlackboardVariableOverride<TValue>(SerializableGUID guid, TValue value)
        {
            if (m_BlackboardOverrides.TryGetValue(guid, out var bbv))
            {
                bbv.ObjectValue = value;
                return true;
            }

            if (m_Graph == null)
            {
                return false;
            }

            // At this point, the variable either don't exist or is not overriden yet. 
            // We check for the source blackboard and override it if needed.
            // TODO: If we want to support override of linked blackboard asset in the future, we will need to also add support for it here.
            var candidate = m_Graph.BlackboardReference.Blackboard.Variables.Where(bbVariable => bbVariable.GUID == guid).First();
            if (candidate is null)
            {
                return false;
            }

            var newOverride = candidate.Duplicate();
            newOverride.SetObjectValueWithoutNotify(value);
            m_BlackboardOverrides.Add(guid, newOverride);
            m_BlackboardVariableOverridesList.Add(newOverride);
            return true;
        }

        private bool TryGetBlackboardVariableGUIDOverride(string variableName, out SerializableGUID guid)
        {
            foreach (var bbvo in m_BlackboardOverrides)
            {
                if (bbvo.Value.Name != variableName)
                {
                    continue;
                }

                guid = bbvo.Value.GUID;
                return true;
            }

            guid = default;
            return false;
        }

#if UNITY_EDITOR // Used for testing
        internal bool TryGetBlackboardVariableOverride<TValue>(SerializableGUID guid, out TValue value)
        {
            if (m_BlackboardOverrides.TryGetValue(guid, out var bbv))
            {
                if (bbv is BlackboardVariable<TValue> castedBbv)
                {
                    value = castedBbv.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        internal bool TryGetBlackboardVariableOverride<TValue>(string variableName, out TValue value)
        {
            foreach (var bbvo in m_BlackboardOverrides)
            {
                if (bbvo.Value.Name != variableName)
                {
                    continue;
                }

                if (bbvo.Value is BlackboardVariable<TValue> castedBbv)
                {
                    value = castedBbv.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }
#endif
    }
}