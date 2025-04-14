using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Behavior.GraphFramework
{
    internal class DuplicateNodeManipulator : Manipulator
    {
        private GraphView Target => target as GraphView;
        private Vector2 m_LastTargetPosition;
        private Vector2 m_LastMousePosition;

        private const float k_DuplicationOffset = 5f;
        
        protected override void RegisterCallbacksOnTarget()
        {
            Target.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            Target.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.D && evt.modifiers is EventModifiers.Control or EventModifiers.Command)
            {
                List<NodeModel> nodeModelsOriginal = new List<NodeModel>();
                foreach (NodeUI node in Target.ViewState.Selected.OfType<NodeUI>())
                {
                    if (node.Model is { IsDuplicatable: true })
                    {
                        nodeModelsOriginal.Add(node.Model);
                    }
                }

                if (!nodeModelsOriginal.Any())
                {
                    return;
                }

                Vector2 position;
                if (evt.originalMousePosition == m_LastMousePosition)
                {
                    // Cursor has not moved, add a small offset to the new target position.
                    position = new Vector2(m_LastTargetPosition.x + k_DuplicationOffset, m_LastTargetPosition.y + k_DuplicationOffset);
                }
                else
                {
                    // Use the new cursor position instead.
                    position = Target.WorldPosToLocal(evt.originalMousePosition);
                }
                Target.Dispatcher.DispatchImmediate(new DuplicateNodeCommand(nodeModelsOriginal, position));
                m_LastMousePosition = evt.originalMousePosition;
                m_LastTargetPosition = position;
            }
        }
    }
}