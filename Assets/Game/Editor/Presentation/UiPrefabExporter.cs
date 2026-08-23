using System;
using System.Collections.Generic;
using Game.Bootstrap;
using Game.Content;
using Game.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor.Presentation
{
    /// <summary>把当前代码生成的 UI 导出为可由画师继续编辑的临时预制体。</summary>
    public static class UiPrefabExporter
    {
        private const string PrefabFolder = "Assets/Game/Prefabs/UI";
        private const string RegistryFolder = "Assets/Game/Content";
        private const string RegistryPath = RegistryFolder + "/ContentAssetRegistry.asset";

        /// <summary>导出开始菜单、MetaHub 和设置弹窗，并登记到官方资源 Registry。</summary>
        [MenuItem("Game/UI/Export Current UI To Prefabs")]
        public static void ExportAll()
        {
            EnsureFolder("Assets/Game", "Prefabs");
            EnsureFolder("Assets/Game/Prefabs", "UI");
            EnsureFolder("Assets/Game", "Content");

            var exported = new List<ExportedPrefab>
            {
                Export("StartMenuUI", UiPrefabIds.StartMenu, root =>
                {
                    root.AddComponent<StartMenuView>().BuildPreview();
                    AddMarker(root, UiScreenId.StartMenu);
                }),
                Export("MetaHubUI", UiPrefabIds.MetaHub, root =>
                {
                    root.AddComponent<MetaHubShell>().BuildPreview();
                    AddMarker(root, UiScreenId.MetaHub);
                }),
                Export("SettingsModalUI", UiPrefabIds.SettingsModal, root =>
                {
                    root.AddComponent<SettingsModalPresenter>().BuildPreview();
                    AddMarker(root, UiScreenId.SettingsModal);
                })
            };

            ContentAssetRegistry registry = LoadOrCreateRegistry();
            SerializedObject serializedRegistry = new SerializedObject(registry);
            SerializedProperty entries = serializedRegistry.FindProperty("uiPrefabs");
            foreach (ExportedPrefab item in exported)
            {
                SerializedProperty match = FindEntry(entries, item.Id);
                if (match == null)
                {
                    entries.arraySize++;
                    match = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                }
                match.FindPropertyRelative("Id").stringValue = item.Id;
                match.FindPropertyRelative("Asset").objectReferenceValue = item.Asset;
            }
            serializedRegistry.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            Selection.activeObject = registry;
            Debug.Log("UI 预制体已导出并登记：" + RegistryPath);
        }

        /// <summary>导出一个由运行时代码生成的 UI 根节点。</summary>
        /// <param name="name">预制体名称。</param>
        /// <param name="id">稳定 UI 预制体 ID。</param>
        /// <param name="configure">向临时根节点添加并构建 UI 组件。</param>
        /// <returns>导出的资源记录。</returns>
        private static ExportedPrefab Export(string name, string id, Action<GameObject> configure)
        {
            var root = new GameObject(name, typeof(RectTransform));
            try
            {
                configure(root);
                ValidateGeneratedRoot(root, id);
                string path = PrefabFolder + "/" + name + ".prefab";
                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, path);
                return new ExportedPrefab(id, asset);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>向导出根节点写入界面契约标记。</summary>
        /// <param name="root">导出根节点。</param>
        /// <param name="screenId">界面类型。</param>
        private static void AddMarker(GameObject root, UiScreenId screenId)
        {
            UiPrefabRoot marker = root.AddComponent<UiPrefabRoot>();
            SerializedObject data = new SerializedObject(marker);
            data.FindProperty("screenId").enumValueIndex = (int)screenId;
            data.ApplyModifiedPropertiesWithoutUndo();

            if (screenId == UiScreenId.StartMenu)
                ConfigureStartMenuBindings(root);
            else if (screenId == UiScreenId.SettingsModal)
                ConfigureSettingsBindings(root);
            else
                ConfigureMetaHubBindings(root);
        }

        /// <summary>确认导出结果已经满足对应界面契约。</summary>
        /// <param name="root">待导出的临时根节点。</param>
        /// <param name="id">稳定 UI 预制体 ID。</param>
        private static void ValidateGeneratedRoot(GameObject root, string id)
        {
            UiPrefabRoot marker = root.GetComponent<UiPrefabRoot>();
            bool complete = marker != null && root.transform is RectTransform;
            if (complete)
            {
                switch (marker.ScreenId)
                {
                    case UiScreenId.StartMenu:
                        complete &= root.GetComponent<StartMenuUiBindings>()?.IsComplete == true;
                        break;
                    case UiScreenId.SettingsModal:
                        complete &= root.GetComponent<SettingsUiBindings>()?.IsComplete == true;
                        break;
                    case UiScreenId.MetaHub:
                        complete &= root.GetComponent<MetaHubUiBindings>()?.IsComplete == true;
                        break;
                }
            }
            if (!complete)
                throw new InvalidOperationException("生成的 UI 预制体契约不完整：" + id);
        }

        /// <summary>写入开始菜单的控件契约引用。</summary>
        /// <param name="root">预制体根节点。</param>
        private static void ConfigureStartMenuBindings(GameObject root)
        {
            var bindings = root.AddComponent<StartMenuUiBindings>();
            bindings.Canvas = Find<Canvas>(root, "SceneCanvas");
            bindings.Title = Find<Text>(root, "Title");
            bindings.Feedback = Find<Text>(root, "Feedback");
            bindings.StartButton = Find<Button>(root, "Start");
            bindings.SettingsButton = Find<Button>(root, "Settings");
            bindings.QuitButton = Find<Button>(root, "Quit");
            bindings.NicknamePanel = FindObject(root, "NicknameModal");
            bindings.NicknamePrompt = Find<Text>(root, "Prompt");
            bindings.NicknameInput = Find<InputField>(root, "Input");
            bindings.NicknameError = Find<Text>(root, "Error");
            bindings.NicknameConfirmButton = Find<Button>(root, "Confirm");
            bindings.NicknameCancelButton = Find<Button>(root, "Cancel");
            EditorUtility.SetDirty(bindings);
        }

        /// <summary>写入设置弹窗的控件契约引用。</summary>
        /// <param name="root">预制体根节点。</param>
        private static void ConfigureSettingsBindings(GameObject root)
        {
            var bindings = root.AddComponent<SettingsUiBindings>();
            bindings.Panel = FindObject(root, "SettingsPanel");
            bindings.Title = Find<Text>(root, "Title");
            bindings.MasterVolumeSlider = Find<Slider>(root, UiTextKeys.MasterVolume);
            bindings.MusicVolumeSlider = Find<Slider>(root, UiTextKeys.MusicVolume);
            bindings.SfxVolumeSlider = Find<Slider>(root, UiTextKeys.SfxVolume);
            bindings.LanguageDropdown = Find<Dropdown>(root, "Language");
            bindings.ResolutionDropdown = Find<Dropdown>(root, "Resolution");
            bindings.FullscreenToggle = Find<Toggle>(root, "Fullscreen");
            bindings.Feedback = Find<Text>(root, "Feedback");
            bindings.RestoreDefaultsButton = Find<Button>(root, "RestoreDefaults");
            bindings.CancelButton = Find<Button>(root, "Cancel");
            bindings.ApplyButton = Find<Button>(root, "Apply");
            EditorUtility.SetDirty(bindings);
        }

        /// <summary>写入 MetaHub 的页面和导航契约引用。</summary>
        /// <param name="root">预制体根节点。</param>
        private static void ConfigureMetaHubBindings(GameObject root)
        {
            var bindings = root.AddComponent<MetaHubUiBindings>();
            bindings.MapPage = FindObject(root, "MapPageView");
            bindings.ArchivePage = FindObject(root, "ArchivePageView");
            bindings.CharacterPage = FindObject(root, "CharacterPageView");
            bindings.LoungePage = FindObject(root, "LoungePlaceholderView");
            bindings.NavigationButtons = new[]
            {
                Find<Button>(root, "Map"), Find<Button>(root, "Archive"),
                Find<Button>(root, "Character"), Find<Button>(root, "Lounge")
            };
            EditorUtility.SetDirty(bindings);
        }

        /// <summary>加载或创建官方资源 Registry。</summary>
        /// <returns>Registry 资源。</returns>
        private static ContentAssetRegistry LoadOrCreateRegistry()
        {
            ContentAssetRegistry registry = AssetDatabase.LoadAssetAtPath<ContentAssetRegistry>(RegistryPath);
            if (registry != null)
                return registry;
            registry = ScriptableObject.CreateInstance<ContentAssetRegistry>();
            AssetDatabase.CreateAsset(registry, RegistryPath);
            return registry;
        }

        /// <summary>查找指定 ID 的序列化映射项。</summary>
        /// <param name="entries">映射项数组。</param>
        /// <param name="id">目标 ID。</param>
        /// <returns>找到的数组元素；否则为 null。</returns>
        private static SerializedProperty FindEntry(SerializedProperty entries, string id)
        {
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("Id").stringValue == id)
                    return entry;
            }
            return null;
        }

        /// <summary>递归查找指定名称的节点。</summary>
        /// <param name="root">查找根节点。</param>
        /// <param name="name">节点名称。</param>
        /// <returns>找到的节点；否则为 null。</returns>
        private static GameObject FindObject(GameObject root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                    return child.gameObject;
            return null;
        }

        /// <summary>递归查找指定名称和类型的组件。</summary>
        /// <typeparam name="T">组件类型。</typeparam>
        /// <param name="root">查找根节点。</param>
        /// <param name="name">节点名称。</param>
        /// <returns>找到的组件；否则为 null。</returns>
        private static T Find<T>(GameObject root, string name) where T : Component
        {
            return FindObject(root, name)?.GetComponent<T>();
        }

        /// <summary>确保 Unity 资源目录存在。</summary>
        /// <param name="parent">已存在的父目录。</param>
        /// <param name="name">子目录名称。</param>
        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }

        /// <summary>导出资源记录。</summary>
        private sealed class ExportedPrefab
        {
            /// <summary>创建记录。</summary>
            /// <param name="id">稳定 ID。</param><param name="asset">预制体资源。</param>
            public ExportedPrefab(string id, GameObject asset) { Id = id; Asset = asset; }
            /// <summary>稳定 ID。</summary>
            public string Id { get; }
            /// <summary>预制体资源。</summary>
            public GameObject Asset { get; }
        }
    }
}
