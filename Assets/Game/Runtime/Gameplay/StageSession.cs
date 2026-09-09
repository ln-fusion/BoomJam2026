using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Contracts.Gameplay;
using Game.Foundation;

namespace Game.Gameplay
{
    /// <summary>
    /// 单次关卡会话的最小状态机实现。
    /// </summary>
    /// <remarks>
    /// C19 只实现 Unloaded → Loading → Deploying 的加载路径; 部署/模拟命令在
    /// C23/C24 落地前一律返回明确的"尚未实现"错误, 不冒充完成。
    /// </remarks>
    public sealed class StageSession : IStageSession
    {
        private static readonly IReadOnlyList<AbilityPlacementData> EmptyPlacements =
            new List<AbilityPlacementData>().AsReadOnly();

        /// <summary>当前会话状态; 初始为 Unloaded。</summary>
        public StageSessionState State { get; private set; } = StageSessionState.Unloaded;

        /// <summary>本会话对应的关卡稳定标识。</summary>
        public LevelId LevelId { get; }

        /// <summary>当前部署方案快照; 未开始部署时为空方案。</summary>
        public DeploymentPlanSnapshot Deployment { get; private set; }

        /// <summary>创建关卡会话。</summary>
        /// <param name="definition">关卡定义; 不能为空且必须包含非空 LevelId。</param>
        /// <exception cref="ArgumentNullException">定义为 null 时抛出。</exception>
        /// <exception cref="ArgumentException">定义的 LevelId 为空或空白时抛出。</exception>
        public StageSession(LevelDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (string.IsNullOrWhiteSpace(definition.LevelId))
                throw new ArgumentException("The level definition requires a non-empty LevelId.", nameof(definition));
            LevelId = new LevelId(definition.LevelId);
            Deployment = new DeploymentPlanSnapshot(EmptyPlacements, 0, definition.CapacityLimit);
        }

        /// <summary>供工厂快捷创建会话; 约定与构造器一致。</summary>
        /// <param name="definition">关卡定义。</param>
        /// <returns>处于 Unloaded 状态的会话。</returns>
        public static IStageSession Create(LevelDefinition definition) => new StageSession(definition);

        /// <summary>加载关卡数据, 使状态从 Unloaded 进入 Deploying。</summary>
        /// <returns>加载结果; 已在 Deploying 时返回 OperationNotAllowed。</returns>
        public Result Load()
        {
            if (State != StageSessionState.Unloaded)
            {
                return Result.Failure(ErrorCode.OperationNotAllowed, "Load is only allowed from the Unloaded state.");
            }
            State = StageSessionState.Loading;
            // C19 最小实现: 定义的加载为同步赋值, 不进行异步 IO。
            // C21 接入 IStageWorldBuilder 后, 这里负责构建本地物理世界。
            State = StageSessionState.Deploying;
            return Result.Success();
        }

        /// <summary>放置能力框; C23 落地前返回"尚未实现"。</summary>
        /// <param name="command">放置命令。</param>
        /// <returns>统一返回 OperationNotAllowed。</returns>
        public Result<PlacementId> PlaceAbility(PlaceAbilityCommand command)
        {
            _ = command;
            return Result<PlacementId>.Failure(
                ErrorCode.OperationNotAllowed,
                "PlaceAbility is not implemented before C23."
            );
        }

        /// <summary>移动能力框; C23 落地前返回"尚未实现"。</summary>
        /// <param name="command">移动命令。</param>
        /// <returns>统一返回 OperationNotAllowed。</returns>
        public Result MoveAbility(MoveAbilityCommand command)
        {
            _ = command;
            return Result.Failure(ErrorCode.OperationNotAllowed, "MoveAbility is not implemented before C23.");
        }

        /// <summary>移除能力框; C23 落地前返回"尚未实现"。</summary>
        /// <param name="placementId">放置稳定标识。</param>
        /// <returns>统一返回 OperationNotAllowed。</returns>
        public Result RemoveAbility(PlacementId placementId)
        {
            _ = placementId;
            return Result.Failure(ErrorCode.OperationNotAllowed, "RemoveAbility is not implemented before C23.");
        }

        /// <summary>清空部署方案; C23 落地前返回"尚未实现"。</summary>
        /// <returns>统一返回 OperationNotAllowed。</returns>
        public Result ClearDeployment()
        {
            return Result.Failure(ErrorCode.OperationNotAllowed, "ClearDeployment is not implemented before C23.");
        }

        /// <summary>开始模拟; C24 落地前返回"尚未实现"。</summary>
        /// <returns>携带 SessionLocked 的失败结果。</returns>
        public StartSimulationResult StartSimulation()
        {
            return StartSimulationResult.Failure(
                PlacementError.SessionLocked,
                "StartSimulation is not implemented before C24."
            );
        }

        /// <summary>主动停止模拟; C24 落地前返回"尚未实现"。</summary>
        /// <returns>统一返回 OperationNotAllowed。</returns>
        public Result StopSimulation()
        {
            return Result.Failure(ErrorCode.OperationNotAllowed, "StopSimulation is not implemented before C24.");
        }
    }
}
