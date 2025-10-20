using Unity.Behavior.GraphFramework;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIExtras;
#endif

namespace Unity.Behavior
{
    [UxmlElement]
    internal partial class BlackboardEditor : VisualElement, IDispatcherContext
    {
        private const string k_PreferencesPrefix = "Muse.Behavior";

        internal BehaviorBlackboardAuthoringAsset Asset;
        private readonly BlackboardView m_Blackboard;

        internal delegate void OnSaveCallback();
        internal OnSaveCallback OnSave;

        public bool AutoSaveIsEnabled = true;

        public (BehaviorAuthoringGraph, long) GraphDependency;

        BlackboardView IDispatcherContext.BlackboardView => m_Blackboard;
        GraphEditor IDispatcherContext.GraphEditor => null;
        GraphView IDispatcherContext.GraphView => null;
        GraphAsset IDispatcherContext.GraphAsset => null;
        BlackboardAsset IDispatcherContext.BlackboardAsset => Asset;
        VisualElement IDispatcherContext.Root => this;
        internal Dispatcher Dispatcher { get; }

        private readonly Text m_AssetTitle;
        private Icon m_AssetIcon;

        public BlackboardEditor()
        {
            focusable = true;
            style.flexGrow = 1;

            AddToClassList("Behavior");
            BehaviorUIThemeManager.RegisterElement(this);
            styleSheets.Add(ResourceLoadAPI.Load<StyleSheet>("Packages/com.unity.behavior/Authoring/UI/AssetEditor/Assets/BlackboardEditorStylesheet.uss"));
            ResourceLoadAPI.Load<VisualTreeAsset>("Packages/com.unity.behavior/Authoring/UI/AssetEditor/Assets/BlackboardEditorLayout.uxml").CloneTree(this);

            GraphToolbar toolbar = this.Q<GraphToolbar>();
            toolbar.OpenAssetButton.clicked += OnOpenAssetButtonClick;

            GraphPrefsUtility.PrefsPrefix = k_PreferencesPrefix;

            Dispatcher = new Dispatcher(this);
            m_Blackboard = CreateBlackboardView();

            // Create a Add button on the Blackboard editor and give the Blackboard view a reference to it
            IconButton addButton = new IconButton();
            addButton.name = "BlackboardAddButton";
            addButton.icon = "plus";
            addButton.quiet = true;
            toolbar.Q<VisualElement>("RightContainer").Add(addButton);
            m_AssetIcon = this.Q<Icon>("AssetIcon");
            m_AssetTitle = this.Q<Text>("AssetTitleElement");
            m_Blackboard.AddButton = addButton;
            m_Blackboard.Dispatcher = Dispatcher;

            RegisterCommandHandlers();
            this.Q<VisualElement>("BlackboardEditorPanel").Add(m_Blackboard);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
#if UNITY_EDITOR
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
#endif

#if UNITY_EDITOR
            // Add graph icon stylesheet for the App UI panel.
            var firstAncestorOfType = GetFirstAncestorOfType<Panel>();
            if (firstAncestorOfType != null)
            {
                BehaviorUIThemeManager.RegisterElement(firstAncestorOfType);
            }
#endif
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            BehaviorUIThemeManager.UnregisterElement(this);

#if UNITY_EDITOR
            var firstAncestorOfType = GetFirstAncestorOfType<Panel>();
            if (firstAncestorOfType != null)
            {
                BehaviorUIThemeManager.UnregisterElement(firstAncestorOfType);
            }
#endif
        }

#if UNITY_EDITOR
        private void OnUndoRedoPerformed()
        {
            if (Asset == null)
            {
                return;
            }

            // Unity UndoRedo handles object states.
            // However we still need to manually refresh open editors and the rebuild of dependent assets.
            if (EditorUtility.IsDirty(Asset))
            {
                Asset.InvokeBlackboardChanged(BlackboardAsset.BlackboardChangedType.UndoRedo);
                OnAssetSave();
            }

            Load(Asset);
        }
#endif

        public void Load(BehaviorBlackboardAuthoringAsset asset)
        {
            Asset = asset;
            m_Blackboard.Load(asset);

            // Set the asset name on the title, and get the asset icon.
            m_AssetTitle.text = Asset?.name;
            m_AssetIcon.image = BlackboardUtils.GetScriptableObjectIcon(Asset);
            m_AssetIcon.Show();

            DispatchOutstandingAssetCommands();

            // Save a dependency to the graph if the blackboard is a graph blackboard.
            BehaviorAuthoringGraph graph = BehaviorGraphAssetRegistry.TryGetAssetFromGraphBlackboard(Asset);
            if (graph != null)
            {
                GraphDependency = (graph, graph.VersionTimestamp);
            }
        }

        private void DispatchOutstandingAssetCommands()
        {
            if (!Asset)
            {
                return;
            }

            Asset.CommandBuffer.DispatchCommands(Dispatcher);
        }

        private void RegisterCommandHandlers()
        {
            Dispatcher.RegisterHandler<CreateVariableCommand, CreateVariableCommandHandler>();
            Dispatcher.RegisterHandler<RenameVariableCommand, RenameVariableCommandHandler>();
            Dispatcher.RegisterHandler<DeleteVariableCommand, DeleteVariableCommandHandler>();
            Dispatcher.RegisterHandler<SetVariableIsSharedCommand, SetVariableIsSharedCommandHandler>();

            Dispatcher.RegisterHandler<SetBlackboardVariableValueCommand, SetBlackboardVariableValueCommandHandler>();
            Dispatcher.RegisterHandler<CreateVariableFromSerializedTypeCommand, CreateVariableFromSerializedTypeCommandHandler>();
        }

        private BlackboardView CreateBlackboardView()
        {
            return new BlackboardView(CreateBlackboardOptions);
        }

        private SearchMenuBuilder CreateBlackboardOptions()
        {
            return Util.CreateBlackboardOptions(Dispatcher, this, Asset.CommandBuffer);
        }

        private void OnOpenAssetButtonClick()
        {
#if UNITY_EDITOR
            BlackboardAsset[] assets = Util.GetNonGraphBlackboardAssets();
            List<SearchView.Item> searchItems = new List<SearchView.Item>();
            foreach (BlackboardAsset asset in assets)
            {
                searchItems.Add(new SearchView.Item(asset.name, data: asset, icon: BlackboardUtils.GetScriptableObjectIcon(asset)));
            }
            SearchWindow.Show("Open Blackboard", searchItems,
                item => BlackboardWindowDelegate.Open(item.Data as BehaviorBlackboardAuthoringAsset),
                this.Q<ActionButton>("OpenAssetButton"), 200, 300);
#endif
        }

        public void OnAssetSave()
        {
            if (Asset != null)
            {
                Asset.BuildRuntimeBlackboard();
                Asset.SaveAsset();
            }
            OnSave?.Invoke();
        }

        internal bool HasGraphDependencyChanged()
        {
            if (GraphDependency.Item1 == null)
            {
                return true;
            }

            // Check if the graph version timestamp has changed.
            if (GraphDependency.Item1.VersionTimestamp != GraphDependency.Item2)
            {
                return true;
            }

            return false;
        }
    }
}
