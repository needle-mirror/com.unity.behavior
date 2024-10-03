using System;
using Unity.Behavior.GraphFramework;
using UnityEngine;

namespace Unity.Behavior
{
    /// <summary>
    /// A reference to a Blackboard.
    /// </summary>
    [Serializable]
    public class BlackboardReference
    {
        [SerializeReference]
        private Blackboard m_Blackboard = new Blackboard();
        
        /// <summary>
        /// An instantiated copy of the Blackboard asset to be used.
        /// </summary>
        public Blackboard Blackboard => m_Blackboard;
        
        [SerializeReference]
        private RuntimeBlackboardAsset m_Source;

        internal RuntimeBlackboardAsset SourceBlackboardAsset
        {
            get => m_Source;
            set
            {
                m_Source = value;
                if (value != null)
                {
                    m_Blackboard.GenerateInstanceData(value.Blackboard, SourceBlackboardAsset);   
                }
            }
        }
        
        /// <summary>
        /// Adds a variable of a given type and value to the blackboard. A variable will only be added if one with the
        /// same name does not already exist within the blackboard's variables.
        /// </summary>
        /// <param name="name">The name of the variable</param> 
        /// <param name="value">The value to assign to be assigned to the variable</param>
        /// <typeparam name="TValue">The type of value stored by the variable</typeparam>
        /// <returns>Returns true if the variable is added and false if a matching variable already exists.</returns>
        public bool AddVariable<TValue>(string name, TValue value)
        {
            var wasSuccessful = Blackboard.AddVariable(name, value);
            return wasSuccessful;
        }
        
        /// <summary>
        /// Gets a variable associated with the specified name and value type. For values of type subclassed from
        /// UnityEngine.Object, use the non-generic method.
        /// </summary>
        /// <param name="name">The name of the variable</param> 
        /// <param name="variable">The blackboard variable matching the name and value type</param>
        /// <typeparam name="TValue">The type of value stored by the variable</typeparam>
        /// <returns>Returns true if a variable matching the name and type is found. Returns false otherwise.</returns>
        public bool GetVariable<TValue>(string name, out BlackboardVariable<TValue> variable)
        {
            var wasSuccessful = Blackboard.GetVariable(name, out variable);
            return wasSuccessful;
        }
        
        /// <summary>
        /// Gets the variable associated with the specified name.
        /// </summary>
        /// <param name="name">The name of the variable</param> 
        /// <param name="variable">Contains the value associated with the specified name, if the named variable is found;
        /// otherwise, the default value is assigned.</param>
        /// <returns>Returns true if a variable matching the name and type is found. Returns false otherwise.</returns>
        public bool GetVariable(string name, out BlackboardVariable variable)
        {
            var wasSuccessful = Blackboard.GetVariable(name, out variable);
            return wasSuccessful;
        }

        /// <summary>
        /// Tries to get the variable's value associated with the specified name.
        /// </summary>
        /// <param name="name">The name of the variable</param> 
        /// <param name="value">The value associated with the specified name, if the named variable is found;
        /// otherwise, the default value is assigned.</param>
        /// <typeparam name="TValue">The type of value stored by the variable</typeparam>
        /// <returns>Returns true if a variable matching the name and type is found. Returns false otherwise.</returns>
        public bool GetVariableValue<TValue>(string name, out TValue value)
        {
            var wasSuccessful = Blackboard.GetVariableValue(name, out value);
            return wasSuccessful;
        }

        /// <summary>
        /// Sets the value of a blackboard variable matching the specified name and value type.
        /// </summary>
        /// <param name="name">The name of the variable</param>
        /// <param name="value">The value to assign to the variable</param>
        /// <typeparam name="TValue">The type of value stored by the variable</typeparam>
        /// <returns>Returns true if a variable matching the name and type is found and set. Returns false otherwise.</returns>
        public bool SetVariableValue<TValue>(string name, TValue value)
        {
            var wasSuccessful = Blackboard.SetVariableValue(name, value);
            return wasSuccessful;
        }

        /// <summary>
        /// Gets the ID of the variable associated with the specified name. 
        /// </summary>
        /// <param name="name">The name of the variable</param> 
        /// <param name="id">Contains the ID associated with the specified name, if the named variable is found;
        /// otherwise, the default value is assigned.</param>
        /// <returns>Returns true if a variable matching the name and type is found. Returns false otherwise.</returns>
        public bool GetVariableID(string name, out SerializableGUID id)
        {
            var wasSuccessful = Blackboard.GetVariableID(name, out id);
            return wasSuccessful;
        }

        /// <summary>
        /// Gets a variable associated with the specified GUID.
        /// </summary>
        /// <param name="guid">The GUID of the variable to get</param>
        /// <param name="variable">The variable associated with the specified GUID.</param>
        /// <returns>Returns true if a variable with a matching GUID was found and false otherwise.</returns>
        public bool GetVariable(SerializableGUID guid, out BlackboardVariable variable)
        {
            var wasSuccessful = Blackboard.GetVariable(guid, out variable);
            return wasSuccessful;
        }
        
        /// <summary>
        /// Gets a variable associated with the specified GUID and value type.
        /// </summary>
        /// <param name="guid">The GUID of the variable to get</param>
        /// <param name="variable">The variable associated with the specified GUID.</param>
        /// <typeparam name="TValue">The value type of the variable</typeparam>
        /// <returns>Returns true if a variable with a matching GUID and type was found and false otherwise.</returns>
        public bool GetVariable<TValue>(SerializableGUID guid, out BlackboardVariable<TValue> variable)
        {
            var wasSuccessful = Blackboard.GetVariable(guid, out variable);
            return wasSuccessful;
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
            var wasSuccessful = Blackboard.SetVariableValue(guid, value);
            return wasSuccessful;
        }
    }
}