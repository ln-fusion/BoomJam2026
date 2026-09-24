using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Contracts.Content;
using Game.Contracts.Gameplay;
using Game.Foundation;

namespace Game.Gameplay.Deployment
{
    /// <summary>
    /// 能力白名单的加载失败原因; <see cref="None"/> 表示白名单可用。
    /// </summary>
    public enum AbilityWhitelistError
    {
        /// <summary>无错误, 白名单可用。</summary>
        None,

        /// <summary>关卡定义为 null, 无法读取白名单。</summary>
        NullDefinition,

        /// <summary>白名单条目或尺寸选项为 null, 或能力类型稳定标识为空。</summary>
        MissingAbilityTypeId,

        /// <summary>白名单条目的角色稳定标识为空。</summary>
        MissingCharacterId,

        /// <summary>尺寸选项稳定标识为空。</summary>
        MissingSizeOptionId,

        /// <summary>白名单条目没有声明任何尺寸选项; 该条目永远无法产生成功放置。</summary>
        MissingSizeOptions,

        /// <summary>尺寸不是有限正数。</summary>
        InvalidSize,

        /// <summary>容量费用为负数。</summary>
        NegativeCapacityCost,

        /// <summary>
        /// 同一 (能力类型, 尺寸选项) 出现多次且参数不一致; 解析结果会依赖遍历顺序, 属于内容错误。
        /// </summary>
        AmbiguousSizeOption,
    }

    /// <summary>
    /// 白名单中一个可放置选项; 已把 (能力类型, 尺寸选项) 归约到唯一的尺寸与容量费用。
    /// </summary>
    /// <remarks>
    /// <see cref="CharacterId"/> 在 C23 的放置解析中不参与判定——放置命令不携带角色——
    /// 它只用于后续能力运行模型与效果解析; 因此同一 (能力类型, 尺寸选项) 由多个角色提供时,
    /// 解析结果取角色稳定标识序数最小的一条, 保证确定性。
    /// </remarks>
    public readonly struct AbilityOption
    {
        /// <summary>提供该选项的角色稳定标识; 取自关卡白名单, 当前不参与放置判定。</summary>
        public readonly string CharacterId;

        /// <summary>能力类型稳定标识。</summary>
        public readonly AbilityTypeId AbilityTypeId;

        /// <summary>尺寸选项稳定标识。</summary>
        public readonly AbilitySizeId SizeOptionId;

        /// <summary>能力框全宽; 有限正数。</summary>
        public readonly float Width;

        /// <summary>能力框全高; 有限正数。</summary>
        public readonly float Height;

        /// <summary>放置该框累计的容量费用; 非负。</summary>
        public readonly int CapacityCost;

        /// <summary>效果优先级; 越大越优先, 供效果解析决议使用。</summary>
        public readonly int EffectPriority;

        /// <summary>创建一个白名单选项。</summary>
        /// <param name="characterId">角色稳定标识。</param>
        /// <param name="abilityTypeId">能力类型稳定标识。</param>
        /// <param name="sizeOptionId">尺寸选项稳定标识。</param>
        /// <param name="width">全宽。</param>
        /// <param name="height">全高。</param>
        /// <param name="capacityCost">容量费用。</param>
        /// <param name="effectPriority">效果优先级。</param>
        internal AbilityOption(
            string characterId,
            AbilityTypeId abilityTypeId,
            AbilitySizeId sizeOptionId,
            float width,
            float height,
            int capacityCost,
            int effectPriority
        )
        {
            CharacterId = characterId;
            AbilityTypeId = abilityTypeId;
            SizeOptionId = sizeOptionId;
            Width = width;
            Height = height;
            CapacityCost = capacityCost;
            EffectPriority = effectPriority;
        }
    }

    /// <summary>
    /// 关卡能力白名单; 把 (能力类型, 尺寸选项) 解析为唯一的尺寸与容量费用。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 构造即校验, 校验通过后 <see cref="TryResolve"/> 对所有输入都是全函数: 参数取自
    /// <see cref="AbilityOption"/> 时无需再做有限性检查, 有限性只可能在放置位置一侧失效。
    /// </para>
    /// <para>
    /// 遍历顺序不得影响结果（技术设计文档 §6.7）: 选项按
    /// (角色, 能力类型, 尺寸选项) 序数升序排序后建立索引, 同一 (能力类型, 尺寸选项)
    /// 只保留排序中的首条, 即角色稳定标识序数最小的一条; 参数不一致的重复声明是内容错误。
    /// 因此声明顺序变化既不改变解析结果, 也不改变胜出的角色。
    /// </para>
    /// </remarks>
    public sealed class AbilityWhitelist
    {
        private readonly ReadOnlyCollection<AbilityOption> _options;
        private readonly Dictionary<AbilityTypeId, Dictionary<AbilitySizeId, AbilityOption>> _byAbility;

        /// <summary>创建白名单; 仅供 <see cref="TryCreate"/> 在校验通过后调用。</summary>
        /// <param name="options">按规范顺序排列的选项集合。</param>
        /// <param name="byAbility">按能力类型与尺寸选项建好的索引。</param>
        private AbilityWhitelist(
            ReadOnlyCollection<AbilityOption> options,
            Dictionary<AbilityTypeId, Dictionary<AbilitySizeId, AbilityOption>> byAbility
        )
        {
            _options = options;
            _byAbility = byAbility;
        }

        /// <summary>白名单中的全部选项; 按 (角色, 能力类型, 尺寸选项) 序数升序, 同一组合只出现一次。</summary>
        public IReadOnlyList<AbilityOption> Options => _options;

        /// <summary>
        /// 从关卡定义构造白名单; 白名单非法时返回具体原因而不是抛出。
        /// </summary>
        /// <param name="definition">关卡定义; 为 null 时返回 <see cref="AbilityWhitelistError.NullDefinition"/>。</param>
        /// <param name="whitelist">成功时的白名单; 失败时为 null。</param>
        /// <param name="message">失败原因的日志用描述; 成功时为空字符串。</param>
        /// <returns>
        /// 成功返回 <see cref="AbilityWhitelistError.None"/>; 否则返回对应原因,
        /// 调用方应据此拒绝加载关卡而不是静默降级。
        /// </returns>
        public static AbilityWhitelistError TryCreate(
            LevelDefinition definition,
            out AbilityWhitelist whitelist,
            out string message
        )
        {
            whitelist = null;
            if (definition == null)
            {
                message = "A level definition is required to build the ability whitelist.";
                return AbilityWhitelistError.NullDefinition;
            }

            List<AllowedAbilityData> entries = definition.AllowedAbilities;
            if (entries == null || entries.Count == 0)
            {
                // 空白名单是合法内容: 该关卡不允许放置任何能力, 放置命令全部返回 AbilityNotAllowed。
                message = string.Empty;
                whitelist = new AbilityWhitelist(
                    new List<AbilityOption>().AsReadOnly(),
                    new Dictionary<AbilityTypeId, Dictionary<AbilitySizeId, AbilityOption>>()
                );
                return AbilityWhitelistError.None;
            }

            var collected = new List<AbilityOption>();
            foreach (AllowedAbilityData entry in entries)
            {
                AbilityWhitelistError entryError = CollectEntry(entry, collected, out message);
                if (entryError != AbilityWhitelistError.None)
                    return entryError;
            }

            // 先排序再归约: 归约只保留"首个"选项, 排序后"首个"即为角色序数最小的一条。
            collected.Sort(CompareOptions);
            return Canonicalize(collected, out whitelist, out message);
        }

        /// <summary>
        /// 判断能力类型是否出现在白名单中, 用于界面提前置灰不可能成功的能力。
        /// </summary>
        /// <param name="abilityTypeId">能力类型稳定标识。</param>
        /// <returns>该能力在白名单中至少有一个尺寸选项时返回 true。</returns>
        public bool IsAbilityAllowed(AbilityTypeId abilityTypeId) =>
            abilityTypeId != null && !abilityTypeId.IsEmpty && _byAbility.ContainsKey(abilityTypeId);

        /// <summary>
        /// 把放置命令携带的 (能力类型, 尺寸选项) 解析为具体尺寸与费用。
        /// </summary>
        /// <param name="abilityTypeId">能力类型稳定标识。</param>
        /// <param name="sizeOptionId">尺寸选项稳定标识。</param>
        /// <param name="option">解析出的选项; 失败时为 <c>default</c>。</param>
        /// <returns>
        /// 成功返回 <see cref="PlacementError.None"/>; 能力类型缺失或不在白名单时返回
        /// <see cref="PlacementError.AbilityNotAllowed"/>; 能力类型存在但尺寸选项缺失时返回
        /// <see cref="PlacementError.UnknownSize"/>。
        /// </returns>
        public PlacementError TryResolve(
            AbilityTypeId abilityTypeId,
            AbilitySizeId sizeOptionId,
            out AbilityOption option
        )
        {
            option = default;
            if (abilityTypeId == null || abilityTypeId.IsEmpty)
                return PlacementError.AbilityNotAllowed;
            if (!_byAbility.TryGetValue(abilityTypeId, out Dictionary<AbilitySizeId, AbilityOption> sizes))
                return PlacementError.AbilityNotAllowed;
            if (sizeOptionId == null || sizeOptionId.IsEmpty)
                return PlacementError.UnknownSize;
            if (!sizes.TryGetValue(sizeOptionId, out option))
                return PlacementError.UnknownSize;
            return PlacementError.None;
        }

        /// <summary>收集单个白名单条目的全部尺寸选项, 同时完成条目级字段校验。</summary>
        /// <param name="entry">白名单条目。</param>
        /// <param name="collected">已收集的选项集合, 本方法会追加新选项。</param>
        /// <param name="message">失败原因的日志用描述。</param>
        /// <returns>成功返回 <see cref="AbilityWhitelistError.None"/>; 否则返回具体原因。</returns>
        private static AbilityWhitelistError CollectEntry(
            AllowedAbilityData entry,
            List<AbilityOption> collected,
            out string message
        )
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.AbilityTypeId))
            {
                message = "Every allowed ability entry requires a non-empty AbilityTypeId.";
                return AbilityWhitelistError.MissingAbilityTypeId;
            }
            if (string.IsNullOrWhiteSpace(entry.CharacterId))
            {
                // 角色稳定标识是同键选项的决胜依据, 缺失会让"谁提供该能力"无从判定。
                message = $"Ability '{entry.AbilityTypeId}' requires a non-empty CharacterId.";
                return AbilityWhitelistError.MissingCharacterId;
            }

            var abilityTypeId = new AbilityTypeId(entry.AbilityTypeId);
            List<AbilitySizeOptionData> sizes = entry.SizeOptions;
            if (sizes == null || sizes.Count == 0)
            {
                // 没有尺寸选项的条目永远无法产生一次成功放置, 判为内容错误而不是静默跳过:
                // 静默跳过会让"能力不允许"与"尺寸不存在"两类失败无法区分, 内容作者看不出问题。
                message = $"Ability '{abilityTypeId.Value}' must declare at least one size option.";
                return AbilityWhitelistError.MissingSizeOptions;
            }

            for (int i = 0; i < sizes.Count; i++)
            {
                AbilityWhitelistError sizeError = CollectSizeOption(
                    entry.CharacterId,
                    abilityTypeId,
                    sizes[i],
                    collected,
                    out message
                );
                if (sizeError != AbilityWhitelistError.None)
                    return sizeError;
            }
            message = string.Empty;
            return AbilityWhitelistError.None;
        }

        /// <summary>校验单个尺寸选项并追加到收集集合; 重复声明由 <see cref="Canonicalize"/> 统一归约。</summary>
        /// <param name="characterId">所属条目的角色稳定标识。</param>
        /// <param name="abilityTypeId">所属条目的能力类型稳定标识。</param>
        /// <param name="size">尺寸选项数据。</param>
        /// <param name="collected">已收集的选项集合, 本方法会追加一条。</param>
        /// <param name="message">失败原因的日志用描述。</param>
        /// <returns>成功返回 <see cref="AbilityWhitelistError.None"/>; 否则返回具体原因。</returns>
        private static AbilityWhitelistError CollectSizeOption(
            string characterId,
            AbilityTypeId abilityTypeId,
            AbilitySizeOptionData size,
            List<AbilityOption> collected,
            out string message
        )
        {
            if (size == null || string.IsNullOrWhiteSpace(size.SizeOptionId))
            {
                message = $"Ability '{abilityTypeId.Value}' has a size option without a SizeOptionId.";
                return AbilityWhitelistError.MissingSizeOptionId;
            }
            if (!IsFinitePositive(size.Width) || !IsFinitePositive(size.Height))
            {
                message = $"Size option '{size.SizeOptionId}' must have finite positive Width and Height.";
                return AbilityWhitelistError.InvalidSize;
            }
            if (size.CapacityCost < 0)
            {
                message = $"Size option '{size.SizeOptionId}' must not have a negative CapacityCost.";
                return AbilityWhitelistError.NegativeCapacityCost;
            }

            collected.Add(
                new AbilityOption(
                    characterId,
                    abilityTypeId,
                    new AbilitySizeId(size.SizeOptionId),
                    size.Width,
                    size.Height,
                    size.CapacityCost,
                    size.EffectPriority
                )
            );
            message = string.Empty;
            return AbilityWhitelistError.None;
        }

        /// <summary>
        /// 把已排序的选项集合归约为规范形式: 同一 (能力类型, 尺寸选项) 只保留排序中的首条,
        /// 参数不一致的重复声明判为内容错误。
        /// </summary>
        /// <param name="sorted">已按 (角色, 能力类型, 尺寸选项) 序数升序排好的选项集合。</param>
        /// <param name="whitelist">成功时的白名单; 失败时为 null。</param>
        /// <param name="message">失败原因的日志用描述; 成功时为空字符串。</param>
        /// <returns>成功返回 <see cref="AbilityWhitelistError.None"/>; 参数冲突时返回对应原因。</returns>
        /// <remarks>
        /// 归约必须发生在排序之后。若在收集阶段按"先声明者胜"去重, 胜出的角色就取决于声明顺序,
        /// 与本节要求的"遍历顺序不得影响结果"矛盾; 排序后首条一定是角色稳定标识序数最小的一条。
        /// 同键选项在排序中不相邻（排序首要关键字是角色）, 因此这里用索引判定重复而不是比较相邻元素。
        /// </remarks>
        private static AbilityWhitelistError Canonicalize(
            List<AbilityOption> sorted,
            out AbilityWhitelist whitelist,
            out string message
        )
        {
            whitelist = null;
            var index = new Dictionary<AbilityTypeId, Dictionary<AbilitySizeId, AbilityOption>>(sorted.Count);
            var canonical = new List<AbilityOption>(sorted.Count);
            foreach (AbilityOption option in sorted)
            {
                if (!index.TryGetValue(option.AbilityTypeId, out Dictionary<AbilitySizeId, AbilityOption> sizes))
                {
                    sizes = new Dictionary<AbilitySizeId, AbilityOption>();
                    index[option.AbilityTypeId] = sizes;
                }
                if (sizes.TryGetValue(option.SizeOptionId, out AbilityOption existing))
                {
                    if (!HasSameParameters(existing, option))
                    {
                        message =
                            $"Size option '{option.SizeOptionId.Value}' of ability '{option.AbilityTypeId.Value}' "
                            + "is declared more than once with different parameters.";
                        return AbilityWhitelistError.AmbiguousSizeOption;
                    }
                    // 参数一致: 保留先到者, 即角色稳定标识序数最小的一条。
                    continue;
                }
                sizes[option.SizeOptionId] = option;
                canonical.Add(option);
            }

            whitelist = new AbilityWhitelist(canonical.AsReadOnly(), index);
            message = string.Empty;
            return AbilityWhitelistError.None;
        }

        /// <summary>判断两个选项的尺寸与费用参数是否完全一致。</summary>
        /// <param name="left">左侧选项。</param>
        /// <param name="right">右侧选项。</param>
        /// <returns>四个参数全部相等时返回 true。</returns>
        private static bool HasSameParameters(AbilityOption left, AbilityOption right) =>
            left.Width.Equals(right.Width)
            && left.Height.Equals(right.Height)
            && left.CapacityCost == right.CapacityCost
            && left.EffectPriority == right.EffectPriority;

        /// <summary>按 (角色, 能力类型, 尺寸选项) 序数升序比较两个选项。</summary>
        /// <param name="left">左侧选项。</param>
        /// <param name="right">右侧选项。</param>
        /// <returns>比较结果, 供稳定排序使用。</returns>
        private static int CompareOptions(AbilityOption left, AbilityOption right)
        {
            int byCharacter = string.CompareOrdinal(
                left.CharacterId ?? string.Empty,
                right.CharacterId ?? string.Empty
            );
            if (byCharacter != 0)
                return byCharacter;
            int byAbility = string.CompareOrdinal(left.AbilityTypeId.Value, right.AbilityTypeId.Value);
            return byAbility != 0
                ? byAbility
                : string.CompareOrdinal(left.SizeOptionId.Value, right.SizeOptionId.Value);
        }

        /// <summary>判断尺寸分量是否为有限正数。</summary>
        /// <param name="value">尺寸分量。</param>
        /// <returns>非 NaN、非无穷且大于零时返回 true。</returns>
        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}
