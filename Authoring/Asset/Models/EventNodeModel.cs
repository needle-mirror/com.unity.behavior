using Unity.Behavior.GraphFramework;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Unity.Behavior
{
    internal class EventNodeModel : BehaviorGraphNodeModel
    {
        public static readonly string ChannelFieldName = "ChannelVariable";

        public override bool IsSequenceable => true;

        public SerializableType EventChannelType
        {
            get
            {
                var field = Fields.FirstOrDefault(field => field.FieldName == ChannelFieldName);
                if (field == null)
                {
                    return null;
                }
                if (field.LinkedVariable != null)
                {
                    return field.LinkedVariable.Type;
                }
                if (field.LocalValue?.ObjectValue != null)
                {
                    return field.LocalValue.ObjectValue.GetType();
                }
                return null;
            }
        }

        public EventNodeModel(NodeInfo nodeInfo) : base(nodeInfo) { }

        protected EventNodeModel(EventNodeModel nodeModelOriginal, BehaviorAuthoringGraph asset) : base(nodeModelOriginal, asset) { }

        protected override void EnsureFieldValuesAreUpToDate()
        {
            Type channelType = EventChannelType;
            if (channelType == null)
            {
                // No channel is assigned, so remove variable fields.
                m_FieldValues.Clear();
                GetOrCreateField(ChannelFieldName, typeof(EventChannelBase));
                return;
            }

            var eventHandlerType = EventChannelUtility.GetEventHandlerType(channelType);
            if (eventHandlerType == null)
            {
                Debug.LogWarning($"Failed to retrieve event handler type for event channel '{channelType.Name}'. " +
                    $"The class should be sealed and inherit from '{typeof(EventChannel).Name}'.");
                return;
            }

            (ParameterInfo[] messageParameters, string[] parametersName) = EventChannelUtility.GetParametersInfoAndNameFromChanneltype(EventChannelType);

            // Check if number of message types is correct
            if (messageParameters.Length != m_FieldValues.Count - 1)
            {
                RecreateFields(messageParameters, channelType, parametersName);
                return;
            }

            // Check if channel message types align with field types
            for (int i = 0; i < messageParameters.Length; ++i)
            {
                int messageFieldIndex = i + 1;  // offset by one due to channel field + any addition field
                ParameterInfo info = messageParameters[i];
                var currentField = m_FieldValues[messageFieldIndex];
                Type fieldValueType = currentField?.Type;
                if (fieldValueType == null || !fieldValueType.IsAssignableFrom(info.ParameterType)
                    || currentField?.FieldName != parametersName[i])
                {
                    RecreateFields(messageParameters, channelType, parametersName);
                    return;
                }
            }
        }

        private void RecreateFields(ParameterInfo[] messageParameters, Type channelType, string[] parametersName)
        {
            bool MatchesMessageParam(FieldModel field)
            {
                return messageParameters.Any(param =>
                    field.FieldName == param.Name && (Type)field.Type == param.ParameterType);
            }

            FieldModel channelField = GetOrCreateField(ChannelFieldName, channelType);
            m_FieldValues.RemoveAll(field => field != channelField && !MatchesMessageParam(field));
            for (int m = 0; m < messageParameters.Length; m++)
            {
                ParameterInfo info = messageParameters[m];
                GetOrCreateField(parametersName[m], info.ParameterType);
            }
        }
    }
}