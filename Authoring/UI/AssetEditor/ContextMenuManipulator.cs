using UnityEngine.UIElements;

namespace Unity.Behavior
{
    internal class ContextMenuManipulator : PointerManipulator
    {
        private bool m_IsActive;
        private System.Action m_CallbackMethod;
        
        internal ContextMenuManipulator(System.Action callbackMethod)
        {
            m_CallbackMethod = callbackMethod;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.RightMouse });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDownEvent);
            target.RegisterCallback<PointerUpEvent>(OnPointerUpEvent);
        }

        private void OnPointerDownEvent(PointerDownEvent evt)
        {
            m_IsActive = true;
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDownEvent);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUpEvent);
        }

        protected void OnPointerUpEvent(PointerUpEvent evt)
        {
            if (!m_IsActive)
            {
                return;
            }

            m_IsActive = false;
            m_CallbackMethod();
        }
    }
}