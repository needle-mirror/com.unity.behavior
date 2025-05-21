using System;
using UnityEngine;
using Unity.Properties;
using System.Collections.Generic;

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

            // Usually this would happen for node in subgraph.
            m_MessageQueue.Clear();
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

            // Restore queue
            m_MessageQueue.Clear();
            if (m_SerializedQueue != null)
            {
                foreach (var message in m_SerializedQueue)
                {
                    if (message != null)
                    {
                        m_MessageQueue.Enqueue(message);
                    }
                }

                m_SerializedQueue = null;
            }
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
            QueuedMessage message = new QueuedMessage(MessageVariables.Length);

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

        [System.Serializable]
        private class QueuedMessage
        {
            public object[] VariableValues;

            public QueuedMessage() { }

            public QueuedMessage(int variableCount)
            { 
                VariableValues = new object[variableCount];
            }
        }
    }
}