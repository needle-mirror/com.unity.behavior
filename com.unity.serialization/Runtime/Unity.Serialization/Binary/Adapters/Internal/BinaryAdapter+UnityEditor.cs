#if UNITY_EDITOR
using Unity.Collections.LowLevel.Unsafe.NotBurstCompatible;

#if UNITY_6000_5_OR_NEWER
using GUID = UnityEngine.GUID;
#else
using GUID = UnityEditor.GUID;
#endif

namespace Unity.Behavior.Serialization.Binary
{
    unsafe partial class BinaryAdapter : IBinaryAdapter
        , IBinaryAdapter<GUID>
        , IBinaryAdapter<UnityEditor.GlobalObjectId>
    {
        void IBinaryAdapter<GUID>.Serialize(in BinarySerializationContext<GUID> context, GUID value)
        {
            context.Writer->AddNBC(value.ToString());
        }

        GUID IBinaryAdapter<GUID>.Deserialize(in BinaryDeserializationContext<GUID> context)
        {
            context.Reader->ReadNextNBC(out var str);
            return GUID.TryParse(str, out var value) ? value : default;
        }

        void IBinaryAdapter<UnityEditor.GlobalObjectId>.Serialize(in BinarySerializationContext<UnityEditor.GlobalObjectId> context, UnityEditor.GlobalObjectId value)
        {
            context.Writer->AddNBC(value.ToString());
        }

        UnityEditor.GlobalObjectId IBinaryAdapter<UnityEditor.GlobalObjectId>.Deserialize(in BinaryDeserializationContext<UnityEditor.GlobalObjectId> context)
        {
            context.Reader->ReadNextNBC(out var str);
            return UnityEditor.GlobalObjectId.TryParse(str, out var value) ? value : default;
        }
    }
}
#endif
