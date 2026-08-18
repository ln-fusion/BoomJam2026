using System.IO;
using Game.Contracts.Logging;
using Game.Contracts.Persistence;
using Game.Contracts.Time;
using UnityEngine;

namespace Game.Persistence
{
    public static class SaveRepositoryFactory
    {
        public static ISaveRepository CreateDefault(IClock clock = null,
            IGameLogger logger = null, string deviceId = null)
        {
            string saveDirectory = Path.Combine(Application.persistentDataPath, "Saves");
            return new JsonSaveRepository(saveDirectory, clock, logger, deviceId);
        }
    }
}
