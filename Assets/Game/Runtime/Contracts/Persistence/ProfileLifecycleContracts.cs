using System.Threading;
using System.Threading.Tasks;
using Game.Foundation;

namespace Game.Contracts.Persistence
{
    /// <summary>
    /// 启动时档案生命周期决策。
    /// </summary>
    public enum ProfileStartupMode
    {
        /// <summary>没有可继续的档案，需要创建新玩家档案。</summary>
        CreateNew,
        /// <summary>存在可继续的档案。</summary>
        Continue
    }

    /// <summary>
    /// 启动阶段加载档案后的下一步决策。
    /// </summary>
    public sealed class ProfileStartupDecision
    {
        /// <summary>启动流程应执行的档案路径。</summary>
        public ProfileStartupMode Mode { get; }
        /// <summary>可继续的档案；需要创建新档案时为 null。</summary>
        public ProfileSave Profile { get; }
        /// <summary>加载时是否发生过备份恢复或默认回退警告。</summary>
        public bool HasRecoveryWarning { get; }

        /// <summary>创建启动决策。</summary>
        /// <param name="mode">启动流程模式。</param>
        /// <param name="profile">可继续的档案；没有档案时为 null。</param>
        /// <param name="hasRecoveryWarning">是否带有恢复警告。</param>
        public ProfileStartupDecision(ProfileStartupMode mode, ProfileSave profile,
            bool hasRecoveryWarning = false)
        {
            Mode = mode;
            Profile = profile;
            HasRecoveryWarning = hasRecoveryWarning;
        }
    }

    /// <summary>
    /// 玩家档案生命周期服务，负责启动加载决策和新档案创建。
    /// </summary>
    public interface IProfileLifecycleService
    {
        /// <summary>加载现有档案，或在缺失时返回创建新档案的决策。</summary>
        /// <param name="cancellationToken">取消标记。</param>
        /// <returns>启动决策结果。</returns>
        Task<Result<ProfileStartupDecision>> LoadOrDecideAsync(CancellationToken cancellationToken);
        /// <summary>创建新的单一玩家档案并保存。</summary>
        /// <param name="nickname">玩家昵称。</param>
        /// <param name="cancellationToken">取消标记。</param>
        /// <returns>创建出的档案或失败错误。</returns>
        Task<Result<ProfileSave>> CreateProfileAsync(string nickname,
            CancellationToken cancellationToken);
    }
}
