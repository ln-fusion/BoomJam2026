using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Contracts.Gameplay;
using Game.Foundation;
using UnityEngine;

namespace Game.Gameplay.Deployment
{
    /// <summary>
    /// 部署规则的唯一权威实现: 白名单解析、几何合法性与容量累计。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 判定顺序固定为 白名单 → 尺寸选项 → 数值有限性 → 部署区/禁放区 → 容量,
    /// 且整个判定都在<em>量化之后</em>进行。顺序固定有两个原因: 让同一份非法输入在不同调用点
    /// 得到同一个原因, 便于界面按原因给反馈; 让"校验通过的值"与"写入方案的值"是同一个
    /// （技术设计文档 §6.4）。
    /// </para>
    /// <para>
    /// 重叠的能力框是合法的: 技术设计文档 §6.8 用"同类型只有一个贡献成为 Winner"处理重叠,
    /// 并明确"并不免除每个框的容量成本"。因此本服务不拒绝对已放置框的重叠覆盖,
    /// 只按每个框各自的费用累计容量。
    /// </para>
    /// <para>
    /// 编辑器预览与界面预检必须复用 <see cref="IPlacementValidator"/> 与
    /// <see cref="PlacementGrid"/> 而不是自己实现一套, 否则会出现"编辑器能放、游戏不能放"。
    /// 本服务是开始模拟时的权威校验入口: <see cref="Validate"/> 会把整份方案重跑一遍,
    /// 用于抵御过期界面状态。
    /// </para>
    /// </remarks>
    public sealed class PlacementService
    {
        private readonly LevelDefinition _definition;
        private readonly AbilityWhitelist _whitelist;
        private readonly IPlacementValidator _validator;
        private readonly DeploymentPlan _plan;

        /// <summary>创建部署服务。</summary>
        /// <param name="definition">关卡定义; 不能为空, 部署区域与容量上限都取自它。</param>
        /// <param name="whitelist">已校验的能力白名单; 不能为空。</param>
        /// <param name="validator">几何校验实现; 编辑器与运行时必须共用同一实现。</param>
        /// <exception cref="ArgumentNullException">任一参数为 null 时抛出。</exception>
        public PlacementService(LevelDefinition definition, AbilityWhitelist whitelist, IPlacementValidator validator)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _whitelist = whitelist ?? throw new ArgumentNullException(nameof(whitelist));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _plan = new DeploymentPlan(definition.CapacityLimit);
        }

        /// <summary>当前部署方案; 供会话读取与快照。</summary>
        public DeploymentPlan Plan => _plan;

        /// <summary>
        /// 放置一个能力框。
        /// </summary>
        /// <param name="command">放置命令; 位置为世界坐标, 会在判定前量化到网格。</param>
        /// <returns>
        /// 成功时携带新分配的放置稳定标识; 失败时返回
        /// <see cref="PlacementError.AbilityNotAllowed"/>、<see cref="PlacementError.UnknownSize"/>、
        /// <see cref="PlacementError.InvalidNumber"/>、<see cref="PlacementError.OutsideDeployableArea"/>、
        /// <see cref="PlacementError.OverlapsForbiddenArea"/> 或 <see cref="PlacementError.CapacityExceeded"/>。
        /// </returns>
        public PlacementResult<PlacementId> Place(PlaceAbilityCommand command)
        {
            PlacementError resolve = _whitelist.TryResolve(
                command.AbilityTypeId,
                command.SizeId,
                out AbilityOption option
            );
            if (resolve != PlacementError.None)
                return PlacementResult<PlacementId>.Failure(
                    resolve,
                    DescribeResolve(command.AbilityTypeId, command.SizeId)
                );

            Vector2 center = PlacementGrid.Quantize(command.WorldPosition);
            PlacementError geometry = ValidateGeometry(center, option.Width, option.Height);
            if (geometry != PlacementError.None)
                return PlacementResult<PlacementId>.Failure(geometry, $"The placement at {center} is not allowed.");

            return _plan.Add(option.AbilityTypeId.Value, option.SizeOptionId.Value, option.CapacityCost, center);
        }

        /// <summary>
        /// 移动一个已放置的能力框, 可同时改变尺寸。
        /// </summary>
        /// <param name="command">
        /// 移动命令; 尺寸选项为空表示保持原尺寸, 位置为世界坐标, 会在判定前量化到网格。
        /// </param>
        /// <returns>
        /// 成功返回 <see cref="PlacementError.None"/>; 目标不存在时返回
        /// <see cref="PlacementError.PlacementNotFound"/>; 其余失败原因与 <see cref="Place"/> 一致。
        /// 目标尺寸在移动后变大导致超出容量时返回 <see cref="PlacementError.CapacityExceeded"/> 且方案保持原状。
        /// </returns>
        public PlacementResult Move(MoveAbilityCommand command)
        {
            if (!_plan.TryGetPlacement(command.PlacementId, out AbilityPlacementData current))
                return PlacementResult.Failure(
                    PlacementError.PlacementNotFound,
                    $"No placement exists for '{command.PlacementId}'."
                );

            if (!TryMakeAbilityTypeId(current.AbilityTypeId, out AbilityTypeId abilityTypeId))
            {
                return PlacementResult.Failure(
                    PlacementError.AbilityNotAllowed,
                    $"Placement '{command.PlacementId}' has no ability type."
                );
            }

            // 保持原尺寸也要重新解析: 解析结果同时给出新的宽高与费用, 避免从 DTO 反推尺寸配置。
            bool keepSize = command.SizeId == null || command.SizeId.IsEmpty;
            AbilitySizeId sizeId = keepSize ? MakeSizeId(current.SizeOptionId) : command.SizeId;

            PlacementError resolve = _whitelist.TryResolve(abilityTypeId, sizeId, out AbilityOption option);
            if (resolve != PlacementError.None)
                return PlacementResult.Failure(resolve, DescribeResolve(abilityTypeId, sizeId));

            Vector2 center = PlacementGrid.Quantize(command.WorldPosition);
            PlacementError geometry = ValidateGeometry(center, option.Width, option.Height);
            if (geometry != PlacementError.None)
                return PlacementResult.Failure(geometry, $"The placement at {center} is not allowed.");

            return _plan.Replace(command.PlacementId, option.SizeOptionId.Value, option.CapacityCost, center);
        }

        /// <summary>移除一个已放置的能力框。</summary>
        /// <param name="placementId">目标放置稳定标识。</param>
        /// <returns>
        /// 成功返回 <see cref="PlacementError.None"/>; 目标不存在时返回
        /// <see cref="PlacementError.PlacementNotFound"/>。
        /// </returns>
        public PlacementResult Remove(PlacementId placementId) => _plan.Remove(placementId);

        /// <summary>
        /// 清空当前部署方案; 只清空玩家方案, 不影响关卡静态对象（技术设计文档 §6.5）。
        /// </summary>
        /// <returns>始终成功。</returns>
        public PlacementResult Clear()
        {
            _plan.Clear();
            return PlacementResult.Success();
        }

        /// <summary>
        /// 对当前方案执行一次权威校验, 用于开始模拟之前。
        /// </summary>
        /// <returns>
        /// 合法时返回 <see cref="PlacementError.None"/>; 否则返回首个失败原因。
        /// </returns>
        public PlacementError ValidateCurrentPlan() => Validate(_plan);

        /// <summary>
        /// 对任意部署方案逐条重跑完整判定链, 返回首个失败原因。
        /// </summary>
        /// <param name="plan">待校验方案; 为 null 时视为空方案并返回 <see cref="PlacementError.None"/>。</param>
        /// <returns>
        /// 全部放置合法且累计费用在上限内时返回 <see cref="PlacementError.None"/>;
        /// 否则返回该放置对应的原因。遍历按放置稳定标识序数升序进行, 因此结果只取决于方案内容。
        /// </returns>
        /// <remarks>
        /// 存在的意义是抵御过期状态: 方案可能来自界面缓存或（C25 的）模拟前恢复快照,
        /// 而不是刚由本服务写入。因此这里不信任方案中会随内容一起变化的部分——
        /// 白名单解析结果、空间合法性、容量合计。
        /// 放置稳定标识不在此重复校验: <see cref="DeploymentPlan"/> 只接受由自身分配的标识,
        /// 空标识在容器不变式下不可能出现。
        /// </remarks>
        public PlacementError Validate(DeploymentPlan plan)
        {
            if (plan == null)
                return PlacementError.None;

            IReadOnlyList<AbilityPlacementData> placements = plan.Snapshot().Placements;
            int totalCapacity = 0;
            for (int i = 0; i < placements.Count; i++)
            {
                AbilityPlacementData data = placements[i];
                if (!TryMakeAbilityTypeId(data.AbilityTypeId, out AbilityTypeId abilityTypeId))
                    return PlacementError.AbilityNotAllowed;

                AbilitySizeId sizeId = MakeSizeId(data.SizeOptionId);
                PlacementError resolve = _whitelist.TryResolve(abilityTypeId, sizeId, out AbilityOption option);
                if (resolve != PlacementError.None)
                    return resolve;

                PlacementError geometry = ValidateGeometry(
                    new Vector2(data.PositionX, data.PositionY),
                    option.Width,
                    option.Height
                );
                if (geometry != PlacementError.None)
                    return geometry;

                totalCapacity += option.CapacityCost;
            }

            return plan.HasCapacityLimit && totalCapacity > plan.CapacityLimit
                ? PlacementError.CapacityExceeded
                : PlacementError.None;
        }

        /// <summary>按中心点与尺寸构造矩形并执行几何校验。</summary>
        /// <param name="center">已经量化的中心坐标。</param>
        /// <param name="width">全宽。</param>
        /// <param name="height">全高。</param>
        /// <returns>
        /// 合法返回 <see cref="PlacementError.None"/>; 尺寸或中心非法返回
        /// <see cref="PlacementError.InvalidNumber"/>; 否则返回几何判定结果。
        /// </returns>
        private PlacementError ValidateGeometry(Vector2 center, float width, float height)
        {
            PlacementError created = PlacementRect.TryCreate(center, width, height, out PlacementRect rect);
            return created != PlacementError.None ? created : _validator.Validate(rect, _definition);
        }

        /// <summary>构造能力类型稳定标识, 空字符串按"缺失"处理而不是抛出。</summary>
        /// <param name="value">原始字符串。</param>
        /// <param name="abilityTypeId">构造出的稳定标识; 失败时为 null。</param>
        /// <returns>字符串可构造稳定标识时返回 true。</returns>
        private static bool TryMakeAbilityTypeId(string value, out AbilityTypeId abilityTypeId)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                abilityTypeId = null;
                return false;
            }
            abilityTypeId = new AbilityTypeId(value);
            return true;
        }

        /// <summary>
        /// 构造尺寸选项稳定标识; 空字符串返回 null。
        /// </summary>
        /// <param name="value">原始字符串。</param>
        /// <returns>稳定标识; 字符串为空时返回 null, 由白名单解析判为 UnknownSize。</returns>
        private static AbilitySizeId MakeSizeId(string value) =>
            string.IsNullOrWhiteSpace(value) ? null : new AbilitySizeId(value);

        /// <summary>生成白名单解析失败的日志消息。</summary>
        /// <param name="abilityTypeId">能力类型稳定标识。</param>
        /// <param name="sizeId">尺寸选项稳定标识。</param>
        /// <returns>包含两个稳定标识的描述文本。</returns>
        private static string DescribeResolve(AbilityTypeId abilityTypeId, AbilitySizeId sizeId) =>
            $"Ability '{abilityTypeId?.Value}' with size '{sizeId?.Value}' is not allowed by the level.";
    }
}
