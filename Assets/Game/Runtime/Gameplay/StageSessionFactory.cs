using System;
using Game.Contracts.Content;
using Game.Contracts.Gameplay;

namespace Game.Gameplay
{
    /// <summary>
    /// 关卡会话工厂的默认实现; 为每个新建会话绑定同一个世界构建器。
    /// </summary>
    /// <remarks>
    /// 世界构建器是会话开始模拟时按关卡定义重建物理世界的唯一入口, 由组合根在构造工厂时注入,
    /// 使会话本身不依赖资源解析等基础设施细节。
    /// </remarks>
    public sealed class StageSessionFactory : IStageSessionFactory
    {
        private readonly IStageWorldBuilder _worldBuilder;

        /// <summary>创建会话工厂。</summary>
        /// <param name="worldBuilder">本地物理世界构建器; 不能为空。</param>
        /// <exception cref="ArgumentNullException"><paramref name="worldBuilder"/> 为 null 时抛出。</exception>
        public StageSessionFactory(IStageWorldBuilder worldBuilder)
        {
            _worldBuilder = worldBuilder ?? throw new ArgumentNullException(nameof(worldBuilder));
        }

        /// <summary>创建未加载状态的会话。</summary>
        /// <param name="definition">关卡定义; 不能为空, 且必须包含非空 LevelId。</param>
        /// <returns>处于 <see cref="StageSessionState.Unloaded"/> 状态的会话。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="definition"/> 为 null 时抛出。</exception>
        /// <exception cref="ArgumentException">定义的 LevelId 为空或空白时抛出。</exception>
        public IStageSession Create(LevelDefinition definition) => new StageSession(definition, _worldBuilder);
    }
}
