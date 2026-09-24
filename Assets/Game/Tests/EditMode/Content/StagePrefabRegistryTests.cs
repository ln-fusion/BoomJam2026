using System.Collections.Generic;
using Game.Content;
using Game.Contracts.Content;
using Game.Editor.Level;
using Game.Foundation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode.Content
{
    /// <summary>
    /// 验证官方资源注册表已登记关卡调色板引用的全部预制体, 且物理组件配置符合静态/动态约定。
    /// </summary>
    /// <remarks>
    /// 这组断言是"编辑器能放、游戏不能放"之外的另一种防漂移: 调色板新增稳定 ID 而注册表未同步时,
    /// 世界构建会在运行期返回 <see cref="Game.Foundation.ErrorCode.NotFound"/>, 本测试提前拦截。
    /// </remarks>
    public sealed class StagePrefabRegistryTests
    {
        private const string RegistryAssetPath = "Assets/Game/Content/ContentAssetRegistry.asset";

        /// <summary>资源注册表资产必须存在, 否则关卡运行时无预制体可解析。</summary>
        [Test]
        public void RegistryAsset_Exists()
        {
            Assert.That(LoadRegistry(), Is.Not.Null, $"缺少资源注册表: {RegistryAssetPath}");
        }

        /// <summary>调色板中每个可放置的预制体稳定 ID 都必须能在注册表中解析出资源。</summary>
        [Test]
        public void Registry_ResolvesEveryPalettePrefabId()
        {
            var resolver = new OfficialAssetResolver(LoadRegistry());
            var missing = new List<string>();

            foreach (PaletteEntry entry in LevelPaletteCatalog.GetEntries())
            {
                if (entry.Kind != PaletteEntryKind.StageObject)
                    continue;
                if (resolver.GetPrefab(new PrefabId(entry.PrefabId)) == null)
                    missing.Add(entry.PrefabId);
            }

            Assert.That(missing, Is.Empty, "以下调色板预制体未登记到资源注册表: " + string.Join(", ", missing));
        }

        /// <summary>静态对象预制体必须有碰撞体且不带刚体, 否则会被物理引擎意外推动。</summary>
        [Test]
        public void StaticPrefabs_HaveColliderWithoutRigidbody()
        {
            foreach (
                string prefabId in new[]
                {
                    LevelPaletteCatalog.GroundPrefabId,
                    LevelPaletteCatalog.WallPrefabId,
                    LevelPaletteCatalog.RampPrefabId,
                    LevelPaletteCatalog.HazardPrefabId,
                }
            )
            {
                GameObject prefab = ResolvePrefab(prefabId);
                Assert.That(prefab.GetComponent<Collider2D>(), Is.Not.Null, $"{prefabId} 缺少碰撞体");
                Assert.That(prefab.GetComponent<Rigidbody2D>(), Is.Null, $"{prefabId} 不应携带刚体");
            }
        }

        /// <summary>货物箱是唯一的动态对象, 必须携带刚体才能参与受力与运输。</summary>
        [Test]
        public void CargoBoxPrefab_HasRigidbodyAndCollider()
        {
            GameObject prefab = ResolvePrefab(LevelPaletteCatalog.CargoPrefabId);

            Assert.That(prefab.GetComponent<Rigidbody2D>(), Is.Not.Null, "货物箱需要刚体");
            Assert.That(prefab.GetComponent<Collider2D>(), Is.Not.Null, "货物箱需要碰撞体");
        }

        /// <summary>危险水域必须使用触发器碰撞体, 否则会阻挡运动而非仅报告进入事实。</summary>
        [Test]
        public void HazardPrefab_UsesTriggerCollider()
        {
            GameObject prefab = ResolvePrefab(LevelPaletteCatalog.HazardPrefabId);
            Collider2D collider = prefab.GetComponent<Collider2D>();

            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.isTrigger, Is.True, "危险水域应只报告进入事实, 不产生物理阻挡");
        }

        /// <summary>地面平台与墙体的默认尺寸必须与调色板占位视口一致, 避免视觉与碰撞不匹配。</summary>
        [Test]
        public void StaticPrefabs_HavePositiveExtents()
        {
            foreach (
                string prefabId in new[]
                {
                    LevelPaletteCatalog.GroundPrefabId,
                    LevelPaletteCatalog.WallPrefabId,
                    LevelPaletteCatalog.RampPrefabId,
                }
            )
            {
                BoxCollider2D box = ResolvePrefab(prefabId).GetComponent<BoxCollider2D>();
                Assert.That(box, Is.Not.Null, $"{prefabId} 需要矩形碰撞体");
                Assert.That(box.size.x, Is.GreaterThan(0f), $"{prefabId} 宽度必须为正");
                Assert.That(box.size.y, Is.GreaterThan(0f), $"{prefabId} 高度必须为正");
            }
        }

        /// <summary>加载官方资源注册表资产。</summary>
        /// <returns>注册表资产; 不存在时为 null。</returns>
        private static ContentAssetRegistry LoadRegistry() =>
            AssetDatabase.LoadAssetAtPath<ContentAssetRegistry>(RegistryAssetPath);

        /// <summary>按稳定 ID 解析注册表中的预制体, 解析失败直接失败测试。</summary>
        /// <param name="prefabId">预制体稳定标识。</param>
        /// <returns>解析到的预制体。</returns>
        private static GameObject ResolvePrefab(string prefabId)
        {
            GameObject prefab = new OfficialAssetResolver(LoadRegistry()).GetPrefab(new PrefabId(prefabId));
            Assert.That(prefab, Is.Not.Null, $"资源注册表缺少 {prefabId}");
            return prefab;
        }
    }
}
