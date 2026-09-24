using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Contracts.Gameplay;
using Game.Foundation;
using Game.Gameplay.Deployment;

namespace Game.Gameplay
{
    /// <summary>
    /// 单次关卡会话的状态机与命令入口。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 状态与合法迁移见技术设计文档 §6.2。部署命令只在 <see cref="StageSessionState.Deploying"/>
    /// 被接受; 其余状态一律返回 <see cref="PlacementError.SessionLocked"/> 而不是静默忽略,
    /// 使界面能据此禁用控件并给出反馈。
    /// </para>
    /// <para>
    /// 世界生命周期遵循 §6.5: 每次开始模拟都按不可变关卡定义重新构建本地物理世界,
    /// 停止时整体释放并重用原部署方案; 不做"把 Transform 与速度写回初值"的局部复位,
    /// 因为物理内部还持有接触对、关节与休眠状态, 局部复位会留下残留。
    /// </para>
    /// <para>
    /// 当前阶段（C24）尚未引入固定 Tick 循环: 进入 <see cref="StageSessionState.Simulating"/>
    /// 后世界不会被步进, 也没有成功/失败判定。恒定步长推进、条件判定与结算属于 C25/C26/C27。
    /// </para>
    /// <para>
    /// 会话在 <see cref="StageSessionState.Simulating"/> 期间持有本地物理 Scene,
    /// 唯一释放路径是 <see cref="StopSimulation"/>。会话被直接丢弃时该 Scene 不会被卸载,
    /// 因此调用方必须在离开玩法流程前停止模拟; 会话的所有权与兜底释放由关卡流程（C31/C32）确定。
    /// </para>
    /// </remarks>
    public sealed class StageSession : IStageSession
    {
        private readonly LevelDefinition _definition;
        private readonly IStageWorldBuilder _worldBuilder;
        private readonly AbilityWhitelistError _contentError;
        private readonly string _contentErrorMessage;
        private readonly PlacementService _placement;
        private readonly DeploymentPlanSnapshot _emptyDeployment;
        private IStageWorld _world;

        /// <summary>当前会话状态; 初始为 Unloaded。</summary>
        public StageSessionState State { get; private set; } = StageSessionState.Unloaded;

        /// <summary>本会话对应的关卡稳定标识。</summary>
        public LevelId LevelId { get; }

        /// <summary>
        /// 当前部署方案快照; 尚未加载或关卡内容非法时为空方案。
        /// </summary>
        /// <remarks>每次读取都返回独立快照, 调用方修改返回值不会影响会话内的方案。</remarks>
        public DeploymentPlanSnapshot Deployment => _placement == null ? _emptyDeployment : _placement.Plan.Snapshot();

        /// <summary>创建关卡会话。</summary>
        /// <param name="definition">关卡定义; 不能为空且必须包含非空 LevelId。</param>
        /// <param name="worldBuilder">本地物理世界构建器; 开始模拟时使用。</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="definition"/> 或 <paramref name="worldBuilder"/> 为 null 时抛出。
        /// </exception>
        /// <exception cref="ArgumentException">定义的 LevelId 为空或空白时抛出。</exception>
        /// <remarks>
        /// 能力白名单在构造时解析。白名单非法不会抛出, 而是使 <see cref="Load"/> 失败并返回具体原因:
        /// 内容错误属于可上报、可修复的失败, 让调用方走统一失败路径比在构造点上抛异常更容易
        /// 定位到具体关卡。
        /// </remarks>
        public StageSession(LevelDefinition definition, IStageWorldBuilder worldBuilder)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (string.IsNullOrWhiteSpace(definition.LevelId))
                throw new ArgumentException("The level definition requires a non-empty LevelId.", nameof(definition));

            _definition = definition;
            _worldBuilder = worldBuilder ?? throw new ArgumentNullException(nameof(worldBuilder));
            LevelId = new LevelId(definition.LevelId);

            _contentError = AbilityWhitelist.TryCreate(definition, out AbilityWhitelist whitelist, out string message);
            _contentErrorMessage = message;
            if (_contentError == AbilityWhitelistError.None)
            {
                _placement = new PlacementService(
                    definition,
                    whitelist,
                    new PlacementValidator(new PlacementGeometry())
                );
            }
            else
            {
                _emptyDeployment = new DeploymentPlanSnapshot(
                    new List<AbilityPlacementData>().AsReadOnly(),
                    0,
                    definition.CapacityLimit
                );
            }
        }

        /// <summary>加载关卡数据, 使状态从 Unloaded 进入 Deploying。</summary>
        /// <returns>
        /// 成功返回 <see cref="ErrorCode.None"/>; 已在其他状态时返回
        /// <see cref="ErrorCode.OperationNotAllowed"/>; 关卡能力白名单非法时返回
        /// <see cref="ErrorCode.InvalidArgument"/> 且状态保持 Unloaded。
        /// </returns>
        public Result Load()
        {
            if (State != StageSessionState.Unloaded)
            {
                return Result.Failure(ErrorCode.OperationNotAllowed, "Load is only allowed from the Unloaded state.");
            }
            if (_contentError != AbilityWhitelistError.None)
                return Result.Failure(ErrorCode.InvalidArgument, _contentErrorMessage);

            State = StageSessionState.Loading;
            // 定义的加载为同步赋值, 不进行异步 IO; 本地物理世界不在此构建——
            // 技术设计文档 §6.5 规定世界在开始模拟时创建, 部署阶段没有物理世界。
            State = StageSessionState.Deploying;
            return Result.Success();
        }

        /// <summary>在部署区放置一个能力框。</summary>
        /// <param name="command">放置命令。</param>
        /// <returns>
        /// 成功时携带放置稳定标识; 非 <see cref="StageSessionState.Deploying"/> 状态返回
        /// <see cref="PlacementError.SessionLocked"/>, 其余失败原因见 <see cref="PlacementService.Place"/>。
        /// </returns>
        public PlacementResult<PlacementId> PlaceAbility(PlaceAbilityCommand command)
        {
            if (State != StageSessionState.Deploying || _placement == null)
            {
                return PlacementResult<PlacementId>.Failure(
                    PlacementError.SessionLocked,
                    LockedMessage("PlaceAbility")
                );
            }
            return _placement.Place(command);
        }

        /// <summary>移动一个已放置的能力框。</summary>
        /// <param name="command">移动命令; 尺寸选项为空表示保持原尺寸。</param>
        /// <returns>
        /// 非部署状态返回 <see cref="PlacementError.SessionLocked"/>,
        /// 其余失败原因见 <see cref="PlacementService.Move"/>。
        /// </returns>
        public PlacementResult MoveAbility(MoveAbilityCommand command)
        {
            if (State != StageSessionState.Deploying || _placement == null)
                return PlacementResult.Failure(PlacementError.SessionLocked, LockedMessage("MoveAbility"));
            return _placement.Move(command);
        }

        /// <summary>移除一个已放置的能力框。</summary>
        /// <param name="placementId">放置稳定标识。</param>
        /// <returns>
        /// 非部署状态返回 <see cref="PlacementError.SessionLocked"/>; 目标不存在返回
        /// <see cref="PlacementError.PlacementNotFound"/>。
        /// </returns>
        public PlacementResult RemoveAbility(PlacementId placementId)
        {
            if (State != StageSessionState.Deploying || _placement == null)
                return PlacementResult.Failure(PlacementError.SessionLocked, LockedMessage("RemoveAbility"));
            return _placement.Remove(placementId);
        }

        /// <summary>清空当前部署方案; 不触碰关卡静态对象。</summary>
        /// <returns>非部署状态返回 <see cref="PlacementError.SessionLocked"/>, 否则始终成功。</returns>
        public PlacementResult ClearDeployment()
        {
            if (State != StageSessionState.Deploying || _placement == null)
                return PlacementResult.Failure(PlacementError.SessionLocked, LockedMessage("ClearDeployment"));
            return _placement.Clear();
        }

        /// <summary>
        /// 开始模拟: 先执行权威校验, 通过后构建本地物理世界并冻结部署方案。
        /// </summary>
        /// <returns>
        /// 成功时状态进入 <see cref="StageSessionState.Simulating"/>; 非部署状态返回
        /// <see cref="PlacementError.SessionLocked"/>; 方案未通过权威校验时返回对应
        /// <see cref="PlacementError"/> 且状态退回 <see cref="StageSessionState.Deploying"/>;
        /// 物理世界构建失败时返回带 <see cref="StartSimulationResult.ErrorCode"/> 的结果,
        /// 状态同样退回 <see cref="StageSessionState.Deploying"/>。
        /// </returns>
        public StartSimulationResult StartSimulation()
        {
            if (State != StageSessionState.Deploying || _placement == null)
                return StartSimulationResult.Failure(PlacementError.SessionLocked, LockedMessage("StartSimulation"));

            State = StageSessionState.Validating;
            // §6.4 第二层: 不信任界面传来的状态, 整份方案重跑一遍判定链。
            PlacementError invalid = _placement.ValidateCurrentPlan();
            if (invalid != PlacementError.None)
            {
                State = StageSessionState.Deploying;
                return StartSimulationResult.Failure(invalid, $"The deployment plan is invalid: {invalid}.");
            }

            // §6.5: 先释放上一次运行的世界再重建。正常路径上上次运行已在停止时释放,
            // 这里再释放一次是幂等的, 用于兜住任何遗漏释放的路径。
            DisposeWorld();
            Result<IStageWorld> built = _worldBuilder.Build(_definition);
            if (!built.IsSuccess)
            {
                State = StageSessionState.Deploying;
                return StartSimulationResult.InfrastructureFailure(built.ErrorCode, built.Message);
            }

            _world = built.Value;
            State = StageSessionState.Simulating;
            return StartSimulationResult.Success();
        }

        /// <summary>
        /// 主动停止模拟, 释放本地物理世界并回到部署阶段。
        /// </summary>
        /// <returns>
        /// 只有 <see cref="StageSessionState.Simulating"/> 接受本命令, 成功返回
        /// <see cref="ErrorCode.None"/>; 其余状态返回 <see cref="ErrorCode.OperationNotAllowed"/>。
        /// </returns>
        /// <remarks>
        /// <see cref="StageSessionState.Resolving"/> 与 <see cref="StageSessionState.Restoring"/>
        /// 在本方法内同步走完。C25 引入固定 Tick 循环之前不存在需要跨帧等待的恢复工作,
        /// 因此这两个状态在外部观察不到停留; 一旦引入异步恢复, 中间态就必须真正可观测。
        /// </remarks>
        public Result StopSimulation()
        {
            if (State != StageSessionState.Simulating)
            {
                return Result.Failure(
                    ErrorCode.OperationNotAllowed,
                    "StopSimulation is only allowed from the Simulating state."
                );
            }

            State = StageSessionState.Resolving;
            State = StageSessionState.Restoring;
            DisposeWorld();
            State = StageSessionState.Deploying;
            return Result.Success();
        }

        /// <summary>释放当前本地物理世界; 无世界时不做任何事。</summary>
        private void DisposeWorld()
        {
            if (_world == null)
                return;
            _world.Dispose();
            _world = null;
        }

        /// <summary>生成"当前状态不接受该命令"的日志消息。</summary>
        /// <param name="commandName">命令名称。</param>
        /// <returns>包含当前状态的描述文本。</returns>
        private string LockedMessage(string commandName) =>
            $"{commandName} is only allowed in the Deploying state (current: {State}).";
    }
}
