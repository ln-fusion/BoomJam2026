using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.Persistence;
using Game.Contracts.Progression;
using Game.Foundation;

namespace Game.Flow
{
    /// <summary>
    /// 剧情完成事务协调器：把完成事实写入玩家档案并发布提交事件。
    /// </summary>
    /// <remarks>
    /// 写档入口复用 <see cref="SaveReason.StoryCommitted"/>，与游戏运行时写档共用一份
    /// Profile 引用；重复提交同一剧情时去重，不重复写入与发布。
    /// Profile 通过延迟委托获取，适配档案在启动后才加载的生命周期。
    /// </remarks>
    public sealed class StoryCompletionCoordinator : IStoryCompletionCoordinator
    {
        private readonly Func<ProfileSave> _getProfile;
        private readonly Func<ProfileSave, SaveReason, CancellationToken, Task<SaveResult>> _saveProfileAsync;
        private readonly IDomainEventBus _eventBus;
        private readonly IGameLogger _logger;

        /// <summary>创建剧情完成协调器。</summary>
        /// <param name="getProfile">获取当前玩家档案的委托；返回 null 时提交失败。</param>
        /// <param name="saveProfileAsync">档案保存委托。</param>
        /// <param name="eventBus">领域事件总线，用于发布完成事件。</param>
        /// <param name="logger">日志；为 null 时静默。</param>
        public StoryCompletionCoordinator(
            Func<ProfileSave> getProfile,
            Func<ProfileSave, SaveReason, CancellationToken, Task<SaveResult>> saveProfileAsync,
            IDomainEventBus eventBus,
            IGameLogger logger = null
        )
        {
            _getProfile = getProfile ?? throw new ArgumentNullException(nameof(getProfile));
            _saveProfileAsync = saveProfileAsync ?? throw new ArgumentNullException(nameof(saveProfileAsync));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logger = logger ?? NullLogger.Instance;
        }

        /// <inheritdoc/>
        public bool IsCompleted(StoryId storyId)
        {
            ProfileSave profile = _getProfile();
            return storyId != null
                && profile?.CompletedStoryIds != null
                && profile.CompletedStoryIds.Contains(storyId.Value);
        }

        /// <inheritdoc/>
        public async Task<SaveResult> CommitCompletedAsync(StoryId storyId, CancellationToken cancellationToken)
        {
            if (storyId == null)
                return SaveResult.Failure(ErrorCode.InvalidArgument, "Story ID is required.");
            ProfileSave profile = _getProfile();
            if (profile == null)
                return SaveResult.Failure(
                    ErrorCode.SaveFailed,
                    "No active profile; story completion cannot be committed."
                );

            if (string.IsNullOrEmpty(storyId.Value) || !profile.CompletedStoryIds.Contains(storyId.Value))
            {
                profile.CompletedStoryIds.Add(storyId.Value);
                SaveResult result = await _saveProfileAsync(profile, SaveReason.StoryCommitted, cancellationToken);
                if (!result.IsSuccess)
                {
                    // 写档失败时移除内存标记，保证重试时仍会尝试提交。
                    profile.CompletedStoryIds.Remove(storyId.Value);
                    _logger.LogError(LogContext.Empty, "[StoryCompletion] 剧情完成事实写入失败: " + storyId.Value);
                    return result;
                }
            }

            _eventBus.Publish(new StoryCompletedCommittedEvent(storyId));
            return SaveResult.Success();
        }
    }
}
