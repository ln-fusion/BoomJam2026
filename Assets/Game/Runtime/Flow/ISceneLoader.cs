using System.Threading;
using System.Threading.Tasks;

namespace Game.Flow
{
    /// <summary>
    /// 场景加载器（Flow 内部接口）：封装功能 Scene 的 Additive 加载/卸载/激活。
    /// </summary>
    /// <remarks>
    /// C02 由 <see cref="UnitySceneLoader"/> 提供默认实现；测试可用内存假实现替代。
    /// </remarks>
    public interface ISceneLoader
    {
        /// <summary>
        /// Additive 加载场景并设为 Active 场景，返回是否成功。
        /// </summary>
        /// <param name="sceneName">Build Settings 中的场景名</param>
        /// <param name="cancellationToken">取消标记；取消时返回失败而非抛异常</param>
        Task<bool> LoadAdditiveAsync(string sceneName, CancellationToken cancellationToken);

        /// <summary>卸载指定场景.</summary>
        /// <param name="sceneName">要卸载的场景名</param>
        /// <param name="cancellationToken">取消标记；取消时返回失败而非抛异常</param>
        Task<bool> UnloadAsync(string sceneName, CancellationToken cancellationToken);

        /// <summary>当前已加载的功能场景名集合（不含 Bootstrap 场景自身）.</summary>
        System.Collections.Generic.IReadOnlyCollection<string> LoadedSceneNames { get; }
    }
}
