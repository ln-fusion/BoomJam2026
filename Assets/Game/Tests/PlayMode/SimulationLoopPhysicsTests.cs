using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Game.Contracts.Gameplay;
using Game.Gameplay.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// 验证 C25 固定 Tick 循环与真实本地 2D 物理的配合: 只有显式步进才推进, 且结果只取决于 Tick 数。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 调度逻辑（时间累加、单帧预算、落后终止）由 EditMode 的 <c>SimulationLoopTests</c> 用替身覆盖;
    /// 本类只覆盖必须真实物理才能证明的部分: 本地场景不被自动推进、显式步进真的让物体运动、
    /// 相同总时长切成不同帧数得到完全相同的物理结果。
    /// </para>
    /// <para>
    /// 帧率无关性用例刻意使用 0.25 秒 Tick: 全部时长都能用二进制精确表示,
    /// 累加器不会因为浮点残差多算或少算一个 Tick, 断言失败就必然指向真实缺陷。
    /// </para>
    /// </remarks>
    public sealed class SimulationLoopPhysicsTests
    {
        private const float StartHeight = 10f;
        private const double ExactTickSeconds = 0.25;
        private const int WideBudget = 600;
        private const double WideThresholdSeconds = 100.0;

        private readonly List<Scene> _localScenes = new List<Scene>();
        private readonly List<GameObject> _bodies = new List<GameObject>();

        /// <summary>每个测试后销毁掉落体并卸载本地场景, 保持用例相互独立。</summary>
        [UnityTearDown]
        public IEnumerator TearDownLocalPhysics()
        {
            foreach (GameObject body in _bodies)
            {
                if (body != null)
                    Object.Destroy(body);
            }
            _bodies.Clear();

            foreach (Scene scene in _localScenes)
            {
                if (scene.IsValid())
                    SceneManager.UnloadSceneAsync(scene);
            }
            _localScenes.Clear();
            yield return null;
        }

        /// <summary>
        /// 项目级 2D 物理模拟模式必须是 Script（技术设计文档 §6.6）。
        /// </summary>
        /// <remarks>
        /// 这条断言同时锁定 <c>ProjectSettings/Physics2DSettings.asset</c> 的序列化取值:
        /// 若有人把模式改回 FixedUpdate/Update, 默认场景会重新开始自动推进,
        /// 计时与复现性约束随之失效。
        /// </remarks>
        [Test]
        public void ProjectSimulationMode_IsScript()
        {
            Assert.That(Physics2D.simulationMode, Is.EqualTo(SimulationMode2D.Script));
        }

        /// <summary>
        /// 本地物理场景不会被自动推进: 帧照常经过, 物体必须纹丝不动。
        /// </summary>
        [UnityTest]
        public IEnumerator LocalScene_IsNotAdvancedWithoutExplicitStep()
        {
            GameObject body = CreateFallingBody();

            for (int i = 0; i < 5; i++)
                yield return null;

            Assert.That(
                body.transform.position.y,
                Is.EqualTo(StartHeight),
                "本地物理场景未显式步进时不得发生任何运动。"
            );
        }

        /// <summary>显式步进必须真的推进本地物理, 使自由落体下落, 且步数与 Tick 数一致。</summary>
        [Test]
        public void SimulationLoop_AdvancesRealLocalPhysics()
        {
            GameObject body = CreateFallingBody();
            var loop = new SimulationLoop(
                new LocalPhysics(body.scene.GetPhysicsScene2D()),
                new SimulationLoopSettings(1.0 / 60.0, WideBudget, WideThresholdSeconds)
            );

            loop.AdvanceFrame(0.5);

            Assert.That(loop.CurrentTick, Is.EqualTo(30), "60 Hz 下交付半秒应得到 30 个 Tick。");
            Assert.That(loop.Status, Is.EqualTo(SimulationLoopStatus.Running));
            Assert.That(body.transform.position.y, Is.LessThan(StartHeight - 0.5f), "自由落体必须真的下落。");
        }

        /// <summary>
        /// 相同总时长切成不同帧数时, 真实物理结果必须完全一致（技术设计文档 §6.6）。
        /// </summary>
        /// <remarks>
        /// 这一点用真实物理比对才有意义: 若循环是按渲染帧时长直接推进物理的, 不同切法会给出不同结果。
        /// </remarks>
        [Test]
        public void SimulationLoop_IsFrameRateIndependentForRealPhysics()
        {
            double[][] partitions =
            {
                new[] { 0.5 },
                new[] { 0.25, 0.25 },
                new[] { 0.125, 0.125, 0.125, 0.125 },
                new[] { 0.0625, 0.0625, 0.0625, 0.0625, 0.0625, 0.0625, 0.0625, 0.0625 },
            };

            float referenceHeight = 0f;
            long referenceTicks = 0;
            for (int i = 0; i < partitions.Length; i++)
            {
                GameObject body = CreateFallingBody();
                var loop = new SimulationLoop(
                    new LocalPhysics(body.scene.GetPhysicsScene2D()),
                    new SimulationLoopSettings(ExactTickSeconds, WideBudget, WideThresholdSeconds)
                );

                foreach (double frame in partitions[i])
                    loop.AdvanceFrame(frame);

                Assert.That(loop.CurrentTick, Is.EqualTo(2), $"第 {i + 1} 种切法未得到预期的 Tick 数。");
                if (i == 0)
                {
                    referenceHeight = body.transform.position.y;
                    referenceTicks = loop.CurrentTick;
                    continue;
                }

                Assert.That(loop.CurrentTick, Is.EqualTo(referenceTicks));
                Assert.That(
                    body.transform.position.y,
                    Is.EqualTo(referenceHeight).Within(1e-6f),
                    $"第 {i + 1} 种切法与单帧交付得到的高度不一致。"
                );
            }
        }

        /// <summary>创建一个带动态刚体的本地物理场景, 并在指定高度放一个自由落体。</summary>
        /// <returns>掉落体; 由测试清理负责销毁。</returns>
        private GameObject CreateFallingBody()
        {
            Scene scene = SceneManager.CreateScene(
                "SimulationLoopPhysicsTests_" + _localScenes.Count.ToString(CultureInfo.InvariantCulture),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D)
            );
            _localScenes.Add(scene);

            var body = new GameObject("falling_body");
            body.AddComponent<Rigidbody2D>();
            body.AddComponent<BoxCollider2D>();
            SceneManager.MoveGameObjectToScene(body, scene);
            body.transform.position = new Vector3(0f, StartHeight, 0f);
            _bodies.Add(body);
            return body;
        }

        /// <summary>
        /// 把本地场景包装成本地物理步进入口的替身, 供用例直接驱动循环。
        /// </summary>
        /// <remarks>
        /// 这里不经过 <c>StageWorld</c>: 本类验证的是循环与本地物理的配合,
        /// 世界对已释放世界的门禁由 <c>StageWorldLifecycleTests</c> 覆盖。
        /// </remarks>
        private sealed class LocalPhysics : ISimulationPhysics
        {
            private readonly PhysicsScene2D _scene;

            /// <summary>使用测试创建的最后一个本地场景。</summary>
            /// <param name="scene">要步进的本地物理场景。</param>
            public LocalPhysics(PhysicsScene2D scene)
            {
                _scene = scene;
            }

            /// <inheritdoc/>
            public bool Simulate(double fixedDeltaSeconds) => _scene.Simulate((float)fixedDeltaSeconds);
        }
    }
}
