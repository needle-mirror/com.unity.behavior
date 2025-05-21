using System;
using System.Collections.Generic;
using Unity.AppUI.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Behavior.GraphFramework
{
    [UxmlElement]
    internal partial class GraphEditor : VisualElement, IDispatcherContext
    {
#if UNITY_EDITOR
        private const string k_PlaymodeEditWarning = "You are modifying the Behavior Graph during Play mode.\n" +
                        "\n- Existing agent instances in the scene are still using the previous version of the graph" +
                        "\n- Debug visualization may show inconsistent information" +
                        "\n\nTo apply changes to running agents, use the 'Reinitialize And Restart Graph' option " +
                        "in the agent's context menu (right-click on BehaviorGraphAgent component).";

        private const string k_UndoScheduledBoolName = "BehaviorAsset_UndoRedoScheduled";
#endif
        public GraphAsset Asset { get; private set; }
        public BlackboardView Blackboard { get; }
        public InspectorView Inspector { get; }

        BlackboardView IDispatcherContext.BlackboardView => Blackboard;

        GraphEditor IDispatcherContext.GraphEditor => this;

        public GraphView GraphView { get; }

        GraphAsset IDispatcherContext.GraphAsset => Asset;

        BlackboardAsset IDispatcherContext.BlackboardAsset => Asset.Blackboard;

        VisualElement IDispatcherContext.Root => this;

        public Dispatcher Dispatcher { get; }

        private readonly GraphToolbar m_Toolbar;

        private const string k_DefaultLayoutFile = "Packages/com.unity.behavior/Tools/Graph/Assets/GraphEditorLayout.uxml";
        private const string k_DefaultStylesheetFile = "Packages/com.unity.behavior/Tools/Graph/Assets/GraphEditorStylesheet.uss";

        private long m_LastAssetVersion = -1u;
        private bool m_UndoRedoDirty;
#if UNITY_EDITOR
        private bool m_PlayModeEditingGranted = false;
#endif

        /// <summary>
        /// Default constructor used by the UXML Serializer.
        /// </summary>
        public GraphEditor()
           : this(k_DefaultLayoutFile, k_DefaultStylesheetFile)
        {
            focusable = true;
        }

        public GraphEditor(string layoutfile = k_DefaultLayoutFile, string stylesheetFile = k_DefaultStylesheetFile)
        {
            focusable = true;

            AddToClassList("GraphEditor");
            styleSheets.Add(ResourceLoadAPI.Load<StyleSheet>(stylesheetFile));
            VisualTreeAsset visualTree = ResourceLoadAPI.Load<VisualTreeAsset>(layoutfile);
            visualTree.CloneTree(this);

            m_Toolbar = this.Q<GraphToolbar>();
            Dispatcher = new Dispatcher(this);
            Blackboard = CreateBlackboardView();
            GraphView = GetOrCreateGraphView();
            Inspector = CreateNodeInspector();
            if (Inspector != null)
            {
                Inspector.GraphEditor = this;
            }

            if (GraphView.parent == null)
            {
                this.Q("EditorPanel")?.Add(GraphView);
            }

            Blackboard.Dispatcher = Dispatcher;
            GraphView.Dispatcher = Dispatcher;

            RegisterCommandHandlers();

            RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            schedule.Execute(Update);
        }

        protected virtual void Update()
        {
            if (!Asset)
            {
                return;
            }

            Dispatcher.Tick();

            if (m_UndoRedoDirty)
            {
                GraphView?.RefreshFromAsset();
                Blackboard?.RefreshFromAsset();
                Inspector?.Refresh();
                m_UndoRedoDirty = false;
            }

            if (Asset.HasOutstandingChanges && m_LastAssetVersion != Asset.VersionTimestamp && CheckPlayModeEditionGranted())
            {
                // BEHAVB-175: Workaround to force refresh field model after a blackboard variable rename.
                Asset.OnValidate();
                OnAssetSave();

                Blackboard?.RefreshFromAsset();
                GraphView?.RefreshFromAsset();
                Inspector?.Refresh();

                m_LastAssetVersion = Asset.VersionTimestamp;

            }
            schedule.Execute(Update);
        }

        public virtual void Load(GraphAsset asset)
        {
            Asset = asset;
            if (asset)
            {
                asset.OnValidate();
            }

            // Check if there is a need to reload.
            if (m_LastAssetVersion == Asset.VersionTimestamp)
            {
                return;
            }

            Blackboard.Load(Asset.Blackboard);
            GraphView.Load(asset);
            m_LastAssetVersion = Asset.VersionTimestamp;
            m_Toolbar.AssetTitle.text = Asset.name;
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.S && evt.modifiers is EventModifiers.Control or EventModifiers.Command)
            {
                OnAssetSave();
                evt.StopImmediatePropagation();
            }
        }

        public virtual void OnAssetSave()
        {
            Asset.SaveAsset();
        }

        public virtual bool IsAssetVersionUpToDate()
        {
            return true;
        }

        private void OnAttachedToPanel(AttachToPanelEvent evt)
        {
            if (panel.contextType == ContextType.Player)
            {
                styleSheets.Add(ResourceLoadAPI.Load<StyleSheet>("Packages/com.unity.behavior/Tools/Graph/Assets/GraphRuntimeStylesheet.uss"));
            }

            // Create Blackboard and Inspector panels.
            ToggleBlackboard(true);
            ToggleNodeInspector(true);

            // Add graph icon stylesheet for the App UI panel.
            if (GetFirstAncestorOfType<Panel>() != null)
            {
                GetFirstAncestorOfType<Panel>().styleSheets.Add(ResourceLoadAPI.Load<StyleSheet>("Packages/com.unity.behavior/Elements/Assets/GraphIconStylesheet.uss"));
            }
#if UNITY_EDITOR
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
#endif
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
#if UNITY_EDITOR
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
#endif
        }

        protected virtual void RegisterCommandHandlers()
        {
            Dispatcher.RegisterHandler<DeleteNodeCommand, DeleteNodeCommandHandler>();
            Dispatcher.RegisterHandler<CreateNodeCommand, CreateNodeCommandHandler>();
            Dispatcher.RegisterHandler<DuplicateNodeCommand, DuplicateNodeCommandHandler>();
            Dispatcher.RegisterHandler<CopyNodeCommand, CopyNodeCommandHandler>();
            Dispatcher.RegisterHandler<PasteNodeCommand, PasteNodeCommandHandler>();
            Dispatcher.RegisterHandler<MoveNodesCommand, MoveNodesCommandHandler>();

            Dispatcher.RegisterHandler<ConnectEdgeCommand, ConnectEdgeCommandHandler>();
            Dispatcher.RegisterHandler<ConnectEdgesCommand, ConnectEdgesCommandHandler>();

            Dispatcher.RegisterHandler<DeleteNodeCommand, DeleteNodeCommandHandler>();
            Dispatcher.RegisterHandler<DeleteEdgeCommand, DeleteEdgeCommandHandler>();
            Dispatcher.RegisterHandler<DeleteNodesAndEdgesCommand, DeleteNodesAndEdgesCommandHandler>();

            Dispatcher.RegisterHandler<AddNodesToSequenceCommand, AddNodesToSequenceCommandHandler>();
            Dispatcher.RegisterHandler<CreateNewSequenceOnDropCommand, CreateNewSequenceOnDropCommandHandler>();

            Dispatcher.RegisterHandler<CreateVariableCommand, CreateVariableCommandHandler>();
            Dispatcher.RegisterHandler<RenameVariableCommand, RenameVariableCommandHandler>();
            Dispatcher.RegisterHandler<DeleteVariableCommand, DeleteVariableCommandHandler>();
            Dispatcher.RegisterHandler<SetVariableIsSharedCommand, SetVariableIsSharedCommandHandler>();
        }

        public virtual SearchMenuBuilder CreateBlackboardOptions()
        {
            SearchMenuBuilder builder = new SearchMenuBuilder();

            void CreateVariableFromMenuAction(string variableTypeName, Type type)
            {
                Dispatcher.DispatchImmediate(new CreateVariableCommand($"New {variableTypeName}", BlackboardUtils.GetVariableModelTypeForType(type)),
                    setHasOutstandingChanges: false);
            }

            builder.Add("Object", onSelected: delegate { CreateVariableFromMenuAction("Object", typeof(GameObject)); }, iconName: "object");
            builder.Add("String", onSelected: delegate { CreateVariableFromMenuAction("String", typeof(string)); }, iconName: "string");
            builder.Add("Float", onSelected: delegate { CreateVariableFromMenuAction("Float", typeof(float)); }, iconName: "float");
            builder.Add("Integer", onSelected: delegate { CreateVariableFromMenuAction("Integer", typeof(int)); }, iconName: "integer");
            builder.Add("Double", onSelected: delegate { CreateVariableFromMenuAction("Double", typeof(double)); }, iconName: "double");
            builder.Add("Boolean", onSelected: delegate { CreateVariableFromMenuAction("Boolean", typeof(bool)); }, iconName: "boolean");
            builder.Add("Vector2", onSelected: delegate { CreateVariableFromMenuAction("Vector2", typeof(Vector2)); }, iconName: "vector2");
            builder.Add("Vector3", onSelected: delegate { CreateVariableFromMenuAction("Vector3", typeof(Vector3)); }, iconName: "vector3");
            builder.Add("Vector4", onSelected: delegate { CreateVariableFromMenuAction("Vector4", typeof(Vector4)); }, iconName: "vector4");
            builder.Add("Color", onSelected: delegate { CreateVariableFromMenuAction("Color", typeof(Color)); }, iconName: "color");

            builder.Add("List/Object", onSelected: delegate { CreateVariableFromMenuAction("Object List", typeof(List<GameObject>)); }, iconName: "object");
            builder.Add("List/String", onSelected: delegate { CreateVariableFromMenuAction("String List", typeof(List<string>)); }, iconName: "string");
            builder.Add("List/Float", onSelected: delegate { CreateVariableFromMenuAction("Float List", typeof(List<float>)); }, iconName: "float");
            builder.Add("List/Integer", onSelected: delegate { CreateVariableFromMenuAction("Integer List", typeof(List<int>)); }, iconName: "integer");
            builder.Add("List/Double", onSelected: delegate { CreateVariableFromMenuAction("Double List", typeof(List<double>)); }, iconName: "double");
            builder.Add("List/Boolean", onSelected: delegate { CreateVariableFromMenuAction("Boolean List", typeof(List<bool>)); }, iconName: "boolean");
            builder.Add("List/Vector2", onSelected: delegate { CreateVariableFromMenuAction("Vector2 List", typeof(List<Vector2>)); }, iconName: "vector2");
            builder.Add("List/Vector3", onSelected: delegate { CreateVariableFromMenuAction("Vector3 List", typeof(List<Vector3>)); }, iconName: "vector3");
            builder.Add("List/Vector4", onSelected: delegate { CreateVariableFromMenuAction("Vector4 List", typeof(List<Vector4>)); }, iconName: "vector4");
            builder.Add("List/Color", onSelected: delegate { CreateVariableFromMenuAction("Color List", typeof(List<Color>)); }, iconName: "color");

            return builder;
        }

        protected virtual GraphView GetOrCreateGraphView() => new GraphView();

        protected virtual BlackboardView CreateBlackboardView()
        {
            return new BlackboardView(CreateBlackboardOptions);
        }

        protected virtual InspectorView CreateNodeInspector()
        {
            return new InspectorView();
        }

        private void ToggleBlackboard(bool displayValue)
        {
            VisualElement editorPanel = this.Q<VisualElement>("EditorPanel");
            if (displayValue)
            {
                if (editorPanel.Q<FloatingPanel>("Blackboard") == null)
                {
                    FloatingPanel blackboardPanel = FloatingPanel.Create(Blackboard, GraphView, "Blackboard");
                    blackboardPanel.IsCollapsable = true;
                    editorPanel.Add(blackboardPanel);
                }
            }
            else
            {
                if (editorPanel.Q<FloatingPanel>("Blackboard") != null)
                {
                    editorPanel.Q<FloatingPanel>("Blackboard").Remove();
                }
            }
        }

        private void ToggleNodeInspector(bool displayValue)
        {
            VisualElement editorPanel = this.Q<VisualElement>("EditorPanel");
            if (displayValue)
            {
                if (editorPanel.Q<FloatingPanel>("Inspector") == null)
                {
                    FloatingPanel nodeInspectorPanel = FloatingPanel.Create(Inspector, GraphView, "Inspector", FloatingPanel.DefaultPosition.TopRight);
                    nodeInspectorPanel.IsCollapsable = true;
                    editorPanel.Add(nodeInspectorPanel);
                }
            }
            else
            {
                if (editorPanel.Q<FloatingPanel>("Inspector") != null)
                {
                    editorPanel.Q<FloatingPanel>("Inspector").Remove();
                }
            }
        }

        private bool CheckPlayModeEditionGranted()
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
            {
                return true;
            }

            if (m_PlayModeEditingGranted)
            {
                return true;
            }

            bool result = EditorUtility.DisplayDialog(
                "Behavior Graph Editing During Play Mode",
                k_PlaymodeEditWarning, "OK",
                DialogOptOutDecisionType.ForThisSession, "Don't Show Again This Session");

            if (!result)
            {
                SessionState.SetBool(k_UndoScheduledBoolName, true);
                Undo.PerformUndo();
            }
            else
            {
                // if user pressed OK, we no longer show the dialog for the playmode session.
                m_PlayModeEditingGranted = true;
            }

            return result;
#else 
            return true;
#endif
        }

#if UNITY_EDITOR
        protected virtual void OnUndoRedoPerformed()
        {
            m_UndoRedoDirty = UnityEditor.EditorUtility.IsDirty(Asset) || UnityEditor.EditorUtility.IsDirty(Asset.Blackboard);
#if BEHAVIOR_DEBUG_UNDO_REDO
            if (m_UndoRedoDirty)
            {
                Debug.Log($"GraphEditor[{Asset.name}].OnUndoRedoPerformed");
            }
#endif
        }

        private void EditorApplication_playModeStateChanged(PlayModeStateChange newState)
        {
            switch (newState)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    m_PlayModeEditingGranted = false;
                    break;
            };
        }
#endif
    }
}