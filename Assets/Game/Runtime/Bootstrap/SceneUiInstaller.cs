using Game.Flow;
using Game.Content;
using Game.Foundation;
using Game.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Bootstrap
{
    /// <summary>按固定功能 Scene 安装运行时 uGUI 入口。</summary>
    public static class SceneUiInstaller
    {
        /// <summary>
        /// 安装功能场景 UI，并向 Gameplay 场景已有的返回按钮注入服务。
        /// </summary>
        /// <param name="scene">刚激活的功能场景。</param>
        /// <param name="runtimeServices">Bootstrap 创建的运行时服务容器。</param>
        /// <param name="globalCanvasLayer">跨场景全局 UI 层。</param>
        /// <param name="contentRegistry">可选官方 UI 预制体 Registry。</param>
        public static void Install(Scene scene, GameRuntimeServices runtimeServices,
            GlobalCanvasLayer globalCanvasLayer, ContentAssetRegistry contentRegistry = null)
        {
            if (!scene.IsValid() || !scene.isLoaded || runtimeServices == null ||
                globalCanvasLayer == null)
                return;

            if (scene.name == SceneNames.StartMenu)
            {
                InstallStartMenu(scene, runtimeServices, globalCanvasLayer, contentRegistry);
                return;
            }

            if (scene.name == SceneNames.MetaHub)
            {
                InstallMetaHub(scene, runtimeServices, globalCanvasLayer, contentRegistry);
                return;
            }

            if (scene.name == SceneNames.Story)
                InstallStory(scene, runtimeServices);
            if (scene.name == SceneNames.Gameplay)
            {
                var returnButton = FindInScene<GameplayReturnButton>(scene);
                if (returnButton != null)
                    returnButton.Initialize(runtimeServices.Flow, globalCanvasLayer);
                else
                    Debug.LogWarning("Gameplay 场景缺少返回地图按钮。请检查已保存的场景。");
            }
        }

        /// <summary>优先复用场景内的开始菜单 UI，缺失时创建，并初始化 View/Presenter。</summary>
        /// <param name="scene">开始菜单场景。</param>
        /// <param name="runtimeServices">运行时服务容器。</param>
        /// <param name="globalCanvasLayer">全局 UI 层。</param>
        /// <param name="contentRegistry">可选官方 UI 预制体 Registry。</param>
        private static void InstallStartMenu(Scene scene, GameRuntimeServices runtimeServices,
            GlobalCanvasLayer globalCanvasLayer, ContentAssetRegistry contentRegistry)
        {
            StartMenuUiBindings bindings = FindInScene<StartMenuUiBindings>(scene);
            GameObject root;

            if (bindings != null)
            {
                root = bindings.gameObject;
            }
            else
            {
                root = InstantiateUiPrefab(contentRegistry, UiPrefabIds.StartMenu,
                    "StartMenuUI");
                SceneManager.MoveGameObjectToScene(root, scene);
            }
            var view = root.GetComponent<StartMenuView>() ?? root.AddComponent<StartMenuView>();
            var presenter = root.GetComponent<StartMenuPresenter>() ?? root.AddComponent<StartMenuPresenter>();
            presenter.Initialize(view, runtimeServices, globalCanvasLayer);
        }

        /// <summary>优先复用场景内的 MetaHub UI，缺失时创建，并初始化主界面壳。</summary>
        /// <param name="scene">MetaHub 场景。</param>
        /// <param name="runtimeServices">运行时服务容器。</param>
        /// <param name="globalCanvasLayer">全局 UI 层。</param>
        /// <param name="contentRegistry">可选官方 UI 预制体 Registry。</param>
        private static void InstallMetaHub(Scene scene, GameRuntimeServices runtimeServices,
            GlobalCanvasLayer globalCanvasLayer, ContentAssetRegistry contentRegistry)
        {
            MetaHubUiBindings bindings = FindInScene<MetaHubUiBindings>(scene);
            MetaHubShell existingShell = FindInScene<MetaHubShell>(scene);
            GameObject root;

            if (bindings != null)
            {
                root = bindings.gameObject;
            }
            else if (existingShell != null)
            {
                root = existingShell.gameObject;
            }
            else
            {
                root = InstantiateUiPrefab(contentRegistry, UiPrefabIds.MetaHub,
                    "MetaHubUI");
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            var shell = root.GetComponent<MetaHubShell>() ?? root.AddComponent<MetaHubShell>();
            shell.Initialize(runtimeServices, globalCanvasLayer);
        }

        /// <summary>安装 C09 剧情白盒表现器。</summary>
        /// <param name="scene">剧情场景。</param>
        /// <param name="runtimeServices">传给剧情表现器的运行时服务。</param>
        private static void InstallStory(Scene scene, GameRuntimeServices runtimeServices)
        {
            if (FindInScene<StoryScenePresenter>(scene) != null)
                return;
            var root = new GameObject("StoryUI");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<StoryScenePresenter>().Initialize(runtimeServices);
        }

        /// <summary>从官方 Registry 实例化 UI 预制体，缺失时回退为空根节点。</summary>
        /// <param name="registry">可选资源 Registry。</param>
        /// <param name="id">UI 预制体稳定 ID。</param>
        /// <param name="fallbackName">回退根节点名称。</param>
        /// <returns>可移动到功能场景的实例根节点。</returns>
        private static GameObject InstantiateUiPrefab(ContentAssetRegistry registry, string id,
            string fallbackName)
        {
            if (registry != null)
            {
                var prefab = new OfficialAssetResolver(registry).GetUiPrefab(new UiPrefabId(id));
                if (prefab != null)
                    return Object.Instantiate(prefab);
            }

            return new GameObject(fallbackName);
        }

        /// <summary>在指定场景根节点中查找第一个组件。</summary>
        /// <typeparam name="T">组件类型。</typeparam>
        /// <param name="scene">目标场景。</param>
        /// <returns>找到的组件；不存在时为 null。</returns>
        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }

            return null;
        }
    }

    /// <summary>内置 UI 预制体稳定 ID。</summary>
    public static class UiPrefabIds
    {
        /// <summary>开始菜单预制体。</summary>
        public const string StartMenu = "ui.start-menu";
        /// <summary>MetaHub 预制体。</summary>
        public const string MetaHub = "ui.meta-hub";
        /// <summary>设置弹窗预制体。</summary>
        public const string SettingsModal = "ui.settings-modal";
    }
}
