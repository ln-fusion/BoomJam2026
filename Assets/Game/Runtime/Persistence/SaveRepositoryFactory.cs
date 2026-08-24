using System.IO;
using Game.Contracts;
using Game.Contracts.Persistence;
using Game.Foundation;
using UnityEngine;

namespace Game.Persistence
{
    /// <summary>
    /// 创建使用 Unity 持久化数据目录的默认本地存档仓储。
    /// </summary>
    public static class SaveRepositoryFactory
    {
        /// <summary>创建默认 JSON 存档仓储。</summary>
        /// <param name="clock">可选时钟。</param>
        /// <param name="logger">可选日志记录器。</param>
        /// <param name="deviceId">可选设备标识。</param>
        /// <returns>写入 persistentDataPath/Saves 的存档仓储。</returns>
        public static ISaveRepository CreateDefault(IClock clock = null,
            IGameLogger logger = null, string deviceId = null)
        {
            string saveDirectory = Path.Combine(Application.persistentDataPath, "Saves");
            return new JsonSaveRepository(saveDirectory, clock, logger, deviceId);
        }
    }
}
