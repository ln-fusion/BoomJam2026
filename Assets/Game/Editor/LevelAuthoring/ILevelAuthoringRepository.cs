using System.Collections.Generic;
using Game.Foundation;

namespace Game.Editor.Level
{
    /// <summary>
    /// 官方关卡源数据读写抽象; 面向编辑器与内容编译工具。
    /// </summary>
    public interface ILevelAuthoringRepository
    {
        /// <summary>枚举全部关卡源数据, 按关卡稳定 ID 排序。</summary>
        /// <returns>只读关卡源数据列表。</returns>
        IReadOnlyList<LevelAuthoringData> GetAllLevels();

        /// <summary>尝试加载指定关卡源数据。</summary>
        /// <param name="levelId">关卡稳定标识。</param>
        /// <param name="data">加载出的关卡源数据; 不存在或损坏时为 null。</param>
        /// <returns>加载成功返回 true。</returns>
        bool TryLoad(LevelId levelId, out LevelAuthoringData data);

        /// <summary>保存关卡源数据; 覆盖同 ID 的既有文件。</summary>
        /// <param name="levelId">关卡稳定标识; 必须与数据内 Definition.LevelId 一致。</param>
        /// <param name="data">待保存数据。</param>
        /// <returns>保存结果。</returns>
        Result Save(LevelId levelId, LevelAuthoringData data);

        /// <summary>删除指定关卡源数据; 不存在时返回 NotFound。</summary>
        /// <param name="levelId">关卡稳定标识。</param>
        /// <returns>删除结果。</returns>
        Result Delete(LevelId levelId);
    }
}
