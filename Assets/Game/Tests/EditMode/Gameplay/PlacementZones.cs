using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Contracts.Gameplay;
using Game.Gameplay.Deployment;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// 放置几何测试共用的区域与矩形构造器。
    /// </summary>
    internal static class PlacementZones
    {
        /// <summary>构造一个区域多边形。</summary>
        /// <param name="zoneId">区域稳定标识。</param>
        /// <param name="vertices">按顺序排列的顶点; 允许为空以覆盖退化数据。</param>
        /// <returns>区域数据。</returns>
        internal static ZoneData Zone(string zoneId, params (float X, float Y)[] vertices)
        {
            var zone = new ZoneData { ZoneId = zoneId, Vertices = new List<PointData>() };
            foreach ((float x, float y) in vertices)
                zone.Vertices.Add(new PointData { X = x, Y = y });
            return zone;
        }

        /// <summary>构造以指定中心与边长生成的正方形区域。</summary>
        /// <param name="zoneId">区域稳定标识。</param>
        /// <param name="centerX">中心 X。</param>
        /// <param name="centerY">中心 Y。</param>
        /// <param name="size">边长。</param>
        /// <param name="clockwise">是否按顺时针给点; 用于验证绕向无关。</param>
        /// <returns>正方形区域。</returns>
        internal static ZoneData Square(string zoneId, float centerX, float centerY, float size, bool clockwise = false)
        {
            float half = size * 0.5f;
            float minX = centerX - half;
            float maxX = centerX + half;
            float minY = centerY - half;
            float maxY = centerY + half;
            return clockwise
                ? Zone(zoneId, (minX, minY), (minX, maxY), (maxX, maxY), (maxX, minY))
                : Zone(zoneId, (minX, minY), (maxX, minY), (maxX, maxY), (minX, maxY));
        }

        /// <summary>构造只包含区域集合的关卡定义。</summary>
        /// <param name="deployable">可部署区域集合。</param>
        /// <param name="forbidden">禁放区域集合。</param>
        /// <returns>关卡定义。</returns>
        internal static LevelDefinition Level(ZoneData[] deployable, ZoneData[] forbidden = null)
        {
            return new LevelDefinition
            {
                LevelId = "official.level.geometry_test",
                DeployableZones = deployable == null ? new List<ZoneData>() : new List<ZoneData>(deployable),
                ForbiddenZones = forbidden == null ? new List<ZoneData>() : new List<ZoneData>(forbidden),
            };
        }

        /// <summary>构造一个必须合法的放置矩形, 夹具数据非法时立即判定测试失败。</summary>
        /// <param name="centerX">中心 X。</param>
        /// <param name="centerY">中心 Y。</param>
        /// <param name="width">全宽。</param>
        /// <param name="height">全高。</param>
        /// <returns>放置矩形。</returns>
        internal static PlacementRect Rect(float centerX, float centerY, float width, float height)
        {
            PlacementError error = PlacementRect.TryCreate(
                new Vector2(centerX, centerY),
                width,
                height,
                out PlacementRect rect
            );
            Assert.That(error, Is.EqualTo(PlacementError.None), "测试夹具构造的矩形必须合法。");
            return rect;
        }

        /// <summary>构造默认的几何判定实现。</summary>
        /// <returns>新的几何判定实例。</returns>
        internal static IPlacementGeometry Geometry() => new PlacementGeometry();

        /// <summary>构造默认的校验器。</summary>
        /// <returns>新的校验器实例。</returns>
        internal static IPlacementValidator Validator() => new PlacementValidator(new PlacementGeometry());
    }
}
