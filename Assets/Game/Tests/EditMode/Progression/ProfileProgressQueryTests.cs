using System.Collections.Generic;
using Game.Contracts.Persistence;
using Game.Foundation;
using Game.Progression;
using NUnit.Framework;

namespace Game.Tests.EditMode.Progression
{
    /// <summary>Verifies mapping from ProfileSave to the C07 read-only progress model.</summary>
    public sealed class ProfileProgressQueryTests
    {
        /// <summary>Maps completed facts and the current best score without exposing mutable DTOs.</summary>
        [Test]
        public void ProfileFacts_AreMappedToReadOnlySnapshot()
        {
            var profile = new ProfileSave
            {
                ProfileId = "00000000000000000000000000000001",
                CompletedLevelIds = new List<string> { "official.level.test_01_01" },
                CompletedStoryIds = new List<string> { "official.story.c06_branch" },
                LevelRecords = new List<LevelRecordSave>
                {
                    new LevelRecordSave
                    {
                        LevelId = "official.level.test_01_01", Completed = true,
                        CurrentBest = new BestScoreSave { ElapsedTicks = 12, TickRate = 60, CapacityUsed = 2 }
                    }
                }
            };
            var query = new ProfileProgressQuery(profile);
            Assert.That(query.IsLevelUnlocked(new LevelId("official.level.test_01_01")), Is.True);
            Assert.That(query.IsStoryReplayUnlocked(new StoryId("official.story.c06_branch")), Is.True);
            Assert.That(query.GetBestScore(new LevelId("official.level.test_01_01")).ElapsedTicks, Is.EqualTo(12));
            Assert.That(query.GetSnapshot().Levels, Has.Count.EqualTo(1));
        }
    }
}
