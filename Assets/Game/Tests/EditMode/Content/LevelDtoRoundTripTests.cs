using Game.Contracts.Content;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Content
{
    /// <summary>验证 C19 关卡 DTO 的 JSON 序列化往返与前向兼容。</summary>
    public sealed class LevelDtoRoundTripTests
    {
        /// <summary>验证最小关卡定义可序列化并还原全部核心字段。</summary>
        [Test]
        public void MinimalDefinition_RoundTrips()
        {
            var level = new LevelDefinition
            {
                LevelId = "official.level.c19_min",
                MapId = "official.map.factory_001",
                DisplayNameKey = "level.c19_min.name",
                SortOrder = 1,
                CapacityLimit = 8,
                WorldBounds = new BoundsData
                {
                    MinX = -10f,
                    MinY = -10f,
                    MaxX = 10f,
                    MaxY = 10f,
                },
                StartPoint = new SpawnPointData
                {
                    PositionX = -8f,
                    PositionY = 2f,
                    RotationZ = 90f,
                },
                GoalPoint = new GoalPointData
                {
                    PositionX = 8f,
                    PositionY = 0f,
                    Width = 2f,
                    Height = 2f,
                },
            };

            string json = JsonUtility.ToJson(level, true);
            LevelDefinition restored = JsonUtility.FromJson<LevelDefinition>(json);

            Assert.That(restored.LevelId, Is.EqualTo(level.LevelId));
            Assert.That(restored.MapId, Is.EqualTo(level.MapId));
            Assert.That(restored.CapacityLimit, Is.EqualTo(8));
            Assert.That(restored.WorldBounds.MinX, Is.EqualTo(-10f));
            Assert.That(restored.StartPoint.PositionX, Is.EqualTo(-8f));
            Assert.That(restored.GoalPoint.Width, Is.EqualTo(2f));
            Assert.That(restored.DeployableZones, Is.Empty);
            Assert.That(restored.AllowedAbilities, Is.Empty);
        }

        /// <summary>验证覆盖对象、区域、能力白名单与条件的完整定义可往返。</summary>
        [Test]
        public void FullDefinition_RoundTrips()
        {
            var level = new LevelDefinition
            {
                LevelId = "official.level.c19_full",
                Objects =
                {
                    new StageObjectData
                    {
                        ObjectId = "obj_001",
                        PrefabId = "official.prefab.platform_01",
                        PositionX = 0f,
                        PositionY = 1f,
                        RotationZ = 15f,
                        ScaleX = 2f,
                        ScaleY = 0.5f,
                        Parameters =
                        {
                            new ParameterData { Key = "surface", Value = "slippery" },
                        },
                    },
                },
                DeployableZones =
                {
                    new ZoneData
                    {
                        ZoneId = "zone_001",
                        Vertices =
                        {
                            new PointData { X = -5f, Y = 0f },
                            new PointData { X = 5f, Y = 0f },
                            new PointData { X = 5f, Y = 5f },
                            new PointData { X = -5f, Y = 5f },
                        },
                    },
                },
                AllowedAbilities =
                {
                    new AllowedAbilityData
                    {
                        CharacterId = "official.character.hani",
                        AbilityTypeId = "official.ability.speed",
                        SizeOptions =
                        {
                            new AbilitySizeOptionData
                            {
                                SizeOptionId = "speed.small",
                                Width = 1f,
                                Height = 1f,
                                CapacityCost = 2,
                                EffectPriority = 10,
                            },
                        },
                    },
                },
                SuccessConditions =
                {
                    new ConditionData
                    {
                        ConditionTypeId = "entity_reached_goal",
                        Parameters =
                        {
                            new ConditionParameterData { Key = "entity", Value = "obj_001" },
                            new ConditionParameterData { Key = "goal", Value = "goal_001" },
                        },
                    },
                },
                FailureConditions =
                {
                    new ConditionData { ConditionTypeId = "entity_outside_bounds", MatchMode = MatchMode.Any },
                },
            };

            string json = JsonUtility.ToJson(level, true);
            LevelDefinition restored = JsonUtility.FromJson<LevelDefinition>(json);

            Assert.That(restored.Objects, Has.Count.EqualTo(1));
            Assert.That(restored.Objects[0].Parameters, Has.Count.EqualTo(1));
            Assert.That(restored.DeployableZones, Has.Count.EqualTo(1));
            Assert.That(restored.DeployableZones[0].Vertices, Has.Count.EqualTo(4));
            Assert.That(restored.AllowedAbilities, Has.Count.EqualTo(1));
            Assert.That(restored.SuccessConditions[0].MatchMode, Is.EqualTo(MatchMode.All));
            Assert.That(restored.FailureConditions[0].MatchMode, Is.EqualTo(MatchMode.Any));
        }

        /// <summary>验证未知字段在反序列化时被忽略; 前向兼容新版本内容。</summary>
        [Test]
        public void UnknownFields_AreIgnored()
        {
            const string json =
                @"
{
    ""LevelId"": ""official.level.c19_legacy"",
    ""MapId"": ""official.map.factory_001"",
    ""FutureField"": 42,
    ""CapacityLimit"": 4
}";
            LevelDefinition restored = JsonUtility.FromJson<LevelDefinition>(json);
            Assert.That(restored.LevelId, Is.EqualTo("official.level.c19_legacy"));
            Assert.That(restored.CapacityLimit, Is.EqualTo(4));
            Assert.That(restored.DeployableZones, Is.Empty);
        }
    }
}
