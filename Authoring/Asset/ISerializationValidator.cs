
namespace Unity.Behavior
{
    /// <summary>
    /// Provides functionality to validate serialization integrity of Unity assets.
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
    }
}
