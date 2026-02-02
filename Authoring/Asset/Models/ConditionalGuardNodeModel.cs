using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable]
    [NodeModelInfo(typeof(ConditionalGuardAction))]
    [NodeModelInfo(typeof(ConditionalGuardModifier))]
    internal class ConditionalGuardNodeModel : BehaviorGraphNodeModel, IObserverAbortNodeModel
    {
        [Serializable]
        public enum GuardMode
        {
            Action,
            Modifier
        }

        public override bool IsSequenceable => true;

        [SerializeField]
        private GuardMode m_Mode = GuardMode.Action;
        public GuardMode Mode { get => m_Mode; set => m_Mode = value; }

        [field: SerializeReference]
        public List<ConditionModel> ConditionModels { get; set; } = new List<ConditionModel>();

        [field: SerializeField]
        public bool RequiresAllConditionsTrue { get; set; }

        [field: SerializeField]
        public bool ShouldTruncateNodeUI { get; set; }

        [field: SerializeField]
        public ObserverAbortTarget ObserverType { get; set; } = ObserverAbortTarget.None;

        public ConditionalGuardNodeModel(NodeInfo nodeInfo) : base(nodeInfo)
        {
            if (nodeInfo == null || nodeInfo.Type == null)
            {
                return;
            }

            Mode = GuardMode.Action;
        }

        protected ConditionalGuardNodeModel(ConditionalGuardNodeModel originalModel, BehaviorAuthoringGraph asset) : base(
            originalModel, asset)
        {
            ConditionModels = IConditionalNodeModel.GetConditionModelCopies(originalModel, this);
            RequiresAllConditionsTrue = originalModel.RequiresAllConditionsTrue;
            ShouldTruncateNodeUI = originalModel.ShouldTruncateNodeUI;
            ObserverType = originalModel.ObserverType;
            Mode = originalModel.Mode;
        }

        public override void OnDefineNode()
        {
            base.OnDefineNode();
            foreach (ConditionModel conditionModel in ConditionModels)
            {
                conditionModel.DefineNode();
            }
        }

        public override void OnValidate()
        {
            base.OnValidate();
            UpdateNodeType();

            // ConditionalGuard only supports None or LowerPriority
            if (Mode == GuardMode.Action && ObserverType != ObserverAbortTarget.None)
            {
                ObserverType = ObserverAbortTarget.None;
                Asset.SetAssetDirty(true);
            }

            IConditionalNodeModel.UpdateConditionModels(this);
        }

        // Ensures the node model type is up to date. If not, dirty asset as runtime graph needs to rebuild.
        private void UpdateNodeType()
        {
            Type expectedType = null;
            
            // Auto-switch to Action/Modifier based on the context.
            Mode = CanUseObserverAbort() ? GuardMode.Modifier : GuardMode.Action;

            switch (Mode)
            {
                case GuardMode.Action:
                    expectedType = typeof(ConditionalGuardAction);
                    ObserverType = ObserverAbortTarget.None;
                    break;

                case GuardMode.Modifier:
                    expectedType = typeof(ConditionalGuardModifier);
                    break;
            }

            if (NodeType != null && expectedType == NodeType.Type)
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
