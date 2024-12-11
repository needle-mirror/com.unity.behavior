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

        private void UpdateNodeType()
        {
            switch (Mode)
            {
                case ParallelMode.Default:
                    NodeType = typeof(ParallelAllComposite);
                    break;

                case ParallelMode.UntilAnySucceed:
                    NodeType = typeof(ParallelAnySuccess);
                    break;

                case ParallelMode.UntilAnyFail:
                    NodeType = typeof(ParallelAllSuccess);
                    break;

                case ParallelMode.UntilAnyComplete:
                    NodeType = typeof(ParallelAnyComposite);
                    break;
            }
            Type type = NodeType;
            NodeDescriptionAttribute attribute = type.GetCustomAttribute<NodeDescriptionAttribute>();
            if (attribute != null)
            {
                NodeTypeID = attribute.GUID;
            }
        }
    }
}