using System.IO;
using Game.Content;
using Game.Foundation;
using Game.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>验证正式剧情场景、UI 预制体和运行时剧情资源的项目级接入。</summary>
    public sealed class StoryIntegrationAssetTests
    {
        /// <summary>正式资源注册表应返回绑定完整且不携带 Presenter 的剧情 UI 预制体。</summary>
        [Test]
        public void StoryPrefab_IsRegisteredAndFullyBound()
        {
            var registry = AssetDatabase.LoadAssetAtPath<ContentAssetRegistry>(
                "Assets/Game/Content/ContentAssetRegistry.asset"
            );
            Assert.That(registry, Is.Not.Null);
            GameObject prefab = new OfficialAssetResolver(registry).GetUiPrefab(
                new UiPrefabId("ui.story-panel")
            );
            Assert.That(prefab, Is.Not.Null);
            StoryUiBindings bindings = prefab.GetComponent<StoryUiBindings>();
            Assert.That(bindings, Is.Not.Null);
            Assert.That(bindings.IsComplete, Is.True);
            Assert.That(prefab.GetComponentInChildren<StoryScenePresenter>(true), Is.Null);
        }

        /// <summary>运行时目录内的所有剧情 JSON 都应能解析并具有稳定 ID。</summary>
        [Test]
        public void GeneratedStories_AllDeserialize()
        {
            TextAsset[] assets = Resources.LoadAll<TextAsset>("StoryRuntime");
            Assert.That(assets, Is.Not.Empty);
            foreach (TextAsset asset in assets)
            {
                Assert.That(
                    StoryRuntimeSerializer.TryDeserialize(asset.text, out var story),
                    Is.True,
                    asset.name
                );
                Assert.That(story.StoryId, Is.Not.Empty, asset.name);
            }
        }

        /// <summary>剧情场景不应序列化局部 EventSystem 或正式 UI 实例。</summary>
        [Test]
        public void StoryScene_ContainsOnlyRuntimeInstalledUi()
        {
            string scene = File.ReadAllText("Assets/Scenes/03_Story.unity");
            Assert.That(scene, Does.Not.Contain("m_Name: EventSystem"));
            Assert.That(scene, Does.Not.Contain("guid: 7a598db9715336746afc295150136b6d"));
        }
    }
}
