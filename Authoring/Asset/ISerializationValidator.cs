using System.Collections.Generic;

namespace Unity.Behavior
{
    /// <summary>
    /// Provides functionality to validate serialization integrity of Unity Behavior assets.
    /// </summary>
    internal interface ISerializationValidator
    {
        /// <summary>
        /// Determines whether this asset contains any SerializeReference fields with missing or invalid type references.
        /// </summary>
        /// <returns>
        /// true if the asset contains invalid serialized references that could cause data loss or runtime errors;
        /// otherwise, false.
        /// </returns>
        /// <remarks>
        /// This is an expensive operation, but necessary to prevent data corruption
        /// when SerializeReference types are renamed/deleted. Caching is not viable because
        /// the asset doesn't change when classes are renamed - only domain reload reveals the issue.
        /// </remarks>
        bool ContainsInvalidSerializedReferences();

        /// <summary>
        /// Determines whether this asset contains any blackboard variable elements with lost or missing type information.
        /// </summary>       
        /// <param name="assetsContainingLostType">
        /// Output parameter containing a list of RuntimeBlackboardAsset objects that contain lost type information.
        /// Empty if no assets contain lost types.
        /// </param>
        /// <returns>
        /// true if at least one blackboard variable element in the asset has lost all its type data and cannot be deserialized; 
        /// otherwise, false.
        /// </returns>
        bool ContainsLostBlackboardVariableType(out List<RuntimeBlackboardAsset> assetsContainingLostType);
    }
}
