using System.Collections.Generic;
using Game.Foundation;

namespace Game.Contracts.Progression
{
    public sealed class BestScoreView
    {
        public long ElapsedTicks { get; }
        public int TickRate { get; }
        public int CapacityUsed { get; }

        public BestScoreView(long elapsedTicks, int tickRate, int capacityUsed)
        {
            ElapsedTicks = elapsedTicks;
            TickRate = tickRate;
            CapacityUsed = capacityUsed;
        }
    }

    public sealed class LevelProgressView
    {
        public LevelId LevelId { get; }
        public bool IsCompleted { get; }
        public BestScoreView BestScore { get; }

        public LevelProgressView(LevelId levelId, bool isCompleted,
            BestScoreView bestScore = null)
        {
            LevelId = levelId;
            IsCompleted = isCompleted;
            BestScore = bestScore;
        }
    }

    public sealed class ProgressSnapshot
    {
        public IReadOnlyCollection<LevelId> CompletedLevels { get; }
        public IReadOnlyCollection<StoryId> CompletedStories { get; }

        public ProgressSnapshot(IReadOnlyCollection<LevelId> completedLevels,
            IReadOnlyCollection<StoryId> completedStories)
        {
            CompletedLevels = completedLevels;
            CompletedStories = completedStories;
        }

        public static ProgressSnapshot Empty => new ProgressSnapshot(
            new List<LevelId>().AsReadOnly(), new List<StoryId>().AsReadOnly());
    }

    public interface IProgressQuery
    {
        ProgressSnapshot GetSnapshot();
        bool IsLevelUnlocked(LevelId levelId);
        bool IsStoryReplayUnlocked(StoryId storyId);
        BestScoreView GetBestScore(LevelId levelId);
    }
}
