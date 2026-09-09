using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts.Persistence;
using Game.Foundation;

namespace Game.Contracts.Progression
{
    /// <summary>
    /// 剧情完成事实提交事件：剧情到达 End 后由协调器提交完成事实并写入档案。
    /// </summary>
    /// <remarks>
    /// 事件只承载已提交的不可变事实，供地图刷新、解锁和重播查询订阅（技术设计文档 §13）。
    /// </remarks>
    public sealed class StoryCompletedCommittedEvent : IDomainEvent
    {
        /// <summary>已提交的剧情稳定标识。</summary>
        public StoryId StoryId { get; }

        /// <summary>创建剧情完成事件。</summary>
        /// <param name="storyId">已提交的剧情稳定标识。</param>
        public StoryCompletedCommittedEvent(StoryId storyId)
        {
            StoryId = storyId ?? throw new ArgumentNullException(nameof(storyId));
        }
    }

    /// <summary>
    /// 剧情完成事务协调器：把剧情完成事实写入玩家档案并发布提交事件。
    /// </summary>
    /// <remarks>
    /// 协调器只负责"提交"这一事务边界，是否继续播放/跳转由调用方决定。
    /// 存档成功是提交成功的底线：一旦写档失败，流程必须中断，不得继续跳转（技术设计文档 §13.3）。
    /// 提交成功后立即可通过 <see cref="IsCompleted"/> 查询到该事实。
    /// </remarks>
    public interface IStoryCompletionCoordinator
    {
        /// <summary>提交一条剧情完成事实（去重后写入档案）。</summary>
        /// <param name="storyId">已完成的剧情稳定标识。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>写入结果；失败时调用方不得继续跳转流程。</returns>
        Task<SaveResult> CommitCompletedAsync(StoryId storyId, CancellationToken cancellationToken);

        /// <summary>查询剧情完成事实是否已经提交。</summary>
        /// <param name="storyId">剧情稳定标识。</param>
        /// <returns>已提交返回 true。</returns>
        bool IsCompleted(StoryId storyId);
    }
}
