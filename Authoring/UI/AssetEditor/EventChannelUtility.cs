using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Behavior.GraphFramework;
using UnityEngine;

namespace Unity.Behavior
{
    internal static class EventChannelUtility
    {
        internal struct EventChannelInfo
        {
            internal string Name;
            internal string Category;
            internal Type VariableModelType;
            
            internal string Path => string.IsNullOrEmpty(Category) ? Name : $"{Category}/{Name}";
        }

        internal static bool IsEventChannelType(Type type, out Type eventChannelModelType)
        {
            if (typeof(EventChannelBase).IsAssignableFrom(type))
            {
                eventChannelModelType = typeof(TypedVariableModel<>).MakeGenericType(type);
                return true;
            }
            eventChannelModelType = null;
            return false;

        }

        internal static IEnumerable<EventChannelInfo> GetEventChannelTypes()
        {   
            foreach (var type in AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes()))
            {
                if (!IsEventChannelType(type, out Type eventChannelModelType) || type.IsGenericType || type.IsAbstract || type.IsNestedPrivate)
                {
                    continue;
                }

                var attribute = (EventChannelDescriptionAttribute) Attribute.GetCustomAttribute(type, typeof (EventChannelDescriptionAttribute));
                
                string channelName;
                string category;
                if (attribute != null)
                {
                    channelName = attribute.Name;
                    category = String.IsNullOrEmpty(attribute.Category) ? "Events" : attribute.Category;
                }
                else
                {
                    // Channels generated prior to the introduction of EventChannelDescriptionAttribute will use default info.
                    channelName = type.Name;
                    category = "Events";
                }

                yield return new EventChannelInfo { Name = channelName, Category = category, VariableModelType = eventChannelModelType };
            }
        }

        internal static (string, Type[]) GetMessageDataFromChannelType(Type channelType)
        {
            if (channelType == null)
                return default;

            var eventHandlerType = GetEventHandlerType(channelType);
            if (eventHandlerType == null)
                return default;

            Type[] eventMessageTypes = eventHandlerType.GetMethod("Invoke")
                .GetParameters()
                .Select(p => p.ParameterType).ToArray();
            var attribute =
                (EventChannelDescriptionAttribute)Attribute.GetCustomAttribute(channelType,
                    typeof(EventChannelDescriptionAttribute));
            return (attribute?.Message, eventMessageTypes);
        }

        /// <summary>
        /// Returns an array of ParameterInfo and their name (from the EventChannel Message) using reflection on the channel type.
        /// </summary>
        internal static (ParameterInfo[], string[]) GetParametersInfoAndNameFromChanneltype(Type channelType)
        {
            if (channelType == null || typeof(EventChannelBase).IsAssignableFrom(channelType) == false)
                return default;

            var eventHandlerType = GetEventHandlerType(channelType);
            if (eventHandlerType == null)
                return default;

            ParameterInfo[] parametersInfo = eventHandlerType.GetMethod("Invoke")
                .GetParameters()
                .ToArray();
            var attribute =
                (EventChannelDescriptionAttribute)Attribute.GetCustomAttribute(channelType,
                    typeof(EventChannelDescriptionAttribute));

            string[] eventFieldTrueName = new string[parametersInfo.Length];
            string[] messageWords = attribute?.Message.Split(" ");
            int parameterIndex = 0;
            for (int i = 0; i < messageWords.Length; ++i)
            {
                string word = messageWords[i];
                if (!(word.StartsWith("[") && word.EndsWith("]"))) //ie a non-parameter word
                {
                    continue;
                }

                word = word.TrimStart('[');
                word = word.TrimEnd(']');
                eventFieldTrueName[parameterIndex] = word;
                parameterIndex++;
            }

            return (parametersInfo, eventFieldTrueName);
        }

        internal static Type GetEventHandlerType(Type channelType)
        {
            // We need to walk back inheritance as GetEvent respect encapsulation by not exposing
            // private members of base classes to derived class reflection operations.
            var eventInfo = channelType.BaseType.GetEvent("m_Event", BindingFlags.NonPublic | BindingFlags.Instance);
            if (eventInfo == null)
            {
                // Backward compatibility with Legacy EventChannelBase derived custom class.
                eventInfo = channelType.GetEvent("Event");
            }

            return eventInfo?.EventHandlerType;
        }
    }
}