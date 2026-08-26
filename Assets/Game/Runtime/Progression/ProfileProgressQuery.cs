using System.Collections.Generic;
using Game.Contracts.Persistence;
using Game.Contracts.Progression;
using Game.Foundation;

namespace Game.Progression
{
    /// <summary>Maps a profile save snapshot to the C07 read-only progress contract.</summary>
    public sealed class ProfileProgressQuery : IProgressQuery
    {
        private readonly ProgressSnapshot _snapshot;

        /// <summary>Creates a query over a profile; null produces an empty snapshot.</summary>
        /// <param name="profile">Profile save data to expose read-only.</param>
        public ProfileProgressQuery(ProfileSave profile)
        {
            _snapshot = BuildSnapshot(profile);
        }

        /// <inheritdoc/>
        public ProgressSnapshot GetSnapshot() => _snapshot;

        /// <inheritdoc/>
        public bool IsLevelUnlocked(LevelId levelId)
        {
            foreach (LevelId completed in _snapshot.CompletedLevels)
                if (completed == levelId)
                    return true;
            return false;
        }

        /// <inheritdoc/>
        public bool IsStoryReplayUnlocked(StoryId storyId)
        {
            foreach (StoryId completed in _snapshot.CompletedStories)
                if (completed == storyId)
                    return true;
            return false;
        }

        /// <inheritdoc/>
        public BestScoreView GetBestScore(LevelId levelId)
        {
            foreach (LevelProgressView level in _snapshot.Levels)
                if (level.LevelId == levelId)
                    return level.BestScore;
            return null;
        }

        private static ProgressSnapshot BuildSnapshot(ProfileSave profile)
        {
            var completedLevels = new List<LevelId>();
            var completedStories = new List<StoryId>();
            var levels = new List<LevelProgressView>();
            if (profile == null)
                return new ProgressSnapshot(completedLevels, completedStories, levels);

            if (profile.CompletedLevelIds != null)
                foreach (string id in profile.CompletedLevelIds)
                    if (!string.IsNullOrWhiteSpace(id))
                        completedLevels.Add(new LevelId(id));
            if (profile.CompletedStoryIds != null)
                foreach (string id in profile.CompletedStoryIds)
                    if (!string.IsNullOrWhiteSpace(id))
                        completedStories.Add(new StoryId(id));
            if (profile.LevelRecords != null)
                foreach (LevelRecordSave record in profile.LevelRecords)
                {
                    if (record == null || string.IsNullOrWhiteSpace(record.LevelId))
                        continue;
                    BestScoreView best = record.CurrentBest == null ? null :
                        new BestScoreView(record.CurrentBest.ElapsedTicks,
                            record.CurrentBest.TickRate, record.CurrentBest.CapacityUsed);
                    levels.Add(new LevelProgressView(new LevelId(record.LevelId),
                        record.Completed, best));
                }
            return new ProgressSnapshot(completedLevels, completedStories, levels);
        }
    }
}
