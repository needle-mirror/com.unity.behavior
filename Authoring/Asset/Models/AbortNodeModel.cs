using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable]
    [NodeModelInfo(typeof(AbortModifier))]
    [NodeModelInfo(typeof(RestartModifier))]
    internal class AbortNodeModel : ModifierNodeModel, IObserverAbortNodeModel
    {
        public AbortType ModelAbortType { get => m_ModelAbortType; set => m_ModelAbortType = value; }
        [SerializeField]
        private AbortType m_ModelAbortType;

        internal enum AbortType
        {
            Fail,
            Restart
        }

        [field: SerializeReference]
        public List<ConditionModel> ConditionModels { get; set; } = new List<ConditionModel>();

        [field: SerializeField]
        public bool RequiresAllConditionsTrue { get; set; }

        [field: SerializeField]
        public bool ShouldTruncateNodeUI { get; set; }

        [field: SerializeField]
        public ObserverAbortTarget ObserverType { get; set; } = ObserverAbortTarget.None;

        public AbortNodeModel(NodeInfo nodeInfo) : base(nodeInfo)
        {
            ModelAbortType = typeof(RestartModifier).IsAssignableFrom(NodeType) ? AbortType.Restart : AbortType.Fail;
        }

        protected AbortNodeModel(AbortNodeModel originalModel, BehaviorAuthoringGraph asset) : base(
            originalModel, asset)
        {
            ConditionModels = IConditionalNodeModel.GetConditionModelCopies(originalModel, this);
            ModelAbortType = originalModel.ModelAbortType;
            RequiresAllConditionsTrue = originalModel.RequiresAllConditionsTrue;
            ShouldTruncateNodeUI = originalModel.ShouldTruncateNodeUI;
            ObserverType = originalModel.ObserverType;
        }

        public override void OnValidate()
        {
            base.OnValidate();
            UpdateNodeType();

            if (CanUseObserverAbort())
            {
                // Abort/Restart nodes only support None or LowerPriority (Self/Both don't make semantic sense)
                if (ObserverType == ObserverAbortTarget.Self || ObserverType == ObserverAbortTarget.Both)
                {
                    ObserverType = ObserverAbortTarget.None;
                    Asset.SetAssetDirty(true);
                }
            }
            else if (ObserverType != ObserverAbortTarget.None)
            {
                ObserverType = ObserverAbortTarget.None;
                Asset.SetAssetDirty(true);
            }

            IConditionalNodeModel.UpdateConditionModels(this);
        }

        public override void OnDefineNode()
        {
            base.OnDefineNode();
            foreach (ConditionModel conditionModel in ConditionModels)
            {
                conditionModel.DefineNode();
            }
        }

        // Ensures the node model type is up to date. If not, dirty asset as runtime graph needs to rebuild.
        private void UpdateNodeType()
        {
            Type expectedType = ModelAbortType == AbortType.Fail ? typeof(AbortModifier) : typeof(RestartModifier);
            if (NodeType != null && NodeType.Type == expectedType)
            {
                return;
            }

            NodeType = expectedType;
            NodeDescriptionAttribute attribute = expectedType.GetCustomAttribute<NodeDescriptionAttribute>();
            if (attribute != null)
            {
                NodeTypeID = attribute.GUID;
            }

            Asset.SetAssetDirty(true);
        }
    }
}
