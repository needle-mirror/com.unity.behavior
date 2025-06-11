using System;
using UnityEngine;
using Unity.Properties;

namespace Unity.Behavior
{
    /// <summary>
    /// Event channels are used to send and receive event messages.
    /// </summary>
    public abstract class EventChannelBase : ScriptableObject
    {
        /// <summary>
        /// Sends an event message on the channel.
        /// </summary>
        /// <param name="messageData">The Blackboard Variables holding the data for the message.</param>
        public abstract void SendEventMessage(BlackboardVariable[] messageData);
        /// <summary>
        /// Creates an event handler for the channel.
        /// </summary>
        /// <param name="vars">The Blackboard Variables which will receive the data for the message.</param>
        /// <param name="callback">The callback to be called for the event.</param>
        /// <returns>The created event handler.</returns>
        public abstract Delegate CreateEventHandler(BlackboardVariable[] vars, System.Action callback);
        /// <summary>
        /// Registers a listener to the channel.
        /// </summary>
        /// <param name="unityAction">The delegate to register.</param>
        public abstract void RegisterListener(Delegate unityAction);
        /// <summary>
        /// Unregisters a listener from the channel.
        /// </summary>
        /// <param name="unityAction">The delegate to unregister.</param>
        public abstract void UnregisterListener(Delegate unityAction);
    }

    /// <summary>
    /// Interface for event channels that support creating event handlers without triggering notifications.
    /// </summary>
    /// <remarks>
    /// Generic implementations of EventChannelBase should implement this interface to properly support
    /// the Queue mode functionality.
    /// </remarks>
    public interface IEventHandlerWithoutNotify
    {
        /// <summary>
        /// Creates an event handler for the channel. Version without notifying OnValueChange for StartOnEvent.TriggerBehavior.Queue.
        /// </summary>
        /// <param name="vars">The Blackboard Variables which will receive the data for the message.</param>
        /// <param name="callback">The callback to be called for the event.</param>
        /// <returns>The created event handler.</returns>
        public Delegate CreateEventHandlerWithoutNotify(BlackboardVariable[] vars, System.Action callback);
    }

    /// <summary>
    /// Event channels are used to send and receive event messages.
    /// </summary>
    public abstract class EventChannel : EventChannelBase, IEventHandlerWithoutNotify
    {
        /// <summary>
        /// Represents the method that will handle events.
        /// </summary>
        public delegate void EventHandlerDelegate();

        /// <summary>
        /// Occurs when the event channel raises an event.
        /// </summary>
        public event EventHandlerDelegate Event
        {
            add { m_Event += value; }
            remove { m_Event -= value; }
        }

        private event EventHandlerDelegate m_Event;

        /// <summary>
        /// Send an event message on the channel.
        /// </summary>
        public void SendEventMessage()
        {
            m_Event?.Invoke();
        }

        /// <inheritdoc cref="EventChannelBase.SendEventMessage"/>
        public override void SendEventMessage(BlackboardVariable[] messageData)
        {
            m_Event?.Invoke();
        }

        /// <inheritdoc cref="EventChannelBase.CreateEventHandler"/>
        public override Delegate CreateEventHandler(BlackboardVariable[] vars, System.Action callback)
        {
            EventHandlerDelegate del = () =>
            {
                callback();
            };

            return del;
        }

        /// <inheritdoc cref="IEventHandlerWithoutNotify.CreateEventHandlerWithoutNotify"/>
        public virtual Delegate CreateEventHandlerWithoutNotify(BlackboardVariable[] vars, System.Action callback)
        {
            EventHandlerDelegate del = () =>
            {
                callback();
            };

            return del;
        }

        /// <inheritdoc cref="EventChannelBase.RegisterListener"/>
        public override void RegisterListener(Delegate del)
        {
            m_Event += del as EventHandlerDelegate;
        }

        /// <inheritdoc cref="EventChannelBase.UnregisterListener"/>
        public override void UnregisterListener(Delegate del)
        {
            m_Event -= del as EventHandlerDelegate;
        }
    }

    /// <summary>
    /// Generic implementation of EventChannelBase. Event channels are used to send and receive event messages.
    /// </summary>
    /// <typeparam name="T0">Type of the message variable</typeparam>
    public abstract class EventChannel<T0> : EventChannelBase, IEventHandlerWithoutNotify
    {
        /// <summary>
        /// Represents the method that will handle events with one parameters of type T0.
        /// </summary>
        /// <param name="value0">The first parameter value provided when the event is raised.</param>
        public delegate void EventHandlerDelegate(T0 value0);
        
        /// <summary>
        /// Occurs when the event channel raises an event.
        /// </summary>
        public event EventHandlerDelegate Event
        {
            add { m_Event += value; }
            remove { m_Event -= value; }
        }

        private event EventHandlerDelegate m_Event;

        /// <summary>
        /// Send an event message on the channel.
        /// </summary>
        /// <param name="value">Value to send on the channel.</param>
        public void SendEventMessage(T0 value)
        {
            m_Event?.Invoke(value);
        }

        /// <inheritdoc cref="EventChannelBase.SendEventMessage"/>
        public override void SendEventMessage(BlackboardVariable[] messageData)
        {
            BlackboardVariable<T0> bbv = messageData[0] as BlackboardVariable<T0>;
            var output = bbv != null ? bbv.Value : default(T0);

            m_Event?.Invoke(output);
        }

        /// <inheritdoc cref="EventChannelBase.CreateEventHandler"/>
        public override Delegate CreateEventHandler(BlackboardVariable[] vars, System.Action callback)
        {
            EventHandlerDelegate del = (value) =>
            {
                BlackboardVariable<T0> var0 = vars[0] as BlackboardVariable<T0>;
                if (var0 != null) var0.Value = value;
                callback();
            };

            return del;
        }

        /// <inheritdoc cref="IEventHandlerWithoutNotify.CreateEventHandlerWithoutNotify"/>
        public virtual Delegate CreateEventHandlerWithoutNotify(BlackboardVariable[] vars, System.Action callback)
        {
            EventHandlerDelegate del = (value) =>
            {
                BlackboardVariable<T0> var0 = vars[0] as BlackboardVariable<T0>;
                if (var0 != null) var0.SetValueWithoutNotify(value);
                callback();
            };

            return del;
        }

        /// <inheritdoc cref="EventChannelBase.RegisterListener"/>
        public override void RegisterListener(Delegate del)
        {
            m_Event += del as EventHandlerDelegate;
        }

        /// <inheritdoc cref="EventChannelBase.UnregisterListener"/>
        public override void UnregisterListener(Delegate del)
        {
            m_Event -= del as EventHandlerDelegate;
        }
    }

    /// <summary>
    /// Generic implementation of EventChannelBase. Event channels are used to send and receive event messages.
    /// </summary>
    /// <typeparam name="T0">Type of the first message variable</typeparam>
    /// <typeparam name="T1">Type of the second message variable</typeparam>
    public abstract class EventChannel<T0, T1> : EventChannelBase, IEventHandlerWithoutNotify
    {
        /// <summary>
        /// Represents the method that will handle events with one parameters of types T0 and T1.
        /// </summary>
        /// <param name="value0">The first parameter value provided when the event is raised.</param>
        /// <param name="value1">The second parameter value provided when the event is raised.</param>
        public delegate void EventHandlerDelegate(T0 value0, T1 value1);

        /// <summary>
        /// Occurs when the event channel raises an event.
        /// </summary>
        public event EventHandlerDelegate Event
        {
            add { m_Event += value; }
            remove { m_Event -= value; }
        }

        private event EventHandlerDelegate m_Event;

        /// <summary>
        /// Send an event message on the channel.
        /// </summary>
        /// <param name="value0">First value to send on the channel.</param>
        /// <param name="value1">Second value to send on the channel.</param>
        public void SendEventMessage(T0 value0, T1 value1)
        {
            m_Event?.Invoke(value0, value1);
        }

        /// <inheritdoc cref="EventChannelBase.SendEventMessage"/>
        public override void SendEventMessage(BlackboardVariable[] messageData)
        {
            BlackboardVariable<T0> bbv0 = messageData[0] as BlackboardVariable<T0>;
            BlackboardVariable<T1> bbv1 = messageData[1] as BlackboardVariable<T1>;
            var output0 = bbv0 != null ? bbv0.Value : default(T0);
            var output1 = bbv1 != null ? bbv1.Value : default(T1);

            m_Event?.Invoke(output0, output1);
        }

        /// <inheritdoc cref="EventChannelBase.CreateEventHandler"/>
        public override Delegate CreateEventHandler(BlackboardVariable[] vars, System.Action callback)
        {
            EventHandlerDelegate del = (val0, val1) =>
            {
                BlackboardVariable<T0> var0 = vars[0] as BlackboardVariable<T0>;
                BlackboardVariable<T1> var1 = vars[1] as BlackboardVariable<T1>;
                if (var0 != null) var0.Value = val0;
                if (var1 != null) var1.Value = val1;

                callback();
            };

            return del;
        }

        /// <inheritdoc cref="IEventHandlerWithoutNotify.CreateEventHandlerWithoutNotify"/>
        public virtual Delegate CreateEventHandlerWithoutNotify(BlackboardVariable[] vars, System.Action callback)
        {
            EventHandlerDelegate del = (val0, val1) =>
            {
                BlackboardVariable<T0> var0 = vars[0] as BlackboardVariable<T0>;
                BlackboardVariable<T1> var1 = vars[1] as BlackboardVariable<T1>;
                if (var0 != null) var0.SetValueWithoutNotify(val0);
                if (var1 != null) var1.SetValueWithoutNotify(val1);

                callback();
            };

            return del;
        }

        /// <inheritdoc cref="EventChannelBase.RegisterListener"/>
        public override void RegisterListener(Delegate del)
        {
            m_Event += del as EventHandlerDelegate;
        }

        /// <inheritdoc cref="EventChannelBase.UnregisterListener"/>
        public override void UnregisterListener(Delegate del)
        {
            m_Event -= del as EventHandlerDelegate;
        }
    }

    /// <summary>
    /// Generic implementation of EventChannelBase. Event channels are used to send and receive event messages.
    /// </summary>
    /// <typeparam name="T0">Type of the first message variable</typeparam>
    /// <typeparam name="T1">Type of the second message variable</typeparam>
    /// <typeparam name="T2">Type of the third message variable</typeparam>
    public abstract class EventChannel<T0, T1, T2> : EventChannelBase, IEventHandlerWithoutNotify
    {
        /// <summary>
        /// Represents the method that will handle events with one parameters of types T0, T1 and T2.
        /// </summary>
        /// <param name="value0">The first parameter value provided when the event is raised.</param>
        /// <param name="value1">The second parameter value provided when the event is raised.</param>
        /// <param name="value2">The third parameter value provided when the event is raised.</param>
        public delegate void EventHandlerDelegate(T0 value0, T1 value1, T2 value2);

        /// <summary>
        /// Occurs when the event channel raises an event.
        /// </summary>
        public event EventHandlerDelegate Event
        {
            add { m_Event += value; }
            remove { m_Event -= value; }
        }

        private event EventHandlerDelegate m_Event;

        /// <summary>
        /// Send an event message on the channel.
        /// </summary>
        /// <param name="value0">First value to send on the channel.</param>
        /// <param name="value1">Second value to send on the channel.</param>
        /// <param name="value2">Third value to send on the channel.</param>
        public void SendEventMessage(T0 value0, T1 value1, T2 value2)
        {
            m_Event?.Invoke(value0, value1, value2);
        }

        /// <inheritdoc cref="EventChannelBase.SendEventMessage"/>
        public override void SendEventMessage(BlackboardVariable[] messageData)
        {
            BlackboardVariable<T0> bbv0 = messageData[0] as BlackboardVariable<T0>;
            BlackboardVariable<T1> bbv1 = messageData[1] as BlackboardVariable<T1>;
            BlackboardVariable<T2> bbv2 = messageData[2] as BlackboardVariable<T2>;
            var output0 = bbv0 != null ? bbv0.Value : default(T0);
            var output1 = bbv1 != null ? bbv1.Value : default(T1);
            var output2 = bbv2 != null ? bbv2.Value : default(T2);

            m_Event?.Invoke(output0, output1, output2);
        }

        /// <inheritdoc cref="EventChannelBase.CreateEventHandler"/>
        public override Delegate CreateEventHandler(BlackboardVariable[] vars, System.Action callback)
        {
            EventHandlerDelegate del = (val0, val1, val2) =>
            {
                BlackboardVariable<T0> var0 = vars[0] as BlackboardVariable<T0>;
                BlackboardVariable<T1> var1 = vars[1] as BlackboardVariable<T1>;
                BlackboardVariable<T2> var2 = vars[2] as BlackboardVariable<T2>;
                if (var0 != null) var0.Value = val0;
                if (var1 != null) var1.Value = val1;
                if (var2 != null) var2.Value = val2;

                callback();
            };

            return del;
        }

        /// <inheritdoc cref="IEventHandlerWithoutNotify.CreateEventHandlerWithoutNotify"/>
        public virtual Delegate CreateEventHandlerWithoutNotify(BlackboardVariable[] vars, System.Action callback)
        {
            EventHandlerDelegate del = (val0, val1, val2) =>
            {
                BlackboardVariable<T0> var0 = vars[0] as BlackboardVariable<T0>;
                BlackboardVariable<T1> var1 = vars[1] as BlackboardVariable<T1>;
                BlackboardVariable<T2> var2 = vars[2] as BlackboardVariable<T2>;
                if (var0 != null) var0.SetValueWithoutNotify(val0);
                if (var1 != null) var1.SetValueWithoutNotify(val1);
                if (var2 != null) var2.SetValueWithoutNotify(val2);

                callback();
            };

            return del;
        }

        /// <inheritdoc cref="EventChannelBase.RegisterListener"/>
        public override void RegisterListener(Delegate del)
        {
            m_Event += del as EventHandlerDelegate;
        }

        /// <inheritdoc cref="EventChannelBase.UnregisterListener"/>
        public override void UnregisterListener(Delegate del)
        {
            m_Event -= del as EventHandlerDelegate;
        }
    }

    /// <summary>
    /// Generic implementation of EventChannelBase. Event channels are used to send and receive event messages.
    /// </summary>
    /// <typeparam name="T0">Type of the first message variable</typeparam>
    /// <typeparam name="T1">Type of the second message variable</typeparam>
    /// <typeparam name="T2">Type of the third message variable</typeparam>
    /// <typeparam name="T3">Type of the fourth message variable</typeparam>
    public abstract class EventChannel<T0, T1, T2, T3> : EventChannelBase, IEventHandlerWithoutNotify
    {
        /// <summary>
        /// Represents the method that will handle events with one parameters of types T0, T1, T2 and T3.
        /// </summary>
        /// <param name="value0">The first parameter value provided when the event is raised.</param>
        /// <param name="value1">The second parameter value provided when the event is raised.</param>
        /// <param name="value2">The third parameter value provided when the event is raised.</param>
        /// <param name="value3">The fourth parameter value provided when the event is raised.</param>
        public delegate void EventHandlerDelegate(T0 value0, T1 value1, T2 value2, T3 value3);

        /// <summary>
        /// Occurs when the event channel raises an event.
        /// </summary>
        public event EventHandlerDelegate Event
        {
            add { m_Event += value; }
            remove { m_Event -= value; }
        }

        private event EventHandlerDelegate m_Event;

        /// <summary>
        /// Send an event message on the channel.
        /// </summary>
        /// <param name="value0">First value to send on the channel.</param>
        /// <param name="value1">Second value to send on the channel.</param>
        /// <param name="value2">Third value to send on the channel.</param>
        /// <param name="value3">Fourth value to send on the channel.</param>
        public void SendEventMessage(T0 value0, T1 value1, T2 value2, T3 value3)
        {
            m_Event?.Invoke(value0, value1, value2, value3);
        }

        /// <inheritdoc cref="EventChannelBase.SendEventMessage"/>
        public override void SendEventMessage(BlackboardVariable[] messageData)
        {
            BlackboardVariable<T0> bbv0 = messageData[0] as BlackboardVariable<T0>;
            BlackboardVariable<T1> bbv1 = messageData[1] as BlackboardVariable<T1>;
            BlackboardVariable<T2> bbv2 = messageData[2] as BlackboardVariable<T2>;
            BlackboardVariable<T3> bbv3 = messageData[3] as BlackboardVariable<T3>;
            var output0 = bbv0 != null ? bbv0.Value : default(T0);
            var output1 = bbv1 != null ? bbv1.Value : default(T1);
            var output2 = bbv2 != null ? bbv2.Value : default(T2);
            var output3 = bbv3 != null ? bbv3.Value : default(T3);

            m_Event?.Invoke(output0, output1, output2, output3);
        }

        /// <inheritdoc cref="EventChannelBase.CreateEventHandler"/>
        public override Delegate CreateEventHandler(BlackboardVariable[] vars, System.Action callback)
        {
            EventHandlerDelegate del = (val0, val1, val2, val3) =>
            {
                BlackboardVariable<T0> var0 = vars[0] as BlackboardVariable<T0>;
                BlackboardVariable<T1> var1 = vars[1] as BlackboardVariable<T1>;
                BlackboardVariable<T2> var2 = vars[2] as BlackboardVariable<T2>;
                BlackboardVariable<T3> var3 = vars[3] as BlackboardVariable<T3>;
                if (var0 != null) var0.Value = val0;
                if (var1 != null) var1.Value = val1;
                if (var2 != null) var2.Value = val2;
                if (var3 != null) var3.Value = val3;

                callback();
            };

            return del;
        }

        /// <inheritdoc cref="IEventHandlerWithoutNotify.CreateEventHandlerWithoutNotify"/>
        public virtual Delegate CreateEventHandlerWithoutNotify(BlackboardVariable[] vars, System.Action callback)
        {
            EventHandlerDelegate del = (val0, val1, val2, val3) =>
            {
                BlackboardVariable<T0> var0 = vars[0] as BlackboardVariable<T0>;
                BlackboardVariable<T1> var1 = vars[1] as BlackboardVariable<T1>;
                BlackboardVariable<T2> var2 = vars[2] as BlackboardVariable<T2>;
                BlackboardVariable<T3> var3 = vars[3] as BlackboardVariable<T3>;
                if (var0 != null) var0.SetValueWithoutNotify(val0);
                if (var1 != null) var1.SetValueWithoutNotify(val1);
                if (var2 != null) var2.SetValueWithoutNotify(val2);
                if (var3 != null) var3.SetValueWithoutNotify(val3);

                callback();
            };

            return del;
        }

        /// <inheritdoc cref="EventChannelBase.RegisterListener"/>
        public override void RegisterListener(Delegate del)
        {
            m_Event += del as EventHandlerDelegate;
        }

        /// <inheritdoc cref="EventChannelBase.UnregisterListener"/>
        public override void UnregisterListener(Delegate del)
        {
            m_Event -= del as EventHandlerDelegate;
        }
    }
}
