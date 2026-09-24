using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Contracts.Gameplay;
using Game.Foundation;
using Game.Gameplay.Deployment;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// 部署规则测试共用的关卡、白名单与命令构造器。
    /// </summary>
    /// <remarks>
    /// 默认关卡是"原点为中心、边长 20 的可部署正方形 + 单一能力两种尺寸", 容量上限由调用方指定。
    /// 需要禁放区或不部署区的用例一律自建关卡, 避免靠修改共享夹具制造场景。
    /// </remarks>
    internal static class DeploymentFixtures
    {
        /// <summary>默认能力类型稳定标识。</summary>
        internal const string SpeedAbility = "official.ability.speed";

        /// <summary>默认小尺寸选项稳定标识; 全宽全高 1, 费用 1。</summary>
        internal const string SmallSize = "speed.small";

        /// <summary>默认大尺寸选项稳定标识; 全宽全高 4, 费用 3。</summary>
        internal const string LargeSize = "speed.large";

        /// <summary>默认角色稳定标识。</summary>
        internal const string Carrier = "official.character.carrier";

        /// <summary>默认可部署正方形的半边长; 即可部署区为 [-10, 10]。</summary>
        internal const float DeployableHalfSize = 10f;

        /// <summary>构造一个尺寸选项数据。</summary>
        /// <param name="sizeOptionId">尺寸选项稳定标识。</param>
        /// <param name="width">全宽。</param>
        /// <param name="height">全高。</param>
        /// <param name="capacityCost">容量费用。</param>
        /// <param name="effectPriority">效果优先级。</param>
        /// <returns>尺寸选项数据。</returns>
        internal static AbilitySizeOptionData Size(
            string sizeOptionId,
            float width,
            float height,
            int capacityCost,
            int effectPriority = 0
        ) =>
            new AbilitySizeOptionData
            {
                SizeOptionId = sizeOptionId,
                Width = width,
                Height = height,
                CapacityCost = capacityCost,
                EffectPriority = effectPriority,
            };

        /// <summary>构造一个白名单条目。</summary>
        /// <param name="characterId">角色稳定标识。</param>
        /// <param name="abilityTypeId">能力类型稳定标识。</param>
        /// <param name="sizes">尺寸选项集合。</param>
        /// <returns>白名单条目。</returns>
        internal static AllowedAbilityData Allowed(
            string characterId,
            string abilityTypeId,
            params AbilitySizeOptionData[] sizes
        ) =>
            new AllowedAbilityData
            {
                CharacterId = characterId,
                AbilityTypeId = abilityTypeId,
                SizeOptions =
                    sizes == null ? new List<AbilitySizeOptionData>() : new List<AbilitySizeOptionData>(sizes),
            };

        /// <summary>构造默认白名单条目: 一种能力, 小尺寸费用 1、大尺寸费用 3。</summary>
        /// <returns>默认白名单条目。</returns>
        internal static AllowedAbilityData DefaultAbility() =>
            Allowed(Carrier, SpeedAbility, Size(SmallSize, 1f, 1f, 1), Size(LargeSize, 4f, 4f, 3));

        /// <summary>构造以原点为中心的默认可部署正方形区域集合。</summary>
        /// <returns>只含一个区域的可部署区域集合。</returns>
        internal static ZoneData[] DeployableZones() =>
            new[] { PlacementZones.Square("zone.deployable.main", 0f, 0f, DeployableHalfSize * 2f) };

        /// <summary>构造默认关卡: 默认可部署区、无禁放区、默认白名单。</summary>
        /// <param name="capacityLimit">容量上限; 小于等于 0 表示未配置上限。</param>
        /// <returns>关卡定义。</returns>
        internal static LevelDefinition Level(int capacityLimit = 6) =>
            Level(capacityLimit, DeployableZones(), null, DefaultAbility());

        /// <summary>构造指定区域与白名单的关卡。</summary>
        /// <param name="capacityLimit">容量上限; 小于等于 0 表示未配置上限。</param>
        /// <param name="deployable">可部署区域集合; 为 null 时为空集合。</param>
        /// <param name="forbidden">禁放区域集合; 为 null 时为空集合。</param>
        /// <param name="allowed">能力白名单集合。</param>
        /// <returns>关卡定义。</returns>
        internal static LevelDefinition Level(
            int capacityLimit,
            ZoneData[] deployable,
            ZoneData[] forbidden,
            params AllowedAbilityData[] allowed
        ) =>
            new LevelDefinition
            {
                LevelId = "official.level.c23_deployment",
                CapacityLimit = capacityLimit,
                DeployableZones = deployable == null ? new List<ZoneData>() : new List<ZoneData>(deployable),
                ForbiddenZones = forbidden == null ? new List<ZoneData>() : new List<ZoneData>(forbidden),
                AllowedAbilities =
                    allowed == null ? new List<AllowedAbilityData>() : new List<AllowedAbilityData>(allowed),
            };

        /// <summary>解析白名单并在夹具非法时立即判定测试失败。</summary>
        /// <param name="definition">关卡定义。</param>
        /// <returns>可用的白名单。</returns>
        internal static AbilityWhitelist Whitelist(LevelDefinition definition)
        {
            AbilityWhitelistError error = AbilityWhitelist.TryCreate(
                definition,
                out AbilityWhitelist whitelist,
                out string message
            );
            Assert.That(error, Is.EqualTo(AbilityWhitelistError.None), $"测试夹具的白名单必须合法: {message}");
            return whitelist;
        }

        /// <summary>构造使用默认几何校验的部署服务。</summary>
        /// <param name="definition">关卡定义。</param>
        /// <returns>部署服务。</returns>
        internal static PlacementService Service(LevelDefinition definition) =>
            new PlacementService(definition, Whitelist(definition), PlacementZones.Validator());

        /// <summary>构造使用指定白名单的部署服务; 用于制造"白名单与方案不一致"的过期状态。</summary>
        /// <param name="definition">关卡定义。</param>
        /// <param name="whitelist">白名单。</param>
        /// <returns>部署服务。</returns>
        internal static PlacementService Service(LevelDefinition definition, AbilityWhitelist whitelist) =>
            new PlacementService(definition, whitelist, PlacementZones.Validator());

        /// <summary>构造使用默认能力与小尺寸的放置命令。</summary>
        /// <param name="x">中心 X。</param>
        /// <param name="y">中心 Y。</param>
        /// <returns>放置命令。</returns>
        internal static PlaceAbilityCommand Place(float x, float y) => Place(SpeedAbility, SmallSize, x, y);

        /// <summary>构造放置命令。</summary>
        /// <param name="abilityTypeId">能力类型稳定标识。</param>
        /// <param name="sizeOptionId">尺寸选项稳定标识。</param>
        /// <param name="x">中心 X。</param>
        /// <param name="y">中心 Y。</param>
        /// <returns>放置命令。</returns>
        internal static PlaceAbilityCommand Place(string abilityTypeId, string sizeOptionId, float x, float y) =>
            new PlaceAbilityCommand(
                new AbilityTypeId(abilityTypeId),
                new AbilitySizeId(sizeOptionId),
                new Vector2(x, y)
            );

        /// <summary>断言放置成功并返回放置稳定标识。</summary>
        /// <param name="result">放置结果。</param>
        /// <returns>放置稳定标识。</returns>
        internal static PlacementId Placed(PlacementResult<PlacementId> result)
        {
            Assert.That(result.IsSuccess, Is.True, result.Message);
            return result.Value;
        }
    }
}
