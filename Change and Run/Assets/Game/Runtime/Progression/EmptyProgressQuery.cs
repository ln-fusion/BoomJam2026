using Game.Contracts.Progression;
using Game.Foundation.Ids;

namespace Game.Progression
{
    public sealed class EmptyProgressQuery : IProgressQuery
    {
        public ProgressSnapshot GetSnapshot()
        {
            return ProgressSnapshot.Empty;
        }

        public bool IsLevelUnlocked(LevelId levelId)
        {
            return false;
        }

        public bool IsStoryReplayUnlocked(StoryId storyId)
        {
            return false;
        }

        public BestScoreView GetBestScore(LevelId levelId)
        {
            return null;
        }
    }
}
