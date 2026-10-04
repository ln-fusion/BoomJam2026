using System.Collections;
using Game.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>在独立 PhysicsScene2D 中验证 C20 回溯与传送的真实触发和碰撞行为。</summary>
    public sealed class TemporalAbilityPhysicsTests
    {
        private TemporalAbilityWorld _world;

        /// <summary>每个测试创建默认两秒历史的本地物理世界。</summary>
        [SetUp]
        public void SetUp() => _world = new TemporalAbilityWorld();

        /// <summary>每个测试后等待本地场景完全卸载，避免污染其他玩法与 UI 测试。</summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _world.Unload();
            Assert.That(_world.Scene.isLoaded, Is.False);
        }

        /// <summary>移动对象首次进入回溯框后瞬移约两秒路程，保持速度，框消失且不再触发。</summary>
        [UnityTest]
        public IEnumerator RewindRestoresTwoSecondsAndConsumesZone()
        {
            var target = Body("car", new Vector2(-4, 0), Vector2.right);
            var zone = Trigger("rewind", Vector2.zero, true);
            for (int tick = 0; tick < 300 && !zone.Consumed; tick++)
            {
                float before = target.Body.position.x;
                _world.Step();
                if (zone.Consumed) Assert.That(target.Body.position.x, Is.EqualTo(before - 2).Within(0.05f));
            }
            Assert.That(zone.Consumed, Is.True);
            Assert.That(zone.gameObject.activeSelf, Is.False);
            Assert.That(target.Body.velocity.x, Is.EqualTo(1).Within(0.001f));
            float after = target.Body.position.x;
            for (int tick = 0; tick < 150; tick++) _world.Step();
            Assert.That(target.Body.position.x, Is.GreaterThan(after + 2));
            yield return null;
        }

        /// <summary>历史不足时回到起始状态，同 Tick 多对象进入时只处理稳定 ID 最小者。</summary>
        [UnityTest]
        public IEnumerator FirstEligibleObjectWinsAndHistoryClampsToStart()
        {
            var first = Body("a", new Vector2(0, -0.6f), Vector2.right);
            var second = Body("b", new Vector2(0, 0.6f), Vector2.right);
            var zone = Trigger("rewind", Vector2.zero, true);
            zone.GetComponent<BoxCollider2D>().size = new Vector2(1, 3);
            _world.Step();
            Assert.That(zone.LastResult, Is.EqualTo("a: rewound"));
            Assert.That(first.Body.position.x, Is.EqualTo(0).Within(0.001f));
            Assert.That(second.Body.position.x, Is.GreaterThan(0.01f));
            yield return null;
        }

        /// <summary>过去位置被当前静态障碍占用时选择较近的第一个安全快照。</summary>
        [UnityTest]
        public IEnumerator RewindSearchesNewerSafePositionInCurrentWorld()
        {
            var target = Body("box", Vector2.zero, Vector2.zero);
            for (int index = 1; index <= 10; index++)
            {
                target.Body.position = new Vector2(index, 0);
                _world.Step();
            }
            Solid("blocker", new Vector2(2, 0), new Vector2(5, 2));
            var zone = Trigger("rewind", new Vector2(10, 0), true);
            _world.Step();
            Assert.That(zone.LastResult, Is.EqualTo("box: rewound"));
            Assert.That(target.Body.position.x, Is.EqualTo(5).Within(0.001f));
            yield return null;
        }

        /// <summary>所有历史落点被占用时仍消耗回溯框，不强行把对象放入历史障碍。</summary>
        [UnityTest]
        public IEnumerator FailedRewindAlsoConsumesZone()
        {
            var target = Body("box", Vector2.zero, Vector2.zero);
            target.Body.constraints = RigidbodyConstraints2D.FreezeAll;
            Solid("blocker", Vector2.zero, new Vector2(5, 5));
            var zone = Trigger("rewind", Vector2.zero, true);
            _world.Step();
            Assert.That(zone.Consumed, Is.True);
            Assert.That(zone.LastResult, Is.EqualTo("box: blocked (consumed)"));
            Assert.That(target.Body.position, Is.EqualTo(Vector2.zero));
            yield return null;
        }

        /// <summary>单向传送保留刚体运动状态，离开后重新进入同一入口可以再次传送。</summary>
        [UnityTest]
        public IEnumerator TeleportPreservesMotionAndCanBeUsedAgain()
        {
            var target = Body("box", Vector2.zero, new Vector2(2, 0));
            target.Body.rotation = 20;
            target.Body.angularVelocity = 6;
            var zone = Trigger("portal", Vector2.zero, false, new Vector2(5, 0));
            _world.Step();
            Assert.That(target.Body.position, Is.EqualTo(new Vector2(5, 0)));
            Assert.That(target.Body.velocity, Is.EqualTo(new Vector2(2, 0)));
            Assert.That(target.Body.rotation, Is.EqualTo(20.1f).Within(0.01f));
            Assert.That(target.Body.angularVelocity, Is.EqualTo(6).Within(0.001f));
            _world.Step();
            Assert.That(target.Body.position.x, Is.GreaterThan(5));
            target.Body.position = Vector2.zero;
            _world.Step();
            Assert.That(target.Body.position.x, Is.EqualTo(5).Within(0.001f));
            Assert.That(zone.Consumed, Is.False);
            yield return null;
        }

        /// <summary>出口位于另一个入口中时禁止自动连锁，必须先离开全部入口。</summary>
        [UnityTest]
        public IEnumerator OverlappingPortalExitDoesNotPingPong()
        {
            var target = Body("car", Vector2.zero, Vector2.zero);
            Trigger("a", Vector2.zero, false, new Vector2(5, 0));
            var second = Trigger("b", new Vector2(5, 0), false, Vector2.zero);
            for (int index = 0; index < 20; index++) _world.Step();
            Assert.That(target.Body.position.x, Is.EqualTo(5).Within(0.001f));
            Assert.That(second.LastResult, Is.EqualTo("Waiting"));
            yield return null;
        }

        /// <summary>未挂适配器的普通物体不会消耗框；传送出口后的实体碰撞仍正常参与物理。</summary>
        [UnityTest]
        public IEnumerator IneligibleBodyIsIgnoredAndExitCollisionsStillWork()
        {
            var plain = Solid("plain", new Vector2(0, 3), Vector2.one).AddComponent<Rigidbody2D>();
            plain.gravityScale = 0;
            var rewind = Trigger("rewind", new Vector2(0, 3), true);
            var target = Body("car", Vector2.zero, new Vector2(2, 0));
            Trigger("portal", Vector2.zero, false, new Vector2(5, 0));
            Solid("wall", new Vector2(6, 0), Vector2.one);
            for (int index = 0; index < 90; index++) _world.Step();
            Assert.That(rewind.Consumed, Is.False);
            Assert.That(target.Body.position.x, Is.LessThan(5.5f));
            Assert.That(Mathf.Abs(target.Body.velocity.x), Is.LessThan(0.01f));
            yield return null;
        }

        /// <summary>创建并注册独立矩形目标。</summary>
        /// <param name="id">稳定标识。</param>
        /// <param name="position">起始位置。</param>
        /// <param name="velocity">初速度。</param>
        /// <returns>新适配器。</returns>
        private TemporalBody2D Body(string id, Vector2 position, Vector2 velocity)
        {
            GameObject root = Solid(id, position, new Vector2(0.4f, 0.4f));
            var rigidbody = root.AddComponent<Rigidbody2D>();
            rigidbody.gravityScale = 0;
            rigidbody.angularDrag = 0;
            rigidbody.position = position;
            rigidbody.velocity = velocity;
            var target = root.AddComponent<TemporalBody2D>();
            target.Configure(id);
            _world.Register(target);
            return target;
        }

        /// <summary>创建并注册位移入口。</summary>
        /// <param name="id">稳定标识。</param>
        /// <param name="position">入口位置。</param>
        /// <param name="rewind">是否回溯。</param>
        /// <param name="exit">单向出口位置。</param>
        /// <returns>新入口。</returns>
        private TemporalTrigger2D Trigger(string id, Vector2 position, bool rewind, Vector2 exit = default)
        {
            GameObject root = Solid(id, position, Vector2.one);
            Transform destination = null;
            if (!rewind)
            {
                var marker = new GameObject(id + " exit");
                _world.AddRoot(marker);
                marker.transform.position = exit;
                destination = marker.transform;
            }
            var trigger = root.AddComponent<TemporalTrigger2D>();
            trigger.Configure(id, rewind ? TemporalTriggerKind.Rewind : TemporalTriggerKind.Portal, destination);
            _world.Register(trigger);
            return trigger;
        }

        /// <summary>创建本地世界中的静态矩形。</summary>
        /// <param name="name">名称。</param>
        /// <param name="position">位置。</param>
        /// <param name="size">碰撞尺寸。</param>
        /// <returns>新对象。</returns>
        private GameObject Solid(string name, Vector2 position, Vector2 size)
        {
            var root = new GameObject(name, typeof(BoxCollider2D));
            _world.AddRoot(root);
            root.transform.position = position;
            root.GetComponent<BoxCollider2D>().size = size;
            return root;
        }
    }

#if UNITY_EDITOR
    /// <summary>验证交付的独立 Scene 可直接启动并推进本地物理，不依赖正式流程。</summary>
    public sealed class C20DemoSceneSmokeTests
    {
        /// <summary>加载真实测试场景并确认小车、Box 和对应回溯入口都已建立。</summary>
        [UnityTest]
        public IEnumerator DemoSceneStartsWithoutBootstrap()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/90_C20_TemporalSpike.unity",
                new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(Object.FindObjectsOfType<C20TemporalDemo>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<TemporalBody2D>(), Has.Length.EqualTo(2));
            Assert.That(Object.FindObjectsOfType<TemporalTrigger2D>(), Has.Length.EqualTo(2));
            foreach (var body in Object.FindObjectsOfType<TemporalBody2D>())
                Assert.That(body.Body.position.x, Is.GreaterThan(-6));
            Object.Destroy(Object.FindObjectOfType<C20TemporalDemo>().gameObject);
            yield return null;
            yield return null;
        }
    }
#endif
}
