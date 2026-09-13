using System;
using System.Linq;
using Game.Content;
using Game.Contracts.Content;
using NUnit.Framework;

namespace Game.Tests.EditMode.Content
{
    /// <summary>Validates the deterministic C06 map and branching story fixtures.</summary>
    public sealed class C06ContentContractTests
    {
        /// <summary>验证 Unity 读取旧关卡字段名后映射到唯一剧情字段，重新导出仅保留统一字段名。</summary>
        [Test]
        public void LegacyLevelStoryFieldsMigrateToCanonicalNames()
        {
            var level = UnityEngine.JsonUtility.FromJson<LevelDefinition>(
                "{\"PreStoryId\":\"pre.c19\",\"PostStoryId\":\"post.c19\"}");
            Assert.That(level.Summary.PreludeStoryId, Is.EqualTo("pre.c19"));
            Assert.That(level.Summary.PostludeStoryId, Is.EqualTo("post.c19"));
            string json = UnityEngine.JsonUtility.ToJson(level);
            Assert.That(json, Does.Contain("\"PreludeStoryId\":\"pre.c19\""));
            Assert.That(json, Does.Contain("\"PostludeStoryId\":\"post.c19\""));
            Assert.That(json, Does.Not.Contain("\"PreStoryId\""));
            Assert.That(json, Does.Not.Contain("\"PostStoryId\""));
            level.PreludeStoryId = null;
            level.PostludeStoryId = null;
            Assert.That(level.Summary.PreludeStoryId, Is.Null);
            Assert.That(level.Summary.PostludeStoryId, Is.Null);
        }

        /// <summary>验证导出剧情可替换目录对白，统一剧情字段可引用新增导出剧情并进入摘要与复播查询。</summary>
        [Test]
        public void GeneratedStoriesAndLevelReferencesUseOneCatalog()
        {
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<OfficialContentCatalog>(
                "Assets/Game/Content/OfficialContentCatalog.asset");
            var copy = UnityEngine.Object.Instantiate(source);
            try
            {
                var first = copy.Levels[0];
                var replacement = new StoryDefinition
                {
                    Header = new ContentHeader { ContentId = first.PreludeStoryId },
                    StoryId = first.PreludeStoryId,
                    Nodes = new System.Collections.Generic.List<StoryNodeDefinition>
                    { new StoryNodeDefinition { NodeId = "start", Type = StoryNodeType.End } }
                };
                var generatedPost = new StoryDefinition
                {
                    Header = new ContentHeader { ContentId = "official.story.generated.post" },
                    StoryId = "official.story.generated.post",
                    Nodes = new System.Collections.Generic.List<StoryNodeDefinition>
                    { new StoryNodeDefinition { NodeId = "start", Type = StoryNodeType.End } }
                };
                first.PostludeStoryId = generatedPost.StoryId;
                var content = copy.CreateValidatedService(
                    new System.Collections.Generic.Dictionary<string, StoryDefinition>
                    { [replacement.StoryId] = replacement, [generatedPost.StoryId] = generatedPost });
                Assert.That(content.GetStory(new Game.Foundation.StoryId(replacement.StoryId)), Is.SameAs(replacement));
                Assert.That(first.Summary.PreludeStoryId, Is.EqualTo(replacement.StoryId));
                Assert.That(first.Summary.PostludeStoryId, Is.EqualTo(generatedPost.StoryId));
                var profile = new Game.Contracts.Persistence.ProfileSave();
                profile.CompletedLevelIds.Add(first.LevelId);
                var card = new Game.Meta.MetaMapQuery(content, new Game.Progression.ProfileProgressQuery(profile))
                    .GetLevelCard(new Game.Foundation.LevelId(first.LevelId));
                Assert.That(card.PostludeReplay.Value, Is.EqualTo(generatedPost.StoryId));
                Assert.That(card.PreludeReplay, Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }

        /// <summary>验证实际目录资产保留三十关映射、关后剧情和地图摘要，并支持资源修改生效。</summary>
        [Test]
        public void AuthoredCatalogBuildsAndUsesEditedReferences()
        {
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<OfficialContentCatalog>(
                "Assets/Game/Content/OfficialContentCatalog.asset");
            Assert.That(source, Is.Not.Null);
            var copy = UnityEngine.Object.Instantiate(source);
            try
            {
                var content = copy.CreateValidatedService();
                Assert.That(content.GetMaps(), Has.Count.EqualTo(6));
                Assert.That(copy.Levels, Has.Count.EqualTo(30));
                Assert.That(copy.Levels.Select(level => level.PostludeStoryId).Distinct().Count(), Is.EqualTo(30));
                foreach (var level in copy.Levels)
                {
                    Assert.That(level.PostludeStoryId, Is.Not.Null.And.Not.Empty);
                    Assert.That(content.GetStory(new Game.Foundation.StoryId(level.PostludeStoryId)), Is.Not.Null);
                }
                var first = copy.Levels[0];
                first.PostludeStoryId = first.PreludeStoryId;
                first.SortOrder = 99;
                content = copy.CreateValidatedService();
                Assert.That(content.GetLevel(new Game.Foundation.LevelId(first.LevelId)).PostludeStoryId,
                    Is.EqualTo(first.PreludeStoryId));
                Assert.That(content.GetLevelsForMap(new Game.Foundation.MapId(first.MapId)).Last().LevelId,
                    Is.EqualTo(first.LevelId));
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }

        /// <summary>验证实际目录中的重复 ID、未知引用和前置环会阻止启动内容服务。</summary>
        /// <param name="fault">注入的配置错误种类。</param>
        [TestCase("duplicate")]
        [TestCase("unknown-level")]
        [TestCase("cycle")]
        [TestCase("unknown-story")]
        [TestCase("unknown-map")]
        public void AuthoredCatalogRejectsInvalidConfiguration(string fault)
        {
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<OfficialContentCatalog>(
                "Assets/Game/Content/OfficialContentCatalog.asset");
            var copy = UnityEngine.Object.Instantiate(source);
            try
            {
                var first = copy.Levels[0];
                switch (fault)
                {
                    case "duplicate": copy.Levels[1].LevelId = first.LevelId; break;
                    case "unknown-level": first.UnlockRequirement.RequiredLevelIds.Add("official.level.missing"); break;
                    case "cycle": first.UnlockRequirement.RequiredLevelIds.Add(copy.Levels[1].LevelId); break;
                    case "unknown-story": first.PostludeStoryId = "official.story.missing"; break;
                    case "unknown-map": first.MapId = "official.map.missing"; break;
                }
                Assert.Throws<ArgumentException>(() => copy.CreateValidatedService());
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }

        /// <summary>验证所有关卡的关后剧情可播放、摘要保留引用，且不会与关前剧情共享完成标识。</summary>
        [Test]
        public void FirstLevel_PostludeIsValidAndDistinctFromPrelude()
        {
            var provider = OfficialTestMapCatalog.CreateProvider();
            var first = provider.Levels.Single(level => level.LevelId == "official.level.test_01_01");
            Assert.That(first.PostludeStoryId, Is.EqualTo("official.story.postlude.test_01_01"));
            Assert.That(first.Summary.PostludeStoryId, Is.EqualTo(first.PostludeStoryId));
            Assert.That(first.PostludeStoryId, Is.Not.EqualTo(first.PreludeStoryId));
            Assert.That(provider.TryGetStory(new Game.Foundation.StoryId(first.PostludeStoryId),
                out StoryDefinition story), Is.True);
            Assert.That(StoryDefinitionValidator.TryValidate(story, out string error), Is.True, error);
            Assert.That(provider.Levels.Select(level => level.PostludeStoryId).Distinct().Count(), Is.EqualTo(30));
            foreach (var level in provider.Levels)
            {
                Assert.That(level.PostludeStoryId, Is.Not.Null.And.Not.Empty);
                Assert.That(level.PostludeStoryId, Is.Not.EqualTo(level.PreludeStoryId));
                Assert.That(level.Summary.PostludeStoryId, Is.EqualTo(level.PostludeStoryId));
                Assert.That(provider.TryGetStory(new Game.Foundation.StoryId(level.PostludeStoryId),
                    out var postlude), Is.True);
                Assert.That(StoryDefinitionValidator.TryValidate(postlude, out error), Is.True, error);
            }
        }

        /// <summary>Ensures six maps and thirty ordered levels pass stable ID validation.</summary>
        [Test]
        public void OfficialFixtures_ContainSixMapsAndThirtyLevels()
        {
            OfficialContentProvider provider = OfficialTestMapCatalog.CreateProvider();
            Assert.That(MapContentValidator.TryValidate(provider, out string error), Is.True, error);
            Assert.That(provider.Maps, Has.Count.EqualTo(6));
            Assert.That(provider.Levels, Has.Count.EqualTo(30));
            Assert.That(provider.Levels, Has.All.Matches<LevelDefinition>(level =>
                !string.IsNullOrWhiteSpace(level.PreludeStoryId)));
            Assert.That(provider.Levels.Select(level => level.PreludeStoryId).Distinct().Count(),
                Is.EqualTo(30));
        }

        /// <summary>Ensures the fixture story has a choice branch that converges.</summary>
        [Test]
        public void OfficialFixtures_ContainValidBranchingStory()
        {
            OfficialContentProvider provider = OfficialTestMapCatalog.CreateProvider();
            Assert.That(StoryDefinitionValidator.TryValidate(
                provider.TryGetStory(new Game.Foundation.StoryId("official.story.c06_branch"),
                    out Game.Contracts.Content.StoryDefinition story) ? story : null,
                out string error), Is.True, error);
        }
    }
}
