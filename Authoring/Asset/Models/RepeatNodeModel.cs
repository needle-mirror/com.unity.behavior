using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable]
    [NodeModelInfo(typeof(RepeaterModifier))]
    [NodeModelInfo(typeof(RepeatUntilFailModifier))]
    [NodeModelInfo(typeof(RepeatUntilSuccessModifier))]
    [NodeModelInfo(typeof(RepeatWhileConditionModifier))]
    internal class RepeatNodeModel : ModifierNodeModel, IConditionalNodeModel
    {
        public bool AllowMultipleRepeatsPerTick = false;

        [field: SerializeReference]
        public List<ConditionModel> ConditionModels { get; set; } = new List<ConditionModel>();

        [field: SerializeField]
        public bool RequiresAllConditionsTrue { get; set; }

        [field: SerializeField]
        public bool ShouldTruncateNodeUI { get; set; }

        [Serializable]
        public enum RepeatMode
        {
            Forever,
            UntilSuccess,
            UntilFail,
            Condition
        }

        [SerializeField]
        private RepeatMode m_RepeatMode;

        public RepeatMode Mode
        {
            get => m_RepeatMode;
            set
            {
                m_RepeatMode = value;
            }
        }

        public RepeatNodeModel(NodeInfo nodeInfo) : base(nodeInfo)
        {
        }

        protected RepeatNodeModel(RepeatNodeModel originalModel, BehaviorAuthoringGraph asset)
            : base(originalModel, asset)
        {
            Mode = originalModel.Mode;
            ConditionModels = IConditionalNodeModel.GetConditionModelCopies(originalModel, this);
            RequiresAllConditionsTrue = originalModel.RequiresAllConditionsTrue;
            ShouldTruncateNodeUI = originalModel.ShouldTruncateNodeUI;
            UpdateNodeType();
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

            IConditionalNodeModel.UpdateConditionModels(this);
        }

        // Ensures the node model type is up to date. If not, dirty asset as runtime graph needs to rebuild.
        private void UpdateNodeType()
        {
            Type expectedType = null;
            switch (Mode)
            {
                case RepeatMode.Forever:
                    expectedType = typeof(RepeaterModifier);
                    break;

                case RepeatMode.UntilSuccess:
                    expectedType = typeof(RepeatUntilSuccessModifier);
                    break;

                case RepeatMode.UntilFail:
                    expectedType = typeof(RepeatUntilFailModifier);
                    break;

                case RepeatMode.Condition:
                    expectedType = typeof(RepeatWhileConditionModifier);
                    break;
            }

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
