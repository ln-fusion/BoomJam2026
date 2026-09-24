using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Contracts.Gameplay;
using Game.Foundation;
using UnityEngine;

namespace Game.Gameplay.Deployment
{
    /// <summary>
    /// 部署方案的可变工作副本; 负责放置集合的增删改、稳定标识分配与容量累计。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本类只承担"容器 + 账本 + 标识分配": 白名单解析与几何合法性由
    /// <see cref="PlacementService"/> 负责。这样容量记账可以在没有关卡几何的前提下独立测试。
    /// </para>
    /// <para>
    /// 放置集合始终按放置稳定标识序数升序保存（技术设计文档 §6.5 要求按稳定标识排序实例化,
    /// §6.7 要求集合遍历在影响结果前明确排序）; 标识单调递增且在本会话内不复用,
    /// 因此清空后重新放置不会产生与旧方案重名的标识。
    /// </para>
    /// <para>
    /// 位置按调用方传入的原值保存, 不在此处量化: 量化必须发生在合法性校验之前,
    /// 否则"校验通过的值"与"保存的值"可能不是同一个, 违背技术设计文档 §6.4
    /// "避免编辑器能放、游戏不能放"的要求。
    /// </para>
    /// </remarks>
    public sealed class DeploymentPlan
    {
        private const string PlacementIdPrefix = "placement.";

        private readonly List<AbilityPlacementData> _placements = new List<AbilityPlacementData>();

        /// <summary>
        /// 每个放置的容量费用账本 (键为放置稳定标识)。
        /// </summary>
        /// <remarks>
        /// <see cref="AbilityPlacementData"/> 是存档 DTO, 只保存玩家可重建的内容, 不带费用;
        /// 费用由白名单在放置时解析得到, 而移动时又需要"新费用减旧费用"的增量,
        /// 因此必须单独记账, 不能从 DTO 反推。
        /// </remarks>
        private readonly Dictionary<string, int> _costs = new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly int _capacityLimit;
        private int _totalCapacity;
        private int _nextOrdinal = 1;

        /// <summary>创建部署方案。</summary>
        /// <param name="capacityLimit">
        /// 关卡容量上限; 小于等于 0 表示关卡未配置上限, 此时不限制累计费用
        /// （<see cref="Game.Contracts.Content.LevelDefinition.CapacityLimit"/> 的约定: 0 表示未配置）。
        /// </param>
        public DeploymentPlan(int capacityLimit)
        {
            _capacityLimit = capacityLimit;
        }

        /// <summary>关卡容量上限原值。</summary>
        public int CapacityLimit => _capacityLimit;

        /// <summary>是否配置了容量上限; 为 false 时容量检查一律放过。</summary>
        public bool HasCapacityLimit => _capacityLimit > 0;

        /// <summary>当前累计容量费用。</summary>
        public int TotalCapacity => _totalCapacity;

        /// <summary>当前放置数量。</summary>
        public int Count => _placements.Count;

        /// <summary>
        /// 追加一个放置; 容量不足时拒绝且不消耗放置稳定标识。
        /// </summary>
        /// <param name="abilityTypeId">能力类型稳定标识; 必须非空。</param>
        /// <param name="sizeOptionId">尺寸选项稳定标识; 必须非空。</param>
        /// <param name="capacityCost">容量费用; 必须非负。</param>
        /// <param name="position">已经量化过的能力框中心坐标。</param>
        /// <returns>
        /// 成功时携带新分配的放置稳定标识; 累计费用加上 <paramref name="capacityCost"/>
        /// 超出上限时返回 <see cref="PlacementError.CapacityExceeded"/>。
        /// </returns>
        /// <exception cref="ArgumentException">任一稳定标识为空或空白时抛出。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacityCost"/> 为负数时抛出。</exception>
        public PlacementResult<PlacementId> Add(
            string abilityTypeId,
            string sizeOptionId,
            int capacityCost,
            Vector2 position
        )
        {
            RequireStableId(abilityTypeId, nameof(abilityTypeId));
            RequireStableId(sizeOptionId, nameof(sizeOptionId));
            if (capacityCost < 0)
                throw new ArgumentOutOfRangeException(nameof(capacityCost), "A capacity cost cannot be negative.");
            if (!CanAfford(capacityCost))
                return PlacementResult<PlacementId>.Failure(
                    PlacementError.CapacityExceeded,
                    $"The placement exceeds the capacity limit of {_capacityLimit}."
                );

            var placementId = new PlacementId(
                PlacementIdPrefix + _nextOrdinal.ToString("D4", CultureInfo.InvariantCulture)
            );
            _nextOrdinal++;
            _totalCapacity += capacityCost;

            var data = new AbilityPlacementData
            {
                PlacementId = placementId.Value,
                AbilityTypeId = abilityTypeId,
                SizeOptionId = sizeOptionId,
                PositionX = position.x,
                PositionY = position.y,
            };
            _costs[data.PlacementId] = capacityCost;
            InsertSorted(data);
            return PlacementResult<PlacementId>.Success(placementId);
        }

        /// <summary>
        /// 就地替换一个放置的尺寸与位置; 放置稳定标识不变, 因此集合顺序也不变。
        /// </summary>
        /// <param name="placementId">目标放置稳定标识。</param>
        /// <param name="sizeOptionId">新的尺寸选项稳定标识; 必须非空。</param>
        /// <param name="capacityCost">新的尺寸对应的容量费用; 必须非负。</param>
        /// <param name="position">已经量化过的新中心坐标。</param>
        /// <returns>
        /// 成功返回 <see cref="PlacementError.None"/>; 目标不存在时返回
        /// <see cref="PlacementError.PlacementNotFound"/>; 费用变化后超出上限时返回
        /// <see cref="PlacementError.CapacityExceeded"/>（此时方案保持原状）。
        /// </returns>
        /// <exception cref="ArgumentException"><paramref name="sizeOptionId"/> 为空或空白时抛出。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacityCost"/> 为负数时抛出。</exception>
        public PlacementResult Replace(PlacementId placementId, string sizeOptionId, int capacityCost, Vector2 position)
        {
            RequireStableId(sizeOptionId, nameof(sizeOptionId));
            if (capacityCost < 0)
                throw new ArgumentOutOfRangeException(nameof(capacityCost), "A capacity cost cannot be negative.");
            if (placementId == null)
                return PlacementResult.Failure(PlacementError.PlacementNotFound, "A placement ID is required.");

            int index = IndexOf(placementId);
            if (index < 0)
                return PlacementResult.Failure(
                    PlacementError.PlacementNotFound,
                    $"No placement exists for '{placementId}'."
                );

            AbilityPlacementData data = _placements[index];
            int costDelta = capacityCost - ResolveCost(data);
            if (!CanAfford(costDelta))
                return PlacementResult.Failure(
                    PlacementError.CapacityExceeded,
                    $"The placement exceeds the capacity limit of {_capacityLimit}."
                );

            _totalCapacity += costDelta;
            data.SizeOptionId = sizeOptionId;
            data.PositionX = position.x;
            data.PositionY = position.y;
            _costs[placementId.Value] = capacityCost;
            return PlacementResult.Success();
        }

        /// <summary>按稳定标识读取一个放置的副本; 副本可安全读取, 修改不影响方案。</summary>
        /// <param name="placementId">目标放置稳定标识。</param>
        /// <param name="placement">放置数据副本; 未命中时为 null。</param>
        /// <returns>方案中存在该标识时返回 true。</returns>
        public bool TryGetPlacement(PlacementId placementId, out AbilityPlacementData placement)
        {
            placement = null;
            if (placementId == null)
                return false;
            int index = IndexOf(placementId);
            if (index < 0)
                return false;
            placement = Copy(_placements[index]);
            return true;
        }

        /// <summary>移除一个放置并退回其容量费用。</summary>
        /// <param name="placementId">目标放置稳定标识。</param>
        /// <returns>
        /// 成功返回 <see cref="PlacementError.None"/>; 目标不存在时返回
        /// <see cref="PlacementError.PlacementNotFound"/>。
        /// </returns>
        public PlacementResult Remove(PlacementId placementId)
        {
            if (placementId == null)
                return PlacementResult.Failure(PlacementError.PlacementNotFound, "A placement ID is required.");
            int index = IndexOf(placementId);
            if (index < 0)
                return PlacementResult.Failure(
                    PlacementError.PlacementNotFound,
                    $"No placement exists for '{placementId}'."
                );

            _totalCapacity -= ResolveCost(_placements[index]);
            _costs.Remove(_placements[index].PlacementId);
            _placements.RemoveAt(index);
            return PlacementResult.Success();
        }

        /// <summary>
        /// 清空全部放置; 累计费用归零, 但放置稳定标识继续递增, 不在本会话内复用。
        /// </summary>
        /// <remarks>只影响玩家方案, 不触碰关卡静态对象（技术设计文档 §6.5）。</remarks>
        public void Clear()
        {
            _placements.Clear();
            _costs.Clear();
            _totalCapacity = 0;
        }

        /// <summary>生成当前方案的快照; 快照内的放置数据是副本, 调用方修改不影响方案本体。</summary>
        /// <returns>与当前方案一致的部署方案快照; 每次调用都是新实例, 与之前返回的快照互不影响。</returns>
        /// <remarks>
        /// 不快照实例缓存: 快照内的 <see cref="AbilityPlacementData"/> 是可变的存档 DTO,
        /// 缓存后多次读取会交出同一批实例, 调用方（界面预检、存档写入）修改一次就会污染后续读取。
        /// 放置数量受关卡容量上限约束且数量很小, 每次重建的代价远低于该后果。
        /// </remarks>
        public DeploymentPlanSnapshot Snapshot()
        {
            var copies = new List<AbilityPlacementData>(_placements.Count);
            foreach (AbilityPlacementData data in _placements)
                copies.Add(Copy(data));
            return new DeploymentPlanSnapshot(copies.AsReadOnly(), _totalCapacity, _capacityLimit);
        }

        /// <summary>读取一条放置的容量费用。</summary>
        /// <param name="data">放置数据。</param>
        /// <returns>该放置写入方案时记账的费用; 缺失时按 0 处理。</returns>
        private int ResolveCost(AbilityPlacementData data) =>
            _costs.TryGetValue(data.PlacementId, out int cost) ? cost : 0;

        /// <summary>按稳定标识序数升序找到放置的下标。</summary>
        /// <param name="placementId">目标放置稳定标识。</param>
        /// <returns>命中的下标; 未命中返回 -1。</returns>
        private int IndexOf(PlacementId placementId)
        {
            if (placementId == null)
                return -1;
            for (int i = 0; i < _placements.Count; i++)
            {
                if (string.Equals(_placements[i].PlacementId, placementId.Value, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        /// <summary>按稳定标识序数升序插入, 保持集合的固定顺序。</summary>
        /// <param name="data">待插入的放置数据。</param>
        private void InsertSorted(AbilityPlacementData data)
        {
            int index = _placements.Count;
            for (int i = 0; i < _placements.Count; i++)
            {
                if (string.CompareOrdinal(_placements[i].PlacementId, data.PlacementId) > 0)
                {
                    index = i;
                    break;
                }
            }
            _placements.Insert(index, data);
        }

        /// <summary>判断累计费用再加上一个增量是否仍在容量上限内。</summary>
        /// <param name="costDelta">增量; 减少费用时为负数。</param>
        /// <returns>未配置上限或未超出上限时返回 true。</returns>
        private bool CanAfford(int costDelta) => !HasCapacityLimit || _totalCapacity + costDelta <= _capacityLimit;

        /// <summary>复制一条放置数据。</summary>
        /// <param name="source">源数据。</param>
        /// <returns>字段完全一致的新实例。</returns>
        private static AbilityPlacementData Copy(AbilityPlacementData source) =>
            new AbilityPlacementData
            {
                PlacementId = source.PlacementId,
                AbilityTypeId = source.AbilityTypeId,
                SizeOptionId = source.SizeOptionId,
                PositionX = source.PositionX,
                PositionY = source.PositionY,
            };

        /// <summary>校验稳定标识非空, 违反时立即抛出而不是静默写入非法方案。</summary>
        /// <param name="value">稳定标识字符串。</param>
        /// <param name="parameterName">参数名。</param>
        /// <exception cref="ArgumentException"><paramref name="value"/> 为空或空白时抛出。</exception>
        private static void RequireStableId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A stable ID is required.", parameterName);
        }
    }
}
