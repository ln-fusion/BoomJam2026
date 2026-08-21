using System.IO;
using Game.Contracts;
using Game.Contracts.Persistence;
using Game.Foundation;
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
