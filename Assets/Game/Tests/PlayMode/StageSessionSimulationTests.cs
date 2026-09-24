using System.Collections;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Contracts.Gameplay;
using Game.Foundation;
using Game.Gameplay;
using Game.Gameplay.Stage;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// 验证 C24 关卡会话使用真实世界构建器时的本地物理 Scene 生命周期。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本类只覆盖"必须真实创建与卸载场景"的部分: 状态迁移与部署规则由 EditMode 的
    /// <c>StageSessionTests</c> 用世界构建器替身覆盖, 不做重复。
    /// </para>
    /// <para>
    /// 会话不暴露世界句柄, 因此这里用 <see cref="SceneManager.sceneCount"/> 作为可观测判据:
    /// 开始模拟恰好多出一个本地 Scene, 停止后必须回落。
    /// </para>
    /// <para>
    /// 预制体使用测试内构造的占位对象而非官方资源: 本装配不能引用编辑器 API,
    /// 官方预制体的组件配置由 EditMode 的注册表测试覆盖。
    /// </para>
    /// </remarks>
    public sealed class StageSessionSimulationTests
    {
        private const string SpeedAbility = "official.ability.speed";
        private const string SmallSize = "speed.small";
        private const string GroundPrefabId = "official.prefab.ground";
        private const int WaitFrames = 60;

        private readonly List<GameObject> _prefabTemplates = new List<GameObject>();
        private StageSession _session;

        /// <summary>每个测试后停止模拟、销毁占位预制体并等待场景卸载完成。</summary>
        [UnityTearDown]
        public IEnumerator TearDownSession()
        {
            if (_session != null && _session.State == StageSessionState.Simulating)
                _session.StopSimulation();
            _session = null;
            yield return null;

            foreach (GameObject template in _prefabTemplates)
            {
                if (template != null)
                    Object.Destroy(template);
            }
            _prefabTemplates.Clear();
            yield return null;
        }

        /// <summary>开始模拟必须创建恰好一个本地 Scene 并进入 Simulating。</summary>
        [UnityTest]
        public IEnumerator StartSimulation_AddsExactlyOneLocalScene()
        {
            _session = CreateLoadedSession();
            Assert.That(Place().IsSuccess, Is.True);
            int baseline = SceneManager.sceneCount;

            StartSimulationResult result = _session.StartSimulation();

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(result.Error, Is.EqualTo(PlacementError.None));
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.None));
            Assert.That(_session.State, Is.EqualTo(StageSessionState.Simulating));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(baseline + 1));
            yield return null;
        }

        /// <summary>停止模拟必须卸载本地 Scene, 使场景数量回落到开始前。</summary>
        [UnityTest]
        public IEnumerator StopSimulation_UnloadsLocalScene()
        {
            _session = CreateLoadedSession();
            Assert.That(Place().IsSuccess, Is.True);
            int baseline = SceneManager.sceneCount;
            Assert.That(_session.StartSimulation().IsSuccess, Is.True);

            Result stopped = _session.StopSimulation();

            Assert.That(stopped.IsSuccess, Is.True, stopped.Message);
            Assert.That(_session.State, Is.EqualTo(StageSessionState.Deploying));
            yield return WaitForSceneCount(baseline);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(baseline), "停止后本地物理 Scene 必须被卸载。");
        }

        /// <summary>停止后必须重回可部署状态, 且原部署方案与容量保持不变。</summary>
        [UnityTest]
        public IEnumerator StopSimulation_KeepsDeploymentPlanAndAllowsFurtherEdits()
        {
            _session = CreateLoadedSession();
            PlacementId first = Place().Value;
            Assert.That(_session.StartSimulation().IsSuccess, Is.True);
            Assert.That(_session.StopSimulation().IsSuccess, Is.True);

            Assert.That(_session.Deployment.Placements, Has.Count.EqualTo(1));
            Assert.That(_session.Deployment.Placements[0].PlacementId, Is.EqualTo(first.Value));
            Assert.That(_session.Deployment.TotalCapacity, Is.EqualTo(1));

            PlacementResult<PlacementId> second = Place(3f);
            Assert.That(second.IsSuccess, Is.True, second.Message);
            Assert.That(second.Value.Value, Is.EqualTo("placement.0002"), "标识必须在会话内继续递增, 不复用。");
            yield return null;
        }

        /// <summary>
        /// 连续十次"开始—停止"不得留下未卸载的本地 Scene;
        /// 这是"停止后立即重新开始"最容易暴露残留的路径。
        /// </summary>
        [UnityTest]
        public IEnumerator RepeatedStopAndStart_LeavesNoResidualScenes()
        {
            _session = CreateLoadedSession();
            Assert.That(Place().IsSuccess, Is.True);
            int baseline = SceneManager.sceneCount;

            for (int i = 0; i < 10; i++)
            {
                Assert.That(_session.StartSimulation().IsSuccess, Is.True, $"第 {i + 1} 次开始失败。");
                Assert.That(_session.StopSimulation().IsSuccess, Is.True, $"第 {i + 1} 次停止失败。");
            }
            yield return WaitForSceneCount(baseline);

            Assert.That(SceneManager.sceneCount, Is.EqualTo(baseline), "十次重建后不得残留本地物理 Scene。");
            Assert.That(_session.State, Is.EqualTo(StageSessionState.Deploying));
            Assert.That(_session.Deployment.Placements, Has.Count.EqualTo(1));
        }

        /// <summary>
        /// 预制体缺失属于内容错误, 必须作为基础设施失败上报, 且不得留下半个场景。
        /// </summary>
        [UnityTest]
        public IEnumerator StartSimulation_WithMissingPrefab_ReportsInfrastructureFailureAndStaysDeploying()
        {
            _session = CreateLoadedSession(objectPrefabId: "official.prefab.missing");
            int baseline = SceneManager.sceneCount;

            StartSimulationResult result = _session.StartSimulation();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.NotFound));
            Assert.That(result.Error, Is.EqualTo(PlacementError.None));
            Assert.That(_session.State, Is.EqualTo(StageSessionState.Deploying));
            yield return null;
            Assert.That(SceneManager.sceneCount, Is.EqualTo(baseline), "构建失败不得留下本地 Scene。");
        }

        /// <summary>等待场景数量回落到目标值, 或等到帧数上限。</summary>
        /// <param name="target">期望的场景数量上限。</param>
        /// <returns>逐帧等待的协程。</returns>
        private static IEnumerator WaitForSceneCount(int target)
        {
            for (int i = 0; i < WaitFrames && SceneManager.sceneCount > target; i++)
                yield return null;
        }

        /// <summary>构造并加载一个使用真实世界构建器的会话。</summary>
        /// <param name="objectPrefabId">关卡静态对象引用的预制体稳定标识。</param>
        /// <returns>处于 Deploying 状态的会话。</returns>
        private StageSession CreateLoadedSession(string objectPrefabId = GroundPrefabId)
        {
            var session = new StageSession(
                CreateDefinition(objectPrefabId),
                new StageWorldBuilder(CreateAssetResolver())
            );
            Result loaded = session.Load();
            Assert.That(loaded.IsSuccess, Is.True, loaded.Message);
            return session;
        }

        /// <summary>在默认可部署区放置一个能力框。</summary>
        /// <param name="x">中心 X 坐标。</param>
        /// <returns>放置结果。</returns>
        private PlacementResult<PlacementId> Place(float x = 0f) =>
            _session.PlaceAbility(
                new PlaceAbilityCommand(
                    new AbilityTypeId(SpeedAbility),
                    new AbilitySizeId(SmallSize),
                    new Vector2(x, 0f)
                )
            );

        /// <summary>构造带一个静态对象、一个可部署区与一条白名单的关卡定义。</summary>
        /// <param name="objectPrefabId">关卡静态对象引用的预制体稳定标识。</param>
        /// <returns>关卡定义。</returns>
        private static LevelDefinition CreateDefinition(string objectPrefabId) =>
            new LevelDefinition
            {
                LevelId = "official.level.c24_session",
                CapacityLimit = 6,
                DeployableZones = new List<ZoneData> { Square("zone.deployable.main", 0f, 0f, 20f) },
                ForbiddenZones = new List<ZoneData>(),
                Objects = new List<StageObjectData> { MakeObject("object.ground", objectPrefabId) },
                AllowedAbilities = new List<AllowedAbilityData>
                {
                    new AllowedAbilityData
                    {
                        CharacterId = "official.character.carrier",
                        AbilityTypeId = SpeedAbility,
                        SizeOptions = new List<AbilitySizeOptionData>
                        {
                            new AbilitySizeOptionData
                            {
                                SizeOptionId = SmallSize,
                                Width = 1f,
                                Height = 1f,
                                CapacityCost = 1,
                            },
                        },
                    },
                },
            };

        /// <summary>构造以指定中心与边长生成的正方形区域。</summary>
        /// <param name="zoneId">区域稳定标识。</param>
        /// <param name="centerX">中心 X。</param>
        /// <param name="centerY">中心 Y。</param>
        /// <param name="size">边长。</param>
        /// <returns>区域数据。</returns>
        private static ZoneData Square(string zoneId, float centerX, float centerY, float size)
        {
            float half = size * 0.5f;
            return new ZoneData
            {
                ZoneId = zoneId,
                Vertices = new List<PointData>
                {
                    new PointData { X = centerX - half, Y = centerY - half },
                    new PointData { X = centerX + half, Y = centerY - half },
                    new PointData { X = centerX + half, Y = centerY + half },
                    new PointData { X = centerX - half, Y = centerY + half },
                },
            };
        }

        /// <summary>构造一个静态关卡对象数据。</summary>
        /// <param name="objectId">对象稳定标识。</param>
        /// <param name="prefabId">预制体稳定标识。</param>
        /// <returns>关卡对象数据。</returns>
        private static StageObjectData MakeObject(string objectId, string prefabId) =>
            new StageObjectData
            {
                ObjectId = objectId,
                PrefabId = prefabId,
                ScaleX = 1f,
                ScaleY = 1f,
            };

        /// <summary>构造只解析地面预制体的资源解析器, 并登记一个占位模板。</summary>
        /// <returns>资源解析器。</returns>
        private IAssetResolver CreateAssetResolver()
        {
            GameObject template = new GameObject("Template_" + GroundPrefabId);
            template.AddComponent<BoxCollider2D>();
            // 占位模板不参与物理, 避免与生成出的实例互相推挤。
            template.SetActive(false);
            _prefabTemplates.Add(template);

            return new FakeAssetResolver(
                new Dictionary<string, GameObject>(System.StringComparer.Ordinal) { [GroundPrefabId] = template }
            );
        }

        /// <summary>只解析预制体的最小资源解析器; 其余资源类型恒为 null。</summary>
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
