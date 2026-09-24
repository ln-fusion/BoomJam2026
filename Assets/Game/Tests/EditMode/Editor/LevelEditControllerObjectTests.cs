using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Editor.Level;
using Game.Foundation;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Editor
{
    /// <summary>
    /// 验证关卡编辑控制器的对象与起终点操作: 调色板约束、稳定 ID、白名单参数与选择定位。
    /// </summary>
    public sealed class LevelEditControllerObjectTests
    {
        /// <summary>调色板外的预制体 ID 必须被拒绝, 否则构建世界时会以 NotFound 失败。</summary>
        [Test]
        public void AddObject_RejectsUnregisteredPrefab()
        {
            LevelEditController controller = NewController();

            Result result = controller.AddObject("official.prefab.not_registered", Vector2.zero, out _);

            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.NotFound));
            Assert.That(controller.Definition.Objects, Is.Empty);
        }

        /// <summary>空或空白预制体 ID 必须被拒绝。</summary>
        [Test]
        public void AddObject_RejectsEmptyPrefabId()
        {
            LevelEditController controller = NewController();

            Assert.That(controller.AddObject(null, Vector2.zero, out _).ErrorCode, Is.EqualTo(ErrorCode.NotFound));
            Assert.That(controller.AddObject("   ", Vector2.zero, out _).ErrorCode, Is.EqualTo(ErrorCode.NotFound));
        }

        /// <summary>登记过的预制体必须能创建对象, 并初始化全部字段。</summary>
        [Test]
        public void AddObject_CreatesObjectWithDefaults()
        {
            LevelEditController controller = NewController();

            Result result = controller.AddObject(
                LevelPaletteCatalog.GroundPrefabId,
                new Vector2(3f, -2f),
                out LevelSelection selection
            );

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(controller.Definition.Objects, Has.Count.EqualTo(1));
            StageObjectData data = controller.Definition.Objects[0];
            Assert.That(data.PrefabId, Is.EqualTo(LevelPaletteCatalog.GroundPrefabId));
            Assert.That(data.PositionX, Is.EqualTo(3f));
            Assert.That(data.PositionY, Is.EqualTo(-2f));
            Assert.That(data.RotationZ, Is.EqualTo(0f));
            Assert.That(data.ScaleX, Is.EqualTo(1f));
            Assert.That(data.ScaleY, Is.EqualTo(1f));
            Assert.That(data.Parameters, Is.Not.Null);
            Assert.That(data.Parameters, Is.Empty);
            Assert.That(selection.Kind, Is.EqualTo(LevelSelectionKind.StageObject));
            Assert.That(selection.ObjectId, Is.EqualTo(data.ObjectId));
        }

        /// <summary>对象稳定 ID 必须包含预制体短名并从 01 开始。</summary>
        [Test]
        public void AddObject_BuildsStableIdFromPrefabShortName()
        {
            LevelEditController controller = NewController();

            controller.AddObject(LevelPaletteCatalog.WallPrefabId, Vector2.zero, out LevelSelection first);
            controller.AddObject(LevelPaletteCatalog.WallPrefabId, Vector2.one, out LevelSelection second);

            Assert.That(first.ObjectId, Is.EqualTo("object.wall_block.01"));
            Assert.That(second.ObjectId, Is.EqualTo("object.wall_block.02"));
        }

        /// <summary>不同预制体各有独立序号, 互不干扰。</summary>
        [Test]
        public void AddObject_SequencesArePerPrefab()
        {
            LevelEditController controller = NewController();

            controller.AddObject(LevelPaletteCatalog.WallPrefabId, Vector2.zero, out _);
            controller.AddObject(LevelPaletteCatalog.CargoPrefabId, Vector2.zero, out LevelSelection cargo);

            Assert.That(cargo.ObjectId, Is.EqualTo("object.cargo_box.01"));
        }

        /// <summary>删除中间对象后再新增不得产生重复 ID。</summary>
        [Test]
        public void AddObject_AvoidsIdCollisionAfterRemoval()
        {
            LevelEditController controller = NewController();
            controller.AddObject(LevelPaletteCatalog.WallPrefabId, Vector2.zero, out LevelSelection first);
            controller.AddObject(LevelPaletteCatalog.WallPrefabId, Vector2.one, out _);
            controller.RemoveObject(first.ObjectId);

            controller.AddObject(LevelPaletteCatalog.WallPrefabId, Vector2.one, out LevelSelection third);

            var ids = new List<string>();
            foreach (StageObjectData item in controller.Definition.Objects)
                ids.Add(item.ObjectId);
            Assert.That(ids, Is.Unique);
            Assert.That(third.ObjectId, Is.EqualTo("object.wall_block.03"));
        }

        /// <summary>五类登记预制体都必须可创建, 防止调色板与命名约定脱节。</summary>
        /// <param name="prefabId">预制体稳定标识。</param>
        [TestCase(LevelPaletteCatalog.GroundPrefabId)]
        [TestCase(LevelPaletteCatalog.WallPrefabId)]
        [TestCase(LevelPaletteCatalog.RampPrefabId)]
        [TestCase(LevelPaletteCatalog.CargoPrefabId)]
        [TestCase(LevelPaletteCatalog.HazardPrefabId)]
        public void AddObject_AcceptsEveryRegisteredPrefab(string prefabId)
        {
            LevelEditController controller = NewController();

            Result result = controller.AddObject(prefabId, Vector2.zero, out _);

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(LevelPaletteCatalog.IsKnownStageObjectPrefab(prefabId), Is.True);
        }

        /// <summary>坐标非有限时必须拒绝。</summary>
        [Test]
        public void AddObject_RejectsNonFinitePosition()
        {
            LevelEditController controller = NewController();

            Result result = controller.AddObject(LevelPaletteCatalog.WallPrefabId, new Vector2(float.NaN, 0f), out _);

            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.InvalidArgument));
        }

        /// <summary>移动对象必须只改目标对象。</summary>
        [Test]
        public void SetObjectPosition_MovesOnlyTarget()
        {
            LevelEditController controller = NewController();
            controller.AddObject(LevelPaletteCatalog.WallPrefabId, Vector2.zero, out LevelSelection first);
            controller.AddObject(LevelPaletteCatalog.WallPrefabId, new Vector2(5f, 5f), out LevelSelection second);

            Result result = controller.SetObjectPosition(first.ObjectId, new Vector2(-3f, 7f));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(controller.TryGetObject(first.ObjectId, out StageObjectData firstData), Is.True);
            Assert.That(firstData.PositionX, Is.EqualTo(-3f));
            Assert.That(firstData.PositionY, Is.EqualTo(7f));
            Assert.That(controller.TryGetObject(second.ObjectId, out StageObjectData secondData), Is.True);
            Assert.That(secondData.PositionX, Is.EqualTo(5f));
        }

        /// <summary>不存在的对象 ID 必须报未找到。</summary>
        [Test]
        public void SetObjectPosition_RejectsUnknownId()
        {
            LevelEditController controller = NewController();

            Assert.That(
                controller.SetObjectPosition("object.none.01", Vector2.zero).ErrorCode,
                Is.EqualTo(ErrorCode.NotFound)
            );
        }

        /// <summary>旋转角度非有限时必须拒绝。</summary>
        [Test]
        public void SetObjectRotation_RejectsNonFinite()
        {
            LevelEditController controller = NewController();
            controller.AddObject(LevelPaletteCatalog.WallPrefabId, Vector2.zero, out LevelSelection selection);

            Assert.That(
                controller.SetObjectRotation(selection.ObjectId, float.NaN).ErrorCode,
                Is.EqualTo(ErrorCode.InvalidArgument)
            );
        }

        /// <summary>关卡静态对象允许非等比缩放与旋转。</summary>
        [Test]
        public void SetObjectRotationAndScale_AllowsAuthoringValues()
        {
            LevelEditController controller = NewController();
            controller.AddObject(LevelPaletteCatalog.RampPrefabId, Vector2.zero, out LevelSelection selection);

            Assert.That(controller.SetObjectRotation(selection.ObjectId, 30f).IsSuccess, Is.True);
            Assert.That(controller.SetObjectScale(selection.ObjectId, 3f, 0.5f).IsSuccess, Is.True);

            controller.TryGetObject(selection.ObjectId, out StageObjectData data);
            Assert.That(data.RotationZ, Is.EqualTo(30f));
            Assert.That(data.ScaleX, Is.EqualTo(3f));
            Assert.That(data.ScaleY, Is.EqualTo(0.5f));
        }

        /// <summary>非正缩放会让对象退化为不可见或反相, 必须拒绝。</summary>
        [Test]
        public void SetObjectScale_RejectsNonPositive()
        {
            LevelEditController controller = NewController();
            controller.AddObject(LevelPaletteCatalog.WallPrefabId, Vector2.zero, out LevelSelection selection);

            Assert.That(
                controller.SetObjectScale(selection.ObjectId, 0f, 1f).ErrorCode,
                Is.EqualTo(ErrorCode.InvalidArgument)
            );
            Assert.That(
                controller.SetObjectScale(selection.ObjectId, 1f, -1f).ErrorCode,
                Is.EqualTo(ErrorCode.InvalidArgument)
            );
        }

        /// <summary>新增参数必须追加, 重复写入同一键必须覆盖而不是产生重复键。</summary>
        [Test]
        public void SetObjectParameter_AddsThenOverwrites()
        {
            LevelEditController controller = NewController();
            controller.AddObject(LevelPaletteCatalog.CargoPrefabId, Vector2.zero, out LevelSelection selection);

            Assert.That(controller.SetObjectParameter(selection.ObjectId, "mass", "2").IsSuccess, Is.True);
            Assert.That(controller.SetObjectParameter(selection.ObjectId, "mass", "5").IsSuccess, Is.True);

            controller.TryGetObject(selection.ObjectId, out StageObjectData data);
            Assert.That(data.Parameters, Has.Count.EqualTo(1));
            Assert.That(data.Parameters[0].Key, Is.EqualTo("mass"));
            Assert.That(data.Parameters[0].Value, Is.EqualTo("5"));
        }

        /// <summary>参数键不能为空或含首尾空白。</summary>
        [Test]
        public void SetObjectParameter_RejectsInvalidKey()
        {
            LevelEditController controller = NewController();
            controller.AddObject(LevelPaletteCatalog.CargoPrefabId, Vector2.zero, out LevelSelection selection);

            Assert.That(
                controller.SetObjectParameter(selection.ObjectId, null, "1").ErrorCode,
                Is.EqualTo(ErrorCode.InvalidArgument)
            );
            Assert.That(
                controller.SetObjectParameter(selection.ObjectId, "  ", "1").ErrorCode,
                Is.EqualTo(ErrorCode.InvalidArgument)
            );
            Assert.That(
                controller.SetObjectParameter(selection.ObjectId, " mass ", "1").ErrorCode,
                Is.EqualTo(ErrorCode.InvalidArgument)
            );
        }

        /// <summary>参数值可以为空字符串, 表示该键无值。</summary>
        [Test]
        public void SetObjectParameter_AllowsEmptyValue()
        {
            LevelEditController controller = NewController();
            controller.AddObject(LevelPaletteCatalog.CargoPrefabId, Vector2.zero, out LevelSelection selection);

            Result result = controller.SetObjectParameter(selection.ObjectId, "tag", null);

            Assert.That(result.IsSuccess, Is.True);
            controller.TryGetObject(selection.ObjectId, out StageObjectData data);
            Assert.That(data.Parameters[0].Value, Is.EqualTo(string.Empty));
        }

        /// <summary>删除参数必须只删除命中的键。</summary>
        [Test]
        public void RemoveObjectParameter_RemovesOnlyTargetKey()
        {
            LevelEditController controller = NewController();
            controller.AddObject(LevelPaletteCatalog.CargoPrefabId, Vector2.zero, out LevelSelection selection);
            controller.SetObjectParameter(selection.ObjectId, "mass", "2");
            controller.SetObjectParameter(selection.ObjectId, "friction", "1");

            Result result = controller.RemoveObjectParameter(selection.ObjectId, "mass");

            Assert.That(result.IsSuccess, Is.True);
            controller.TryGetObject(selection.ObjectId, out StageObjectData data);
            Assert.That(data.Parameters, Has.Count.EqualTo(1));
            Assert.That(data.Parameters[0].Key, Is.EqualTo("friction"));
        }

        /// <summary>删除不存在的参数键必须报未找到。</summary>
        [Test]
        public void RemoveObjectParameter_RejectsUnknownKey()
        {
            LevelEditController controller = NewController();
            controller.AddObject(LevelPaletteCatalog.CargoPrefabId, Vector2.zero, out LevelSelection selection);

            Assert.That(
                controller.RemoveObjectParameter(selection.ObjectId, "nope").ErrorCode,
                Is.EqualTo(ErrorCode.NotFound)
            );
        }

        /// <summary>删除对象必须只删除目标对象。</summary>
        [Test]
        public void RemoveObject_RemovesOnlyTarget()
        {
            LevelEditController controller = NewController();
            controller.AddObject(LevelPaletteCatalog.WallPrefabId, Vector2.zero, out LevelSelection first);
            controller.AddObject(LevelPaletteCatalog.WallPrefabId, Vector2.one, out LevelSelection second);

            Result result = controller.RemoveObject(first.ObjectId);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(controller.Definition.Objects, Has.Count.EqualTo(1));
            Assert.That(controller.Definition.Objects[0].ObjectId, Is.EqualTo(second.ObjectId));
        }

        /// <summary>起点缺失时必须能创建, 而不是要求先手工补数据。</summary>
        [Test]
        public void SetStartPoint_CreatesWhenMissing()
        {
            LevelEditController controller = NewController();

            Result result = controller.SetStartPoint(new Vector2(-5f, 1f), 45f);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(controller.Definition.StartPoint, Is.Not.Null);
            Assert.That(controller.Definition.StartPoint.PositionX, Is.EqualTo(-5f));
            Assert.That(controller.Definition.StartPoint.PositionY, Is.EqualTo(1f));
            Assert.That(controller.Definition.StartPoint.RotationZ, Is.EqualTo(45f));
        }

        /// <summary>角度非有限时必须拒绝。</summary>
        [Test]
        public void SetStartPoint_RejectsNonFiniteRotation()
        {
            LevelEditController controller = NewController();

            Assert.That(
                controller.SetStartPoint(Vector2.zero, float.PositiveInfinity).ErrorCode,
                Is.EqualTo(ErrorCode.InvalidArgument)
            );
            Assert.That(controller.Definition.StartPoint, Is.Null, "失败时不得留下部分创建的起点。");
        }

        /// <summary>终点缺失时必须能创建。</summary>
        [Test]
        public void SetGoalPoint_CreatesWhenMissing()
        {
            LevelEditController controller = NewController();

            Result result = controller.SetGoalPoint(new Vector2(8f, -1f), 2f, 3f);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(controller.Definition.GoalPoint.Width, Is.EqualTo(2f));
            Assert.That(controller.Definition.GoalPoint.Height, Is.EqualTo(3f));
        }

        /// <summary>终点宽高必须为正。</summary>
        [Test]
        public void SetGoalPoint_RejectsNonPositiveSize()
        {
            LevelEditController controller = NewController();

            Assert.That(controller.SetGoalPoint(Vector2.zero, 0f, 1f).ErrorCode, Is.EqualTo(ErrorCode.InvalidArgument));
            Assert.That(
                controller.SetGoalPoint(Vector2.zero, 1f, -2f).ErrorCode,
                Is.EqualTo(ErrorCode.InvalidArgument)
            );
            Assert.That(controller.Definition.GoalPoint, Is.Null);
        }

        /// <summary>删除起点与终点必须可用, 以便校验报出缺失。</summary>
        [Test]
        public void RemoveStartAndGoalPoint_ReportsNotFoundWhenAbsent()
        {
            LevelEditController controller = NewController();
            controller.SetStartPoint(Vector2.zero, 0f);
            controller.SetGoalPoint(Vector2.zero, 1f, 1f);

            Assert.That(controller.RemoveStartPoint().IsSuccess, Is.True);
            Assert.That(controller.RemoveGoalPoint().IsSuccess, Is.True);

            Assert.That(controller.RemoveStartPoint().ErrorCode, Is.EqualTo(ErrorCode.NotFound));
            Assert.That(controller.RemoveGoalPoint().ErrorCode, Is.EqualTo(ErrorCode.NotFound));
        }

        /// <summary>同一份数据上重复分配必须得到同一结果, 保证保存可复现。</summary>
        [Test]
        public void AllocateObjectId_IsDeterministic()
        {
            var data = new LevelAuthoringData
            {
                Definition = new LevelDefinition { LevelId = "official.level.t_01_01" },
            };
            var controller = new LevelEditController(data);
            controller.AddObject(LevelPaletteCatalog.WallPrefabId, Vector2.zero, out _);

            string firstCall = controller.AllocateObjectId(LevelPaletteCatalog.WallPrefabId);
            string secondCall = controller.AllocateObjectId(LevelPaletteCatalog.WallPrefabId);

            Assert.That(secondCall, Is.EqualTo(firstCall), "未改动数据时两次分配必须一致。");
        }

        /// <summary>预制体短名必须从稳定 ID 末段提取并清洗非法字符。</summary>
        /// <param name="prefabId">预制体稳定标识。</param>
        /// <param name="expected">期望短名。</param>
        [TestCase("official.prefab.ground_platform", "ground_platform")]
        [TestCase("official.prefab.wall_block", "wall_block")]
        [TestCase("single", "single")]
        [TestCase("", "obj")]
        [TestCase(null, "obj")]
        [TestCase("   ", "obj")]
        [TestCase("official.prefab.", "obj")]
        [TestCase("official.prefab.some-thing", "some_thing")]
        public void GetPrefabShortName_SanitizesTailSegment(string prefabId, string expected)
        {
            Assert.That(LevelPaletteCatalog.GetPrefabShortName(prefabId), Is.EqualTo(expected));
        }

        /// <summary>构造一个空关卡控制器。</summary>
        /// <returns>编辑控制器。</returns>
        private static LevelEditController NewController() =>
            new LevelEditController(
                new LevelAuthoringData { Definition = new LevelDefinition { LevelId = "official.level.t_01_01" } }
            );
    }
}
