using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;
using UnityEngine.Pool;

namespace Unity.Behavior
{
    /// <summary>
    /// Starts the subgraph upon receiving an event message.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Start On Event Message",
        category: "Events",
        story: "When a message is received on [ChannelVariable]",
        description: "Starts the subgraph upon receiving an event message.",
        id: "a90ecb9b9ff9932eb96f04424549494c")]
    internal partial class StartOnEvent : Modifier
    {
        [Serializable]
        internal enum TriggerBehavior
        {
            Default,
            Restart,
            Once,
            Queue
        }

        /// <summary>
        /// The event channel to listen to.
        /// </summary>
        [SerializeReference]
        public BlackboardVariable ChannelVariable;
        private EventChannelBase EventChannel => ChannelVariable?.ObjectValue as EventChannelBase;

        /// <summary>
        /// The variables sent with the event message.
        /// </summary>
        [SerializeReference]
        internal BlackboardVariable[] MessageVariables = new BlackboardVariable[4];

        [SerializeField]
        internal TriggerBehavior Mode = TriggerBehavior.Default;
        private Delegate m_CaptureVariablesDelegate;
        private EventChannelBase m_CurrentChannel;
        [CreateProperty]
        private bool m_BranchRunning = false;
        [CreateProperty]
        private QueuedMessage m_ActiveMessage;
        [CreateProperty]
        private QueuedMessage[] m_SerializedQueue = null;

        // Queue to store messages
        private Queue<QueuedMessage> m_MessageQueue = new Queue<QueuedMessage>();

        /// <inheritdoc cref="OnStart" />
        protected override Status OnStart()
        {
            if (!EventChannel)
            {
                return Status.Failure;
            }

            m_BranchRunning = false;
            m_MessageQueue.Clear();
            RegisterListener();
            ChannelVariable.OnValueChanged += UnregisterListener;
            ChannelVariable.OnValueChanged += RegisterListener;

            return Status.Waiting;
        }

        /// <inheritdoc cref="OnUpdate" />
        protected override Status OnUpdate()
        {
            if (Child != null)
            {
                EndNode(Child);
            }

            // Otherwise, reset and wait for next message.
            m_BranchRunning = false;

            // If we are in Queue mode and have queued messages, process the next one.
            if (Mode == TriggerBehavior.Queue && m_MessageQueue.Count > 0)
            {
                ProcessQueuedMessage();
                return Status.Waiting;
            }

            if (Mode == TriggerBehavior.Default)
            {
                // If interrupt is enabled, the delegate will still be registered.
                m_CurrentChannel.RegisterListener(m_CaptureVariablesDelegate);
            }

            return Status.Waiting;
        }

        /// <inheritdoc cref="OnEnd" />
        protected override void OnEnd()
        {
            UnregisterListener();
            if (ChannelVariable != null)
            {
                ChannelVariable.OnValueChanged -= UnregisterListener;
                ChannelVariable.OnValueChanged -= RegisterListener;
            }

            // Release active message back to the pool
            if (m_ActiveMessage != null)
            {
                QueuedMessagePool.Release(m_ActiveMessage);
                m_ActiveMessage = null;
            }

            // Release all queued messages back to the pool
            while (m_MessageQueue.Count > 0)
            {
                var message = m_MessageQueue.Dequeue();
                if (message != null)
                {
                    QueuedMessagePool.Release(message);
                }
            }
        }

        protected override void OnSerialize()
        {
            if (m_MessageQueue.Count > 0)
            {
                m_SerializedQueue = m_MessageQueue.ToArray();
            }
        }

        protected override void OnDeserialize()
        {
            RegisterListener();

            if (ChannelVariable != null)
            {
                ChannelVariable.OnValueChanged += UnregisterListener;
                ChannelVariable.OnValueChanged += RegisterListener;
            }

            // Clear any existing messages in the queue and release them back to the pool
            while (m_MessageQueue.Count > 0)
            {
                var message = m_MessageQueue.Dequeue();
                if (message != null)
                {
                    QueuedMessagePool.Release(message);
                }
            }

            if (m_SerializedQueue == null)
            {
                return;
            }

            // Restore queue from serialized data
            foreach (var serializedMessage in m_SerializedQueue)
            {
                if (serializedMessage != null)
                {
                    // Get a new message from the pool with the correct array size
                    int variableCount = serializedMessage.VariableValues?.Length ?? 0;
                    var message = QueuedMessagePool.Get(variableCount);
                    message.Copy(variableCount, serializedMessage);
                    m_MessageQueue.Enqueue(message);
                }
            }

            m_SerializedQueue = null;
        }

        private void OnMessageReceived()
        {
            // Queue mode handling.
            if (Mode == TriggerBehavior.Queue)
            {
                m_MessageQueue.Enqueue(CaptureCurrentVariableValues());
                // If first message received, awake the node to call update and start processing at the next update.
                // This is required as receiving another message would override the active variables.
                if (!m_BranchRunning)
                {
                    AwakeNode(this);
                }

                return;
            }

            // If interrupts disabled, unregister from future events to prevent further messages writing to variables.
            if (Mode != TriggerBehavior.Restart && Mode != TriggerBehavior.Queue)
            {
                m_CurrentChannel.UnregisterListener(m_CaptureVariablesDelegate);
            }

            ProcessMessage();
        }

        private void ProcessMessage()
        {
            // No subgraph exists. Awaken this node.
            if (Child == null)
            {
                AwakeNode(this);
                return;
            }

            // Ensures the node is Waiting state before starting the subgraph.
            CurrentStatus = Status.Waiting;

            // A subgraph exists but is not running. Start it.
            if (!m_BranchRunning)
            {
                // The order is important as it is possible to have a child Triggering an Event
                // that would need the current child to end. i.e.:
                // StartOnEvent<State>
                //      -> Switch<State>
                //          -> State 'A'
                //              -> TriggerEvent: 'To State B'
                //          -> State 'B'
                m_BranchRunning = true;
                StartNode(Child);
                return;
            }

            // The subgraph is running. Check for interrupt.
            if (Mode == TriggerBehavior.Restart)
            {
                EndNode(Child);
                StartNode(Child);
            }
        }

        private void RegisterListener()
        {
            m_CurrentChannel = EventChannel;
            if (m_CurrentChannel)
            {
                if (Mode == TriggerBehavior.Queue)
                {
                    if (m_CurrentChannel is IEventHandlerWithoutNotify channel)
                    {
                        // if queue mode, we will notify the variables before processing the message.
                        m_CaptureVariablesDelegate = channel.CreateEventHandlerWithoutNotify(MessageVariables, OnMessageReceived);
                    }
                    else
                    {
                        Debug.LogWarning($"EventChannel of type '{m_CurrentChannel.GetType().Name}' does not implement '{typeof(IEventHandlerWithoutNotify).Name}'. " +
                            $"When using Queue mode, BlackboardVariable values may trigger unexpected OnValueChanged events during message queueing. " +
                            $"To fix this, make your {typeof(EventChannelBase).Name} class inherit from {typeof(EventChannel).Name} or a generic {typeof(EventChannel).Name}<T0,...>.");

                        m_CaptureVariablesDelegate = m_CurrentChannel.CreateEventHandler(MessageVariables, OnMessageReceived);
                    }
                }
                else
                {
                    m_CaptureVariablesDelegate = m_CurrentChannel.CreateEventHandler(MessageVariables, OnMessageReceived);
                }

                m_CurrentChannel.RegisterListener(m_CaptureVariablesDelegate);
            }
        }

        private void UnregisterListener()
        {
            if (m_CurrentChannel)
            {
                m_CurrentChannel.UnregisterListener(m_CaptureVariablesDelegate);
            }
        }

        // Captures the current variable values for queueing and revert active value if any.
        private QueuedMessage CaptureCurrentVariableValues()
        {
            // Get a QueuedMessage from the specialized pool with the correct array size
            QueuedMessage message = QueuedMessagePool.Get(MessageVariables.Length);

            // Copy current variable values
            for (int i = 0; i < MessageVariables.Length; i++)
            {
                if (MessageVariables[i] != null)
                {
                    message.VariableValues[i] = MessageVariables[i].ObjectValue;
                }
            }

            // Revert working value to active message.
            if (m_ActiveMessage != null)
            {
                for (int i = 0; i < m_ActiveMessage.VariableValues.Length; i++)
                {
                    if (MessageVariables[i] != null)
                    {
                        MessageVariables[i].SetObjectValueWithoutNotify(m_ActiveMessage.VariableValues[i]);
                    }
                }
            }

            return message;
        }

        // Process a queued message by restoring variable values
        private void ProcessQueuedMessage()
        {
            if (m_MessageQueue.Count == 0)
                return;

            // Release previous active message if it exists
            if (m_ActiveMessage != null)
            {
                QueuedMessagePool.Release(m_ActiveMessage);
            }

            m_ActiveMessage = m_MessageQueue.Dequeue();

            // Restore variable values from the queued message
            for (int i = 0; i < MessageVariables.Length && i < m_ActiveMessage.VariableValues.Length; i++)
            {
                if (MessageVariables[i] != null)
                {
                    MessageVariables[i].ObjectValue = m_ActiveMessage.VariableValues[i];
                }
            }

            // Process the message
            ProcessMessage();
        }

        /// <summary>
        /// Static pool manager for QueuedMessage objects with specialized pools for each array size (up to 4).
        /// Get rid of GC allocations when reusing QueuedMessage objects with different array sizes.
        /// </summary>
        private static class QueuedMessagePool
        {
            private const int k_MaxVariableCount = 4;
            private static readonly ObjectPool<QueuedMessage>[] s_Pools;

            static QueuedMessagePool()
            {
                s_Pools = new ObjectPool<QueuedMessage>[k_MaxVariableCount + 1];

                for (int i = 0; i <= k_MaxVariableCount; i++)
                {
                    int variableCount = i; // Capture for lambda
                    s_Pools[i] = new ObjectPool<QueuedMessage>(
                        createFunc: () => new QueuedMessage(variableCount),
                        actionOnGet: (msg) => msg.Reset(),
                        actionOnRelease: null,
                        actionOnDestroy: null,
                        collectionCheck: true,
                        defaultCapacity: 10,
                        maxSize: 124
                    );
                }
            }

            /// <summary>
            /// Get a QueuedMessage instance with the specified variable count.
            /// </summary>
            /// <param name="variableCount">Number of variables (up to k_MaxVariableCount)</param>
            /// <returns>A pooled QueuedMessage instance with the correct array size</returns>
            public static QueuedMessage Get(int variableCount)
            {
                variableCount = Mathf.Clamp(variableCount, 0, k_MaxVariableCount);
                return s_Pools[variableCount].Get();
            }

            /// <summary>
            /// Release a QueuedMessage back to its appropriate pool based on array size.
            /// </summary>
            /// <param name="message">The message to release</param>
            public static void Release(QueuedMessage message)
            {
                if (message == null || message.VariableValues == null)
                {
                    return;
                }

                int variableCount = message.VariableValues.Length;
                if (variableCount >= 0 && variableCount <= k_MaxVariableCount)
                {
                    s_Pools[variableCount].Release(message);
                }
            }
        }

        [System.Serializable]
        private class QueuedMessage
        {
            public object[] VariableValues;

            public QueuedMessage() { }

            public QueuedMessage(int variableCount)
            {
                VariableValues = new object[variableCount];
            }

            public void Copy(int variableCount, QueuedMessage other)
            {
                // Copy values from serialized message
                for (int i = 0; i < variableCount; i++)
                {
                    VariableValues[i] = other.VariableValues[i];
                }
            }

            // Reset the QueuedMessage for reuse - QueuedMessagePool.
            public void Reset()
            {
                if (VariableValues != null)
                {
                    Array.Clear(VariableValues, 0, VariableValues.Length);
                }
            }
        }
    }
}
