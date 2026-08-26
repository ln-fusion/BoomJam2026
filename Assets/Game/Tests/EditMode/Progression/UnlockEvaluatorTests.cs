using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Contracts.Progression;
using Game.Foundation;
using Game.Progression;
using NUnit.Framework;

namespace Game.Tests.EditMode.Progression
{
    /// <summary>验证 C12 All/Any 关卡解锁评估。</summary>
    public sealed class UnlockEvaluatorTests
    {
        /// <summary>All 要求全部前置完成，Any 只要求一个前置完成。</summary>
        [Test]
        public void EvaluatesAllAndAnyRequirements()
        {
            var progress = new ProgressSnapshot(
                new List<LevelId> { new LevelId("a") },
                new List<StoryId>());
            var evaluator = new UnlockEvaluator();
            var all = new LevelSummary
            {
                UnlockRequirement = new UnlockRequirementData
                {
                    Mode = UnlockRequirementMode.All,
                    RequiredLevelIds = new List<string> { "a", "b" }
                }
            };
            var any = new LevelSummary
            {
                UnlockRequirement = new UnlockRequirementData
                {
                    Mode = UnlockRequirementMode.Any,
                    RequiredLevelIds = new List<string> { "a", "b" }
                }
            };
            Assert.That(evaluator.IsUnlocked(all, progress), Is.False);
            Assert.That(evaluator.IsUnlocked(any, progress), Is.True);
        }
    }
}
