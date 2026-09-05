using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.Persistence;
using Game.Contracts.Progression;
using Game.Flow;
using Game.Foundation;
using NUnit.Framework;

namespace Game.Tests.EditMode.Flow
{
    /// <summary>
    /// 剧情完成事务协调器测试：写档、去重、失败阻断和事件发布。
    /// </summary>
    public sealed class StoryCompletionCoordinatorTests
    {
        private ProfileSave _profile;
        private List<SaveReason> _reasons;
        private SaveResult _nextResult;
        private DomainEventBus _eventBus;
        private StoryCompletionCoordinator _coordinator;

        /// <summary>创建协调器测试依赖。</summary>
        [SetUp]
        public void SetUp()
        {
            _profile = new ProfileSave();
            _reasons = new List<SaveReason>();
            _nextResult = SaveResult.Success();
            _eventBus = new DomainEventBus(NullLogger.Instance);
            _coordinator = new StoryCompletionCoordinator(
                () => _profile,
                (profile, reason, token) =>
                {
                    _reasons.Add(reason);
                    return Task.FromResult(_nextResult);
                },
                _eventBus,
                NullLogger.Instance
            );
        }

        /// <summary>验证提交成功后写入 CompletedStoryIds 并发布事件。</summary>
        [Test]
        public void CommitCompleted_WritesFact_AndPublishesEvent()
        {
            var published = new List<StoryCompletedCommittedEvent>();
            using (_eventBus.Subscribe<StoryCompletedCommittedEvent>(e => published.Add(e)))
            {
                RunAsync(() =>
                    _coordinator.CommitCompletedAsync(new StoryId("official.story.prologue"), CancellationToken.None)
                );
            }

            Assert.That(_profile.CompletedStoryIds, Does.Contain("official.story.prologue"));
            Assert.That(_reasons, Does.Contain(SaveReason.StoryCommitted));
            Assert.That(published, Has.Count.EqualTo(1));
        }

        /// <summary>验证重复提交同一剧情时只写一次档。</summary>
        [Test]
        public void CommitCompleted_SameStory_Deduplicates()
        {
            RunAsync(async () =>
            {
                await _coordinator.CommitCompletedAsync(new StoryId("official.story.prologue"), CancellationToken.None);
                await _coordinator.CommitCompletedAsync(new StoryId("official.story.prologue"), CancellationToken.None);
            });

            Assert.That(_profile.CompletedStoryIds, Has.Count.EqualTo(1));
            Assert.That(_reasons, Has.Count.EqualTo(1));
        }

        /// <summary>验证写档失败时返回失败且不发布事件。</summary>
        [Test]
        public void CommitCompleted_SaveFails_ReturnsFailure_NoEvent()
        {
            _nextResult = SaveResult.Failure(ErrorCode.SaveFailed, "disk full");
            var published = new List<StoryCompletedCommittedEvent>();
            using (_eventBus.Subscribe<StoryCompletedCommittedEvent>(e => published.Add(e)))
            {
                SaveResult result = RunAsync(() =>
                    _coordinator.CommitCompletedAsync(new StoryId("official.story.prologue"), CancellationToken.None)
                );

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(published, Is.Empty);
                Assert.That(_profile.CompletedStoryIds, Does.Not.Contain("official.story.prologue"));
            }
        }

        /// <summary>验证无档案时提交失败。</summary>
        [Test]
        public void CommitCompleted_NoProfile_Fails()
        {
            _profile = null;
            SaveResult result = RunAsync(() =>
                _coordinator.CommitCompletedAsync(new StoryId("official.story.prologue"), CancellationToken.None)
            );

            Assert.That(result.IsSuccess, Is.False);
        }

        /// <summary>在同步测试中执行异步操作并等待结果。</summary>
        /// <param name="operation">要执行的异步操作。</param>
        private static T RunAsync<T>(Func<Task<T>> operation)
        {
            return Task.Run(operation).GetAwaiter().GetResult();
        }

        /// <summary>在同步测试中执行异步操作并等待结果。</summary>
        /// <param name="operation">要执行的异步操作。</param>
        private static void RunAsync(Func<Task> operation)
        {
            Task.Run(operation).GetAwaiter().GetResult();
        }
    }
}
