using System;
using Game.Contracts.Content;
using Game.Contracts.Gameplay;
using Game.Foundation;
using Game.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// 验证 C23/C24 关卡会话的状态迁移、部署命令门禁与物理世界生命周期。
    /// </summary>
    /// <remarks>
    /// 世界构建使用替身, 因此本类不创建任何 Scene, 也不依赖帧与场景卸载时序;
    /// 真实世界的创建与释放由 PlayMode 的会话测试覆盖。
    /// </remarks>
    public sealed class StageSessionTests
    {
        private const int DefaultLimit = 6;

        /// <summary>初始状态必须是 Unloaded, 且携带空部署方案与关卡容量上限。</summary>
        [Test]
        public void NewSession_StartsUnloadedWithEmptyDeployment()
        {
            var session = new StageSession(DeploymentFixtures.Level(DefaultLimit), new FakeStageWorldBuilder());

            Assert.That(session.State, Is.EqualTo(StageSessionState.Unloaded));
            Assert.That(session.LevelId.Value, Is.EqualTo("official.level.c23_deployment"));
            Assert.That(session.Deployment.Placements, Is.Empty);
            Assert.That(session.Deployment.TotalCapacity, Is.Zero);
            Assert.That(session.Deployment.CapacityLimit, Is.EqualTo(DefaultLimit));
        }

        /// <summary>Load 后状态进入 Deploying。</summary>
        [Test]
        public void Load_TransitionsToDeploying()
        {
            StageSession session = CreateSession(out _);

            Result result = session.Load();

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(session.State, Is.EqualTo(StageSessionState.Deploying));
        }

        /// <summary>非 Unloaded 状态下的重复 Load 必须被拒绝。</summary>
        [Test]
        public void Load_WhenNotUnloaded_ReturnsOperationNotAllowed()
        {
            StageSession session = LoadedSession(out _);

            Result result = session.Load();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.OperationNotAllowed));
        }

        /// <summary>能力白名单存在歧义时加载必须失败, 且状态停留在 Unloaded。</summary>
        [Test]
        public void Load_WhenWhitelistIsAmbiguous_ReturnsInvalidArgumentAndStaysUnloaded()
        {
            LevelDefinition definition = DeploymentFixtures.Level(
                DefaultLimit,
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
            var session = new StageSession(definition, new FakeStageWorldBuilder());

            Result result = session.Load();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.InvalidArgument));
            Assert.That(session.State, Is.EqualTo(StageSessionState.Unloaded));
            Assert.That(session.Deployment.Placements, Is.Empty);
        }

        /// <summary>构造器拒绝 null 定义。</summary>
        [Test]
        public void Constructor_WithNullDefinition_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new StageSession(null, new FakeStageWorldBuilder()));
        }

        /// <summary>构造器拒绝 null 世界构建器: 没有构建器就无法开始模拟。</summary>
        [Test]
        public void Constructor_WithNullWorldBuilder_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new StageSession(DeploymentFixtures.Level(), null));
        }

        /// <summary>构造器拒绝缺少 LevelId 的定义。</summary>
        [Test]
        public void Constructor_WithMissingLevelId_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new StageSession(new LevelDefinition(), new FakeStageWorldBuilder())
            );
        }

        /// <summary>加载后放置必须成功并进入部署方案。</summary>
        [Test]
        public void PlaceAbility_AfterLoad_AddsToDeployment()
        {
            StageSession session = LoadedSession(out _);

            PlacementResult<PlacementId> result = session.PlaceAbility(DeploymentFixtures.Place(0f, 0f));

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(session.Deployment.Placements, Has.Count.EqualTo(1));
            Assert.That(session.Deployment.Placements[0].PlacementId, Is.EqualTo(result.Value.Value));
            Assert.That(session.Deployment.TotalCapacity, Is.EqualTo(1));
        }

        /// <summary>未加载时放置必须返回 SessionLocked 而不是抛异常。</summary>
        [Test]
        public void PlaceAbility_BeforeLoad_ReturnsSessionLocked()
        {
            StageSession session = CreateSession(out _);

            PlacementResult<PlacementId> result = session.PlaceAbility(DeploymentFixtures.Place(0f, 0f));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(PlacementError.SessionLocked));
        }

        /// <summary>加载后移动必须更新坐标并保留放置稳定标识。</summary>
        [Test]
        public void MoveAbility_UpdatesPositionAndKeepsIdentifier()
        {
            StageSession session = LoadedSession(out _);
            PlacementId id = DeploymentFixtures.Placed(session.PlaceAbility(DeploymentFixtures.Place(0f, 0f)));

            PlacementResult result = session.MoveAbility(new MoveAbilityCommand(id, null, new Vector2(3f, 4f)));

            Assert.That(result.IsSuccess, Is.True, result.Message);
            AbilityPlacementData stored = session.Deployment.Placements[0];
            Assert.That(stored.PlacementId, Is.EqualTo(id.Value));
            Assert.That(stored.PositionX, Is.EqualTo(3f));
            Assert.That(stored.PositionY, Is.EqualTo(4f));
        }

        /// <summary>移动不存在的放置必须返回 PlacementNotFound。</summary>
        [Test]
        public void MoveAbility_WithUnknownIdentifier_ReturnsPlacementNotFound()
        {
            StageSession session = LoadedSession(out _);

            PlacementResult result = session.MoveAbility(
                new MoveAbilityCommand(new PlacementId("placement.9999"), null, Vector2.zero)
            );

            Assert.That(result.Error, Is.EqualTo(PlacementError.PlacementNotFound));
        }

        /// <summary>移除必须同时清掉放置与它占用的容量。</summary>
        [Test]
        public void RemoveAbility_DropsPlacementAndCapacity()
        {
            StageSession session = LoadedSession(out _);
            PlacementId id = DeploymentFixtures.Placed(session.PlaceAbility(DeploymentFixtures.Place(0f, 0f)));

            PlacementResult result = session.RemoveAbility(id);

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(session.Deployment.Placements, Is.Empty);
            Assert.That(session.Deployment.TotalCapacity, Is.Zero);
        }

        /// <summary>移除不存在的放置必须返回 PlacementNotFound。</summary>
        [Test]
        public void RemoveAbility_WithUnknownIdentifier_ReturnsPlacementNotFound()
        {
            StageSession session = LoadedSession(out _);

            PlacementResult result = session.RemoveAbility(new PlacementId("placement.9999"));

            Assert.That(result.Error, Is.EqualTo(PlacementError.PlacementNotFound));
        }

        /// <summary>清空部署方案必须清掉全部放置与容量。</summary>
        [Test]
        public void ClearDeployment_EmptiesPlan()
        {
            StageSession session = LoadedSession(out _);
            session.PlaceAbility(DeploymentFixtures.Place(0f, 0f));
            session.PlaceAbility(DeploymentFixtures.Place(3f, 3f));

            PlacementResult result = session.ClearDeployment();

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(session.Deployment.Placements, Is.Empty);
            Assert.That(session.Deployment.TotalCapacity, Is.Zero);
        }

        /// <summary>部署方案快照必须是独立副本, 调用方修改不得影响会话。</summary>
        [Test]
        public void Deployment_ReturnsIndependentSnapshot()
        {
            StageSession session = LoadedSession(out _);
            session.PlaceAbility(DeploymentFixtures.Place(0f, 0f));

            DeploymentPlanSnapshot snapshot = session.Deployment;
            snapshot.Placements[0].PositionX = 999f;

            Assert.That(session.Deployment.Placements[0].PositionX, Is.EqualTo(0f));
        }

        /// <summary>开始模拟必须执行权威校验、构建世界并进入 Simulating。</summary>
        [Test]
        public void StartSimulation_TransitionsToSimulatingAndBuildsWorld()
        {
            StageSession session = LoadedSession(out FakeStageWorldBuilder builder);
            session.PlaceAbility(DeploymentFixtures.Place(0f, 0f));

            StartSimulationResult result = session.StartSimulation();

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(session.State, Is.EqualTo(StageSessionState.Simulating));
            Assert.That(builder.BuildCount, Is.EqualTo(1));
            Assert.That(builder.LiveWorldCount, Is.EqualTo(1));
        }

        /// <summary>未加载时开始模拟必须返回 SessionLocked。</summary>
        [Test]
        public void StartSimulation_BeforeLoad_ReturnsSessionLocked()
        {
            StageSession session = CreateSession(out FakeStageWorldBuilder builder);

            StartSimulationResult result = session.StartSimulation();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(PlacementError.SessionLocked));
            Assert.That(builder.BuildCount, Is.Zero);
        }

        /// <summary>世界构建失败必须作为基础设施失败上报, 状态退回 Deploying 而不是留在 Validating。</summary>
        [Test]
        public void StartSimulation_WhenWorldBuildFails_ReturnsInfrastructureFailureAndStaysDeploying()
        {
            StageSession session = LoadedSession(out FakeStageWorldBuilder builder);
            builder.FailureCode = ErrorCode.NotFound;

            StartSimulationResult result = session.StartSimulation();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.NotFound));
            Assert.That(result.Error, Is.EqualTo(PlacementError.None));
            Assert.That(session.State, Is.EqualTo(StageSessionState.Deploying));
            Assert.That(builder.LiveWorldCount, Is.Zero);
        }

        /// <summary>
        /// 方案与关卡内容出现不一致时（界面持有过期状态）, 开始前的权威校验必须拦下并退回 Deploying。
        /// </summary>
        [Test]
        public void StartSimulation_WhenLevelGeometryChangedUnderneathPlan_ReturnsPlacementErrorAndStaysDeploying()
        {
            LevelDefinition definition = DeploymentFixtures.Level(DefaultLimit);
            var builder = new FakeStageWorldBuilder();
            var session = new StageSession(definition, builder);
            Assert.That(session.Load().IsSuccess, Is.True);
            Assert.That(session.PlaceAbility(DeploymentFixtures.Place(0f, 0f)).IsSuccess, Is.True);

            // 生产代码把关卡定义当作不可变数据; 这里刻意违反该前提, 用于验证权威校验确实重跑判定链,
            // 而不是直接相信方案里记录的内容。
            definition.DeployableZones.Clear();

            StartSimulationResult result = session.StartSimulation();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(PlacementError.OutsideDeployableArea));
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.None));
            Assert.That(session.State, Is.EqualTo(StageSessionState.Deploying));
            Assert.That(builder.BuildCount, Is.Zero, "权威校验失败时不得创建物理世界。");
        }

        /// <summary>已在 Simulating 时重复开始必须被拒绝。</summary>
        [Test]
        public void StartSimulation_WhenAlreadySimulating_ReturnsSessionLocked()
        {
            StageSession session = LoadedSession(out _);
            Assert.That(session.StartSimulation().IsSuccess, Is.True);

            StartSimulationResult result = session.StartSimulation();

            Assert.That(result.Error, Is.EqualTo(PlacementError.SessionLocked));
        }

        /// <summary>模拟期间四个部署命令必须全部被锁定, 且方案保持不变。</summary>
        [Test]
        public void WhileSimulating_DeploymentCommandsAreLocked()
        {
            StageSession session = LoadedSession(out _);
            PlacementId id = DeploymentFixtures.Placed(session.PlaceAbility(DeploymentFixtures.Place(0f, 0f)));
            Assert.That(session.StartSimulation().IsSuccess, Is.True);

            Assert.That(
                session.PlaceAbility(DeploymentFixtures.Place(5f, 5f)).Error,
                Is.EqualTo(PlacementError.SessionLocked)
            );
            Assert.That(
                session.MoveAbility(new MoveAbilityCommand(id, null, new Vector2(2f, 2f))).Error,
                Is.EqualTo(PlacementError.SessionLocked)
            );
            Assert.That(session.RemoveAbility(id).Error, Is.EqualTo(PlacementError.SessionLocked));
            Assert.That(session.ClearDeployment().Error, Is.EqualTo(PlacementError.SessionLocked));

            Assert.That(session.Deployment.Placements, Has.Count.EqualTo(1));
            Assert.That(session.Deployment.Placements[0].PositionX, Is.EqualTo(0f));
        }

        /// <summary>停止模拟必须释放世界并回到 Deploying。</summary>
        [Test]
        public void StopSimulation_ReturnsToDeployingAndDisposesWorld()
        {
            StageSession session = LoadedSession(out FakeStageWorldBuilder builder);
            Assert.That(session.StartSimulation().IsSuccess, Is.True);
            Assert.That(builder.LiveWorldCount, Is.EqualTo(1));

            Result result = session.StopSimulation();

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(session.State, Is.EqualTo(StageSessionState.Deploying));
            Assert.That(builder.DisposeCount, Is.EqualTo(1));
            Assert.That(builder.LiveWorldCount, Is.Zero);
        }

        /// <summary>非模拟状态下停止必须被拒绝。</summary>
        [Test]
        public void StopSimulation_WhenNotSimulating_ReturnsOperationNotAllowed()
        {
            StageSession session = LoadedSession(out _);

            Result result = session.StopSimulation();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.OperationNotAllowed));
            Assert.That(session.State, Is.EqualTo(StageSessionState.Deploying));
        }

        /// <summary>停止后必须重用原部署方案, 不做任何复位（技术设计文档 §6.5）。</summary>
        [Test]
        public void StopSimulation_KeepsDeploymentPlan()
        {
            StageSession session = LoadedSession(out _);
            PlacementId id = DeploymentFixtures.Placed(session.PlaceAbility(DeploymentFixtures.Place(1f, 2f)));
            Assert.That(session.StartSimulation().IsSuccess, Is.True);

            Assert.That(session.StopSimulation().IsSuccess, Is.True);

            Assert.That(session.Deployment.Placements, Has.Count.EqualTo(1));
            Assert.That(session.Deployment.Placements[0].PlacementId, Is.EqualTo(id.Value));
            Assert.That(session.Deployment.Placements[0].PositionX, Is.EqualTo(1f));
            Assert.That(session.Deployment.TotalCapacity, Is.EqualTo(1));
        }

        /// <summary>停止后必须能再次开始, 且重复循环不得留下未释放的世界。</summary>
        [Test]
        public void RepeatedStopAndStart_LeavesNoLiveWorld()
        {
            StageSession session = LoadedSession(out FakeStageWorldBuilder builder);

            for (int i = 0; i < 10; i++)
            {
                Assert.That(session.StartSimulation().IsSuccess, Is.True, $"第 {i + 1} 次开始失败。");
                Assert.That(session.StopSimulation().IsSuccess, Is.True, $"第 {i + 1} 次停止失败。");
            }

            Assert.That(builder.BuildCount, Is.EqualTo(10));
            Assert.That(builder.DisposeCount, Is.EqualTo(10));
            Assert.That(builder.LiveWorldCount, Is.Zero);
            Assert.That(session.State, Is.EqualTo(StageSessionState.Deploying));
        }

        /// <summary>工厂必须创建出未加载状态的会话。</summary>
        [Test]
        public void StageSessionFactory_CreatesUnloadedSession()
        {
            IStageSessionFactory factory = new StageSessionFactory(new FakeStageWorldBuilder());

            IStageSession session = factory.Create(DeploymentFixtures.Level());

            Assert.That(session.State, Is.EqualTo(StageSessionState.Unloaded));
            Assert.That(session.LevelId.Value, Is.EqualTo("official.level.c23_deployment"));
        }

        /// <summary>工厂拒绝 null 世界构建器。</summary>
        [Test]
        public void StageSessionFactory_WithNullWorldBuilder_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new StageSessionFactory(null));
        }

        /// <summary>构造未加载的会话与记账用的世界构建器替身。</summary>
        /// <param name="builder">输出参数: 新建的世界构建器替身。</param>
        /// <param name="capacityLimit">关卡容量上限。</param>
        /// <returns>处于 Unloaded 状态的会话。</returns>
        private static StageSession CreateSession(out FakeStageWorldBuilder builder, int capacityLimit = DefaultLimit)
        {
            builder = new FakeStageWorldBuilder();
            return new StageSession(DeploymentFixtures.Level(capacityLimit), builder);
        }

        /// <summary>构造已加载到 Deploying 的会话。</summary>
        /// <param name="builder">输出参数: 新建的世界构建器替身。</param>
        /// <param name="capacityLimit">关卡容量上限。</param>
        /// <returns>处于 Deploying 状态的会话。</returns>
        private static StageSession LoadedSession(out FakeStageWorldBuilder builder, int capacityLimit = DefaultLimit)
        {
            StageSession session = CreateSession(out builder, capacityLimit);
            Result loaded = session.Load();
            Assert.That(loaded.IsSuccess, Is.True, loaded.Message);
            return session;
        }
    }
}
