using System;
using System.Collections.Generic;
using Game.Contracts.Persistence;

namespace Game.Persistence
{
    public interface IProfileMigrator
    {
        int FromVersion { get; }
        int ToVersion { get; }
        ProfileSave Migrate(ProfileSave oldData);
    }

    public sealed class ProfileMigrationPipeline
    {
        private readonly Dictionary<int, IProfileMigrator> _migrators =
            new Dictionary<int, IProfileMigrator>();

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
