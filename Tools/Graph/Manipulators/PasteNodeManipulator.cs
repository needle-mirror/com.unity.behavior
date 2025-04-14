using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Behavior.GraphFramework
{
    internal class PasteNodeManipulator : Manipulator
    {
        private GraphView Target => target as GraphView;
        private Vector2 m_LastTargetPosition;
        private Vector2 m_LastMousePosition;
        
        private const float k_PasteOffset = 5f;
        
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
            if (evt.keyCode == KeyCode.V && evt.modifiers is EventModifiers.Control or EventModifiers.Command)
            {
                string jsonString = GUIUtility.systemCopyBuffer;
                NodeCopyData copyData = new NodeCopyData();
                try
                {
                    JsonUtility.FromJsonOverwrite(jsonString, copyData);
                }
                catch
                {
                    return;
                }

                Vector2 position;
                if (evt.originalMousePosition == m_LastMousePosition)
                {
                    // Cursor has not moved, add a small offset to the new target position.
                    position = new Vector2(m_LastTargetPosition.x + k_PasteOffset, m_LastTargetPosition.y + k_PasteOffset);
                }
                else
                {
                    position = Target.WorldPosToLocal(evt.originalMousePosition);
                }
                
                Target.Dispatcher.DispatchImmediate(new PasteNodeCommand(copyData.Nodes, position));
                m_LastMousePosition = evt.originalMousePosition;
                m_LastTargetPosition = position;
            }
        }
    }
}