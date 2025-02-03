using System;
using System.Collections.Generic;
using Unity.Behavior.GraphFramework;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    /// <summary>
    /// A variable used within a blackboard.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    public abstract class BlackboardVariable
    {
        /// <summary>
        /// A GUID used to uniquely identify the variable.
        /// </summary>
        [SerializeField]
        public SerializableGUID GUID;

        /// <summary>
        /// The name of the variable.
        /// </summary>
        [SerializeField]
        public string Name;

        /// <summary>
        /// The value of the variable.
        /// </summary>
        public abstract object ObjectValue { get; set; }

        /// <summary>
        /// The type of the variable.
        /// </summary>
        public abstract Type Type { get; }

        /// <summary>
        /// Delegate for blackboard variable value changes.
        /// </summary>
        public delegate void ValueChangedCallback();

        /// <summary>
        /// Callback used for changes in the blackboard variable value.
        /// </summary>
        public event ValueChangedCallback OnValueChanged = delegate { };

        /// <summary>
        /// Invokes the OnValueChanged callback.
        /// </summary>
        internal void InvokeValueChanged() => OnValueChanged.Invoke();

        /// <summary>
        /// Duplicates the variable
        /// </summary>
        /// <returns>Returns a copy of the Blackboard Variable.</returns>
        internal abstract BlackboardVariable Duplicate();

        /// <summary>
        /// Allow to set the object value without being notified of the change, if any.
        /// </summary>
        /// <param name="newValue">The new value to be assigned.</param>
        public abstract void SetObjectValueWithoutNotify(object newValue);

        /// <summary>
        /// Creates a BlackboardVariable for a given type.
        /// </summary>
        /// <param name="type">The type of the variable to be created.</param>
        /// <returns>A BlackboardVariable for a given type.</returns>
        /// <exception cref="Exception">Throws an exception when an unsupported type is specified.</exception>
        internal static BlackboardVariable CreateForType(Type type, bool isShared = false)
        {
            if (isShared)
            {
                // For global variables
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(BlackboardVariable<>))
                {
                    return (SharedBlackboardVariable)Activator.CreateInstance(type);
                }

                return Activator.CreateInstance(typeof(SharedBlackboardVariable<>).MakeGenericType(type)) as BlackboardVariable;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(BlackboardVariable<>))
            {
                return (BlackboardVariable)Activator.CreateInstance(type);
            }

            return Activator.CreateInstance(typeof(BlackboardVariable<>).MakeGenericType(type)) as BlackboardVariable;
        }

        /// <summary>
        /// Sets the value of a given variable.
        /// </summary>
        /// <param name="var">The variable to be assigned the specified value.</param>
        /// <param name="value">The value to be assigned to the variable.</param>
        /// <typeparam name="DataType">The type of value to be assigned to the variable.</typeparam>
        /// <returns>Returns true if the value was set on the variable and false otherwise.</returns>
        internal static bool SetTypedBlackboardVariableValue<DataType>(BlackboardVariable var, DataType value)
        {
            if (var is BlackboardVariable<DataType> typedVar)
            {
                typedVar.Value = value;
                return true;
            }

            Debug.LogError($"Variable of type {var?.GetType()} cannot be set with value of type {typeof(DataType)}.");
            return false;
        }

        /// <summary>
        /// Returns true if the two variables are equal and false otherwise.
        /// </summary>
        /// <param name="other">The variable to compare to.</param>
        /// <returns>Returns true if the two variables are equal and false otherwise.</returns>
        public virtual bool Equals(BlackboardVariable other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return GUID.Equals(other.GUID) && Equals(Type, other.Type);
        }

        /// <summary>
        /// Returns true if the values of the two variables are equal and false otherwise.
        /// </summary>
        /// <param name="other">The variable whose value is compared to.</param>
        /// <returns>Returns true if the values of the two variables are equal and false otherwise.</returns>
        public abstract bool ValueEquals(BlackboardVariable other);
    }

    /// <summary>
    /// A base class for blackboard variables serialized by value.
    /// </summary>
    /// <typeparam name="DataType">The type of value stored in the variable.</typeparam>
    [Serializable]
    public partial class BlackboardVariable<DataType> : BlackboardVariable
    {
        /// <summary>
        /// The value of the blackboard variable.
        /// </summary>
        [SerializeField]
        protected DataType m_Value;

        /// <summary>
        /// see <see cref="BlackboardVariable.ObjectValue"/>
        /// </summary>
        public virtual DataType Value
        {
            get => m_Value;
            set
            {
                bool valueChanged = !EqualityComparer<DataType>.Default.Equals(m_Value, value);
                if (valueChanged)
                {
                    m_Value = value;
                    InvokeValueChanged();
                }
            }
        }

        /// <summary>
        /// see <see cref="BlackboardVariable.ObjectValue"/>
        /// </summary>
        public override sealed object ObjectValue
        {
            get => Value;
            set => Value = (DataType)value;
        }

        /// <summary>
        /// see <see cref="BlackboardVariable.Type"/>
        /// </summary>
        public override Type Type => typeof(DataType);

        /// <summary>
        /// BlackboardVariable constructor.
        /// </summary>
        public BlackboardVariable()
        { }

        /// <summary>
        /// BlackboardVariable constructor.
        /// </summary>
        /// <param name="value"> Datatype </param>
        public BlackboardVariable(DataType value)
        {
            m_Value = value;
        }

        /// <inheritdoc cref="SetObjectValueWithoutNotify"/>
        public override void SetObjectValueWithoutNotify(object newValue)
        {
            SetValueWithoutNotify((DataType)newValue);
        }

        /// <summary>
        /// Allow to set the value without being notified of the change, if any.
        /// </summary>
        /// <param name="newValue">The new value to be assigned.</param>
        public virtual void SetValueWithoutNotify(DataType newValue)
        {
            m_Value = newValue;
        }

        /// <summary>
        /// Implicit conversion from BlackboardVariable to DataType.
        /// </summary>
        /// <param name="value">The variable holding the DataType value</param>
        /// <returns>Returns the value stored in the Blackboard Variable.</returns>
        public static implicit operator DataType(BlackboardVariable<DataType> value)
        {
            if (value == null)
            {
                return default;
            }
            return value.Value;
        }

        /// <summary>
        /// Implicit conversion from DataType to BlackboardVariable.
        /// </summary>
        /// <param name="value">Datatype value used to create a BlackboardVariable</param>
        /// <returns>Returns a Blackboard Variable with the given value.</returns>
        public static explicit operator BlackboardVariable<DataType>(DataType value)
        {
            return new BlackboardVariable<DataType>(value);
        }

        /// <summary>
        /// see <see cref="BlackboardVariable.Duplicate"/>
        /// </summary>
        /// <returns>Returns a copy of the Blackboard Variable.</returns>
        internal override BlackboardVariable Duplicate()
        {
            var blackboardVariableDuplicate = CreateForType(Type);
            blackboardVariableDuplicate.Name = Name;
            blackboardVariableDuplicate.ObjectValue = Value;
            blackboardVariableDuplicate.GUID = GUID;
            return blackboardVariableDuplicate;
        }

        /// <inheritdoc cref="Equals"/>
        public override bool Equals(BlackboardVariable other)
        {
            return base.Equals(other) && ValueEquals(other);
        }

        /// <inheritdoc cref="BlackboardVariable.ValueEquals"/>
        public override bool ValueEquals(BlackboardVariable other)
        {
            return other is BlackboardVariable<DataType> typedOther && ValueEquals(typedOther);
        }

        /// <summary>
        /// Returns true if the values of the two variables are equal and false otherwise.
        /// </summary>
        /// <param name="other">The variable whose value is compared to.</param>
        /// <returns>Returns true if the values of the two variables are equal and false otherwise.</returns>
        public bool ValueEquals(BlackboardVariable<DataType> other)
        {
            return EqualityComparer<DataType>.Default.Equals(Value, other.Value);
        }
    }

    /// <summary>
    /// A variable override used for initializing Blackboards on subgraphs that are running dynamically.
    /// </summary>
    [Serializable]
    public class DynamicBlackboardVariableOverride
    {
        /// <summary>
        /// The name of the variable that is being overridden.
        /// </summary>
        public string Name;

        /// <summary>
        /// The Blackboard variable or value used for the override.
        /// </summary>
        [SerializeReference] public BlackboardVariable Variable;
    }
}