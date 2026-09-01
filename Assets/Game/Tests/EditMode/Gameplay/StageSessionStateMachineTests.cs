using System;
using Game.Contracts.Content;
using Game.Contracts.Gameplay;
using Game.Foundation;
using Game.Gameplay;
using NUnit.Framework;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>验证 C19 StageSession 最小状态机: Unloaded → Loading → Deploying。</summary>
    public sealed class StageSessionStateMachineTests
    {
        /// <summary>构造一个最小关卡定义。</summary>
        /// <returns>含非空 LevelId 的定义。</returns>
        private static LevelDefinition CreateDefinition() =>
            new LevelDefinition { LevelId = "official.level.c19_session", CapacityLimit = 6 };

        /// <summary>初始状态必须是 Unloaded 且携带空部署方案。</summary>
        [Test]
        public void NewSession_StartsUnloadedWithEmptyDeployment()
        {
            var session = new StageSession(CreateDefinition());
            Assert.That(session.State, Is.EqualTo(StageSessionState.Unloaded));
            Assert.That(session.LevelId.Value, Is.EqualTo("official.level.c19_session"));
            Assert.That(session.Deployment, Is.Not.Null);
            Assert.That(session.Deployment.Placements, Is.Empty);
            Assert.That(session.Deployment.TotalCapacity, Is.Zero);
        }

        /// <summary>Load 后状态进入 Deploying, 部署方案容量上限来自定义。</summary>
        [Test]
        public void Load_TransitionsToDeploying()
        {
            var session = new StageSession(CreateDefinition());
            Result result = session.Load();
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(session.State, Is.EqualTo(StageSessionState.Deploying));
            Assert.That(session.Deployment.CapacityLimit, Is.EqualTo(6));
        }

        /// <summary>重复 Load 必须被拒绝。</summary>
        [Test]
        public void Load_WhenAlreadyDeploying_ReturnsError()
        {
            var session = new StageSession(CreateDefinition());
            session.Load();
            Result result = session.Load();
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.OperationNotAllowed));
        }

        /// <summary>构造器拒绝 null 定义。</summary>
        [Test]
        public void Constructor_WithNullDefinition_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new StageSession(null));
        }

        /// <summary>构造器拒绝缺少 LevelId 的定义。</summary>
        [Test]
        public void Constructor_WithMissingLevelId_Throws()
        {
            Assert.Throws<ArgumentException>(() => new StageSession(new LevelDefinition()));
        }

        /// <summary>工厂与构造器行为一致。</summary>
        [Test]
        public void Factory_CreatesUnloadedSession()
        {
            IStageSession session = StageSession.Create(CreateDefinition());
            Assert.That(session.State, Is.EqualTo(StageSessionState.Unloaded));
        }

        /// <summary>C23 落地前, 放置命令返回明确的"尚未实现"错误。</summary>
        [Test]
        public void PlaceAbility_ReturnsNotImplemented()
        {
            var session = new StageSession(CreateDefinition());
            session.Load();
            Result<PlacementId> result = session.PlaceAbility(
                new PlaceAbilityCommand(
                    new AbilityTypeId("speed"),
                    new AbilitySizeId("speed.small"),
                    new UnityEngine.Vector2(0f, 0f)
                )
            );
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.OperationNotAllowed));
        }

        /// <summary>C24 落地前, 开始模拟返回 SessionLocked。</summary>
        [Test]
        public void StartSimulation_ReturnsSessionLocked()
        {
            var session = new StageSession(CreateDefinition());
            session.Load();
            StartSimulationResult result = session.StartSimulation();
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(PlacementError.SessionLocked));
        }

        /// <summary>清空部署方案在 C23 落地前返回"尚未实现"。</summary>
        [Test]
        public void ClearDeployment_ReturnsNotImplemented()
        {
            var session = new StageSession(CreateDefinition());
            session.Load();
            Result result = session.ClearDeployment();
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.OperationNotAllowed));
        }
    }
}
