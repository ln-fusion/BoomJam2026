using System.Collections.Generic;
using System.IO;
using Game.Contracts.Content;
using Game.Editor.Level;
using Game.Foundation;
using NUnit.Framework;

namespace Game.Tests.EditMode.Editor
{
    /// <summary>验证 C19 FileLevelAuthoringRepository 的读写与损坏文件处理。</summary>
    public sealed class FileLevelAuthoringRepositoryTests
    {
        private string _root;
        private FileLevelAuthoringRepository _repository;

        /// <summary>每个用例使用独立临时目录, 结束后清理。</summary>
        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "C19LevelAuthoringTests", System.Guid.NewGuid().ToString("N"));
            _repository = new FileLevelAuthoringRepository(_root);
        }

        /// <summary>清理临时目录。</summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        /// <summary>构造最小 Authoring 数据。</summary>
        /// <param name="levelId">关卡稳定标识。</param>
        /// <returns>待保存的 Authoring 数据。</returns>
        private static LevelAuthoringData CreateData(string levelId)
        {
            var data = new LevelAuthoringData
            {
                Definition = new LevelDefinition
                {
                    LevelId = levelId,
                    MapId = "official.map.factory_001",
                    CapacityLimit = 4,
                },
                EditorViewState = new EditorViewStateData { ViewCenterX = 3f, Zoom = 2f },
            };
            return data;
        }

        /// <summary>保存后可枚举并按顺序加载; 视口元数据也正确往返。</summary>
        [Test]
        public void Save_ThenGetAll_LoadsRoundTrip()
        {
            LevelId levelId = new LevelId("official.level.c19_repo_01");
            Result save = _repository.Save(levelId, CreateData(levelId.Value));
            Assert.That(save.IsSuccess, Is.True);

            IReadOnlyList<LevelAuthoringData> all = _repository.GetAllLevels();
            Assert.That(all, Has.Count.EqualTo(1));
            Assert.That(all[0].Definition.LevelId, Is.EqualTo(levelId.Value));
            Assert.That(all[0].Definition.CapacityLimit, Is.EqualTo(4));
            Assert.That(all[0].EditorViewState.Zoom, Is.EqualTo(2f));

            Assert.That(_repository.TryLoad(levelId, out LevelAuthoringData loaded), Is.True);
            Assert.That(loaded.Definition.LevelId, Is.EqualTo(levelId.Value));
        }

        /// <summary>重复保存同 ID 覆盖旧内容。</summary>
        [Test]
        public void Save_TwiceSameId_Overwrites()
        {
            LevelId levelId = new LevelId("official.level.c19_repo_02");
            Assert.That(_repository.Save(levelId, CreateData(levelId.Value)).IsSuccess, Is.True);

            LevelAuthoringData updated = CreateData(levelId.Value);
            updated.Definition.CapacityLimit = 12;
            Assert.That(_repository.Save(levelId, updated).IsSuccess, Is.True);

            Assert.That(_repository.TryLoad(levelId, out LevelAuthoringData loaded), Is.True);
            Assert.That(loaded.Definition.CapacityLimit, Is.EqualTo(12));
        }

        /// <summary>保存时 LevelId 与定义内不一致必须拒绝。</summary>
        [Test]
        public void Save_WithMismatchedLevelId_ReturnsInvalidArgument()
        {
            Result result = _repository.Save(
                new LevelId("official.level.c19_repo_03"),
                CreateData("official.level.c19_different")
            );
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.InvalidArgument));
        }

        /// <summary>损坏文件 TryLoad 返回 false, GetAll 跳过。</summary>
        [Test]
        public void CorruptFile_TryLoadFails_AndGetAllSkips()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, "official.level.c19_broken.level.authoring.json"), "{ not json");

            Assert.That(_repository.TryLoad(new LevelId("official.level.c19_broken"), out _), Is.False);
            Assert.That(_repository.GetAllLevels(), Is.Empty);
        }

        /// <summary>删除不存在的 ID 返回 NotFound。</summary>
        [Test]
        public void Delete_MissingLevel_ReturnsNotFound()
        {
            Result result = _repository.Delete(new LevelId("official.level.c19_missing"));
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.NotFound));
        }

        /// <summary>删除成功后 TryLoad 返回 false。</summary>
        [Test]
        public void Delete_ExistingLevel_RemovesFile()
        {
            LevelId levelId = new LevelId("official.level.c19_repo_04");
            Assert.That(_repository.Save(levelId, CreateData(levelId.Value)).IsSuccess, Is.True);
            Assert.That(_repository.Delete(levelId).IsSuccess, Is.True);
            Assert.That(_repository.TryLoad(levelId, out _), Is.False);
        }

        /// <summary>保存后不残留 .tmp 文件。</summary>
        [Test]
        public void Save_DoesNotLeaveTemporaryFiles()
        {
            LevelId levelId = new LevelId("official.level.c19_repo_05");
            Assert.That(_repository.Save(levelId, CreateData(levelId.Value)).IsSuccess, Is.True);
            Assert.That(
                Directory.GetFiles(_root, "*.tmp"),
                Is.Empty,
                "SafeJsonWrite must not leave temporary files behind."
            );
        }
    }
}
