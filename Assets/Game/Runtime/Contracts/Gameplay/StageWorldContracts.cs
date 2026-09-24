using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Foundation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Contracts.Gameplay
{
    /// <summary>
    /// 世界中单个已生成对象的稳定引用; 由世界构建阶段产出, 供物理与条件系统按实体查询。
    /// </summary>
    /// <remarks>
    /// 实体标识直接取自关卡数据的对象稳定 ID, 因此同一关卡重复构建得到相同的标识集合,
    /// 可安全作为字典键与排序依据（技术设计文档 §6.7）。
    /// </remarks>
    public readonly struct StageWorldObject
    {
        /// <summary>实体稳定标识; 取自关卡数据的对象稳定 ID。</summary>
        public readonly EntityId EntityId;

        /// <summary>该对象使用的预制体稳定标识。</summary>
        public readonly string PrefabId;

        /// <summary>生成出的 GameObject 实例; 生命周期由所属世界管理。</summary>
        public readonly GameObject Instance;

        /// <summary>创建世界对象引用。</summary>
        /// <param name="entityId">实体稳定标识。</param>
        /// <param name="prefabId">预制体稳定标识。</param>
        /// <param name="instance">生成出的实例。</param>
        public StageWorldObject(EntityId entityId, string prefabId, GameObject instance)
        {
            EntityId = entityId;
            PrefabId = prefabId;
            Instance = instance;
        }
    }

    /// <summary>
    /// 一次模拟运行的本地物理世界; 持有创建出的对象与独立物理 Scene。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 世界不是关卡真相来源。每次开始模拟都按不可变的关卡定义与部署方案重新构建,
    /// 失败或停止时整体释放, 不做局部复位（技术设计文档 §6.5）。
    /// </para>
    /// <para>
    /// 物理 Scene 使用 <see cref="LocalPhysicsMode.Physics2D"/>, 不会被 Unity 自动推进,
    /// 只能由 <see cref="PhysicsScene2D.Simulate"/> 显式步进（技术设计文档 §6.6）。
    /// </para>
    /// </remarks>
    public interface IStageWorld : IDisposable
    {
        /// <summary>承载本世界的本地 Scene 句柄; 已释放时 <c>IsValid()</c> 返回 false。</summary>
        /// <remarks>这是判断世界是否仍可用的可靠判据。</remarks>
        Scene Scene { get; }

        /// <summary>
        /// 本世界的独立物理 Scene; 与全局物理互不影响。
        /// </summary>
        /// <remarks>
        /// 世界释放后本属性返回零值句柄, 但 <c>PhysicsScene2D.IsValid()</c> 对零值仍返回 true,
        /// 因此不要用它判断世界是否已释放, 应改用 <see cref="Scene"/>。
        /// </remarks>
        PhysicsScene2D PhysicsScene { get; }

        /// <summary>
        /// 已生成对象列表; 顺序为稳定的创建顺序（按对象稳定 ID 序数升序）。
        /// </summary>
        IReadOnlyList<StageWorldObject> Objects { get; }

        /// <summary>按实体标识查询已生成对象。</summary>
        /// <param name="entityId">实体稳定标识。</param>
        /// <param name="worldObject">查找到的对象引用; 未命中时为 default。</param>
        /// <returns>存在该实体时返回 true。</returns>
        bool TryGetObject(EntityId entityId, out StageWorldObject worldObject);
    }

    /// <summary>
    /// 按关卡定义创建本地物理世界。
    /// </summary>
    /// <remarks>
    /// 调用方为 <c>StageSession</c>; 实现位于 Gameplay 模块。编辑器预览不经过本接口,
    /// 它直接读取 Authoring 数据绘制, 不创建物理世界（技术设计文档 §5.5）。
    /// </remarks>
    public interface IStageWorldBuilder
    {
        /// <summary>按关卡定义构建世界; 失败时不留下任何已创建对象。</summary>
        /// <param name="definition">关卡定义; 必须包含有效的 LevelId 与对象稳定 ID。</param>
        /// <returns>
        /// 成功时返回可用的世界, 由调用方负责释放;
        /// 失败时返回 <see cref="ErrorCode.InvalidArgument"/>（定义或稳定 ID 非法）
        /// 或 <see cref="ErrorCode.NotFound"/>（引用的预制体资源不存在）。
        /// </returns>
        Result<IStageWorld> Build(LevelDefinition definition);
    }
}
