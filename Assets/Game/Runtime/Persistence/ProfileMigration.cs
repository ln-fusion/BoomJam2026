using System;
using System.Collections.Generic;
using Game.Contracts.Persistence;

namespace Game.Persistence
{
    /// <summary>
    /// 单步玩家档案结构迁移器。
    /// </summary>
    public interface IProfileMigrator
    {
        /// <summary>迁移输入的档案版本。</summary>
        int FromVersion { get; }
        /// <summary>迁移输出的档案版本。</summary>
        int ToVersion { get; }
        /// <summary>把旧版本档案迁移到 <see cref="ToVersion"/>。</summary>
        /// <param name="oldData">旧版本档案。</param>
        /// <returns>迁移后的档案。</returns>
        ProfileSave Migrate(ProfileSave oldData);
    }

    /// <summary>
    /// 按版本链执行玩家档案迁移的管线。
    /// </summary>
    public sealed class ProfileMigrationPipeline
    {
        private readonly Dictionary<int, IProfileMigrator> _migrators =
            new Dictionary<int, IProfileMigrator>();

        /// <summary>注册一条从指定输入版本开始的迁移步骤。</summary>
        /// <param name="migrator">迁移器。</param>
        public void Register(IProfileMigrator migrator)
        {
            if (migrator == null)
                throw new ArgumentNullException(nameof(migrator));
            if (migrator.ToVersion <= migrator.FromVersion)
                throw new ArgumentException("A migration must increase the schema version.");
            if (_migrators.ContainsKey(migrator.FromVersion))
                throw new ArgumentException("A migration is already registered for this version.");

            _migrators.Add(migrator.FromVersion, migrator);
        }

        /// <summary>将档案逐步迁移到当前结构版本。</summary>
        /// <param name="data">待迁移档案；为空时返回 null。</param>
        /// <returns>当前版本档案。</returns>
        public ProfileSave Migrate(ProfileSave data)
        {
            if (data == null)
                return null;

            var visited = new HashSet<int>();
            while (data.SchemaVersion < ProfileSave.CurrentSchemaVersion)
            {
                if (!visited.Add(data.SchemaVersion) ||
                    !_migrators.TryGetValue(data.SchemaVersion, out IProfileMigrator migrator))
                    throw new InvalidOperationException(
                        "No profile migration is registered for schema " + data.SchemaVersion);
                if (migrator.FromVersion != data.SchemaVersion)
                    throw new InvalidOperationException("Profile migration chain is invalid.");

                data = migrator.Migrate(data);
                if (data == null || data.SchemaVersion != migrator.ToVersion)
                    throw new InvalidOperationException("Profile migration returned an invalid version.");
            }

            if (data.SchemaVersion > ProfileSave.CurrentSchemaVersion)
                throw new InvalidOperationException("Profile schema is newer than this build.");
            return data;
        }
    }
}
