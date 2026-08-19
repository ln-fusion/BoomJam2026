using System;
using System.IO;
using System.Threading;
using Game.Contracts.Persistence;
using Game.Contracts.Time;
using Game.Foundation.Results;
using Game.Persistence;
using NUnit.Framework;

namespace Game.Tests.EditMode.Persistence
{
    public sealed class ProfileLifecycleTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(),
                "BoomJam2026-profile-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public void MissingProfile_RequestsNewNickname_ThenExistingProfileContinues()
        {
            using (var repository = new JsonSaveRepository(_directory,
                       new FixedClock()))
            {
                var lifecycle = new ProfileLifecycleService(repository, new FixedClock());
                Result<ProfileStartupDecision> first = lifecycle
                    .LoadOrDecideAsync(CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(first.IsSuccess, Is.True);
                Assert.That(first.Value.Mode, Is.EqualTo(ProfileStartupMode.CreateNew));

                Result<ProfileSave> created = lifecycle.CreateProfileAsync("  Player  ",
                    CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(created.IsSuccess, Is.True);
                Assert.That(created.Value.PlayerNickname, Is.EqualTo("Player"));

                Result<ProfileStartupDecision> second = lifecycle
                    .LoadOrDecideAsync(CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(second.Value.Mode, Is.EqualTo(ProfileStartupMode.Continue));
                Assert.That(second.Value.Profile.ProfileId, Is.EqualTo(created.Value.ProfileId));
            }
        }

        [Test]
        public void InvalidNickname_IsRejectedBeforeWriting()
        {
            using (var repository = new JsonSaveRepository(_directory))
            {
                var lifecycle = new ProfileLifecycleService(repository);
                Result<ProfileSave> result = lifecycle.CreateProfileAsync("\n",
                    CancellationToken.None).GetAwaiter().GetResult();

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(File.Exists(Path.Combine(_directory, "profile.json")), Is.False);
            }
        }

        [Test]
        public void MigrationPipeline_RequiresAndAppliesExplicitVersionSteps()
        {
            var pipeline = new ProfileMigrationPipeline();
            pipeline.Register(new VersionZeroToOneMigrator());
            var profile = new ProfileSave { SchemaVersion = 0 };

            ProfileSave migrated = pipeline.Migrate(profile);

            Assert.That(migrated.SchemaVersion, Is.EqualTo(ProfileSave.CurrentSchemaVersion));
        }

        private sealed class FixedClock : IClock
        {
            public DateTimeOffset UtcNow => new DateTimeOffset(2026, 8, 18, 12, 0, 0,
                TimeSpan.Zero);
            public DateTimeOffset LocalNow => UtcNow;
        }

        private sealed class VersionZeroToOneMigrator : IProfileMigrator
        {
            public int FromVersion => 0;
            public int ToVersion => 1;
            public ProfileSave Migrate(ProfileSave oldData)
            {
                oldData.SchemaVersion = 1;
                return oldData;
            }
        }
    }
}
