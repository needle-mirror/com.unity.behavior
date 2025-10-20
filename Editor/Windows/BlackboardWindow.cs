using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using Unity.AppUI.UI;
using UnityEngine.UIElements;

namespace Unity.Behavior
{
    internal class BlackboardWindow : EditorWindow
    {
        [SerializeField]
        private BehaviorBlackboardAuthoringAsset m_Asset;

        internal BehaviorBlackboardAuthoringAsset Asset
        {
            get => m_Asset;
            set
            {
                m_Asset = value;
                if (Editor.Asset != value)
                {
                    Editor.Load(value);
                }
                titleContent.text = m_Asset.name + " (Blackboard)";
            }
        }

        [SerializeField]
        private int m_DebugAgentId;

        internal BlackboardEditor Editor;
        private Panel m_AppUIPanel;
        private long m_PreviousVersionTimestamp = 0;

        private const string k_WindowDockedKey = "BlackboardWindowDocked";
        private const string k_WindowXKey = "BlackboardWindowX";
        private const string k_WindowYKey = "BlackboardWindowY";
        private const string k_WindowWidthKey = "BlackboardWindowWidth";
        private const string k_WindowHeightKey = "BlackboardWindowHeight";

        private void OnEnable()
        {
            this.SetAntiAliasing(8);
            titleContent.text = "Blackboard";
            minSize = new Vector2(300, 400);

            Editor = new BlackboardEditor();
            m_AppUIPanel = WindowUtils.CreateAndGetAppUIPanel(Editor, rootVisualElement);

            rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            if (m_Asset != null)
            {
                if (!IsAssetValid(m_Asset))
                {
                    return;
                }

                Editor.Load(m_Asset);
                m_PreviousVersionTimestamp = m_Asset.VersionTimestamp;
            }

            SetWindowTitleFromAsset();

            EditorApplication.playModeStateChanged += OnEditorStateChange;
            Editor.OnSave += base.SaveChanges;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnEditorStateChange;
            AutoSaveIfEnabledInEditor();
            SetWindowPosition();
            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnEditorStateChange(PlayModeStateChange stateChange)
        {
            if (stateChange == PlayModeStateChange.EnteredPlayMode)
            {
                AutoSaveIfEnabledInEditor();
            }

            if (stateChange == PlayModeStateChange.ExitingPlayMode)
            {
                AutoSaveIfEnabledInEditor();
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (m_AppUIPanel == null)
            {
                return;
            }

            m_AppUIPanel.forceUseTooltipSystem = (change == PlayModeStateChange.EnteredPlayMode);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            // Note: The window undocks when closed, so we store the value when geometry changes.
            EditorPrefs.SetBool(k_WindowDockedKey, docked);
            SetWindowPosition();
        }

        private void SetWindowPosition()
        {
            EditorPrefs.SetFloat(k_WindowXKey, position.x);
            EditorPrefs.SetFloat(k_WindowYKey, position.y);
            EditorPrefs.SetFloat(k_WindowWidthKey, position.width);
            EditorPrefs.SetFloat(k_WindowHeightKey, position.height);
        }

        private void SetWindowTitleFromAsset()
        {
            if (m_Asset != null)
            {
                titleContent.text = m_Asset.name + " (Blackboard)";
            }
        }

        internal void OnFocus()
        {
            // If the Blackboard asset is deleted outside of Unity, this will close the window when focused.
            if (!ReferenceEquals(m_Asset, null) && !EditorUtility.IsPersistent(m_Asset))
            {
                Close();
                return;
            }

            if (!IsAssetValid(m_Asset))
            {
                return;
            }

            SetWindowTitleFromAsset();
            UpdateBlackboardEditor();
        }

        private void OnLostFocus()
        {
            if (!IsAssetValid(m_Asset))
            {
                return;
            }

            AutoSaveIfEnabledInEditor();
            UpdateBlackboardEditor();
        }

        private void UpdateBlackboardEditor()
        {
            if (Editor == null)
            {
                return;
            }

            // Reload the editor if any graph or blackboard assets which the graph is depending on has changed.
            if ((Editor.GraphDependency.Item1 != null && Editor.HasGraphDependencyChanged())
                || (m_Asset != null && m_Asset.VersionTimestamp != m_PreviousVersionTimestamp))
            {
                Editor.Load(Asset);
                m_PreviousVersionTimestamp = m_Asset.VersionTimestamp;
            }
        }

        private void AutoSaveIfEnabledInEditor()
        {
            if (Editor is { AutoSaveIsEnabled: true } && !Asset.IsAssetVersionUpToDate())
            {
                Editor.OnAssetSave();
            }
        }

        internal static void Open(BehaviorBlackboardAuthoringAsset asset)
        {
            if (!IsAssetValid(asset))
            {
                ErrorMessages.LogSerializedReferenceWindowError(asset);
                return;
            }

            BlackboardWindow[] windows = Resources.FindObjectsOfTypeAll<BlackboardWindow>();
            foreach (BlackboardWindow window in windows)
            {
                if (window.Asset == asset)
                {
                    window.Show();
                    window.Focus();
                    return;
                }
            }

            if (!IsAssetValid(asset))
            {
                return;
            }

            // Create a window using docking if possible.
            bool willBeUndocked = !HasOpenInstances<BlackboardWindow>();
            BlackboardWindow newWindow = CreateWindow<BlackboardWindow>(typeof(BlackboardWindow));
            if (willBeUndocked)
            {
                WindowUtils.ApplyWindowOffsetFromPrefs(newWindow, k_WindowDockedKey, k_WindowXKey, k_WindowYKey, k_WindowWidthKey, k_WindowHeightKey);
            }
            newWindow.titleContent.text = asset.name;
            newWindow.Asset = asset;
            newWindow.Show();
            newWindow.Focus();
        }

        [InitializeOnLoadMethod]
        private static void RegisterWindowDelegates()
        {
            BlackboardWindowDelegate.openHandler = Open;
            AssemblyReloadEvents.afterAssemblyReload += CheckAssetValidityAfterAssemblyReload;
        }

        private static void CheckAssetValidityAfterAssemblyReload()
        {
            BlackboardWindow[] windows = Resources.FindObjectsOfTypeAll<BlackboardWindow>();
            foreach (BlackboardWindow window in windows)
            {
                var asset = window.Asset;
                if (!IsAssetValid(asset))
                {
                    ErrorMessages.LogSerializedReferenceWindowError(asset);
                    ErrorMessages.DisplayWindowClosingDialog(asset);
                    window.Close();
                }
            }
        }

        // Checks if the asset has no missing serialize reference.
        private static bool IsAssetValid(BehaviorBlackboardAuthoringAsset asset)
        {
            if (asset == null || asset.ContainsInvalidSerializedReferences())
            {
                return false;
            }

            return true;
        }
    }
}
