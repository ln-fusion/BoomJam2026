using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Contracts.Content;
using Game.Editor.Level;
using Game.Foundation;
using NUnit.Framework;

namespace Game.Tests.EditMode.Editor
{
    /// <summary>验证 C20 关卡编辑器工厂、调色板目录与磁盘往返。</summary>
    public sealed class LevelEditorAuthoringTests
    {
        private string _root;
        private FileLevelAuthoringRepository _repository;

        /// <summary>每个用例使用独立临时目录。</summary>
        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "C20LevelEditorTests", Guid.NewGuid().ToString("N"));
            _repository = new FileLevelAuthoringRepository(_root);
        }

        /// <summary>清理临时目录。</summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        /// <summary>新模板必须字段完整, 使视口绘制无需判空。</summary>
        [Test]
        public void CreateNew_ProducesCompleteNonNullDefinition()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.test_01_01");
            LevelDefinition definition = data.Definition;

            Assert.That(data.FormatVersion, Is.EqualTo(LevelAuthoringSerializer.CurrentFormatVersion));
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.LevelId, Is.EqualTo("official.level.test_01_01"));
            Assert.That(definition.Header, Is.Not.Null);
            Assert.That(definition.PhysicsProfile, Is.Not.Null);
            Assert.That(definition.WorldBounds, Is.Not.Null);
            Assert.That(definition.StartPoint, Is.Not.Null);
            Assert.That(definition.GoalPoint, Is.Not.Null);
            Assert.That(definition.UnlockRequirement, Is.Not.Null);
            Assert.That(definition.Objects, Is.Not.Null);
            Assert.That(definition.DeployableZones, Is.Not.Null);
            Assert.That(definition.ForbiddenZones, Is.Not.Null);
            Assert.That(definition.AllowedAbilities, Is.Not.Null);
            Assert.That(definition.SuccessConditions, Is.Not.Null);
            Assert.That(definition.FailureConditions, Is.Not.Null);
            Assert.That(data.EditorViewState, Is.Not.Null);
        }

        /// <summary>世界边界必须包含起点与终点, 否则视口初始视图会落在内容之外。</summary>
        [Test]
        public void CreateNew_PlacesStartAndGoalInsideWorldBounds()
        {
            LevelDefinition definition = LevelAuthoringFactory.CreateNew("official.level.test_01_01").Definition;
            BoundsData bounds = definition.WorldBounds;

            Assert.That(bounds.MinX, Is.LessThan(bounds.MaxX));
            Assert.That(bounds.MinY, Is.LessThan(bounds.MaxY));
            AssertContains(bounds, definition.StartPoint.PositionX, definition.StartPoint.PositionY);
            AssertContains(bounds, definition.GoalPoint.PositionX, definition.GoalPoint.PositionY);
            Assert.That(definition.StartPoint.PositionX, Is.LessThan(definition.GoalPoint.PositionX));
        }

        /// <summary>新模板必须能被仓库保存并原样加载。</summary>
        [Test]
        public void CreateNew_RoundTripsThroughRepository()
        {
            LevelAuthoringData created = LevelAuthoringFactory.CreateNew("official.level.test_01_01");
            Result saved = _repository.Save(new LevelId("official.level.test_01_01"), created);

            Assert.That(saved.IsSuccess, Is.True, saved.Message);
            Assert.That(
                _repository.TryLoad(new LevelId("official.level.test_01_01"), out LevelAuthoringData loaded),
                Is.True
            );
            Assert.That(loaded.Definition.LevelId, Is.EqualTo(created.Definition.LevelId));
            Assert.That(loaded.Definition.MapId, Is.EqualTo(created.Definition.MapId));
            Assert.That(loaded.Definition.CapacityLimit, Is.EqualTo(created.Definition.CapacityLimit));
            Assert.That(loaded.Definition.WorldBounds.MaxX, Is.EqualTo(created.Definition.WorldBounds.MaxX));
            Assert.That(loaded.EditorViewState.Zoom, Is.EqualTo(created.EditorViewState.Zoom));
        }

        /// <summary>保存必须拒绝 LevelId 与参数不一致的数据, 避免写错文件。</summary>
        [Test]
        public void Save_RejectsMismatchedLevelId()
        {
            LevelAuthoringData created = LevelAuthoringFactory.CreateNew("official.level.test_01_01");
            Result saved = _repository.Save(new LevelId("official.level.other_01_01"), created);

            Assert.That(saved.IsSuccess, Is.False);
            Assert.That(saved.ErrorCode, Is.EqualTo(ErrorCode.InvalidArgument));
        }

        /// <summary>地图 ID 应从关卡 ID 推导为同图前缀。</summary>
        [TestCase("official.level.test_01_01", "official.map.test_01")]
        [TestCase("official.level.test_06_05", "official.map.test_06")]
        public void CreateNew_DerivesMapIdFromLevelId(string levelId, string expectedMapId)
        {
            LevelDefinition definition = LevelAuthoringFactory.CreateNew(levelId).Definition;

            Assert.That(definition.MapId, Is.EqualTo(expectedMapId));
        }

        /// <summary>无法推导地图 ID 的关卡应留空, 由人工填写而非猜错。</summary>
        [TestCase("short")]
        [TestCase("official.only_three")]
        public void CreateNew_LeavesMapIdEmptyWhenNotDerivable(string levelId)
        {
            LevelDefinition definition = LevelAuthoringFactory.CreateNew(levelId).Definition;

            Assert.That(definition.MapId, Is.Empty);
        }

        /// <summary>调色板条目必须含起点终点且预制体稳定 ID 唯一。</summary>
        [Test]
        public void PaletteCatalog_HasUniqueStablePrefabIds()
        {
            IReadOnlyList<PaletteEntry> entries = LevelPaletteCatalog.GetEntries();
            List<string> spawnKinds = entries
                .Where(entry => entry.Kind == PaletteEntryKind.SpawnPoint)
                .Select(entry => entry.DisplayName)
                .ToList();
            List<string> goalKinds = entries
                .Where(entry => entry.Kind == PaletteEntryKind.GoalPoint)
                .Select(entry => entry.DisplayName)
                .ToList();
            List<string> prefabIds = entries
                .Where(entry => entry.Kind == PaletteEntryKind.StageObject)
                .Select(entry => entry.PrefabId)
                .ToList();

            Assert.That(spawnKinds, Has.Count.EqualTo(1));
            Assert.That(goalKinds, Has.Count.EqualTo(1));
            Assert.That(prefabIds, Has.Count.GreaterThan(0));
            Assert.That(prefabIds.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(prefabIds.Count));
            Assert.That(prefabIds, Has.None.Empty);
        }

        /// <summary>调色板必须能反查显示名称, 未知预制体回退为稳定 ID 而非报错。</summary>
        [Test]
        public void PaletteCatalog_ResolvesDisplayNameAndFallsBack()
        {
            Assert.That(LevelPaletteCatalog.GetDisplayName(LevelPaletteCatalog.GroundPrefabId), Is.EqualTo("地面平台"));
            Assert.That(
                LevelPaletteCatalog.GetDisplayName("official.prefab.unknown"),
                Is.EqualTo("official.prefab.unknown")
            );
            Assert.That(LevelPaletteCatalog.GetDisplayName(null), Is.Empty);
        }

        /// <summary>多次保存必须递增修订号, 以便后续编译摘要能识别内容变化。</summary>
        [Test]
        public void Save_PreservesIncrementedContentRevision()
        {
            LevelAuthoringData created = LevelAuthoringFactory.CreateNew("official.level.test_01_01");
            _repository.Save(new LevelId("official.level.test_01_01"), created);
            created.ContentRevision++;
            _repository.Save(new LevelId("official.level.test_01_01"), created);

            Assert.That(
                _repository.TryLoad(new LevelId("official.level.test_01_01"), out LevelAuthoringData loaded),
                Is.True
            );
            Assert.That(loaded.ContentRevision, Is.EqualTo(2));
        }

        /// <summary>断言坐标落在矩形边界内。</summary>
        /// <param name="bounds">矩形边界。</param>
        /// <param name="x">待检查的 X 坐标。</param>
        /// <param name="y">待检查的 Y 坐标。</param>
        private static void AssertContains(BoundsData bounds, float x, float y)
        {
            Assert.That(x, Is.InRange(bounds.MinX, bounds.MaxX));
            Assert.That(y, Is.InRange(bounds.MinY, bounds.MaxY));
        }
    }
}
