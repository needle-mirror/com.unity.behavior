using System;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable]
    [NodeModelInfo(typeof(SetVariableValueAction))]
    internal class SetValueNodeModel : BehaviorGraphNodeModel
    {
        internal const string k_VariableFieldName = "Variable";
        internal const string k_ValueFieldName = "Value";
        public override bool IsSequenceable => true;

        public SetValueNodeModel(NodeInfo nodeInfo) : base(nodeInfo) { }

        protected SetValueNodeModel(SetValueNodeModel nodeModelOriginal, BehaviorAuthoringGraph asset) : base(nodeModelOriginal, asset)
        {
        }

        protected override void EnsureFieldValuesAreUpToDate()
        {
            FieldModel variableField = null;
            FieldModel valueField = null;
            foreach (FieldModel field in Fields)
            {
                if (field.FieldName == k_ValueFieldName)
                {
                    valueField = field;
                }
                else if (field.FieldName == k_VariableFieldName)
                {
                    variableField = field;
                }
            }

            Type variableType = variableField?.LinkedVariable?.Type;
            if (variableType == null || valueField == null)
            {
                return;
            }

            // If the value field types do not match the variable field type, clear the linked variable and local value.
            Type linkType = valueField.LinkedVariable?.Type;
            if (valueField.LinkedVariable != null && !variableType.IsAssignableFrom(linkType) && GraphAssetProcessor.GetBlackboardVariableConverter(linkType, variableType) == null)
            {
                Debug.LogWarning($"{Asset.name}: Linked variable of type {linkType} cannot be assigned to type {variableType} and no suitable converter was found. Resetting the link.", Asset);
                valueField.LinkedVariable = null;
            }
            if (valueField.LocalValue?.Type != variableType)
            {
                valueField.LocalValue = BlackboardVariable.CreateForType(variableType);
            }
        }
    }
}