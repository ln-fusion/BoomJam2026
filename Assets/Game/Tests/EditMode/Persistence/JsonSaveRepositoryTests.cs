using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.Persistence;
using Game.Persistence;
using NUnit.Framework;

namespace Game.Tests.EditMode.Persistence
{
    public sealed class JsonSaveRepositoryTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(),
                "BoomJam2026-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public void Settings_SecondSaveCreatesBackup_AndCorruptPrimaryRecoversIt()
        {
            RunAsync(async () =>
            {
                using (var repository = new JsonSaveRepository(_directory))
                {
                    var first = SettingsSave.CreateDefault();
                    first.MasterVolume = 0.25f;
                    Assert.That((await repository.SaveSettingsAsync(first,
                        CancellationToken.None)).IsSuccess, Is.True);

                    var second = SettingsSave.CreateDefault();
                    second.MasterVolume = 0.75f;
                    Assert.That((await repository.SaveSettingsAsync(second,
                        CancellationToken.None)).IsSuccess, Is.True);

                    File.WriteAllText(Path.Combine(_directory, "settings.json"), "{broken");
                    LoadResult<SettingsSave> loaded = await repository
                        .LoadSettingsAsync(CancellationToken.None);

                    Assert.That(loaded.Source, Is.EqualTo(LoadSource.Backup));
                    Assert.That(loaded.HasRecoveryWarning, Is.True);
                    Assert.That(loaded.Data.MasterVolume, Is.EqualTo(0.25f));
                    Assert.That(File.Exists(Path.Combine(_directory, "settings.tmp")), Is.False);
                }
            });
        }

        [Test]
        public void Settings_MissingFilesReturnSafeDefaults()
        {
            RunAsync(async () =>
            {
                using (var repository = new JsonSaveRepository(_directory))
                {
                    LoadResult<SettingsSave> loaded = await repository
                        .LoadSettingsAsync(CancellationToken.None);

                    Assert.That(loaded.Source, Is.EqualTo(LoadSource.Default));
                    Assert.That(loaded.HasRecoveryWarning, Is.False);
                    Assert.That(loaded.Data.LanguageCode, Is.EqualTo("zh-CN"));
                }
            });
        }

        [Test]
        public void Profile_SaveIncrementsRevisionOnlyAfterSuccessfulWrite()
        {
            RunAsync(async () =>
            {
                var clock = new FixedClock(new DateTimeOffset(2026, 8, 17, 12, 0, 0,
                    TimeSpan.Zero));
                using (var repository = new JsonSaveRepository(_directory, clock,
                           deviceId: "test-device"))
                {
                    var profile = new ProfileSave
                    {
                        ProfileId = Guid.NewGuid().ToString("N"),
                        PlayerNickname = "Tester",
                        CreatedAtUtc = clock.UtcNow.ToString("O"),
                        LastModifiedAtUtc = clock.UtcNow.ToString("O")
                    };

                    SaveResult result = await repository.SaveProfileAsync(profile,
                        SaveReason.ProfileCreated, CancellationToken.None);

                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(profile.Revision, Is.EqualTo(1));
                    Assert.That(profile.LastWriterDeviceId, Is.EqualTo("test-device"));
                }
            });
        }

        private static void RunAsync(Func<Task> operation)
        {
            Task.Run(operation).GetAwaiter().GetResult();
        }

        private sealed class FixedClock : IClock
        {
            public DateTimeOffset UtcNow { get; }
            public DateTimeOffset LocalNow => UtcNow.ToLocalTime();

            public FixedClock(DateTimeOffset utcNow)
            {
                UtcNow = utcNow;
            }
        }
    }
}
