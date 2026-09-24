using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Contracts.Content;
using Game.Contracts.Gameplay;
using Game.Foundation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Gameplay.Stage
{
    /// <summary>
    /// 按关卡定义构建本地物理世界的默认实现。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 构建分两阶段（技术设计文档 §6.5）: 先规划——校验稳定 ID 并解析全部预制体资源,
    /// 再创建——建立本地物理 Scene 并按序实例化。规划阶段完成全部可能失败的检查,
    /// 因此创建阶段没有失败分支, 也不需要回滚已创建的部分世界。
    /// </para>
    /// <para>
    /// 对象按稳定对象 ID 的序数升序创建, 使同一关卡的生成顺序在重跑时一致;
    /// 静态与动态的区分由预制体自身是否携带 <c>Rigidbody2D</c> 决定, 关卡数据不重复声明。
    /// </para>
    /// </remarks>
    public sealed class StageWorldBuilder : IStageWorldBuilder
    {
        /// <summary>
        /// 本地物理 Scene 的名称前缀; 实际名称附加递增序号。
        /// </summary>
        /// <remarks>
        /// 名称必须唯一: <c>SceneManager.CreateScene</c> 在存在同名场景时直接抛异常,
        /// 而 <c>Dispose</c> 走异步卸载, 前一场景可能尚未从列表中移除。
        /// 固定名称会让"停止后立即重新开始"以及连续重建失败, 因此按序号区分。
        /// </remarks>
        public const string SceneNamePrefix = "StageWorld";

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static int _sceneSequence;

        private readonly IAssetResolver _assets;

        /// <summary>创建世界构建器。</summary>
        /// <param name="assets">预制体资源解析器; 未知稳定 ID 视为内容错误。</param>
        /// <exception cref="ArgumentNullException">资源解析器为空时抛出。</exception>
        public StageWorldBuilder(IAssetResolver assets)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        }

        /// <summary>按关卡定义构建世界; 失败时不创建任何 Scene 或对象。</summary>
        /// <param name="definition">关卡定义。</param>
        /// <returns>
        /// 成功时返回可用的世界; 失败时返回 <see cref="ErrorCode.InvalidArgument"/>
        /// 或 <see cref="ErrorCode.NotFound"/>。
        /// </returns>
        public Result<IStageWorld> Build(LevelDefinition definition)
        {
            Result<List<PlannedObject>> plan = Plan(definition);
            return plan.IsSuccess ? Create(plan.Value) : Result<IStageWorld>.Failure(plan.ErrorCode, plan.Message);
        }

        /// <summary>
        /// 规划阶段: 校验定义、解析预制体并按稳定顺序排列待创建对象。
        /// </summary>
        /// <param name="definition">关卡定义。</param>
        /// <returns>成功时返回按创建顺序排列的待创建对象列表。</returns>
        private Result<List<PlannedObject>> Plan(LevelDefinition definition)
        {
            if (definition == null)
                return Result<List<PlannedObject>>.Failure(
                    ErrorCode.InvalidArgument,
                    "A level definition is required to build a stage world."
                );
            if (string.IsNullOrWhiteSpace(definition.LevelId))
                return Result<List<PlannedObject>>.Failure(
                    ErrorCode.InvalidArgument,
                    "The level definition requires a non-empty LevelId."
                );

            List<StageObjectData> objects = definition.Objects ?? new List<StageObjectData>();
            var ordered = new List<StageObjectData>(objects.Count);
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (StageObjectData data in objects)
            {
                Result<StageObjectData> idCheck = ValidateObjectId(data, seenIds);
                if (!idCheck.IsSuccess)
                    return Result<List<PlannedObject>>.Failure(idCheck.ErrorCode, idCheck.Message);
                ordered.Add(idCheck.Value);
            }
            // 稳定创建顺序: 对象稳定 ID 序数升序, 不依赖关卡数据中的声明顺序。
            ordered.Sort((left, right) => string.CompareOrdinal(left.ObjectId, right.ObjectId));

            var plan = new List<PlannedObject>(ordered.Count);
            foreach (StageObjectData data in ordered)
            {
                GameObject prefab = _assets.GetPrefab(new PrefabId(data.PrefabId));
                if (prefab == null)
                    return Result<List<PlannedObject>>.Failure(
                        ErrorCode.NotFound,
                        $"No prefab is registered for PrefabId '{data.PrefabId}' (object '{data.ObjectId}')."
                    );
                plan.Add(new PlannedObject(data, prefab));
            }
            return Result<List<PlannedObject>>.Success(plan);
        }

        /// <summary>校验单个对象的稳定 ID: 必须非空、无首尾空白且在关卡内唯一。</summary>
        /// <param name="data">待校验的对象数据。</param>
        /// <param name="seenIds">已出现的对象稳定 ID 集合, 命中即判重复。</param>
        /// <returns>成功时返回原对象数据。</returns>
        private static Result<StageObjectData> ValidateObjectId(StageObjectData data, HashSet<string> seenIds)
        {
            if (data == null)
                return Result<StageObjectData>.Failure(
                    ErrorCode.InvalidArgument,
                    "The level definition contains a null stage object entry."
                );
            string objectId = data.ObjectId;
            // 强类型 ID 不接受空值与首尾空白, 此处提前拦截以返回 Result 而非抛异常。
            if (
                string.IsNullOrWhiteSpace(objectId)
                || !string.Equals(objectId, objectId.Trim(), StringComparison.Ordinal)
            )
                return Result<StageObjectData>.Failure(
                    ErrorCode.InvalidArgument,
                    "Every stage object requires a stable ObjectId without surrounding whitespace."
                );
            if (string.IsNullOrWhiteSpace(data.PrefabId))
                return Result<StageObjectData>.Failure(
                    ErrorCode.InvalidArgument,
                    $"Stage object '{objectId}' requires a non-empty PrefabId."
                );
            if (!seenIds.Add(objectId))
                return Result<StageObjectData>.Failure(
                    ErrorCode.InvalidArgument,
                    $"Duplicate stage object ObjectId '{objectId}'."
                );
            return Result<StageObjectData>.Success(data);
        }

        /// <summary>创建阶段: 建立本地物理 Scene 并实例化全部已规划对象。</summary>
        /// <param name="plan">规划阶段产出的待创建对象列表。</param>
        /// <returns>成功时返回世界实例。</returns>
        private static Result<IStageWorld> Create(List<PlannedObject> plan)
        {
            int sequence = System.Threading.Interlocked.Increment(ref _sceneSequence);
            Scene scene = SceneManager.CreateScene(
                SceneNamePrefix + "_" + sequence.ToString(Invariant),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D)
            );
            if (!scene.IsValid())
                return Result<IStageWorld>.Failure(
                    ErrorCode.SceneLoadFailed,
                    "Failed to create the local physics scene for the stage world."
                );

            var created = new List<StageWorldObject>(plan.Count);
            foreach (PlannedObject planned in plan)
            {
                StageObjectData data = planned.Data;
                GameObject instance = UnityEngine.Object.Instantiate(planned.Prefab);
                instance.name = data.ObjectId;
                // 实例化后默认落在上一个活动 Scene, 必须显式移入本地物理 Scene,
                // 否则对象会留在调用方场景且不参与本地物理步进。
                SceneManager.MoveGameObjectToScene(instance, scene);
                instance.transform.position = new Vector3(data.PositionX, data.PositionY, 0f);
                instance.transform.rotation = Quaternion.Euler(0f, 0f, data.RotationZ);
                instance.transform.localScale = new Vector3(data.ScaleX, data.ScaleY, 1f);
                created.Add(new StageWorldObject(new EntityId(data.ObjectId), data.PrefabId, instance));
            }
            return Result<IStageWorld>.Success(new StageWorld(scene, created));
        }

        /// <summary>规划阶段产出的一项待创建对象: 关卡数据与已解析的预制体。</summary>
        private sealed class PlannedObject
        {
            /// <summary>对应的关卡对象数据。</summary>
            internal readonly StageObjectData Data;

            /// <summary>已解析的预制体资源; 保证非空。</summary>
            internal readonly GameObject Prefab;

            /// <summary>创建待创建项。</summary>
            /// <param name="data">关卡对象数据。</param>
            /// <param name="prefab">已解析的预制体。</param>
            internal PlannedObject(StageObjectData data, GameObject prefab)
            {
                Data = data;
                Prefab = prefab;
            }
        }
    }
}
