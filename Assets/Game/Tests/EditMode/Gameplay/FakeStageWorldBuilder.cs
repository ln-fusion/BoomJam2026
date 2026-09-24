using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Contracts.Gameplay;
using Game.Foundation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// 会话测试用的世界构建器替身: 不创建任何 Scene, 只记账构建与释放次数。
    /// </summary>
    /// <remarks>
    /// 状态机与部署规则的验证不应该启动 Unity 本地物理 Scene, 否则用例会依赖帧与场景卸载时序;
    /// 真实世界的创建与释放由 PlayMode 的会话测试覆盖。
    /// </remarks>
    internal sealed class FakeStageWorldBuilder : IStageWorldBuilder
    {
        private int _buildCount;
        private int _disposeCount;

        /// <summary>物理替身; 由本构建器统一持有, 使用例不必再从世界实例上取。</summary>
        internal FakeSimulationPhysics Physics { get; } = new FakeSimulationPhysics();

        /// <summary>成功构建出的世界数量。</summary>
        internal int BuildCount => _buildCount;

        /// <summary>已释放的世界数量。</summary>
        internal int DisposeCount => _disposeCount;

        /// <summary>尚未释放的世界数量; 正常流程下应恒为 0 或 1。</summary>
        internal int LiveWorldCount => _buildCount - _disposeCount;

        /// <summary>构建失败时返回的错误码; 默认 <see cref="ErrorCode.None"/> 表示构建总是成功。</summary>
        internal ErrorCode FailureCode { get; set; } = ErrorCode.None;

        /// <summary>构建失败时返回的日志消息。</summary>
        internal string FailureMessage { get; set; } = "Fake world build failure.";

        /// <summary>按配置构建世界或返回失败; 失败时不改变任何计数。</summary>
        /// <param name="definition">关卡定义。</param>
        /// <returns>成功时返回不持有任何 Scene 的世界替身。</returns>
        public Result<IStageWorld> Build(LevelDefinition definition)
        {
            if (FailureCode != ErrorCode.None)
                return Result<IStageWorld>.Failure(FailureCode, FailureMessage);

            _buildCount++;
            return Result<IStageWorld>.Success(new FakeStageWorld(this, Physics));
        }

        /// <summary>记录一次世界释放; 由 <see cref="FakeStageWorld"/> 回调。</summary>
        internal void NotifyDisposed() => _disposeCount++;
    }

    /// <summary>
    /// 本地物理世界的替身: 场景句柄均为零值, 对象集合恒为空。
    /// </summary>
    internal sealed class FakeStageWorld : IStageWorld
    {
        private readonly FakeStageWorldBuilder _owner;
        private readonly ISimulationPhysics _physics;
        private bool _disposed;

        /// <summary>创建世界替身。</summary>
        /// <param name="owner">记账用的构建器。</param>
        /// <param name="physics">本轮运行使用的物理替身, 由构建器统一持有以便用例断言。</param>
        internal FakeStageWorld(FakeStageWorldBuilder owner, ISimulationPhysics physics)
        {
            _owner = owner;
            _physics = physics;
        }

        /// <summary>零值 Scene 句柄; 世界替身不承载真实场景。</summary>
        public Scene Scene => default;

        /// <summary>本地物理替身; 只记账步进次数与实际步长。</summary>
        /// <remarks>替身不拒绝步进: 世界替身的 Scene 是零值但不存在真实的默认场景风险。</remarks>
        public ISimulationPhysics Physics => _physics;

        /// <summary>恒为空的已生成对象集合。</summary>
        public IReadOnlyList<StageWorldObject> Objects => Array.Empty<StageWorldObject>();

        /// <summary>世界替身不含任何实体, 查询恒失败。</summary>
        /// <param name="entityId">实体稳定标识。</param>
        /// <param name="worldObject">输出参数; 恒为 default。</param>
        /// <returns>恒返回 false。</returns>
        public bool TryGetObject(EntityId entityId, out StageWorldObject worldObject)
        {
            worldObject = default;
            return false;
        }

        /// <summary>释放世界; 幂等, 只在首次调用时计入一次释放。</summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _owner.NotifyDisposed();
        }
    }
}
