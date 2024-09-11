using UnityEngine;
using UnityEngine.Audio;
using System;

namespace Unity.Behavior
{
    internal class AudioResourceToAudioClipBlackboardVariableConverter : IBlackboardVariableConverter
    {
        public bool CanConvert(Type fromType, Type toType)
        {
            return fromType == typeof(AudioResource) && toType == typeof(AudioClip);
        }

        public BlackboardVariable Convert(Type fromType, Type toType, BlackboardVariable variable)
        {
            return new UnityObjectToUnityObjectBlackboardVariable<AudioResource, AudioClip>(variable as BlackboardVariable<AudioResource>);
        }
    }

    internal class AudioClipToAudioResourceBlackboardVariableConverter : IBlackboardVariableConverter
    {
        public bool CanConvert(Type fromType, Type toType)
        {
            return fromType == typeof(AudioClip) && toType == typeof(AudioResource);
        }

        public BlackboardVariable Convert(Type fromType, Type toType, BlackboardVariable variable)
        {
            return new UnityObjectToUnityObjectBlackboardVariable<AudioClip, AudioResource>(variable as BlackboardVariable<AudioClip>);
        }
    }
}