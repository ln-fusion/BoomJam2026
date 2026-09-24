using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Contracts.Gameplay;
using Game.Foundation;
using Game.Gameplay.Stage;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// 验证 C21 世界构建器的规划阶段: 稳定 ID 校验、资源解析失败语义,
    /// 以及规划失败时不产生任何 Scene 副作用。
    /// </summary>
    /// <remarks>
    /// 真实世界创建需要本地物理 Scene, 属 PlayMode 覆盖范围; 本类只测不需要场景的路径。
    /// </remarks>
    public sealed class StageWorldBuilderTests
    {
        /// <summary>构建器不接受空资源解析器, 否则后续解析会静默返回空。</summary>
        [Test]
        public void Constructor_RejectsNullResolver()
        {
            Assert.Throws<ArgumentNullException>(() => new StageWorldBuilder(null));
        }

        /// <summary>缺少关卡定义时必须立即失败。</summary>
        [Test]
        public void Build_RejectsNullDefinition()
        {
            var builder = new StageWorldBuilder(new EmptyAssetResolver());

            Result<IStageWorld> result = builder.Build(null);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.InvalidArgument));
        }

        /// <summary>关卡 ID 缺失或为空白时无法定位关卡, 必须拒绝。</summary>
        /// <param name="levelId">待校验的关卡 ID。</param>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Build_RejectsEmptyLevelId(string levelId)
        {
            var builder = new StageWorldBuilder(new EmptyAssetResolver());

            Result<IStageWorld> result = builder.Build(new LevelDefinition { LevelId = levelId });

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.InvalidArgument));
        }

        /// <summary>对象集合含 null 项时数据已损坏, 必须拒绝而非跳过。</summary>
        [Test]
        public void Build_RejectsNullObjectEntry()
        {
            var builder = new StageWorldBuilder(new EmptyAssetResolver());
            LevelDefinition definition = CreateDefinition(ValidObject("object.a", "official.prefab.x"), null);

            Result<IStageWorld> result = builder.Build(definition);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.InvalidArgument));
        }

        /// <summary>对象稳定 ID 必须非空且无首尾空白, 否则无法作为排序与字典键。</summary>
        /// <param name="objectId">待校验的对象稳定 ID。</param>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(" object.a")]
        [TestCase("object.a ")]
        public void Build_RejectsInvalidObjectId(string objectId)
        {
            var builder = new StageWorldBuilder(new EmptyAssetResolver());
            LevelDefinition definition = CreateDefinition(ValidObject(objectId, "official.prefab.x"));

            Result<IStageWorld> result = builder.Build(definition);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.InvalidArgument));
        }

        /// <summary>缺少预制体标识时无法解析资源, 必须拒绝。</summary>
        [Test]
        public void Build_RejectsEmptyPrefabId()
        {
            var builder = new StageWorldBuilder(new EmptyAssetResolver());
            LevelDefinition definition = CreateDefinition(ValidObject("object.a", "  "));

            Result<IStageWorld> result = builder.Build(definition);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.InvalidArgument));
        }

        /// <summary>重复对象 ID 会让实体索引产生歧义, 必须拒绝。</summary>
        [Test]
        public void Build_RejectsDuplicateObjectId()
        {
            var builder = new StageWorldBuilder(new EmptyAssetResolver());
            LevelDefinition definition = CreateDefinition(
                ValidObject("object.duplicate", "official.prefab.x"),
                ValidObject("object.duplicate", "official.prefab.y")
            );

            Result<IStageWorld> result = builder.Build(definition);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.InvalidArgument));
        }

        /// <summary>引用了未登记的预制体稳定 ID 属内容错误, 与参数非法区分。</summary>
        [Test]
        public void Build_ReturnsNotFoundForUnregisteredPrefab()
        {
            var builder = new StageWorldBuilder(new EmptyAssetResolver());
            LevelDefinition definition = CreateDefinition(ValidObject("object.a", "official.prefab.missing"));

            Result<IStageWorld> result = builder.Build(definition);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.NotFound));
        }

        /// <summary>
        /// 规划阶段必须完成全部检查后才进入创建, 因此失败时不得留下新 Scene。
        /// </summary>
        [Test]
        public void Build_DoesNotCreateSceneWhenPlanningFails()
        {
            var builder = new StageWorldBuilder(new EmptyAssetResolver());
            int sceneCountBefore = SceneManager.sceneCount;
            LevelDefinition definition = CreateDefinition(
                ValidObject("object.a", "official.prefab.x"),
                ValidObject("object.b", "official.prefab.missing")
            );

            Result<IStageWorld> result = builder.Build(definition);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCountBefore));
            // 逐个场景检查前缀: 世界场景名带递增序号, 不能按固定名查询。
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Assert.That(SceneManager.GetSceneAt(i).name, Does.Not.StartWith(StageWorldBuilder.SceneNamePrefix));
        }

        /// <summary>构造一个含指定对象集合的关卡定义。</summary>
        /// <param name="objects">关卡对象数据。</param>
        /// <returns>关卡定义。</returns>
        private static LevelDefinition CreateDefinition(params StageObjectData[] objects) =>
            new LevelDefinition { LevelId = "official.level.c21_world", Objects = new List<StageObjectData>(objects) };

        /// <summary>构造一个字段合法的关卡对象数据。</summary>
        /// <param name="objectId">对象稳定 ID。</param>
        /// <param name="prefabId">预制体稳定 ID。</param>
        /// <returns>关卡对象数据。</returns>
        private static StageObjectData ValidObject(string objectId, string prefabId) =>
            new StageObjectData { ObjectId = objectId, PrefabId = prefabId };

        /// <summary>测试用资源解析器: 不登记任何资源, 使全部预制体解析返回空。</summary>
        private sealed class EmptyAssetResolver : IAssetResolver
        {
            /// <inheritdoc/>
            public GameObject GetPrefab(PrefabId id) => null;

            /// <inheritdoc/>
            public GameObject GetUiPrefab(UiPrefabId id) => null;

            /// <inheritdoc/>
            public Sprite GetSprite(SpriteId id) => null;

            /// <inheritdoc/>
            public AudioClip GetAudio(AudioId id) => null;
        }
    }
}
