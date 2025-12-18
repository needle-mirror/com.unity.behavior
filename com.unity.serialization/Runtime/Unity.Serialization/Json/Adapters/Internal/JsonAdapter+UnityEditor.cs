#if UNITY_EDITOR

#if UNITY_6000_5_OR_NEWER
using GUID = UnityEngine.GUID;
#else
using GUID = UnityEditor.GUID;
#endif

namespace Unity.Behavior.Serialization.Json
{
    partial class JsonAdapter : IJsonAdapter
        , IJsonAdapter<GUID>
        , IJsonAdapter<UnityEditor.GlobalObjectId>
    {
        void IJsonAdapter<GUID>.Serialize(in JsonSerializationContext<GUID> context, GUID value)
            => context.Writer.WriteValue(value.ToString());

        GUID IJsonAdapter<GUID>.Deserialize(in JsonDeserializationContext<GUID> context)
            => GUID.TryParse(context.SerializedValue.ToString(), out var value) ? value : default;

        void IJsonAdapter<UnityEditor.GlobalObjectId>.Serialize(in JsonSerializationContext<UnityEditor.GlobalObjectId> context, UnityEditor.GlobalObjectId value)
            => context.Writer.WriteValue(value.ToString());

        UnityEditor.GlobalObjectId IJsonAdapter<UnityEditor.GlobalObjectId>.Deserialize(in JsonDeserializationContext<UnityEditor.GlobalObjectId> context)
            => UnityEditor.GlobalObjectId.TryParse(context.SerializedValue.ToString(), out var value) ? value : default;
    }
}
#endif
