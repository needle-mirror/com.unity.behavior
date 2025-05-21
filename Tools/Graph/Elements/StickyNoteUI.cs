using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Behavior.GraphFramework
{
    [NodeUI(typeof(StickyNoteModel))]
    internal class StickyNoteUI : NodeUI
    {
        private const long k_UndoTimeThresholdInMs = 2000; // Milliseconds
        private const int k_UndoCharThreshold = 15;

        public new StickyNoteModel Model { get => base.Model as StickyNoteModel; }
        
        private EditableLabel m_EditableLabel;
        private string m_LastUndoText;
        private long m_LastMarkTime;

        public StickyNoteUI(NodeModel nodeModel) : base(nodeModel)
        {
            AddToClassList("StickyNote");
            styleSheets.Add(ResourceLoadAPI.Load<StyleSheet>("Packages/com.unity.behavior/Tools/Graph/Assets/StickyNoteStylesheet.uss"));
            Add(ResourceLoadAPI.Load<VisualTreeAsset>("Packages/com.unity.behavior/Tools/Graph/Assets/StickyNoteLayout.uxml").CloneTree());

            m_EditableLabel = this.Q<EditableLabel>();
            m_EditableLabel.UserInputTextChanged += OnValueChanged;
            m_EditableLabel.RegisterCallback<InputEvent>(OnTextValueChanged);
            m_EditableLabel.Text = Model.Text;
            
            m_LastUndoText = Model.Text;
            m_LastMarkTime = Environment.TickCount;
        }

        public override void Refresh(bool isDragging)
        {
            base.Refresh(isDragging);
            m_EditableLabel.Text = Model.Text;
            m_LastUndoText = Model.Text;
        }

        private void OnValueChanged(string newValue)
        {
            Model.Asset.MarkUndo("Edit Note", hasOutstandingChange: false);
            Model.Text = newValue;
            m_LastUndoText = Model.Text;
            m_LastMarkTime = Environment.TickCount;
        }

        private void OnTextValueChanged(InputEvent evt)
        {
            var currentTime = Environment.TickCount;
            string newText = evt.newData;

            bool shouldMarkUndo = (currentTime - m_LastMarkTime) > k_UndoTimeThresholdInMs;

            // natural breaking points
            if (!shouldMarkUndo && newText.Length > 0)
            {
                char lastChar = newText[newText.Length - 1];
                shouldMarkUndo = lastChar == ' ' || lastChar == '\n' ||
                                 lastChar == '.' || lastChar == '!' || lastChar == '?' ||
                                 lastChar == ',' || lastChar == ';' || lastChar == ':';
            }

            // character treshold
            if (!shouldMarkUndo && Math.Abs(newText.Length - m_LastUndoText.Length) > k_UndoCharThreshold) 
            {
                shouldMarkUndo = true;
                m_LastUndoText = newText;
            }

            if (shouldMarkUndo)
            {
                Model.Asset.MarkUndo("Edit Note", hasOutstandingChange: false);
                m_LastMarkTime = currentTime;
            }

            Model.Text = newText;
        }
    }
}