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
    /// </remarks>
    public sealed class StageWorld : IStageWorld
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
        /// <remarks>判断世界是否仍可用请用本属性的 <c>IsValid()</c>, 不要用 <see cref="PhysicsScene"/>。</remarks>
        public Scene Scene => _scene;

        /// <summary>
        /// 本世界的独立物理 Scene; 已释放时返回零值句柄。
        /// </summary>
        /// <remarks>
        /// 已实测: <c>default(PhysicsScene2D).IsValid()</c> 返回 <c>true</c>,
        /// 因此该句柄的有效性不能作为"世界已释放"的判据, 必须改用 <see cref="Scene"/>。
        /// </remarks>
        public PhysicsScene2D PhysicsScene => _scene.IsValid() ? _scene.GetPhysicsScene2D() : default;

        /// <summary>已生成对象列表; 按对象稳定 ID 序数升序。</summary>
        public IReadOnlyList<StageWorldObject> Objects => _objects;

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
        /// 逐个销毁反而会与卸载流程竞争。卸载是异步的, 调用后到实际释放之间有若干帧;
        /// 期间 <see cref="Scene"/> 仍返回原句柄, 因此调用方不应在释放后继续使用本实例。
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
