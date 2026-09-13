using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.Persistence;
using Game.Content;
using Game.Foundation;
using Game.Persistence;
using Game.Presentation;
using NUnit.Framework;
using EditorAssetDatabase = UnityEditor.AssetDatabase;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>验证 C03-C05 的设置、本地化和页面路由服务。</summary>
    public sealed class PresentationServicesTests
    {
        /// <summary>验证设置草稿会应用音量、Locale、窗口并保存。</summary>
        [Test]
        public void SettingsService_AppliesAndPersistsDraft()
        {
            RunAsync(async () =>
            {
                var repository = new FakeSaveRepository();
                var localization = new FakeLocalizationService();
                var audio = new RecordingAudioService();
                var window = new RecordingWindowSettingsApplier();
                using (var service = new SettingsService(repository, audio, localization, window))
                {
                    Result result = await service.ApplyAsync(new SettingsDraft(service.Current)
                    {
                        LanguageCode = "en-US",
                        MasterVolume = 0.7f,
                        MusicVolume = 0.5f,
                        SfxVolume = 0.25f,
                        Fullscreen = false,
                        ResolutionWidth = 1280,
                        ResolutionHeight = 720
                    }, CancellationToken.None);

                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(repository.Settings.LanguageCode, Is.EqualTo("en-US"));
                    Assert.That(audio.Values, Is.EqualTo(new[] { 0.7f, 0.5f, 0.25f }));
                    Assert.That(localization.CurrentLocaleCode, Is.EqualTo("en-US"));
                    Assert.That(window.Width, Is.EqualTo(1280));
                    Assert.That(window.Height, Is.EqualTo(720));
                    Assert.That(service.Current.Fullscreen, Is.False);
                }
            });
        }

        /// <summary>验证非法音量不会触发运行时副作用或写入。</summary>
        [Test]
        public void SettingsService_RejectsInvalidDraftBeforeApplying()
        {
            RunAsync(async () =>
            {
                var repository = new FakeSaveRepository();
                var audio = new RecordingAudioService();
                using (var service = new SettingsService(repository, audio,
                           new FakeLocalizationService(), new RecordingWindowSettingsApplier()))
                {
                    Result result = await service.ApplyAsync(new SettingsDraft(service.Current)
                    {
                        MasterVolume = 2f
                    }, CancellationToken.None);

                    Assert.That(result.IsSuccess, Is.False);
                    Assert.That(audio.ApplyCount, Is.EqualTo(0));
                    Assert.That(repository.SaveSettingsCount, Is.EqualTo(0));
                }
            });
        }

        /// <summary>验证稳定本地化 Key 唯一，并且可由版本化 CSV 完整提供。</summary>
        [Test]
        public void LocalizationKeys_AreUniqueAndPresentInSourceCsv()
        {
            Assert.That(UiTextKeys.All.Count, Is.EqualTo(UiTextKeys.All.Distinct().Count()));

            TextAsset source = EditorAssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Localization/UI.csv");
            Assert.That(source, Is.Not.Null);

            var sourceKeys = new HashSet<string>(StringComparer.Ordinal);
            string[] rows = source.text.Split(new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < rows.Length; i++)
            {
                string[] columns = rows[i].Split(',');
                if (columns.Length > 0)
                    sourceKeys.Add(columns[0].Trim().Trim('"'));
            }

            foreach (string key in UiTextKeys.All)
                Assert.That(sourceKeys, Does.Contain(key), "Missing source key: " + key);

            OfficialContentProvider provider = OfficialTestMapCatalog.CreateProvider();
            foreach (var map in provider.Maps)
                Assert.That(sourceKeys, Does.Contain(map.DisplayNameKey),
                    "Missing map display key: " + map.DisplayNameKey);
            foreach (var level in provider.Levels)
                Assert.That(sourceKeys, Does.Contain(level.DisplayNameKey),
                    "Missing level display key: " + level.DisplayNameKey);
        }

        /// <summary>验证四个页面可以切换，休息室存档恢复时回退地图。</summary>
        [Test]
        public void MetaPageRouter_SwitchesFourPages_AndRestoresSafeFallback()
        {
            var router = new MetaPageRouter();
            var pages = new List<MetaPageId>();
            router.PageChanged += pages.Add;

            router.Navigate(MetaPageId.Archive);
            router.Navigate(MetaPageId.Character);
            router.Navigate(MetaPageId.Lounge);

            Assert.That(router.CurrentPage, Is.EqualTo(MetaPageId.Lounge));
            Assert.That(pages, Is.EqualTo(new[]
            {
                MetaPageId.Archive, MetaPageId.Character, MetaPageId.Lounge
            }));
            Assert.That(router.Restore("lounge"), Is.EqualTo(MetaPageId.Map));
        }

        /// <summary>验证配置的 Mixer 暴露参数实际接收三类音量的分贝值。</summary>
        [Test]
        public void UnityAudioService_AppliesVolumesToConfiguredMixer()
        {
            AudioMixer mixer = EditorAssetDatabase.LoadAssetAtPath<AudioMixer>(
                "Assets/Game/Audio/Main.mixer");
            Assert.That(mixer, Is.Not.Null);

            var service = new UnityAudioService(mixer, null, null, null);
            service.ApplyVolumes(0.5f, 0.25f, 0f);

            Assert.That(mixer.GetFloat("MasterVolume", out _), Is.True);
            Assert.That(mixer.GetFloat("MusicVolume", out _), Is.True);
            Assert.That(mixer.GetFloat("SfxVolume", out _), Is.True);
            Assert.That(service.MasterVolume, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(service.MusicVolume, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(service.SfxVolume, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(UnityAudioService.ToDecibels(0.5f), Is.EqualTo(-6.0206f).Within(0.01f));
            Assert.That(UnityAudioService.ToDecibels(0.25f), Is.EqualTo(-12.0412f).Within(0.01f));
            Assert.That(UnityAudioService.ToDecibels(0f), Is.EqualTo(-80f).Within(0.01f));
        }

        /// <summary>验证第二关首次通关保存后播关后剧情，重复通关回地图，未观看的剧情不被伪造完成。</summary>
        [Test]
        public void WhiteboxCompletion_FirstAndRepeatUseSamePipeline()
        {
            RunAsync(async () =>
            {
                var saves = new List<ProfileSave>();
                var runtime = CreateRuntime((profile, reason, token) =>
                {
                    saves.Add(profile);
                    return Task.FromResult(SaveResult.Success());
                }, out var loader);
                var first = new LevelId("official.level.test_01_01");
                var second = new LevelId("official.level.test_01_02");
                runtime.CurrentProfile.CompletedLevelIds.Add(first.Value);
                BeginWhitebox(runtime, second);
                await runtime.Flow.CompleteLevelAsync(second, CancellationToken.None);
                var flow = (Game.Flow.GameFlowService)runtime.Flow;
                Assert.That(flow.CurrentStoryId.Value, Is.EqualTo("official.story.postlude.test_01_02"));
                Assert.That(runtime.CurrentProfile.CompletedLevelIds, Does.Contain(second.Value));
                Assert.That(runtime.CurrentProfile.CompletedStoryIds, Is.Empty);
                Assert.That(runtime.CurrentProfile.LevelRecords.Single(r => r.LevelId == second.Value).Completed, Is.True);
                Assert.That(new Game.Meta.MetaMapQuery(runtime.Content, runtime.ProgressQuery)
                    .GetLevelCard(new LevelId("official.level.test_01_03")).Node.IsInteractable, Is.True);
                BeginWhitebox(runtime, second);
                await runtime.Flow.CompleteLevelAsync(second, CancellationToken.None);
                Assert.That(loader.LastLoadRequest, Is.EqualTo(Game.Flow.SceneNames.MetaHub));
                Assert.That(saves, Has.Count.EqualTo(2));
                Assert.That(runtime.CurrentProfile.AppliedCompletionRunIds, Has.Count.EqualTo(2));
            });
        }

        /// <summary>验证通关写档失败不更新内存、不解锁下一关，也不跳转剧情。</summary>
        [Test]
        public void WhiteboxCompletion_SaveFailurePreservesProgress()
        {
            var runtime = CreateRuntime((profile, reason, token) =>
                Task.FromResult(SaveResult.Failure(ErrorCode.SaveFailed, "disk full")), out var loader);
            var original = runtime.CurrentProfile;
            var first = new LevelId("official.level.test_01_01");
            BeginWhitebox(runtime, first);
            Assert.Throws<InvalidOperationException>(() => RunAsync(() =>
                runtime.Flow.CompleteLevelAsync(first, CancellationToken.None)));
            Assert.That(runtime.CurrentProfile, Is.SameAs(original));
            Assert.That(original.CompletedLevelIds, Is.Empty);
            Assert.That(original.AppliedCompletionRunIds, Is.Empty);
            Assert.That(loader.LoadedSceneNames, Is.Empty);
        }

        /// <summary>验证页面与剧情写入串行完成、剧情事件只发布一次，重载档案后保留完成事实。</summary>
        [Test]
        public void PageAndStorySaves_ShareGateAndPersistCompletion()
        {
            RunAsync(async () =>
            {
                int writes = 0;
                int events = 0;
                var reasons = new List<SaveReason>();
                var bus = new Game.Flow.DomainEventBus(NullLogger.Instance);
                ProfileSave saved = null;
                var runtime = CreateRuntime((profile, reason, token) =>
                {
                    writes++;
                    reasons.Add(reason);
                    saved = profile;
                    return Task.FromResult(SaveResult.Success());
                }, out _, bus);
                var story = new StoryId("official.story.prelude.test_01_01");
                using var subscription = bus.Subscribe<Game.Contracts.Progression.StoryCompletedCommittedEvent>(e =>
                {
                    events++;
                    Assert.That(e.StoryId.Value, Is.EqualTo(story.Value));
                    Assert.That(runtime.CurrentProfile.CompletedStoryIds, Does.Contain(story.Value));
                });
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await runtime.SaveLastMetaPageAsync(MetaPageId.Archive, timeout.Token);
                await runtime.SaveStoryCompletedAsync(story, timeout.Token);
                await runtime.SaveStoryCompletedAsync(story, timeout.Token);
                Assert.That(writes, Is.EqualTo(2));
                Assert.That(events, Is.EqualTo(1));
                Assert.That(reasons.Count(reason => reason == SaveReason.StoryCommitted), Is.EqualTo(1));
                Assert.That(saved.CompletedStoryIds, Has.Count.EqualTo(1));
                var reloaded = CreateRuntime((profile, reason, token) => Task.FromResult(SaveResult.Success()), out _);
                reloaded.SetCurrentProfile(saved);
                Assert.That(reloaded.ProgressQuery.IsStoryReplayUnlocked(story), Is.True);
                Assert.That(saved.LastMetaPageId, Is.EqualTo("archive"));
            });
        }

        /// <summary>验证剧情保存失败不修改当前档案或发布事件，重试成功后才记录完成并发布一次事件。</summary>
        [Test]
        public void StoryCompletion_SaveFailurePreservesProgressAndAllowsRetry()
        {
            RunAsync(async () =>
            {
                int writes = 0;
                int events = 0;
                bool shouldFail = true;
                var bus = new Game.Flow.DomainEventBus(NullLogger.Instance);
                var runtime = CreateRuntime((profile, reason, token) =>
                {
                    writes++;
                    return Task.FromResult(shouldFail
                        ? SaveResult.Failure(ErrorCode.SaveFailed, "disk full")
                        : SaveResult.Success());
                }, out var loader, bus);
                using var subscription = bus.Subscribe<Game.Contracts.Progression.StoryCompletedCommittedEvent>(e => events++);
                var original = runtime.CurrentProfile;
                var story = new StoryId("official.story.prelude.test_01_01");
                var result = await runtime.SaveStoryCompletedAsync(story, CancellationToken.None);
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(runtime.CurrentProfile, Is.SameAs(original));
                Assert.That(original.CompletedStoryIds, Is.Empty);
                Assert.That(events, Is.Zero);

                shouldFail = false;
                result = await runtime.SaveStoryCompletedAsync(story, CancellationToken.None);
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(runtime.CurrentProfile.CompletedStoryIds, Does.Contain(story.Value));
                Assert.That(original.CompletedStoryIds, Is.Empty);
                Assert.That(events, Is.EqualTo(1));
                Assert.That(writes, Is.EqualTo(2));
                Assert.That(loader.LoadedSceneNames, Is.Empty);
            });
        }

        /// <summary>验证没有当前档案时剧情提交失败，不尝试写档或发布事件。</summary>
        [Test]
        public void StoryCompletion_NoProfileFailsWithoutWriteOrEvent()
        {
            RunAsync(async () =>
            {
                int writes = 0;
                int events = 0;
                var bus = new Game.Flow.DomainEventBus(NullLogger.Instance);
                var runtime = CreateRuntime((profile, reason, token) =>
                {
                    writes++;
                    return Task.FromResult(SaveResult.Success());
                }, out _, bus);
                using var subscription = bus.Subscribe<Game.Contracts.Progression.StoryCompletedCommittedEvent>(e => events++);
                runtime.SetCurrentProfile(null);
                var result = await runtime.SaveStoryCompletedAsync(
                    new StoryId("official.story.prelude.test_01_01"), CancellationToken.None);
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(runtime.CurrentProfile, Is.Null);
                Assert.That(writes, Is.Zero);
                Assert.That(events, Is.Zero);
            });
        }

        /// <summary>创建使用内存档案和假场景加载器的完整白盒服务链。</summary>
        /// <param name="save">可控的保存委托。</param>
        /// <param name="loader">用于断言导航的场景加载器。</param>
        /// <param name="bus">可选事件总线。</param>
        /// <returns>包含新档案的运行时服务。</returns>
        private static GameRuntimeServices CreateRuntime(
            Func<ProfileSave, SaveReason, CancellationToken, Task<SaveResult>> save,
            out FakeSceneLoader loader, IDomainEventBus bus = null)
        {
            loader = new FakeSceneLoader();
            bus = bus ?? new Game.Flow.DomainEventBus(NullLogger.Instance);
            GameRuntimeServices runtime = null;
            var flow = new Game.Flow.GameFlowService(loader, new FixedClock(), NullLogger.Instance, bus,
                completeLevel: (id, token) => runtime.CompleteLevelAndReturnAsync(id, token));
            var repository = new FakeSaveRepository();
            var localization = new FakeLocalizationService();
            var audio = new RecordingAudioService();
            var settings = new SettingsService(repository, audio, localization, new RecordingWindowSettingsApplier());
            runtime = new GameRuntimeServices(flow, settings, localization, audio,
                new ProfileLifecycleService(repository, new FixedClock()), new Game.Progression.EmptyProgressQuery(),
                new FixedClock(), save, eventBus: bus);
            runtime.SetCurrentProfile(new ProfileSave());
            return runtime;
        }

        /// <summary>在测试中建立地图创建的白盒会话，不扩大运行时入口的可见性。</summary>
        /// <param name="runtime">被测运行时服务。</param>
        /// <param name="level">目标关卡。</param>
        private static void BeginWhitebox(GameRuntimeServices runtime, LevelId level)
        {
            typeof(GameRuntimeServices).GetMethod("BeginWhiteboxLevel",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(runtime, new object[] { level });
        }

        /// <summary>在同步测试中执行异步操作并等待结果。</summary>
        /// <param name="operation">异步操作。</param>
        private static void RunAsync(Func<Task> operation)
        {
            Task.Run(operation).GetAwaiter().GetResult();
        }

        /// <summary>不依赖 Unity 主线程的设置用本地化替身。</summary>
        private sealed class FakeLocalizationService : ILocalizationService
        {
            private string _currentLocaleCode = DefaultLocalizationService.DefaultLocale;

            /// <summary>当前替身 Locale。</summary>
            public string CurrentLocaleCode => _currentLocaleCode;

            /// <summary>Locale 变更事件。</summary>
            public event Action<string> LocaleChanged;

            /// <summary>同步切换测试 Locale。</summary>
            /// <param name="localeCode">目标 Locale 代码。</param>
            /// <param name="cancellationToken">取消令牌。</param>
            /// <returns>切换结果。</returns>
            public Task<Result> SetLocaleAsync(string localeCode,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(localeCode))
                    return Task.FromResult(Result.Failure(ErrorCode.LocaleUnsupported,
                        "Locale code is required."));

                string normalizedCode = localeCode.Trim();
                if (string.Equals(_currentLocaleCode, normalizedCode,
                        StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(Result.Success());

                _currentLocaleCode = normalizedCode;
                LocaleChanged?.Invoke(normalizedCode);
                return Task.FromResult(Result.Success());
            }

            /// <summary>返回稳定 Key，供设置服务测试避免读取 Unity 资源。</summary>
            /// <param name="key">稳定本地化 Key。</param>
            /// <param name="arguments">未使用的格式化参数。</param>
            /// <returns>稳定 Key。</returns>
            public string Get(LocalizationKey key, params object[] arguments)
            {
                return key?.Value ?? string.Empty;
            }
        }

        /// <summary>最小设置/Profile 内存仓储替身。</summary>
        private sealed class FakeSaveRepository : ISaveRepository
        {
            /// <summary>当前设置数据。</summary>
            public SettingsSave Settings { get; private set; } = SettingsSave.CreateDefault();
            /// <summary>当前档案数据。</summary>
            public ProfileSave Profile { get; private set; }
            /// <summary>设置保存次数。</summary>
            public int SaveSettingsCount { get; private set; }

            /// <summary>读取设置。</summary>
            /// <param name="cancellationToken">取消令牌。</param>
            /// <returns>设置结果。</returns>
            public Task<LoadResult<SettingsSave>> LoadSettingsAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(new LoadResult<SettingsSave>(Settings, LoadSource.Primary,
                    ErrorCode.None));
            }

            /// <summary>读取档案。</summary>
            /// <param name="cancellationToken">取消令牌。</param>
            /// <returns>档案结果。</returns>
            public Task<LoadResult<ProfileSave>> LoadProfileAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(new LoadResult<ProfileSave>(Profile, LoadSource.NotFound,
                    ErrorCode.None));
            }

            /// <summary>保存设置。</summary>
            /// <param name="data">设置数据。</param>
            /// <param name="cancellationToken">取消令牌。</param>
            /// <returns>保存结果。</returns>
            public Task<SaveResult> SaveSettingsAsync(SettingsSave data,
                CancellationToken cancellationToken)
            {
                Settings = data;
                SaveSettingsCount++;
                return Task.FromResult(SaveResult.Success());
            }

            /// <summary>保存档案。</summary>
            /// <param name="data">档案数据。</param>
            /// <param name="reason">保存原因。</param>
            /// <param name="cancellationToken">取消令牌。</param>
            /// <returns>保存结果。</returns>
            public Task<SaveResult> SaveProfileAsync(ProfileSave data, SaveReason reason,
                CancellationToken cancellationToken)
            {
                Profile = data;
                return Task.FromResult(SaveResult.Success());
            }
        }

        /// <summary>记录音量应用参数的音频替身。</summary>
        private sealed class RecordingAudioService : IAudioService
        {
            /// <summary>最后一次音量参数。</summary>
            public float[] Values { get; private set; } = Array.Empty<float>();
            /// <summary>应用次数。</summary>
            public int ApplyCount { get; private set; }

            /// <summary>记录音量。</summary>
            /// <param name="master">主音量。</param>
            /// <param name="music">音乐音量。</param>
            /// <param name="sfx">音效音量。</param>
            public void ApplyVolumes(float master, float music, float sfx)
            {
                Values = new[] { master, music, sfx };
                ApplyCount++;
            }

            /// <summary>忽略音乐播放。</summary>
            /// <param name="musicId">音乐 ID。</param>
            /// <param name="transition">切换方式。</param>
            public void PlayMusic(MusicId musicId, MusicTransition transition) { }

            /// <summary>忽略音乐停止。</summary>
            /// <param name="transition">停止方式。</param>
            public void StopMusic(MusicTransition transition) { }

            /// <summary>忽略音效播放。</summary>
            /// <param name="sfxId">音效 ID。</param>
            public void PlaySfx(SfxId sfxId) { }
        }

        /// <summary>记录窗口参数的替身。</summary>
        private sealed class RecordingWindowSettingsApplier : IWindowSettingsApplier
        {
            /// <summary>最后宽度。</summary>
            public int Width { get; private set; }
            /// <summary>最后高度。</summary>
            public int Height { get; private set; }

            /// <summary>记录窗口设置。</summary>
            /// <param name="width">宽度。</param>
            /// <param name="height">高度。</param>
            /// <param name="fullscreen">是否全屏。</param>
            /// <returns>成功结果。</returns>
            public Result Apply(int width, int height, bool fullscreen)
            {
                Width = width;
                Height = height;
                return Result.Success();
            }
        }
    }
}
