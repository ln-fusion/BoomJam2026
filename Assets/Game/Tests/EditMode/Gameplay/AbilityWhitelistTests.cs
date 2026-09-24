using Game.Contracts.Content;
using Game.Contracts.Gameplay;
using Game.Gameplay.Deployment;
using NUnit.Framework;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// 验证 C23 能力白名单的加载校验、规范顺序与解析语义。
    /// </summary>
    public sealed class AbilityWhitelistTests
    {
        /// <summary>关卡定义为 null 时必须返回 NullDefinition 而不是抛出。</summary>
        [Test]
        public void TryCreate_WithNullDefinition_ReturnsNullDefinition()
        {
            AbilityWhitelistError error = AbilityWhitelist.TryCreate(null, out AbilityWhitelist whitelist, out _);

            Assert.That(error, Is.EqualTo(AbilityWhitelistError.NullDefinition));
            Assert.That(whitelist, Is.Null);
        }

        /// <summary>空白名单是合法内容: 关卡不允许放置任何能力, 但加载必须成功。</summary>
        [Test]
        public void TryCreate_WithEmptyWhitelist_SucceedsWithNoOptions()
        {
            LevelDefinition definition = DeploymentFixtures.Level(6, DeploymentFixtures.DeployableZones(), null);

            AbilityWhitelistError error = AbilityWhitelist.TryCreate(definition, out AbilityWhitelist whitelist, out _);

            Assert.That(error, Is.EqualTo(AbilityWhitelistError.None));
            Assert.That(whitelist.Options, Is.Empty);
            Assert.That(
                whitelist.IsAbilityAllowed(new Game.Foundation.AbilityTypeId(DeploymentFixtures.SpeedAbility)),
                Is.False
            );
        }

        /// <summary>白名单条目为 null 时必须报缺失能力类型。</summary>
        [Test]
        public void TryCreate_WithNullEntry_ReturnsMissingAbilityTypeId()
        {
            // 必须传"含 null 元素的数组": 直接传 null 会被 C# 当作 "params 数组本身为空",
            // 得到的是空白名单而不是一个 null 条目。
            LevelDefinition definition = DeploymentFixtures.Level(
                6,
                DeploymentFixtures.DeployableZones(),
                null,
                new AllowedAbilityData[] { null }
            );

            AbilityWhitelistError error = AbilityWhitelist.TryCreate(definition, out _, out _);

            Assert.That(error, Is.EqualTo(AbilityWhitelistError.MissingAbilityTypeId));
        }

        /// <summary>能力类型稳定标识为空时必须报缺失能力类型。</summary>
        [Test]
        public void TryCreate_WithEmptyAbilityTypeId_ReturnsMissingAbilityTypeId()
        {
            LevelDefinition definition = DeploymentFixtures.Level(
                6,
                DeploymentFixtures.DeployableZones(),
                null,
                DeploymentFixtures.Allowed(DeploymentFixtures.Carrier, " ")
            );

            AbilityWhitelistError error = AbilityWhitelist.TryCreate(definition, out _, out _);

            Assert.That(error, Is.EqualTo(AbilityWhitelistError.MissingAbilityTypeId));
        }

        /// <summary>尺寸选项为 null 时必须报缺失尺寸选项。</summary>
        [Test]
        public void TryCreate_WithNullSizeOption_ReturnsMissingSizeOptionId()
        {
            // 同 TryCreate_WithNullEntry_ReturnsMissingAbilityTypeId: 传含 null 元素的数组才能表达"条目里有 null"。
            LevelDefinition definition = DeploymentFixtures.Level(
                6,
                DeploymentFixtures.DeployableZones(),
                null,
                DeploymentFixtures.Allowed(
                    DeploymentFixtures.Carrier,
                    DeploymentFixtures.SpeedAbility,
                    new AbilitySizeOptionData[] { null }
                )
            );

            AbilityWhitelistError error = AbilityWhitelist.TryCreate(definition, out _, out _);

            Assert.That(error, Is.EqualTo(AbilityWhitelistError.MissingSizeOptionId));
        }

        /// <summary>零宽尺寸必须被拒绝: 零面积矩形会让几何判定失去意义。</summary>
        [Test]
        public void TryCreate_WithZeroWidth_ReturnsInvalidSize()
        {
            LevelDefinition definition = DeploymentFixtures.Level(
                6,
                DeploymentFixtures.DeployableZones(),
                null,
                DeploymentFixtures.Allowed(
                    DeploymentFixtures.Carrier,
                    DeploymentFixtures.SpeedAbility,
                    DeploymentFixtures.Size(DeploymentFixtures.SmallSize, 0f, 1f, 1)
                )
            );

            AbilityWhitelistError error = AbilityWhitelist.TryCreate(definition, out _, out _);

            Assert.That(error, Is.EqualTo(AbilityWhitelistError.InvalidSize));
        }

        /// <summary>NaN 尺寸必须被拒绝: NaN 参与任何比较都为 false, 会静默放行非法放置。</summary>
        [Test]
        public void TryCreate_WithNaNHeight_ReturnsInvalidSize()
        {
            LevelDefinition definition = DeploymentFixtures.Level(
                6,
                DeploymentFixtures.DeployableZones(),
                null,
                DeploymentFixtures.Allowed(
                    DeploymentFixtures.Carrier,
                    DeploymentFixtures.SpeedAbility,
                    DeploymentFixtures.Size(DeploymentFixtures.SmallSize, 1f, float.NaN, 1)
                )
            );

            AbilityWhitelistError error = AbilityWhitelist.TryCreate(definition, out _, out _);

            Assert.That(error, Is.EqualTo(AbilityWhitelistError.InvalidSize));
        }

        /// <summary>负容量费用必须被拒绝。</summary>
        [Test]
        public void TryCreate_WithNegativeCost_ReturnsNegativeCapacityCost()
        {
            LevelDefinition definition = DeploymentFixtures.Level(
                6,
                DeploymentFixtures.DeployableZones(),
                null,
                DeploymentFixtures.Allowed(
                    DeploymentFixtures.Carrier,
                    DeploymentFixtures.SpeedAbility,
                    DeploymentFixtures.Size(DeploymentFixtures.SmallSize, 1f, 1f, -1)
                )
            );

            AbilityWhitelistError error = AbilityWhitelist.TryCreate(definition, out _, out _);

            Assert.That(error, Is.EqualTo(AbilityWhitelistError.NegativeCapacityCost));
        }

        /// <summary>
        /// 白名单条目必须声明至少一个尺寸选项: 零尺寸条目永远无法产生成功放置,
        /// 且会让"能力不允许"与"尺寸不存在"两类失败无法区分, 因此必须在加载时判为内容错误。
        /// </summary>
        [Test]
        public void TryCreate_WhenEntryHasNoSizes_ReturnsMissingSizeOptions()
        {
            LevelDefinition definition = DeploymentFixtures.Level(
                6,
                DeploymentFixtures.DeployableZones(),
                null,
                DeploymentFixtures.Allowed(DeploymentFixtures.Carrier, DeploymentFixtures.SpeedAbility)
            );

            AbilityWhitelistError error = AbilityWhitelist.TryCreate(
                definition,
                out AbilityWhitelist whitelist,
                out string message
            );

            Assert.That(error, Is.EqualTo(AbilityWhitelistError.MissingSizeOptions));
            Assert.That(message, Does.Contain(DeploymentFixtures.SpeedAbility));
            Assert.That(whitelist, Is.Null);
        }

        /// <summary>
        /// 角色稳定标识为空时必须拒绝: 它是同一 (能力类型, 尺寸选项) 的决胜依据,
        /// 缺失会让"谁提供该能力"无从判定。
        /// </summary>
        [Test]
        public void TryCreate_WithEmptyCharacterId_ReturnsMissingCharacterId()
        {
            LevelDefinition definition = DeploymentFixtures.Level(
                6,
                DeploymentFixtures.DeployableZones(),
                null,
                DeploymentFixtures.Allowed(
                    " ",
                    DeploymentFixtures.SpeedAbility,
                    DeploymentFixtures.Size(DeploymentFixtures.SmallSize, 1f, 1f, 1)
                )
            );

            AbilityWhitelistError error = AbilityWhitelist.TryCreate(
                definition,
                out AbilityWhitelist whitelist,
                out string message
            );

            Assert.That(error, Is.EqualTo(AbilityWhitelistError.MissingCharacterId));
            Assert.That(message, Does.Contain(DeploymentFixtures.SpeedAbility));
            Assert.That(whitelist, Is.Null);
        }

        /// <summary>
        /// 同一 (能力类型, 尺寸选项) 在两个角色下参数不同时必须判为内容错误, 否则解析结果依赖遍历顺序。
        /// </summary>
        [Test]
        public void TryCreate_WithConflictingDuplicateAcrossCharacters_ReturnsAmbiguousSizeOption()
        {
            LevelDefinition definition = DeploymentFixtures.Level(
                6,
                DeploymentFixtures.DeployableZones(),
                null,
                DeploymentFixtures.Allowed(
                    "official.character.alpha",
                    DeploymentFixtures.SpeedAbility,
                    DeploymentFixtures.Size(DeploymentFixtures.SmallSize, 1f, 1f, 1)
                ),
                DeploymentFixtures.Allowed(
                    "official.character.beta",
                    DeploymentFixtures.SpeedAbility,
                    DeploymentFixtures.Size(DeploymentFixtures.SmallSize, 2f, 2f, 5)
                )
            );

            AbilityWhitelistError error = AbilityWhitelist.TryCreate(
                definition,
                out AbilityWhitelist whitelist,
                out string message
            );

            Assert.That(error, Is.EqualTo(AbilityWhitelistError.AmbiguousSizeOption));
            Assert.That(message, Does.Contain(DeploymentFixtures.SmallSize));
            Assert.That(whitelist, Is.Null);
        }

        /// <summary>同一角色重复声明同参数尺寸时必须合并为一条, 而不是产生两个选项。</summary>
        [Test]
        public void TryCreate_WithIdenticalDuplicate_MergesIntoOneOption()
        {
            AbilitySizeOptionData size = DeploymentFixtures.Size(DeploymentFixtures.SmallSize, 1f, 1f, 1);
            LevelDefinition definition = DeploymentFixtures.Level(
                6,
                DeploymentFixtures.DeployableZones(),
                null,
                DeploymentFixtures.Allowed(DeploymentFixtures.Carrier, DeploymentFixtures.SpeedAbility, size),
                DeploymentFixtures.Allowed(DeploymentFixtures.Carrier, DeploymentFixtures.SpeedAbility, size)
            );

            AbilityWhitelistError error = AbilityWhitelist.TryCreate(definition, out AbilityWhitelist whitelist, out _);

            Assert.That(error, Is.EqualTo(AbilityWhitelistError.None));
            Assert.That(whitelist.Options, Has.Count.EqualTo(1));
        }

        /// <summary>选项必须按 (角色, 能力类型, 尺寸选项) 序数升序排列, 与声明顺序无关。</summary>
        [Test]
        public void TryCreate_OrdersOptionsByCharacterThenAbilityThenSize()
        {
            AllowedAbilityData beta = DeploymentFixtures.Allowed(
                "official.character.beta",
                DeploymentFixtures.SpeedAbility,
                DeploymentFixtures.Size(DeploymentFixtures.SmallSize, 1f, 1f, 1)
            );
            AllowedAbilityData alpha = DeploymentFixtures.Allowed(
                "official.character.alpha",
                DeploymentFixtures.SpeedAbility,
                DeploymentFixtures.Size(DeploymentFixtures.LargeSize, 4f, 4f, 3)
            );
            LevelDefinition definition = DeploymentFixtures.Level(
                6,
                DeploymentFixtures.DeployableZones(),
                null,
                beta,
                alpha
            );

            AbilityWhitelistError error = AbilityWhitelist.TryCreate(definition, out AbilityWhitelist whitelist, out _);

            Assert.That(error, Is.EqualTo(AbilityWhitelistError.None));
            Assert.That(whitelist.Options[0].CharacterId, Is.EqualTo("official.character.alpha"));
            Assert.That(whitelist.Options[0].SizeOptionId.Value, Is.EqualTo(DeploymentFixtures.LargeSize));
            Assert.That(whitelist.Options[1].CharacterId, Is.EqualTo("official.character.beta"));
        }

        /// <summary>打乱声明顺序必须得到完全一致的选项序列与解析结果。</summary>
        [Test]
        public void TryCreate_IsIndependentOfDeclarationOrder()
        {
            AllowedAbilityData beta = DeploymentFixtures.Allowed(
                "official.character.beta",
                DeploymentFixtures.SpeedAbility,
                DeploymentFixtures.Size(DeploymentFixtures.SmallSize, 1f, 1f, 1)
            );
            AllowedAbilityData alpha = DeploymentFixtures.Allowed(
                "official.character.alpha",
                DeploymentFixtures.SpeedAbility,
                DeploymentFixtures.Size(DeploymentFixtures.LargeSize, 4f, 4f, 3)
            );

            AbilityWhitelist forward = DeploymentFixtures.Whitelist(
                DeploymentFixtures.Level(6, DeploymentFixtures.DeployableZones(), null, beta, alpha)
            );
            AbilityWhitelist backward = DeploymentFixtures.Whitelist(
                DeploymentFixtures.Level(6, DeploymentFixtures.DeployableZones(), null, alpha, beta)
            );

            Assert.That(backward.Options.Count, Is.EqualTo(forward.Options.Count));
            for (int i = 0; i < forward.Options.Count; i++)
            {
                Assert.That(backward.Options[i].CharacterId, Is.EqualTo(forward.Options[i].CharacterId));
                Assert.That(
                    backward.Options[i].AbilityTypeId.Value,
                    Is.EqualTo(forward.Options[i].AbilityTypeId.Value)
                );
                Assert.That(backward.Options[i].SizeOptionId.Value, Is.EqualTo(forward.Options[i].SizeOptionId.Value));
                Assert.That(backward.Options[i].CapacityCost, Is.EqualTo(forward.Options[i].CapacityCost));
            }
        }

        /// <summary>同一 (能力类型, 尺寸选项) 由多个角色提供且参数一致时, 解析必须取角色序数最小的一条。</summary>
        [Test]
        public void TryResolve_WithMultipleCharacters_PicksSmallestCharacterId()
        {
            LevelDefinition definition = DeploymentFixtures.Level(
                6,
                DeploymentFixtures.DeployableZones(),
                null,
                DeploymentFixtures.Allowed(
                    "official.character.zulu",
                    DeploymentFixtures.SpeedAbility,
                    DeploymentFixtures.Size(DeploymentFixtures.SmallSize, 1f, 1f, 1)
                ),
                DeploymentFixtures.Allowed(
                    "official.character.alpha",
                    DeploymentFixtures.SpeedAbility,
                    DeploymentFixtures.Size(DeploymentFixtures.SmallSize, 1f, 1f, 1)
                )
            );
            AbilityWhitelist whitelist = DeploymentFixtures.Whitelist(definition);

            PlacementError error = whitelist.TryResolve(
                new Game.Foundation.AbilityTypeId(DeploymentFixtures.SpeedAbility),
                new Game.Foundation.AbilitySizeId(DeploymentFixtures.SmallSize),
                out AbilityOption option
            );

            Assert.That(error, Is.EqualTo(PlacementError.None));
            Assert.That(option.CharacterId, Is.EqualTo("official.character.alpha"));
        }

        /// <summary>未知能力类型必须返回 AbilityNotAllowed。</summary>
        [Test]
        public void TryResolve_WithUnknownAbility_ReturnsAbilityNotAllowed()
        {
            AbilityWhitelist whitelist = DeploymentFixtures.Whitelist(DeploymentFixtures.Level());

            PlacementError error = whitelist.TryResolve(
                new Game.Foundation.AbilityTypeId("official.ability.unknown"),
                new Game.Foundation.AbilitySizeId(DeploymentFixtures.SmallSize),
                out _
            );

            Assert.That(error, Is.EqualTo(PlacementError.AbilityNotAllowed));
        }

        /// <summary>能力类型为 null 必须归入 AbilityNotAllowed 而不是抛出。</summary>
        [Test]
        public void TryResolve_WithNullAbility_ReturnsAbilityNotAllowed()
        {
            AbilityWhitelist whitelist = DeploymentFixtures.Whitelist(DeploymentFixtures.Level());

            PlacementError error = whitelist.TryResolve(
                null,
                new Game.Foundation.AbilitySizeId(DeploymentFixtures.SmallSize),
                out _
            );

            Assert.That(error, Is.EqualTo(PlacementError.AbilityNotAllowed));
        }

        /// <summary>能力类型存在但尺寸选项不存在时必须返回 UnknownSize, 与"能力不允许"区分开。</summary>
        [Test]
        public void TryResolve_WithUnknownSize_ReturnsUnknownSize()
        {
            AbilityWhitelist whitelist = DeploymentFixtures.Whitelist(DeploymentFixtures.Level());

            PlacementError error = whitelist.TryResolve(
                new Game.Foundation.AbilityTypeId(DeploymentFixtures.SpeedAbility),
                new Game.Foundation.AbilitySizeId("speed.unknown"),
                out _
            );

            Assert.That(error, Is.EqualTo(PlacementError.UnknownSize));
        }

        /// <summary>尺寸选项为 null 必须归入 UnknownSize。</summary>
        [Test]
        public void TryResolve_WithNullSize_ReturnsUnknownSize()
        {
            AbilityWhitelist whitelist = DeploymentFixtures.Whitelist(DeploymentFixtures.Level());

            PlacementError error = whitelist.TryResolve(
                new Game.Foundation.AbilityTypeId(DeploymentFixtures.SpeedAbility),
                null,
                out _
            );

            Assert.That(error, Is.EqualTo(PlacementError.UnknownSize));
        }

        /// <summary>解析成功时必须返回尺寸、费用与优先级完整的选项。</summary>
        [Test]
        public void TryResolve_WhenAllowed_ReturnsCompleteOption()
        {
            AbilityWhitelist whitelist = DeploymentFixtures.Whitelist(DeploymentFixtures.Level());

            PlacementError error = whitelist.TryResolve(
                new Game.Foundation.AbilityTypeId(DeploymentFixtures.SpeedAbility),
                new Game.Foundation.AbilitySizeId(DeploymentFixtures.LargeSize),
                out AbilityOption option
            );

            Assert.That(error, Is.EqualTo(PlacementError.None));
            Assert.That(option.Width, Is.EqualTo(4f));
            Assert.That(option.Height, Is.EqualTo(4f));
            Assert.That(option.CapacityCost, Is.EqualTo(3));
            Assert.That(option.AbilityTypeId.Value, Is.EqualTo(DeploymentFixtures.SpeedAbility));
            Assert.That(option.SizeOptionId.Value, Is.EqualTo(DeploymentFixtures.LargeSize));
        }
    }
}
