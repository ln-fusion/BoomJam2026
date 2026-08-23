using System.Threading;
using System.Threading.Tasks;

namespace Game.Contracts.Persistence
{
    /// <summary>
    /// 本地存档仓储接口，读写设置文件和单一玩家档案文件。
    /// </summary>
    /// <remarks>
    /// 业务系统不直接操作 JSON 文件。当前实现使用 <c>settings.json</c> 和
    /// <c>profile.json</c>，仓储负责校验、原子写入，并在读取失败时报告恢复来源。
    /// </remarks>
    public interface ISaveRepository
    {
        /// <summary>加载玩家设置。</summary>
        /// <param name="cancellationToken">取消标记。</param>
        /// <returns>设置加载结果；缺失或损坏时包含安全默认设置。</returns>
        Task<LoadResult<SettingsSave>> LoadSettingsAsync(CancellationToken cancellationToken);
        /// <summary>加载玩家档案。</summary>
        /// <param name="cancellationToken">取消标记。</param>
        /// <returns>档案加载结果；没有档案时数据可为 null。</returns>
        Task<LoadResult<ProfileSave>> LoadProfileAsync(CancellationToken cancellationToken);
        /// <summary>保存玩家设置。</summary>
        /// <param name="data">要保存的设置数据。</param>
        /// <param name="cancellationToken">取消标记。</param>
        /// <returns>写入结果。</returns>
        Task<SaveResult> SaveSettingsAsync(SettingsSave data,
            CancellationToken cancellationToken);
        /// <summary>保存玩家档案，并记录触发写入的业务原因。</summary>
        /// <param name="data">要保存的档案数据。</param>
        /// <param name="reason">触发写入的业务原因。</param>
        /// <param name="cancellationToken">取消标记。</param>
        /// <returns>写入结果。</returns>
        Task<SaveResult> SaveProfileAsync(ProfileSave data, SaveReason reason,
            CancellationToken cancellationToken);
    }
}
