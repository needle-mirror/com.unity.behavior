using System;
using Unity.AppUI.UI;
using Unity.Behavior.GraphFramework;
using UnityEngine.UIElements;
using ContextMenu = Unity.Behavior.GraphFramework.ContextMenu;

namespace Unity.Behavior
{
    internal class BehaviorGraphBlackboardView : BlackboardView
    {
        internal BehaviorAuthoringGraph GraphAsset;
        private VisualElement m_BlackboardAssetsContainer;
        private bool m_NeedBlackboardReferencesViewRefresh = true;
#if UNITY_TEST_FRAMEWORK
        /// <summary>
        /// Set to false everytime InitializedListView is called.
        /// Set to true only if blackboard assets view is refreshed during the call.
        /// </summary>
        internal bool IsBlackboardAssetViewRefreshed = false;
#endif

        internal BehaviorGraphBlackboardView(BlackboardMenuCreationCallback menuCreationCallback) : base(menuCreationCallback) { }
        
        internal override void InitializeListView()
        {
#if UNITY_TEST_FRAMEWORK
            IsBlackboardAssetViewRefreshed = false;
#endif
            // Update graph asset blackboard
            base.InitializeListView();

            if (GraphAsset == null)
            {
                return;
            }

            // Early out if no need to rebuild blackboard reference.
            if (m_NeedBlackboardReferencesViewRefresh == false)
            {
                return;
            }

            m_BlackboardAssetsContainer?.Clear();
            m_NeedBlackboardReferencesViewRefresh = false;
#if UNITY_TEST_FRAMEWORK
            IsBlackboardAssetViewRefreshed = true;
#endif
            if (GraphAsset.m_Blackboards.Count == 0)
            {
                return;
            }

            CreateBlackboardAssetsSection();
            
            // Initializing blackboards for each added Blackboard asset group.
            foreach (BehaviorBlackboardAuthoringAsset blackboardAsset in GraphAsset.m_Blackboards)
            {
                BlackboardAssetElement element = CreateAndGetBlackboardAssetElement(blackboardAsset);
                m_BlackboardAssetsContainer?.Add(element);

                UpdateListViewFromAsset(element.Variables, blackboardAsset, false);

                element.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.clickCount == 1 && evt.button == 1)
                    {
                        ContextMenu menu = new ContextMenu(this);
                        menu.AddItem("Delete Blackboard", () => Dispatcher.DispatchImmediate(new RemoveBlackboardAssetFromGraphCommand(GraphAsset, blackboardAsset, true)));
                        menu.Show();
                    }
                });

                // Register change callback for blackboard references.
                blackboardAsset.OnBlackboardChanged -= OnBlackboardReferenceAssetChanged;
                blackboardAsset.OnBlackboardChanged += OnBlackboardReferenceAssetChanged;

                blackboardAsset.OnBlackboardDeleted -= OnRemoveBlackboardAssetFromGraphCommand;
                blackboardAsset.OnBlackboardDeleted += OnRemoveBlackboardAssetFromGraphCommand;
            }
        }

        /// <summary>
        /// To be called before InitializeListView to let the view know that it needs to refresh the
        /// blackboard assets view.
        /// </summary>
        public void RequestBlackboardReferenceAssetsViewRefresh()
        {
            m_NeedBlackboardReferencesViewRefresh = true;
        }

        protected internal override void RefreshFromAsset()
        {
            // In case there is a mistmatched between bbref visual element and graphAsset.blackboards
            // probably means that undo/redo happened and we need to refresh the view. 
            // (minus 1 for the divider)
            if (m_BlackboardAssetsContainer != null && (m_BlackboardAssetsContainer.childCount - 1) != GraphAsset.m_Blackboards.Count)
            {
                m_NeedBlackboardReferencesViewRefresh = true;
                InitializeListView();
            }
            else
            {
                base.RefreshFromAsset();
            }
        }

        private void OnBlackboardReferenceAssetChanged(BlackboardAsset.BlackboardChangedType changeType)
        {
            m_NeedBlackboardReferencesViewRefresh = true;
            InitializeListView();
        }

        private void OnRemoveBlackboardAssetFromGraphCommand(BlackboardAsset blackboardAsset)
        {
            Dispatcher.DispatchImmediate(new RemoveBlackboardAssetFromGraphCommand(GraphAsset, (BehaviorBlackboardAuthoringAsset)blackboardAsset, false));
        }

        private BlackboardAssetElement CreateAndGetBlackboardAssetElement(BlackboardAsset asset)
        {
            BlackboardAssetElement element = new BlackboardAssetElement(asset);
            return element;
        }

        private void CreateBlackboardAssetsSection()
        {
            // Create the additional Blackboard Assets section with a divider.
            m_BlackboardAssetsContainer = new VisualElement();
            m_BlackboardAssetsContainer.name = "BlackboardAssetElementContainer";
            Divider divider = new Divider();
            divider.size = Size.S;
            m_BlackboardAssetsContainer.Add(divider);
            ViewContent.Add(m_BlackboardAssetsContainer);
        }

        protected override BlackboardVariableElement CreateVariableUI(VariableModel variable, ListView listView, bool isEditable)
        {
            BlackboardVariableElement variableUI = base.CreateVariableUI(variable, listView, isEditable);

            if (typeof(EventChannelBase).IsAssignableFrom(variable.Type))
            {
                variableUI.IconImage = null;
                variableUI.IconName = "event";
            }

            return variableUI;
        }

        protected override string GetBlackboardVariableTypeName(Type variableType)
        {
            if (variableType == typeof(BehaviorGraph))
            {
                return "Subgraph";
            }
            return BlackboardUtils.GetNameForType(variableType);
        }
    }
}