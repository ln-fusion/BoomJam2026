using System;
using Game.Contracts.Content;
using Game.Contracts.Progression;
using Game.Foundation;

namespace Game.Progression
{
    /// <summary>按内容定义评估关卡解锁条件的统一实现。</summary>
    public sealed class UnlockEvaluator : IUnlockEvaluator
    {
        /// <inheritdoc/>
        public bool IsUnlocked(LevelSummary level, ProgressSnapshot progress)
        {
            if (level == null) return false;
            UnlockRequirementData rule = level.UnlockRequirement;
            if (rule == null || rule.Mode == UnlockRequirementMode.None ||
                rule.RequiredLevelIds == null || rule.RequiredLevelIds.Count == 0)
                return true;
            if (progress == null) progress = ProgressSnapshot.Empty;
            int completed = 0;
            foreach (string required in rule.RequiredLevelIds)
            {
                if (string.IsNullOrWhiteSpace(required)) return false;
                foreach (LevelId done in progress.CompletedLevels)
                    if (done != null && string.Equals(done.Value, required, StringComparison.Ordinal))
                    { completed++; break; }
            }
            return rule.Mode == UnlockRequirementMode.All
                ? completed == rule.RequiredLevelIds.Count : completed > 0;
        }
    }
}
