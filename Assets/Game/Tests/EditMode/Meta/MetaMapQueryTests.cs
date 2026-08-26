using System.Collections.Generic;
using Game.Content;
using Game.Contracts.Meta;
using Game.Contracts.Persistence;
using Game.Foundation;
using Game.Meta;
using Game.Progression;
using NUnit.Framework;

namespace Game.Tests.EditMode.Meta
{
    /// <summary>验证 C09 地图查询合并内容和进度后的节点状态。</summary>
    public sealed class MetaMapQueryTests
    {
        /// <summary>空存档应产生六张地图和每张五个节点。</summary>
        [Test]
        public void EmptyProfileBuildsAllMapTabsAndFirstCurrent()
        {
            var query = new MetaMapQuery(new OfficialContentService(
                OfficialTestMapCatalog.CreateProvider()), new ProfileProgressQuery(null));
            IReadOnlyList<MapTabViewModel> maps = query.GetMaps();

            Assert.That(maps, Has.Count.EqualTo(6));
            Assert.That(maps[0].Levels, Has.Count.EqualTo(5));
            Assert.That(maps[0].Levels[0].State, Is.EqualTo(LevelNodeState.Current));
            Assert.That(maps[0].Levels[1].State, Is.EqualTo(LevelNodeState.Locked));
        }

        /// <summary>完成首关后第二关应成为当前节点并显示最佳成绩。</summary>
        [Test]
        public void CompletedLevelAdvancesCurrentAndExposesBestScore()
        {
            var profile = new ProfileSave { CompletedLevelIds = new List<string> { "official.level.test_01_01" } };
            profile.LevelRecords.Add(new LevelRecordSave
            {
                LevelId = "official.level.test_01_01", Completed = true,
                CurrentBest = new BestScoreSave { ElapsedTicks = 120, TickRate = 60, CapacityUsed = 3 }
            });
            var query = new MetaMapQuery(new OfficialContentService(
                OfficialTestMapCatalog.CreateProvider()), new ProfileProgressQuery(profile));

            IReadOnlyList<LevelNodeViewModel> levels = query.GetLevels(new MapId("official.map.test_01"));
            Assert.That(levels[0].State, Is.EqualTo(LevelNodeState.Completed));
            Assert.That(levels[1].State, Is.EqualTo(LevelNodeState.Current));
            LevelCardViewModel card = query.GetLevelCard(new LevelId("official.level.test_01_01"));
            Assert.That(card.BestScore.ElapsedTicks, Is.EqualTo(120));
        }
    }
}
