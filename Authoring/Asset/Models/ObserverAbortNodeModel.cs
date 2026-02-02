using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable]
    [NodeModelInfo(typeof(ObserverAbortModifier))]
    internal class ObserverAbortNodeModel : ModifierNodeModel, IObserverAbortNodeModel
    {
        [field: SerializeReference]
        public List<ConditionModel> ConditionModels { get; set; } = new List<ConditionModel>();

        [field: SerializeField]
        public bool RequiresAllConditionsTrue { get; set; }

        [field: SerializeField]
        public bool ShouldTruncateNodeUI { get; set; }

        [field: SerializeField]
        public ObserverAbortTarget ObserverType { get; set; } = ObserverAbortTarget.LowerPriority;

        public ObserverAbortNodeModel(NodeInfo nodeInfo) : base(nodeInfo)
        {
        }

        protected ObserverAbortNodeModel(ObserverAbortNodeModel originalModel, BehaviorAuthoringGraph asset)
            : base(originalModel, asset)
        {
            ConditionModels = IConditionalNodeModel.GetConditionModelCopies(originalModel, this);
            RequiresAllConditionsTrue = originalModel.RequiresAllConditionsTrue;
            ShouldTruncateNodeUI = originalModel.ShouldTruncateNodeUI;
            ObserverType = originalModel.ObserverType;
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
            

            if (CanUseObserverAbort())
            {
                if (ObserverType == ObserverAbortTarget.None)
                {
                    ObserverType = ObserverAbortTarget.LowerPriority;
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
    }
}

