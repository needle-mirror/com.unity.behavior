#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Unity.Behavior
{
    /// <summary>
    /// Store the settings for Timeline that will be stored with the Unity Project.
    /// </summary>
    [FilePath("ProjectSettings/BehaviorSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal class BehaviorProjectSettings : ScriptableSingleton<BehaviorProjectSettings>
    {
        private const string k_DefaultGraphOwnerName = "Self";

        [SerializeField]
        private string m_GraphOwnerName = "Self";
        [SerializeField]
        private bool m_AutoSaveLastSaveLocation = true;
        [SerializeField]
        private string m_SaveFolder = "";
        [SerializeField]
        private bool m_UseSeparateSaveFolders = false;
        [SerializeField]
        private string m_SaveFolderAction = "";
        [SerializeField]
        private string m_SaveFolderModifier = "";
        [SerializeField]
        private string m_SaveFolderFlow = "";
        [SerializeField]
        private string m_SaveFolderCondition = "";
        [SerializeField]
        private string m_SaveFolderEventChannels = "";
        [SerializeField]
        private string m_SaveFolderEnum = "";

        [SerializeField]
        private string m_Namespace = "";

        public string GraphOwnerName
        {
            get => string.IsNullOrEmpty(m_GraphOwnerName) ? k_DefaultGraphOwnerName : m_GraphOwnerName;
            set => m_GraphOwnerName = value;
        }
        public bool AutoSaveLastSaveLocation
        {
            get => m_AutoSaveLastSaveLocation;
            set => m_AutoSaveLastSaveLocation = value;
        }
        public string SaveFolder
        {
            get => m_SaveFolder;
            set => m_SaveFolder = Util.MakePathRelativeToProject(value);
        }
        public bool UseSeparateSaveFolders
        {
            get => m_UseSeparateSaveFolders;
            set => m_UseSeparateSaveFolders = value;
        }
        public string SaveFolderAction
        {
            get => UseSeparateSaveFolders ? m_SaveFolderAction : m_SaveFolder;
            set
            {
                if (m_UseSeparateSaveFolders)
                {
                    m_SaveFolderAction = Util.MakePathRelativeToProject(value);
                }
                else
                {
                    m_SaveFolder = Util.MakePathRelativeToProject(value);
                }
            }
        }
        public string SaveFolderModifier
        {
            get => UseSeparateSaveFolders ? m_SaveFolderModifier : m_SaveFolder;
            set
            {
                if (m_UseSeparateSaveFolders)
                {
                    m_SaveFolderModifier = Util.MakePathRelativeToProject(value);
                }
                else
                {
                    m_SaveFolder = Util.MakePathRelativeToProject(value);
                }
            }
        }
        public string SaveFolderFlow
        {
            get => UseSeparateSaveFolders ? m_SaveFolderFlow : m_SaveFolder;
            set
            {
                if (m_UseSeparateSaveFolders)
                {
                    m_SaveFolderFlow = Util.MakePathRelativeToProject(value);
                }
                else
                {
                    m_SaveFolder = Util.MakePathRelativeToProject(value);
                }
            }
        }
        public string SaveFolderCondition
        {
            get => UseSeparateSaveFolders ? m_SaveFolderCondition : m_SaveFolder;
            set
            {
                if (m_UseSeparateSaveFolders)
                {
                    m_SaveFolderCondition = Util.MakePathRelativeToProject(value);
                }
                else
                {
                    m_SaveFolder = Util.MakePathRelativeToProject(value);
                }
            }
        }
        public string SaveFolderEventChannels
        {
            get => UseSeparateSaveFolders ? m_SaveFolderEventChannels : m_SaveFolder;
            set
            {
                if (m_UseSeparateSaveFolders)
                {
                    m_SaveFolderEventChannels = Util.MakePathRelativeToProject(value);
                }
                else
                {
                    m_SaveFolder = Util.MakePathRelativeToProject(value);
                }
            }
        }
        public string SaveFolderEnum
        {
            get => UseSeparateSaveFolders ? m_SaveFolderEnum : m_SaveFolder;
            set
            {
                if (m_UseSeparateSaveFolders)
                {
                    m_SaveFolderEnum = Util.MakePathRelativeToProject(value);
                }
                else
                {
                    m_SaveFolder = Util.MakePathRelativeToProject(value);
                }
            }
        }

        public string Namespace
        {
            get => m_Namespace;
            set => m_Namespace = value;
        }


        void OnDisable()
        {
            Save();
        }

        /// <summary>
        /// Save the timeline project settings file in the project directory.
        /// </summary>
        public void Save()
        {
            Save(true);
        }

        internal SerializedObject GetSerializedObject()
        {
            return new SerializedObject(this);
        }
    }
}
#endif