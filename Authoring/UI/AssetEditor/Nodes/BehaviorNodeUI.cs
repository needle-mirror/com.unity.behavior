using System.Collections.Generic;
using Unity.Behavior.GraphFramework;
using UnityEngine.UIElements;

namespace Unity.Behavior
{
    internal class BehaviorNodeUI : NodeUI
    {
        private Node.Status m_CurrentStatus = Node.Status.Uninitialized;
        private List<BaseLinkField> m_CachedLinkFields;
        private bool m_LinkFieldsDirty = true;

        public override NodeModel Model
        {
            get => base.Model;
            set
            {
                base.Model = value;
                m_LinkFieldsDirty = true;
                UpdateLinkFields();
            }
        }

        public BehaviorNodeUI(NodeModel nodeModel) : base(nodeModel)
        {
            styleSheets.Add(ResourceLoadAPI.Load<StyleSheet>("Packages/com.unity.behavior/Authoring/UI/AssetEditor/Assets/BehaviorNodeStylesheet.uss"));
            VisualElement badges = new VisualElement() { name = "Badges" };
            ResourceLoadAPI.Load<VisualTreeAsset>("Packages/com.unity.behavior/Authoring/UI/AssetEditor/Assets/NodeBadgesLayout.uxml").CloneTree(badges);
            badges.styleSheets.Add(ResourceLoadAPI.Load<StyleSheet>("Packages/com.unity.behavior/Authoring/UI/AssetEditor/Assets/NodeBadgesStylesheet.uss"));
            SelectionBorder.Add(badges);

#if UNITY_EDITOR
            if (nodeModel is BehaviorGraphNodeModel)
            {
                BehaviorAuthoringGraph asset = nodeModel.Asset as BehaviorAuthoringGraph;
                if (asset && asset.DebugInfo != null && asset.DebugInfo.IsNodeBreakpointEnabled(nodeModel.ID))
                {
                    AddToClassList("BreakpointEnabled");
                }
            }
#endif
        }

        internal virtual void InitFromNodeInfo(NodeInfo nodeInfo)
        {
            this.AddNodeIcon(nodeInfo);

            ReflectionElement reflectionElement = this.Q<ReflectionElement>();
            if (reflectionElement == null)
            {
                reflectionElement = new ReflectionElement();
                NodeValueContainer.Add(reflectionElement);
            }
            reflectionElement.CreateFields(nodeInfo);
            reflectionElement.Node = Model as BehaviorGraphNodeModel; // Sets the linkfield visuals

            Title = nodeInfo.Name;
            if (reflectionElement.IsTwoLineElement)
            {
                EnableInClassList("TwoLineNode", true);
            }
            else
            {
                EnableInClassList("TwoLineNode", false);
                NodeValueContainer.Hide();
            }
        }

        public override void Refresh(bool isDragging)
        {
            base.Refresh(isDragging);

            var model = Model as BehaviorGraphNodeModel;
            foreach (BaseLinkField field in GetLinkFields())
            {
                if (field.LinkedVariable == null)
                {
                    continue;
                }

                // Ensures that node ui linked variable are up to date.
                foreach (var sourceField in model.Fields)
                {
                    if (sourceField.FieldName != field.FieldName)
                        continue;

                    if (sourceField.LinkedVariable == null || sourceField.LinkedVariable.Name != field.LinkedVariable.Name)
                    {
                        UpdateLinkFields();
                        break;
                    }
                }

                // Keep the linked label prefix updated on Blackboard asset group variables.
                Util.UpdateLinkFieldBlackboardPrefixes(field);
            }
        }

        internal List<BaseLinkField> GetLinkFields()
        {
            if (m_LinkFieldsDirty || m_CachedLinkFields == null)
            {
                m_CachedLinkFields = this.Query<BaseLinkField>().ToList();
                m_LinkFieldsDirty = false;
            }
            return m_CachedLinkFields;
        }

        internal virtual void UpdateLinkFields()
        {
            m_LinkFieldsDirty = true;
            foreach (var linkField in GetLinkFields())
            {
                linkField.Model = Model;
            }
        }

        internal virtual void UpdateStatus(Node.Status status)
        {
            if (m_CurrentStatus == status)
                return;
            m_CurrentStatus = status;

            EnableInClassList("NodeStatus_Uninitialized", status is Node.Status.Uninitialized);
            EnableInClassList("NodeStatus_Running", status is Node.Status.Running);
            EnableInClassList("NodeStatus_Success", status is Node.Status.Success);
            EnableInClassList("NodeStatus_Failure", status is Node.Status.Failure);
            EnableInClassList("NodeStatus_Waiting", status is Node.Status.Waiting);
            EnableInClassList("NodeStatus_Interrupted", status is Node.Status.Interrupted);

            DebugIconElement.tooltip = $"Current State: {status}.";

            var firstInputPort = GetFirstInputPort();
            if (firstInputPort != null)
            {
                bool isRunning = !(status is Node.Status.Uninitialized);
                foreach (Edge edge in firstInputPort.Edges)
                {
                    edge.IsDebugHighlighted = isRunning;
                    edge.MarkDirtyAndRepaint();
                    edge.EnableInClassList("NodeStatus_Running", isRunning);
                    edge.EnableInClassList("NodeStatus_Uninitialized", !isRunning);
                }
            }
        }
    }
}
