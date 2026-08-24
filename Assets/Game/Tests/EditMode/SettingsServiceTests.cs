#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.Persistence;
using Game.Flow;
using Game.Foundation;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 设置服务测试：加载默认值、应用、回滚、恢复默认.
    /// </summary>
    public sealed class SettingsServiceTests
    {
        private FakeSaveRepository _repository = null!;
        private FakeSettingsApplier _applier = null!;
        private DomainEventBus _eventBus = null!;
        private SettingsService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _repository = new FakeSaveRepository();
            _applier = new FakeSettingsApplier();
            _eventBus = new DomainEventBus(NullLogger.Instance);
            _service = new SettingsService(_repository, _applier, _eventBus, new FixedClock(), NullLogger.Instance);
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
        }

        [Test]
        public void Load_WithNoSave_UsesDefaults()
        {
            RunAsync(() => _service.LoadAsync(CancellationToken.None));

            Assert.That(_service.Current.MasterVolume, Is.EqualTo(1f));
            Assert.That(_service.Current.LanguageCode, Is.EqualTo("zh-CN"));
        }

        [Test]
        public void ApplyAsync_UpdatesSnapshot_AndPublishesEvent()
        {
            var applied = false;
            using (_eventBus.Subscribe<SettingsAppliedEvent>(e => applied = true))
            {
                RunAsync(() =>
                    _service.ApplyAsync(
                        new SettingsDraft
                        {
                            MasterVolume = 0.5f,
                            MusicVolume = 0.4f,
                            SfxVolume = 0.3f,
                            LanguageCode = "en-US",
                            Fullscreen = false,
                            ResolutionWidth = 1280,
                            ResolutionHeight = 720,
                        },
                        CancellationToken.None
                    )
                );
            }

            Assert.That(_service.Current.MasterVolume, Is.EqualTo(0.5f));
            Assert.That(_service.Current.LanguageCode, Is.EqualTo("en-US"));
            Assert.That(_applier.AppliedVolumes, Is.True);
            Assert.That(applied, Is.True);
        }

        [Test]
        public void ApplyAsync_InvalidVolume_IsRejected()
        {
            Result result = RunAsync(() =>
                _service.ApplyAsync(new SettingsDraft { MasterVolume = 2f }, CancellationToken.None)
            );

            Assert.That(result.IsSuccess, Is.False);
        }

        [Test]
        public void ApplyAsync_SaveFails_RollsBackSnapshot()
        {
            _repository.FailNextSave = true;
            var original = _service.Current.MasterVolume;

            Result result = RunAsync(() =>
                _service.ApplyAsync(new SettingsDraft { MasterVolume = 0.1f }, CancellationToken.None)
            );

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(_service.Current.MasterVolume, Is.EqualTo(original));
        }

        [Test]
        public void RestoreDefaults_ResetsAllValues()
        {
            RunAsync(async () =>
            {
                await _service.ApplyAsync(
                    new SettingsDraft { MasterVolume = 0.2f, LanguageCode = "en-US" },
                    CancellationToken.None
                );
                await _service.RestoreDefaultsAsync(CancellationToken.None);
            });

            Assert.That(_service.Current.MasterVolume, Is.EqualTo(1f));
            Assert.That(_service.Current.LanguageCode, Is.EqualTo("zh-CN"));
        }

        private static T RunAsync<T>(Func<Task<T>> operation) => Task.Run(operation).GetAwaiter().GetResult();

        private static void RunAsync(Func<Task> operation) => Task.Run(operation).GetAwaiter().GetResult();

        private sealed class FixedClock : IClock
        {
            public DateTimeOffset UtcNow => new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);
            public DateTimeOffset LocalNow => UtcNow;
        }
    }

    /// <summary>内存存档仓库（测试替身）.</summary>
    public sealed class FakeSaveRepository : ISaveRepository
    {
        public SettingsSave? Settings { get; private set; }
        public ProfileSave? Profile { get; private set; }
        public bool FailNextSave { get; set; }

        public Task<LoadResult<SettingsSave>> LoadSettingsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(
                new LoadResult<SettingsSave>(
                    Settings ?? SettingsSave.CreateDefault(),
                    LoadSource.Default,
                    ErrorCode.None
                )
            );

        public Task<LoadResult<ProfileSave>> LoadProfileAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new LoadResult<ProfileSave>(Profile!, LoadSource.NotFound, ErrorCode.None));

        public Task<SaveResult> SaveSettingsAsync(SettingsSave data, CancellationToken cancellationToken)
        {
            if (FailNextSave)
            {
                FailNextSave = false;
                return Task.FromResult(
                    SaveResult.Failure(new ErrorCode(ErrorCategory.SaveIo, "test"), "Simulated failure")
                );
            }

            Settings = data;
            return Task.FromResult(SaveResult.Success());
        }

        public Task<SaveResult> SaveProfileAsync(
            ProfileSave data,
            SaveReason reason,
            CancellationToken cancellationToken
        )
        {
            if (FailNextSave)
            {
                FailNextSave = false;
                return Task.FromResult(
                    SaveResult.Failure(new ErrorCode(ErrorCategory.SaveIo, "test"), "Simulated failure")
                );
            }

            Profile = data;
            return Task.FromResult(SaveResult.Success());
        }
    }

    /// <summary>设置应用器记录（测试替身）.</summary>
    public sealed class FakeSettingsApplier : ISettingsApplier
    {
        public bool AppliedVolumes { get; private set; }
        public bool AppliedWindow { get; private set; }
        public bool AppliedLanguage { get; private set; }

        public void ApplyVolumes(float master, float music, float sfx) => AppliedVolumes = true;

        public void ApplyWindow(bool fullscreen, int width, int height) => AppliedWindow = true;

        public bool ApplyLanguage(string languageCode)
        {
            AppliedLanguage = true;
            return true;
        }
    }
}
