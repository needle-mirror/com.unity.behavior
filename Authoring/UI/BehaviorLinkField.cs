using Unity.Behavior.GraphFramework;
using UnityEngine.UIElements;
using System;

namespace Unity.Behavior
{
    internal class BehaviorLinkField<TValueType, TFieldType> : LinkField<TValueType, TFieldType> where TFieldType : VisualElement, INotifyValueChanged<TValueType>, new()
    {
        public override bool IsAssignable(Type type)
        {
            if (GraphAssetProcessor.GetBlackboardVariableConverter(type, LinkVariableType) != null)
            {
                return true;
            }
            return base.IsAssignable(type);
        }

        internal override void OnDragEnter(VariableModel variable)
        {
            LinkedLabelPrefix = Util.GetBlackboardVariablePrefix(Model.Asset, variable);
        }

        internal override void OnDragExit()
        {
            Util.UpdateLinkFieldBlackboardPrefixes(this);
        }
    }
}
