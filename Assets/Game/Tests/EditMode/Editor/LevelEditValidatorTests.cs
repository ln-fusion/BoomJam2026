using System.Collections.Generic;
using System.Linq;
using Game.Contracts.Content;
using Game.Editor.Level;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Editor
{
    /// <summary>
    /// 验证关卡编辑期校验: 起点终点唯一性、区域与对象完整性, 以及问题定位目标。
    /// </summary>
    public sealed class LevelEditValidatorTests
    {
        /// <summary>工厂产出的新关卡模板应当直接通过编辑期校验。</summary>
        [Test]
        public void Validate_AcceptsFactoryTemplate()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.t_01_01");

            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(data);

            Assert.That(issues, Is.Empty, Describe(issues));
        }

        /// <summary>缺少关卡定义时不能静默通过。</summary>
        [Test]
        public void Validate_ReportsMissingDefinition()
        {
            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(null);

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0].Code, Is.EqualTo(LevelValidationCode.MissingWorldBounds));
        }

        /// <summary>缺少起点必须报错, 并定位到起点。</summary>
        [Test]
        public void Validate_ReportsMissingStartPoint()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.t_01_01");
            data.Definition.StartPoint = null;

            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(data);

            Assert.That(Codes(issues), Does.Contain(LevelValidationCode.MissingStartPoint));
            Assert.That(
                TargetOf(issues, LevelValidationCode.MissingStartPoint).Kind,
                Is.EqualTo(LevelSelectionKind.SpawnPoint)
            );
        }

        /// <summary>缺少终点必须报错, 并定位到终点。</summary>
        [Test]
        public void Validate_ReportsMissingGoalPoint()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.t_01_01");
            data.Definition.GoalPoint = null;

            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(data);

            Assert.That(Codes(issues), Does.Contain(LevelValidationCode.MissingGoalPoint));
            Assert.That(
                TargetOf(issues, LevelValidationCode.MissingGoalPoint).Kind,
                Is.EqualTo(LevelSelectionKind.GoalPoint)
            );
        }

        /// <summary>终点宽高非正必须报错。</summary>
        [Test]
        public void Validate_ReportsInvalidGoalSize()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.t_01_01");
            data.Definition.GoalPoint.Width = 0f;

            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(data);

            Assert.That(Codes(issues), Does.Contain(LevelValidationCode.InvalidGoalSize));
        }

        /// <summary>缺少世界边界必须报错。</summary>
        [Test]
        public void Validate_ReportsMissingWorldBounds()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.t_01_01");
            data.Definition.WorldBounds = null;

            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(data);

            Assert.That(Codes(issues), Does.Contain(LevelValidationCode.MissingWorldBounds));
        }

        /// <summary>世界边界反向必须报错。</summary>
        [Test]
        public void Validate_ReportsInvalidWorldBounds()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.t_01_01");
            data.Definition.WorldBounds.MaxX = data.Definition.WorldBounds.MinX;

            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(data);

            Assert.That(Codes(issues), Does.Contain(LevelValidationCode.InvalidWorldBounds));
        }

        /// <summary>顶点不足三个的区域无法构成面积, 必须报错。</summary>
        [Test]
        public void Validate_ReportsZoneTooFewVertices()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.t_01_01");
            var controller = new LevelEditController(data);
            controller.AddRectZone(ZoneKind.Deployable, 0f, 0f, 2f, 2f, out _);
            // 直接改数据绕过控制器下限, 模拟手写 JSON 或外部工具产出的退化区域。
            controller.ZoneList(ZoneKind.Deployable)[0].Vertices.RemoveRange(0, 2);

            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(data);

            Assert.That(Codes(issues), Does.Contain(LevelValidationCode.ZoneTooFewVertices));
            LevelSelection target = TargetOf(issues, LevelValidationCode.ZoneTooFewVertices);
            Assert.That(target.ZoneKind, Is.EqualTo(ZoneKind.Deployable));
            Assert.That(target.ZoneIndex, Is.EqualTo(0));
        }

        /// <summary>恰好三个顶点的区域是合法的, 不得误报。</summary>
        [Test]
        public void Validate_AcceptsZoneAtVertexMinimum()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.t_01_01");
            var controller = new LevelEditController(data);
            controller.AddZone(
                ZoneKind.Deployable,
                new List<Vector2> { Vector2.zero, new Vector2(2f, 0f), new Vector2(0f, 2f) },
                out _
            );

            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(data);

            Assert.That(Codes(issues), Has.No.Member(LevelValidationCode.ZoneTooFewVertices));
        }

        /// <summary>缺少顶点集合的区域必须报错且不抛异常。</summary>
        [Test]
        public void Validate_ReportsZoneWithNullVertices()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.t_01_01");
            data.Definition.ForbiddenZones.Add(new ZoneData { ZoneId = "zone.forbidden.01", Vertices = null });

            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(data);

            Assert.That(Codes(issues), Does.Contain(LevelValidationCode.ZoneDegenerate));
        }

        /// <summary>区域稳定 ID 缺失必须报错。</summary>
        [Test]
        public void Validate_ReportsZoneIdMissing()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.t_01_01");
            data.Definition.DeployableZones.Add(
                new ZoneData
                {
                    ZoneId = "  ",
                    Vertices = new List<PointData>
                    {
                        new PointData { X = 0f, Y = 0f },
                        new PointData { X = 1f, Y = 0f },
                        new PointData { X = 0f, Y = 1f },
                    },
                }
            );

            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(data);

            Assert.That(Codes(issues), Does.Contain(LevelValidationCode.ZoneIdMissing));
        }

        /// <summary>同种区域间稳定 ID 重复必须报错。</summary>
        [Test]
        public void Validate_ReportsDuplicateZoneIdWithinSameKind()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.t_01_01");
            var controller = new LevelEditController(data);
            controller.AddRectZone(ZoneKind.Deployable, 0f, 0f, 2f, 2f, out _);
            controller.AddRectZone(ZoneKind.Deployable, 4f, 0f, 6f, 2f, out _);
            controller.ZoneList(ZoneKind.Deployable)[1].ZoneId = controller.ZoneList(ZoneKind.Deployable)[0].ZoneId;

            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(data);

            Assert.That(Codes(issues), Does.Contain(LevelValidationCode.ZoneIdDuplicate));
        }

        /// <summary>不同种类的区域可以使用相同稳定 ID, 不视为冲突。</summary>
        [Test]
        public void Validate_AllowsSameZoneIdAcrossKinds()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.t_01_01");
            var controller = new LevelEditController(data);
            controller.AddRectZone(ZoneKind.Deployable, 0f, 0f, 2f, 2f, out _);
            controller.AddRectZone(ZoneKind.Forbidden, 4f, 0f, 6f, 2f, out _);
            controller.ZoneList(ZoneKind.Forbidden)[0].ZoneId = controller.ZoneList(ZoneKind.Deployable)[0].ZoneId;

            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(data);

            Assert.That(Codes(issues), Has.No.Member(LevelValidationCode.ZoneIdDuplicate));
        }

        /// <summary>调色板外的预制体 ID 必须报错并定位到该对象。</summary>
        [Test]
        public void Validate_ReportsUnknownObjectPrefab()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.t_01_01");
            var controller = new LevelEditController(data);
            controller.AddObject(LevelPaletteCatalog.WallPrefabId, Vector2.zero, out LevelSelection selection);
            controller.TryGetObject(selection.ObjectId, out StageObjectData item);
            item.PrefabId = "official.prefab.mystery";

            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(data);

            Assert.That(Codes(issues), Does.Contain(LevelValidationCode.ObjectPrefabUnknown));
            LevelSelection target = TargetOf(issues, LevelValidationCode.ObjectPrefabUnknown);
            Assert.That(target.Kind, Is.EqualTo(LevelSelectionKind.StageObject));
            Assert.That(target.ObjectId, Is.EqualTo(selection.ObjectId));
        }

        /// <summary>对象稳定 ID 重复必须报错。</summary>
        [Test]
        public void Validate_ReportsDuplicateObjectId()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.t_01_01");
            var controller = new LevelEditController(data);
            controller.AddObject(LevelPaletteCatalog.WallPrefabId, Vector2.zero, out LevelSelection first);
            controller.AddObject(LevelPaletteCatalog.WallPrefabId, Vector2.zero, out _);
            controller.Definition.Objects[1].ObjectId = first.ObjectId;

            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(data);

            Assert.That(Codes(issues), Does.Contain(LevelValidationCode.ObjectIdDuplicate));
        }

        /// <summary>对象稳定 ID 为空必须报错。</summary>
        [Test]
        public void Validate_ReportsMissingObjectId()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.t_01_01");
            data.Definition.Objects.Add(
                new StageObjectData
                {
                    ObjectId = string.Empty,
                    PrefabId = LevelPaletteCatalog.WallPrefabId,
                    ScaleX = 1f,
                    ScaleY = 1f,
                }
            );

            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(data);

            Assert.That(Codes(issues), Does.Contain(LevelValidationCode.ObjectIdMissing));
        }

        /// <summary>非正缩放必须报错。</summary>
        [Test]
        public void Validate_ReportsInvalidObjectScale()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.t_01_01");
            var controller = new LevelEditController(data);
            controller.AddObject(LevelPaletteCatalog.WallPrefabId, Vector2.zero, out LevelSelection selection);
            controller.TryGetObject(selection.ObjectId, out StageObjectData item);
            item.ScaleY = -1f;

            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(data);

            Assert.That(Codes(issues), Does.Contain(LevelValidationCode.ObjectScaleInvalid));
        }

        /// <summary>参数键重复必须报错, 因为解释顺序会依赖数组顺序。</summary>
        [Test]
        public void Validate_ReportsDuplicateParameterKey()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.t_01_01");
            var controller = new LevelEditController(data);
            controller.AddObject(LevelPaletteCatalog.WallPrefabId, Vector2.zero, out LevelSelection selection);
            controller.TryGetObject(selection.ObjectId, out StageObjectData item);
            item.Parameters.Add(new ParameterData { Key = "mass", Value = "1" });
            item.Parameters.Add(new ParameterData { Key = "mass", Value = "2" });

            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(data);

            Assert.That(Codes(issues), Does.Contain(LevelValidationCode.ObjectParameterKeyDuplicate));
        }

        /// <summary>空参数键必须报错。</summary>
        [Test]
        public void Validate_ReportsMissingParameterKey()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.t_01_01");
            var controller = new LevelEditController(data);
            controller.AddObject(LevelPaletteCatalog.WallPrefabId, Vector2.zero, out LevelSelection selection);
            controller.TryGetObject(selection.ObjectId, out StageObjectData item);
            item.Parameters.Add(new ParameterData { Key = " ", Value = "1" });

            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(data);

            Assert.That(Codes(issues), Does.Contain(LevelValidationCode.ObjectParameterKeyMissing));
        }

        /// <summary>控制器编辑后校验必须仍然通过, 保证编辑操作不会引入问题。</summary>
        [Test]
        public void Validate_AcceptsLevelEditedThroughController()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.t_01_01");
            var controller = new LevelEditController(data);
            controller.AddRectZone(ZoneKind.Deployable, -10f, -8f, 10f, -6f, out _);
            controller.AddRectZone(ZoneKind.Forbidden, -2f, -8f, 2f, -7f, out _);
            controller.AddObject(LevelPaletteCatalog.GroundPrefabId, new Vector2(0f, -8f), out LevelSelection ground);
            controller.SetObjectScale(ground.ObjectId, 6f, 1f);
            controller.AddObject(LevelPaletteCatalog.CargoPrefabId, new Vector2(0f, 2f), out LevelSelection cargo);
            controller.SetObjectParameter(cargo.ObjectId, "mass", "3");
            controller.SetStartPoint(new Vector2(-8f, -7f), 0f);
            controller.SetGoalPoint(new Vector2(8f, -7f), 2f, 2f);

            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(data);

            Assert.That(issues, Is.Empty, Describe(issues));
        }

        /// <summary>校验结果缓存必须只读取数据, 不改动关卡内容。</summary>
        [Test]
        public void Validate_DoesNotMutateData()
        {
            LevelAuthoringData data = LevelAuthoringFactory.CreateNew("official.level.t_01_01");
            var controller = new LevelEditController(data);
            controller.AddObject(LevelPaletteCatalog.WallPrefabId, Vector2.zero, out _);
            string before = LevelAuthoringSerializer.Serialize(data);

            LevelEditValidator.Validate(data);

            Assert.That(LevelAuthoringSerializer.Serialize(data), Is.EqualTo(before));
        }

        /// <summary>问题列表为空时 IsValid 必须为真; 为 null 也按无问题处理。</summary>
        [Test]
        public void IsValid_HandlesEmptyAndNull()
        {
            Assert.That(LevelEditValidator.IsValid(null), Is.True);
            Assert.That(LevelEditValidator.IsValid(new List<LevelValidationIssue>()), Is.True);
            Assert.That(
                LevelEditValidator.IsValid(
                    new List<LevelValidationIssue>
                    {
                        new LevelValidationIssue(LevelValidationCode.MissingStartPoint, LevelSelection.None, "x"),
                    }
                ),
                Is.False
            );
        }

        /// <summary>取出问题列表中的问题种类集合。</summary>
        /// <param name="issues">问题列表。</param>
        /// <returns>问题种类序列。</returns>
        private static IReadOnlyCollection<LevelValidationCode> Codes(IReadOnlyList<LevelValidationIssue> issues) =>
            issues.Select(issue => issue.Code).ToList();

        /// <summary>取出指定种类的第一条问题的编辑目标。</summary>
        /// <param name="issues">问题列表。</param>
        /// <param name="code">问题种类。</param>
        /// <returns>该种类第一条问题的目标; 未出现时返回 <see cref="LevelSelection.None"/>。</returns>
        private static LevelSelection TargetOf(IReadOnlyList<LevelValidationIssue> issues, LevelValidationCode code)
        {
            foreach (LevelValidationIssue issue in issues)
            {
                if (issue.Code == code)
                    return issue.Target;
            }
            return LevelSelection.None;
        }

        /// <summary>把问题列表整理为便于断言失败时阅读的文本。</summary>
        /// <param name="issues">问题列表。</param>
        /// <returns>逐条问题描述。</returns>
        private static string Describe(IReadOnlyList<LevelValidationIssue> issues) =>
            issues == null || issues.Count == 0
                ? "(无问题)"
                : string.Join("; ", issues.Select(issue => issue.Code + "@" + issue.Target + ": " + issue.Message));
    }
}
