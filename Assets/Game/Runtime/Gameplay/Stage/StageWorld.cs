using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Contracts.Gameplay;
using Game.Foundation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Gameplay.Stage
{
    /// <summary>
    /// 本地物理世界的默认实现; 持有独立 Scene 句柄与按稳定 ID 索引的对象表。
    /// </summary>
    /// <remarks>
    /// 构造由 <see cref="StageWorldBuilder"/> 完成, 不对外公开: 世界一旦建立即不可增删对象,
    /// 保证 <see cref="Objects"/> 在生命周期内是稳定集合, 调用方可安全缓存顺序。
    /// 世界自身实现 <see cref="ISimulationPhysics"/>, 使本地物理句柄不外流;
    /// 见 <see cref="Physics"/> 的说明。
    /// </remarks>
    public sealed class StageWorld : IStageWorld, ISimulationPhysics
    {
        private readonly List<StageWorldObject> _objects;
        private readonly Dictionary<EntityId, StageWorldObject> _byEntityId;
        private Scene _scene;
        private bool _disposed;

        /// <summary>创建世界实例; 仅供构建器调用。</summary>
        /// <param name="scene">承载本世界的本地 Scene。</param>
        /// <param name="objects">按稳定创建顺序排列的对象列表。</param>
        /// <exception cref="ArgumentNullException">对象列表为空时抛出。</exception>
        internal StageWorld(Scene scene, List<StageWorldObject> objects)
        {
            _scene = scene;
            _objects = objects ?? throw new ArgumentNullException(nameof(objects));
            _byEntityId = new Dictionary<EntityId, StageWorldObject>(_objects.Count);
            foreach (StageWorldObject worldObject in _objects)
                _byEntityId[worldObject.EntityId] = worldObject;
        }

        /// <summary>承载本世界的本地 Scene 句柄; 已释放时返回无效句柄。</summary>
        /// <remarks>
        /// 本属性的 <c>IsValid()</c> 是世界生命周期的唯一可靠判据;
        /// 零值 <c>PhysicsScene2D</c> 句柄的 <c>IsValid()</c> 返回 true, 不能用作判据。
        /// </remarks>
        public Scene Scene => _scene;

        /// <summary>本世界的本地物理步进入口; 世界自身即入口, 不额外分配对象。</summary>
        /// <remarks>
        /// 以 <see cref="Scene"/> 的有效性作为唯一闸门: 已释放世界的承载 Scene 立即失效,
        /// 于是步进被拒绕而不是落到零值句柄所指的默认场景上（技术设计文档 §6.6）。
        /// </remarks>
        public ISimulationPhysics Physics => this;

        /// <summary>已生成对象列表; 按对象稳定 ID 序数升序。</summary>
        public IReadOnlyList<StageWorldObject> Objects => _objects;

        /// <summary>按固定步长推进本世界的本地物理。</summary>
        /// <param name="fixedDeltaSeconds">本次步长, 单位为秒。</param>
        /// <returns>推进成功返回 true; 本世界已释放时返回 false 且不做任何事。</returns>
        public bool Simulate(double fixedDeltaSeconds)
        {
            if (!_scene.IsValid())
                return false;
            return _scene.GetPhysicsScene2D().Simulate((float)fixedDeltaSeconds);
        }

        /// <summary>按实体标识查询已生成对象。</summary>
        /// <param name="entityId">实体稳定标识; 为 null 时返回 false。</param>
        /// <param name="worldObject">查找到的对象引用; 未命中时为 default。</param>
        /// <returns>存在该实体时返回 true。</returns>
        public bool TryGetObject(EntityId entityId, out StageWorldObject worldObject)
        {
            if (entityId == null)
            {
                worldObject = default;
                return false;
            }
            return _byEntityId.TryGetValue(entityId, out worldObject);
        }

        /// <summary>
        /// 释放整个本地世界; 幂等, 重复调用无副作用。
        /// </summary>
        /// <remarks>
        /// 只卸载 Scene, 不逐个 <c>Destroy</c> 场景内对象——对象随场景卸载一并销毁,
        /// 逐个销毁反而会与卸载流程竞争。
        /// 本方法返回时 <see cref="Scene"/> 立即变为无效句柄, 但底层卸载是异步的:
        /// 场景与其对象仍会存续若干帧（实测在连续重建时能同时观察到多个已释放世界）。
        /// 因此调用方既不应在释放后继续使用本实例, 也不应假设场景内对象已立刻消失。
        /// </remarks>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _objects.Clear();
            _byEntityId.Clear();
            if (!_scene.IsValid())
            {
                _scene = default;
                return;
            }
            SceneManager.UnloadSceneAsync(_scene);
            _scene = default;
        }
    }
}
