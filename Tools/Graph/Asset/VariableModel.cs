using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.Behavior.GraphFramework
{
    /// <summary>
    /// Data model representing a variable.
    /// </summary>
    [Serializable]
    public abstract class VariableModel : IEquatable<VariableModel>
    {
        /// <summary>
        /// ID of the variable.
        /// </summary>
        [SerializeField]
        public SerializableGUID ID = SerializableGUID.Generate();

        /// <summary>
        /// Name of the variable.
        /// </summary>
        [SerializeField]
        public string Name;

        /// <summary>
        /// Boolean indicating whether the variable is exposed.
        /// </summary>
        [SerializeField]
        public bool IsExposed = true;

        /// <summary>
        /// Bool indicating whether the variable is shared.
        /// </summary>
        public bool IsShared
        {
            get => m_IsShared;
            set
            {
                if (m_IsShared != value)
                {
                    m_IsShared = value;
                    IsSharedChanged?.Invoke();
                }
            }
        }
        
        [SerializeField, FormerlySerializedAs("IsShared")]
        private bool m_IsShared = false;
        
        /// <summary>
        /// Delegate for variable shared setting changes.
        /// </summary>
        public delegate void IsSharedChangedCallback();

        /// <summary>
        /// Callback used for changes in the variable IsShared value.
        /// </summary>
        public event IsSharedChangedCallback IsSharedChanged = delegate { };
        
        /// <summary>
        /// Invokes the OnIsSharedChanged callback.
        /// </summary>
        internal void InvokeIsSharedChanged() => IsSharedChanged.Invoke();

        /// <summary>
        /// Interface for getting or setting the value of the variable.
        /// </summary>
        public abstract object ObjectValue { get; set; }
        /// <summary>
        /// Interface for getting the type of the variable.
        /// </summary>
        public abstract Type Type { get; }

        /// <summary>
        /// Base method allowing variable models to be validated.
        /// </summary>
        public virtual void OnValidate() { }
        
        /// <summary>
        /// Get's the hash code of the variable ID.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode() => ID.GetHashCode();
        
        /// <summary>
        /// Compares an object to this variable model, casts to VariableModel and invokes an id and type check.
        /// </summary>
        /// <param name="other">Object to compare.</param>
        /// <returns>True if equal, false otherwise</returns>
        public override bool Equals(object other) => Equals(other as VariableModel);
        
        /// <summary>
        /// Compares two VariableModels for equality of ID and Type.
        /// </summary>
        /// <param name="other">VariableModel to compare to.</param>
        /// <returns>True if equal, false otherwise</returns>
        public bool Equals(VariableModel other) => other != null && other.ID == ID && other.Type == Type && other.Name == Name;
        /// <summary>
        /// Compares two VariableModels for equality of ID and Type.
        /// </summary>
        /// <param name="a">First to compare.</param>
        /// <param name="b">Second to compare.</param>
        /// <returns>True if equal, false otherwise</returns>
        public static bool operator ==(VariableModel a, VariableModel b) => a?.Equals(b) ?? b is null;
        /// <summary>
        /// Compares two VariableModels for oinequality of ID and Type.
        /// </summary>
        /// <param name="a">First to compare.</param>
        /// <param name="b">Second to compare.</param>
        /// <returns>True if not equal, false otherwise.</returns>
        public static bool operator !=(VariableModel a, VariableModel b) => !(a == b);
    }
}