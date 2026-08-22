using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.Persistence;
using Game.Foundation;
using Game.Persistence;
using NUnit.Framework;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>
    /// ProfileLifecycleService 测试：启动决策、昵称校验和迁移管线。
    /// </summary>
    public sealed class ProfileLifecycleTests
    {
        private string _directory;

        /// <summary>创建临时档案目录。</summary>
        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(),
                "BoomJam2026-profile-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        /// <summary>清理临时档案目录。</summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        /// <summary>验证缺少档案时先要求新昵称，随后可继续已有档案。</summary>
        [Test]
        public void MissingProfile_RequestsNewNickname_ThenExistingProfileContinues()
        {
            RunAsync(async () =>
            {
                using (var repository = new JsonSaveRepository(_directory,
                           new FixedClock()))
                {
                    var lifecycle = new ProfileLifecycleService(repository, new FixedClock());
                    Result<ProfileStartupDecision> first = await lifecycle
                        .LoadOrDecideAsync(CancellationToken.None);
                    Assert.That(first.IsSuccess, Is.True);
                    Assert.That(first.Value.Mode, Is.EqualTo(ProfileStartupMode.CreateNew));

                    Result<ProfileSave> created = await lifecycle.CreateProfileAsync("  Player  ",
                        CancellationToken.None);
                    Assert.That(created.IsSuccess, Is.True);
                    Assert.That(created.Value.PlayerNickname, Is.EqualTo("Player"));

                    Result<ProfileStartupDecision> second = await lifecycle
                        .LoadOrDecideAsync(CancellationToken.None);
                    Assert.That(second.Value.Mode, Is.EqualTo(ProfileStartupMode.Continue));
                    Assert.That(second.Value.Profile.ProfileId, Is.EqualTo(created.Value.ProfileId));
                }
            });
        }

        /// <summary>验证非法昵称会在写入前被拒绝。</summary>
        [Test]
        public void InvalidNickname_IsRejectedBeforeWriting()
        {
            RunAsync(async () =>
            {
                using (var repository = new JsonSaveRepository(_directory))
                {
                    var lifecycle = new ProfileLifecycleService(repository);
                    Result<ProfileSave> result = await lifecycle.CreateProfileAsync("\n",
                        CancellationToken.None);

                    Assert.That(result.IsSuccess, Is.False);
                    Assert.That(File.Exists(Path.Combine(_directory, "profile.json")), Is.False);
                }
            });
        }

        /// <summary>验证迁移管线要求显式版本步骤并能按链路迁移。</summary>
        [Test]
        public void MigrationPipeline_RequiresAndAppliesExplicitVersionSteps()
        {
            var pipeline = new ProfileMigrationPipeline();
            pipeline.Register(new VersionZeroToOneMigrator());
            var profile = new ProfileSave { SchemaVersion = 0 };

            ProfileSave migrated = pipeline.Migrate(profile);

            Assert.That(migrated.SchemaVersion, Is.EqualTo(ProfileSave.CurrentSchemaVersion));
        }

        /// <summary>固定时钟替身。</summary>
        private sealed class FixedClock : IClock
        {
            /// <summary>固定 UTC 时间。</summary>
            public DateTimeOffset UtcNow => new DateTimeOffset(2026, 8, 18, 12, 0, 0,
                TimeSpan.Zero);
            /// <summary>固定本地时间。</summary>
            public DateTimeOffset LocalNow => UtcNow;
        }

        /// <summary>在同步测试中执行异步操作并等待结果。</summary>
        /// <param name="operation">要执行的异步操作。</param>
        private static void RunAsync(Func<Task> operation)
        {
            Task.Run(operation).GetAwaiter().GetResult();
        }

        /// <summary>从 0 版本迁移到 1 版本的测试迁移器。</summary>
        private sealed class VersionZeroToOneMigrator : IProfileMigrator
        {
            /// <summary>迁移输入版本。</summary>
            public int FromVersion => 0;
            /// <summary>迁移输出版本。</summary>
            public int ToVersion => 1;
            /// <summary>把旧档案版本号更新为 1。</summary>
            /// <param name="oldData">旧版本档案。</param>
            /// <returns>更新后的档案。</returns>
            public ProfileSave Migrate(ProfileSave oldData)
            {
                oldData.SchemaVersion = 1;
                return oldData;
            }
        }
    }
}
