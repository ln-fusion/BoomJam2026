using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Foundation;
using UnityEngine;

namespace Game.Contracts.Gameplay
{
    /// <summary>
    /// 单次关卡会话状态; 合法迁移见技术设计文档 6.2。
    /// </summary>
    public enum StageSessionState
    {
        /// <summary>会话已创建但未加载关卡数据。</summary>
        Unloaded,

        /// <summary>正在加载关卡定义。</summary>
        Loading,

        /// <summary>接受部署命令（放置/移动/删除/清空/开始）。</summary>
        Deploying,

        /// <summary>开始前执行权威校验并冻结部署方案。</summary>
        Validating,

        /// <summary>固定 Tick 物理模拟进行中。</summary>
        Simulating,

        /// <summary>结果解析（成功/失败/主动停止）。</summary>
        Resolving,

        /// <summary>从模拟结果恢复部署阶段预览。</summary>
        Restoring,

        /// <summary>关卡成功完成。</summary>
        Succeeded,

        /// <summary>关卡失败。</summary>
        Failed,

        /// <summary>会话退出。</summary>
        Exiting,
    }

    /// <summary>
    /// 部署命令失败原因枚举; 与设计文档 6.4 一致。
    /// </summary>
    public enum PlacementError
    {
        /// <summary>无错误。</summary>
        None,

        /// <summary>当前状态不接受部署命令。</summary>
        SessionLocked,

        /// <summary>能力不在关卡白名单内。</summary>
        AbilityNotAllowed,

        /// <summary>尺寸选项不存在。</summary>
        UnknownSize,

        /// <summary>位置超出可部署区域。</summary>
        OutsideDeployableArea,

        /// <summary>与禁放区相交。</summary>
        OverlapsForbiddenArea,

        /// <summary>数值非法（NaN/Inf/非正尺寸）。</summary>
        InvalidNumber,

        /// <summary>超出关卡容量上限。</summary>
        CapacityExceeded,
    }

    /// <summary>
    /// 放置一个能力框的命令。
    /// </summary>
    public readonly struct PlaceAbilityCommand
    {
        /// <summary>能力类型稳定标识。</summary>
        public readonly AbilityTypeId AbilityTypeId;

        /// <summary>尺寸选项稳定标识。</summary>
        public readonly AbilitySizeId SizeId;

        /// <summary>能力框世界坐标（中心点）。</summary>
        public readonly Vector2 WorldPosition;

        /// <summary>创建放置命令。</summary>
        /// <param name="abilityTypeId">能力类型稳定标识。</param>
        /// <param name="sizeId">尺寸选项稳定标识。</param>
        /// <param name="worldPosition">能力框世界坐标。</param>
        public PlaceAbilityCommand(AbilityTypeId abilityTypeId, AbilitySizeId sizeId, Vector2 worldPosition)
        {
            AbilityTypeId = abilityTypeId;
            SizeId = sizeId;
            WorldPosition = worldPosition;
        }
    }

    /// <summary>
    /// 移动一个能力框的命令; 尺寸保留时可保持原尺寸。
    /// </summary>
    public readonly struct MoveAbilityCommand
    {
        /// <summary>被移动的能力框稳定标识。</summary>
        public readonly PlacementId PlacementId;

        /// <summary>新的尺寸选项稳定标识; 为空表示保持原尺寸。</summary>
        public readonly AbilitySizeId SizeId;

        /// <summary>新的世界坐标（中心点）。</summary>
        public readonly Vector2 WorldPosition;

        /// <summary>创建移动命令。</summary>
        /// <param name="placementId">被移动的能力框稳定标识。</param>
        /// <param name="sizeId">新尺寸选项; 可为空。</param>
        /// <param name="worldPosition">新世界坐标。</param>
        public MoveAbilityCommand(PlacementId placementId, AbilitySizeId sizeId, Vector2 worldPosition)
        {
            PlacementId = placementId;
            SizeId = sizeId;
            WorldPosition = worldPosition;
        }
    }

    /// <summary>
    /// 回合部署方案快照; 放置集合与容量累计。
    /// </summary>
    public sealed class DeploymentPlanSnapshot
    {
        /// <summary>当前方案中的放置集合。</summary>
        public IReadOnlyList<AbilityPlacementData> Placements { get; }

        /// <summary>累计容量费用。</summary>
        public int TotalCapacity { get; }

        /// <summary>关卡容量上限。</summary>
        public int CapacityLimit { get; }

        /// <summary>创建部署方案快照。</summary>
        /// <param name="placements">放置集合。</param>
        /// <param name="totalCapacity">累计容量费用。</param>
        /// <param name="capacityLimit">关卡容量上限。</param>
        public DeploymentPlanSnapshot(
            IReadOnlyList<AbilityPlacementData> placements,
            int totalCapacity,
            int capacityLimit
        )
        {
            Placements = placements ?? new List<AbilityPlacementData>().AsReadOnly();
            TotalCapacity = totalCapacity;
            CapacityLimit = capacityLimit;
        }
    }

    /// <summary>
    /// 部署方案中的单个放置项; 序列化时可保存到本地。
    /// </summary>
    [Serializable]
    public sealed class AbilityPlacementData
    {
        /// <summary>放置稳定标识。</summary>
        public string PlacementId;

        /// <summary>能力类型稳定标识。</summary>
        public string AbilityTypeId;

        /// <summary>尺寸选项稳定标识。</summary>
        public string SizeOptionId;

        /// <summary>世界 X 坐标。</summary>
        public float PositionX;

        /// <summary>世界 Y 坐标。</summary>
        public float PositionY;
    }

    /// <summary>
    /// 开始模拟的结果; 携带是否通过权威校验与错误原因。
    /// </summary>
    /// <remarks>
    /// C19 只提供最小字段, 校验细节与恢复语义由 C23/C24 扩展。
    /// </remarks>
    public readonly struct StartSimulationResult
    {
        /// <summary>是否成功进入模拟状态。</summary>
        public bool IsSuccess { get; }

        /// <summary>失败原因; 成功时为 <see cref="PlacementError.None"/>。</summary>
        public PlacementError Error { get; }

        /// <summary>日志用错误消息。</summary>
        public string Message { get; }

        /// <summary>创建开始模拟结果。</summary>
        /// <param name="isSuccess">是否成功。</param>
        /// <param name="error">失败原因。</param>
        /// <param name="message">日志用错误消息。</param>
        public StartSimulationResult(bool isSuccess, PlacementError error, string message = null)
        {
            IsSuccess = isSuccess;
            Error = error;
            Message = message ?? string.Empty;
        }

        /// <summary>创建成功结果。</summary>
        /// <returns>成功的开始模拟结果。</returns>
        public static StartSimulationResult Success() => new StartSimulationResult(true, PlacementError.None);

        /// <summary>创建失败结果。</summary>
        /// <param name="error">失败原因。</param>
        /// <param name="message">日志用错误消息。</param>
        /// <returns>失败的开始模拟结果。</returns>
        public static StartSimulationResult Failure(PlacementError error, string message = null) =>
            new StartSimulationResult(false, error, message);
    }

    /// <summary>
    /// 关卡会话工厂; 按关卡定义创建未加载状态的会话。
    /// </summary>
    public interface IStageSessionFactory
    {
        /// <summary>创建未加载状态的会话。</summary>
        /// <param name="definition">关卡定义; 不能为空。</param>
        /// <returns>处于 <see cref="StageSessionState.Unloaded"/> 状态的会话。</returns>
        IStageSession Create(LevelDefinition definition);
    }

    /// <summary>
    /// 单次关卡会话的命令入口与状态机。
    /// </summary>
    /// <remarks>
    /// 每个命令都是同步调用, 返回失败携带明确错误码; 部署命令只在
    /// <see cref="StageSessionState.Deploying"/> 状态接受。
    /// </remarks>
    public interface IStageSession
    {
        /// <summary>当前会话状态。</summary>
        StageSessionState State { get; }

        /// <summary>本会话对应的关卡稳定标识。</summary>
        LevelId LevelId { get; }

        /// <summary>当前部署方案快照; 未开始部署时为空方案。</summary>
        DeploymentPlanSnapshot Deployment { get; }

        /// <summary>加载关卡数据, 使状态从 Unloaded 进入 Deploying。</summary>
        /// <returns>加载结果; 失败时返回错误码。</returns>
        Result Load();

        /// <summary>在部署区放置一个能力框。</summary>
        /// <param name="command">放置命令。</param>
        /// <returns>放置结果, 成功时携带放置稳定标识。</returns>
        Result<PlacementId> PlaceAbility(PlaceAbilityCommand command);

        /// <summary>移动一个已放置的能力框。</summary>
        /// <param name="command">移动命令。</param>
        /// <returns>移动结果。</returns>
        Result MoveAbility(MoveAbilityCommand command);

        /// <summary>移除一个已放置的能力框。</summary>
        /// <param name="placementId">放置稳定标识。</param>
        /// <returns>移除结果。</returns>
        Result RemoveAbility(PlacementId placementId);

        /// <summary>清空当前部署方案。</summary>
        /// <returns>清空结果。</returns>
        Result ClearDeployment();

        /// <summary>开始模拟; 先执行权威校验并冻结部署方案。</summary>
        /// <returns>开始结果（结构细节待 C24 定稿）。</returns>
        StartSimulationResult StartSimulation();

        /// <summary>主动停止模拟并恢复部署阶段。</summary>
        /// <returns>停止结果。</returns>
        Result StopSimulation();
    }
}
