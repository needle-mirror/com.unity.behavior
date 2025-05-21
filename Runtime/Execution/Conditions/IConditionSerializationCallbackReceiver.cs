namespace Unity.Behavior
{
    /// <summary>
    /// Interface for conditions use by Behavior nodes that need to handle custom serialization/deserialization logic.
    /// Implement this interface on condition classes that contain state which cannot be automatically
    /// serialized, or that need to pre-process data before serialization.
    /// 
    /// Note: These callbacks are only invoked for conditions that are part of a running node.
    /// Standard runtime registration logic should still be implemented in the Condition's OnStart/OnEnd methods.
    /// </summary>
    public interface IConditionSerializationCallbackReceiver
    {
        /// <summary>
        /// Called before a condition is serialized. Use this method to prepare data for serialization,
        /// such as converting complex runtime data into serializable formats or caching state information.
        /// </summary>
        void OnSerialize();

        /// <summary>
        /// Called after a graph containing this condition is deserialized. Use this method to:
        /// - Restore runtime state that cannot be directly serialized
        /// - Rebind references or listeners
        /// - Rebuild caches or re-initialize condition state based on the deserialized data
        /// </summary>
        void OnDeserialize();
    }
}
