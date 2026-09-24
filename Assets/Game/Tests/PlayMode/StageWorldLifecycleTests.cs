using System.Collections;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Contracts.Gameplay;
using Game.Foundation;
using Game.Gameplay.Stage;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// 验证 C21 本地物理世界的真实创建、稳定生成顺序与释放语义。
    /// </summary>
    /// <remarks>
    /// 使用测试内构造的占位预制体而非官方资源: 本类 PlayMode 程序集不能引用编辑器 API,
    /// 官方预制体的组件配置由 EditMode 的注册表测试覆盖。这里只关心世界构建行为本身。
    /// </remarks>
    public sealed class StageWorldLifecycleTests
    {
        private readonly List<GameObject> _prefabTemplates = new List<GameObject>();
        private IStageWorld _world;

        /// <summary>每个测试后释放世界与占位预制体, 保持用例相互独立。</summary>
        [UnityTearDown]
        public IEnumerator TearDownWorld()
        {
            _world?.Dispose();
            _world = null;
            yield return null;

            foreach (GameObject template in _prefabTemplates)
            {
                if (template != null)
                    Object.Destroy(template);
            }
            _prefabTemplates.Clear();
            yield return null;
        }

        /// <summary>构建后的世界必须持有一个有效的本地物理 Scene。</summary>
        [UnityTest]
        public IEnumerator Build_CreatesValidLocalPhysicsScene()
        {
            StageWorldBuilder builder = CreateBuilder(("official.prefab.ground", false));

            Result<IStageWorld> result = builder.Build(
                CreateDefinition(MakeObject("object.a", "official.prefab.ground"))
            );

            Assert.That(result.IsSuccess, Is.True, result.Message);
            _world = result.Value;
            Assert.That(_world.Scene.IsValid(), Is.True);
            Assert.That(_world.Scene.name, Does.StartWith(StageWorldBuilder.SceneNamePrefix));
            Assert.That(_world.Scene.GetPhysicsScene2D().IsValid(), Is.True, "本地物理 Scene 必须可用于显式步进。");
            yield return null;
        }

        /// <summary>每个关卡对象必须生成恰好一个实例。</summary>
        [UnityTest]
        public IEnumerator Build_CreatesOneInstancePerObject()
        {
            StageWorldBuilder builder = CreateBuilder(("official.prefab.ground", false));
            LevelDefinition definition = CreateDefinition(
                MakeObject("object.a", "official.prefab.ground"),
                MakeObject("object.b", "official.prefab.ground")
            );

            _world = BuildOrFail(builder, definition);

            Assert.That(_world.Objects, Has.Count.EqualTo(2));
            yield return null;
        }

        /// <summary>生成顺序必须按稳定 ID 升序, 与关卡数据中的声明顺序无关。</summary>
        [UnityTest]
        public IEnumerator Build_OrdersObjectsByStableIdRegardlessOfDeclarationOrder()
        {
            StageWorldBuilder builder = CreateBuilder(("official.prefab.ground", false));
            // 故意按逆序声明, 验证排序而非声明顺序决定生成次序。
            LevelDefinition definition = CreateDefinition(
                MakeObject("object.c", "official.prefab.ground"),
                MakeObject("object.b", "official.prefab.ground"),
                MakeObject("object.a", "official.prefab.ground")
            );

            _world = BuildOrFail(builder, definition);

            Assert.That(_world.Objects[0].EntityId.Value, Is.EqualTo("object.a"));
            Assert.That(_world.Objects[1].EntityId.Value, Is.EqualTo("object.b"));
            Assert.That(_world.Objects[2].EntityId.Value, Is.EqualTo("object.c"));
            yield return null;
        }

        /// <summary>同一输入重复构建必须得到完全一致的实体顺序, 否则物理结果不可复现。</summary>
        [UnityTest]
        public IEnumerator Build_ProducesIdenticalOrderAcrossRuns()
        {
            StageWorldBuilder builder = CreateBuilder(("official.prefab.ground", false));
            LevelDefinition first = CreateDefinition(
                MakeObject("object.b", "official.prefab.ground"),
                MakeObject("object.a", "official.prefab.ground")
            );
            LevelDefinition second = CreateDefinition(
                MakeObject("object.a", "official.prefab.ground"),
                MakeObject("object.b", "official.prefab.ground")
            );

            IStageWorld firstWorld = BuildOrFail(builder, first);
            IStageWorld secondWorld = BuildOrFail(builder, second);
            try
            {
                Assert.That(secondWorld.Objects.Count, Is.EqualTo(firstWorld.Objects.Count));
                for (int i = 0; i < firstWorld.Objects.Count; i++)
                {
                    Assert.That(
                        secondWorld.Objects[i].EntityId.Value,
                        Is.EqualTo(firstWorld.Objects[i].EntityId.Value)
                    );
                }
            }
            finally
            {
                secondWorld.Dispose();
                firstWorld.Dispose();
            }
            yield return null;
        }

        /// <summary>关卡数据中的位置、旋转与缩放必须原样应用到实例。</summary>
        [UnityTest]
        public IEnumerator Build_AppliesPositionRotationAndScale()
        {
            StageWorldBuilder builder = CreateBuilder(("official.prefab.ground", false));
            StageObjectData data = MakeObject("object.a", "official.prefab.ground");
            data.PositionX = 3.5f;
            data.PositionY = -2.25f;
            data.RotationZ = 30f;
            data.ScaleX = 2f;
            data.ScaleY = 0.5f;

            _world = BuildOrFail(builder, CreateDefinition(data));

            Transform instance = _world.Objects[0].Instance.transform;
            Assert.That(instance.position.x, Is.EqualTo(3.5f).Within(1e-4f));
            Assert.That(instance.position.y, Is.EqualTo(-2.25f).Within(1e-4f));
            Assert.That(instance.localEulerAngles.z, Is.EqualTo(30f).Within(1e-3f));
            Assert.That(instance.localScale.x, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(instance.localScale.y, Is.EqualTo(0.5f).Within(1e-4f));
            yield return null;
        }

        /// <summary>实例必须落在本地 Scene 内, 否则不会参与本地物理步进。</summary>
        [UnityTest]
        public IEnumerator Build_PlacesInstancesInsideLocalScene()
        {
            StageWorldBuilder builder = CreateBuilder(("official.prefab.ground", false));

            _world = BuildOrFail(builder, CreateDefinition(MakeObject("object.a", "official.prefab.ground")));

            GameObject instance = _world.Objects[0].Instance;
            Assert.That(instance.scene, Is.EqualTo(_world.Scene));
            Assert.That(instance.scene.name, Does.StartWith(StageWorldBuilder.SceneNamePrefix));
            yield return null;
        }

        /// <summary>静态与动态的区别由预制体承载, 构建器不得擅自增删物理组件。</summary>
        [UnityTest]
        public IEnumerator Build_PreservesPrefabPhysicsComponents()
        {
            StageWorldBuilder builder = CreateBuilder(
                ("official.prefab.ground", false),
                ("official.prefab.cargo", true)
            );
            LevelDefinition definition = CreateDefinition(
                MakeObject("object.static", "official.prefab.ground"),
                MakeObject("object.dynamic", "official.prefab.cargo")
            );

            _world = BuildOrFail(builder, definition);

            // 按实体标识取用, 不依赖列表下标: 生成顺序由稳定 ID 字典序决定,
            // "object.dynamic" 排在 "object.static" 之前。
            Assert.That(_world.TryGetObject(new EntityId("object.static"), out StageWorldObject staticObject), Is.True);
            Assert.That(
                _world.TryGetObject(new EntityId("object.dynamic"), out StageWorldObject dynamicObject),
                Is.True
            );
            Assert.That(staticObject.Instance.GetComponent<Rigidbody2D>(), Is.Null, "静态对象不应获得刚体。");
            Assert.That(staticObject.Instance.GetComponent<Collider2D>(), Is.Not.Null);
            Assert.That(dynamicObject.Instance.GetComponent<Rigidbody2D>(), Is.Not.Null, "动态对象应保留刚体。");
            yield return null;
        }

        /// <summary>没有对象的最小关卡也必须能构建出可用世界。</summary>
        [UnityTest]
        public IEnumerator Build_WithNoObjectsSucceeds()
        {
            StageWorldBuilder builder = CreateBuilder();

            _world = BuildOrFail(builder, CreateDefinition());

            Assert.That(_world.Objects, Is.Empty);
            Assert.That(_world.Scene.IsValid(), Is.True);
            Assert.That(_world.Scene.GetPhysicsScene2D().IsValid(), Is.True);
            yield return null;
        }

        /// <summary>实体查询只命中已生成对象, 未知标识必须安全失败。</summary>
        [UnityTest]
        public IEnumerator TryGetObject_ResolvesRegisteredEntityAndRejectsUnknown()
        {
            StageWorldBuilder builder = CreateBuilder(("official.prefab.ground", false));

            _world = BuildOrFail(builder, CreateDefinition(MakeObject("object.a", "official.prefab.ground")));

            Assert.That(_world.TryGetObject(new EntityId("object.a"), out StageWorldObject found), Is.True);
            Assert.That(found.EntityId.Value, Is.EqualTo("object.a"));
            Assert.That(_world.TryGetObject(new EntityId("object.missing"), out _), Is.False);
            Assert.That(_world.TryGetObject(null, out _), Is.False);
            yield return null;
        }

        /// <summary>释放必须卸载本地 Scene, 且重复调用保持幂等。</summary>
        [UnityTest]
        public IEnumerator Dispose_UnloadsSceneAndIsIdempotent()
        {
            StageWorldBuilder builder = CreateBuilder(("official.prefab.ground", false));
            IStageWorld world = BuildOrFail(
                builder,
                CreateDefinition(MakeObject("object.a", "official.prefab.ground"))
            );
            Scene worldScene = world.Scene;

            world.Dispose();
            for (int i = 0; i < 20 && worldScene.IsValid(); i++)
                yield return null;

            Assert.That(worldScene.IsValid(), Is.False, "释放后本地物理 Scene 必须被卸载。");

            // 第二次释放不得抛出, 也不得影响其他场景。
            Assert.DoesNotThrow(() => world.Dispose());
        }

        /// <summary>释放后不得再暴露有效场景; 世界清空且 Scene 句柄失效。</summary>
        [UnityTest]
        public IEnumerator Dispose_InvalidatesSceneHandle()
        {
            StageWorldBuilder builder = CreateBuilder(("official.prefab.ground", false));
            IStageWorld world = BuildOrFail(
                builder,
                CreateDefinition(MakeObject("object.a", "official.prefab.ground"))
            );

            world.Dispose();
            yield return null;

            // Scene.IsValid() 是可靠判据; PhysicsScene2D.IsValid() 对零值句柄仍返回 true, 不能用作判据。
            Assert.That(world.Scene.IsValid(), Is.False);
            Assert.That(world.Objects, Is.Empty);
        }

        /// <summary>
        /// 释放后的世界必须拒绝步进本地物理, 而不是落到零值句柄所指的默认场景上。
        /// </summary>
        /// <remarks>
        /// 零值 <c>PhysicsScene2D</c> 句柄的 <c>IsValid()</c> 返回 true 且指向默认场景,
        /// 单靠句柄无法判断能否步进; 世界以承载 Scene 的有效性把守这一步, 因此断言必须落在返回值上。
        /// </remarks>
        [UnityTest]
        public IEnumerator Dispose_RejectsPhysicsStepping()
        {
            StageWorldBuilder builder = CreateBuilder(("official.prefab.ground", false));
            IStageWorld world = BuildOrFail(
                builder,
                CreateDefinition(MakeObject("object.a", "official.prefab.ground"))
            );
            Assert.That(world.Physics.Simulate(1.0 / 60.0), Is.True, "存活世界的本地物理必须可以步进。");

            world.Dispose();

            Assert.That(world.Physics.Simulate(1.0 / 60.0), Is.False, "已释放世界不得再步进任何物理。");
            yield return null;
        }

        /// <summary>构建世界或在失败时断言失败消息。</summary>
        /// <param name="builder">世界构建器。</param>
        /// <param name="definition">关卡定义。</param>
        /// <returns>构建出的世界; 由调用方负责释放。</returns>
        private static IStageWorld BuildOrFail(IStageWorldBuilder builder, LevelDefinition definition)
        {
            Result<IStageWorld> result = builder.Build(definition);
            Assert.That(result.IsSuccess, Is.True, result.Message);
            return result.Value;
        }

        /// <summary>创建使用指定预制体映射的世界构建器。</summary>
        /// <param name="prefabs">预制体稳定 ID 与是否携带刚体的组合。</param>
        /// <returns>世界构建器。</returns>
        private StageWorldBuilder CreateBuilder(params (string Id, bool Dynamic)[] prefabs)
        {
            var map = new Dictionary<string, GameObject>(System.StringComparer.Ordinal);
            foreach ((string id, bool isDynamic) in prefabs)
            {
                GameObject template = new GameObject("Template_" + id);
                template.AddComponent<BoxCollider2D>();
                if (isDynamic)
                    template.AddComponent<Rigidbody2D>();
                // 占位模板不参与物理, 避免与生成出的实例互相推挤造成偶发差异。
                template.SetActive(false);
                _prefabTemplates.Add(template);
                map[id] = template;
            }
            return new StageWorldBuilder(new FakeAssetResolver(map));
        }

        /// <summary>构造含指定对象集合的关卡定义。</summary>
        /// <param name="objects">关卡对象数据。</param>
        /// <returns>关卡定义。</returns>
        private static LevelDefinition CreateDefinition(params StageObjectData[] objects) =>
            new LevelDefinition { LevelId = "official.level.c21_world", Objects = new List<StageObjectData>(objects) };

        /// <summary>构造一个关卡对象数据。</summary>
        /// <param name="objectId">对象稳定 ID。</param>
        /// <param name="prefabId">预制体稳定 ID。</param>
        /// <returns>关卡对象数据。</returns>
        private static StageObjectData MakeObject(string objectId, string prefabId) =>
            new StageObjectData { ObjectId = objectId, PrefabId = prefabId };

        /// <summary>测试用资源解析器: 按稳定 ID 返回预先构造的占位预制体。</summary>
        private sealed class FakeAssetResolver : IAssetResolver
        {
            private readonly Dictionary<string, GameObject> _prefabs;

            /// <summary>创建使用指定映射的解析器。</summary>
            /// <param name="prefabs">预制体稳定 ID 到占位对象的映射。</param>
            public FakeAssetResolver(Dictionary<string, GameObject> prefabs) => _prefabs = prefabs;

            /// <inheritdoc/>
            public GameObject GetPrefab(PrefabId id) =>
                id != null && _prefabs.TryGetValue(id.Value, out GameObject prefab) ? prefab : null;

            /// <inheritdoc/>
            public GameObject GetUiPrefab(UiPrefabId id) => null;

            /// <inheritdoc/>
            public Sprite GetSprite(SpriteId id) => null;

            /// <inheritdoc/>
            public AudioClip GetAudio(AudioId id) => null;
        }
    }
}
