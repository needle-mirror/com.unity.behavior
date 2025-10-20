using System;
using System.Reflection;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable]
    [NodeModelInfo(typeof(ParallelAllComposite))]
    [NodeModelInfo(typeof(ParallelAllSuccess))]
    [NodeModelInfo(typeof(ParallelAnyComposite))]
    [NodeModelInfo(typeof(ParallelAnySuccess))]
    internal class RunInParallelNodeModel : CompositeNodeModel
    {
        [Serializable]
        public enum ParallelMode
        {
            Default,
            UntilAnyComplete,
            UntilAnySucceed,
            UntilAnyFail
        }
        [SerializeField]
        private ParallelMode m_Mode;
        public ParallelMode Mode { get => m_Mode; set => m_Mode = value; }

        public RunInParallelNodeModel(NodeInfo nodeInfo) : base(nodeInfo)
        {
            if (nodeInfo == null || nodeInfo.Type == null)
            {
                return;
            }

            if (nodeInfo.Type == typeof(ParallelAllComposite)) Mode = ParallelMode.Default;
            else if (nodeInfo.Type == typeof(ParallelAnySuccess)) Mode = ParallelMode.UntilAnySucceed;
            else if (nodeInfo.Type == typeof(ParallelAllSuccess)) Mode = ParallelMode.UntilAnyFail;
            else if (nodeInfo.Type == typeof(ParallelAnyComposite)) Mode = ParallelMode.UntilAnyComplete;
        }

        protected RunInParallelNodeModel(RunInParallelNodeModel nodeModelOriginal, BehaviorAuthoringGraph asset) : base(nodeModelOriginal, asset)
        {
            this.Mode = nodeModelOriginal.Mode;
        }

        public override void OnValidate()
        {
            base.OnValidate();
            UpdateNodeType();
        }

        // Ensures the node model type is up to date. If not, dirty asset as runtime graph needs to rebuild.
        private void UpdateNodeType()
        {
            Type expectedType = null;
            switch (Mode)
            {
                case ParallelMode.Default:
                    expectedType = typeof(ParallelAllComposite);
                    break;

                case ParallelMode.UntilAnySucceed:
                    expectedType = typeof(ParallelAnySuccess);
                    break;

                case ParallelMode.UntilAnyFail:
                    expectedType = typeof(ParallelAllSuccess);
                    break;

                case ParallelMode.UntilAnyComplete:
                    expectedType = typeof(ParallelAnyComposite);
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
