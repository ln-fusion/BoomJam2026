using System;
using System.Collections.Generic;
using Game.Content;
using Game.Presentation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Presentation
{
    /// <summary>检查 Registry 中 UI 预制体是否满足运行时控件契约。</summary>
    public static class UiPrefabValidator
    {
        private const string RegistryPath = "Assets/Game/Content/ContentAssetRegistry.asset";

        /// <summary>校验已登记的全部 UI 预制体并在 Console 输出结果。</summary>
        [MenuItem("Game/UI/Validate Registered UI Prefabs")]
        public static void ValidateRegisteredPrefabs()
        {
            ContentAssetRegistry registry = AssetDatabase.LoadAssetAtPath<ContentAssetRegistry>(RegistryPath);
            if (registry == null)
            {
                Debug.LogError("未找到 UI Registry：" + RegistryPath);
                return;
            }

            if (registry.UiPrefabs == null)
            {
                Debug.LogError("UI Registry 缺少 uiPrefabs 列表：" + RegistryPath);
                return;
            }

            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (UiPrefabAssetEntry entry in registry.UiPrefabs)
            {
                if (entry == null || entry.Asset == null)
                {
                    errors.Add("存在空的 UI 预制体登记项。");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.Id))
                {
                    errors.Add("UI 预制体登记项缺少稳定 ID：" + entry.Asset.name);
                    continue;
                }

                if (!ids.Add(entry.Id))
                {
                    errors.Add("UI 预制体稳定 ID 重复：" + entry.Id);
                    continue;
                }

                ValidatePrefab(entry, errors);
            }

            if (errors.Count == 0)
            {
                Debug.Log("UI 预制体契约校验通过，共检查 " + registry.UiPrefabs.Count + " 项。");
                return;
            }

            foreach (string error in errors)
                Debug.LogError(error);
            Debug.LogError("UI 预制体契约校验失败，共 " + errors.Count + " 个问题。");
        }

        /// <summary>校验单个预制体的用途标记和控件绑定。</summary>
        /// <param name="entry">Registry 映射项。</param>
        /// <param name="errors">错误输出集合。</param>
        private static void ValidatePrefab(UiPrefabAssetEntry entry, List<string> errors)
        {
            GameObject prefab = entry.Asset;
            UiPrefabRoot marker = prefab.GetComponent<UiPrefabRoot>();
            if (marker == null)
            {
                errors.Add(entry.Id + " 缺少 UiPrefabRoot 标记。");
                return;
            }

            if (!(prefab.transform is RectTransform))
            {
                errors.Add(entry.Id + " 的预制体根节点必须使用 RectTransform。");
                return;
            }

            bool complete;
            switch (marker.ScreenId)
            {
                case UiScreenId.StartMenu:
                    complete = prefab.GetComponent<StartMenuUiBindings>()?.IsComplete == true;
                    break;
                case UiScreenId.SettingsModal:
                    complete = prefab.GetComponent<SettingsUiBindings>()?.IsComplete == true;
                    break;
                case UiScreenId.MetaHub:
                    complete = prefab.GetComponent<MetaHubUiBindings>()?.IsComplete == true;
                    break;
                default:
                    complete = false;
                    break;
            }

            if (!complete)
                errors.Add(entry.Id + " 的 " + marker.ScreenId + " 契约未完整绑定。");
        }
    }
}
