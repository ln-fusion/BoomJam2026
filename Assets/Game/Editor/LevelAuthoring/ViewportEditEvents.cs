using UnityEngine;

namespace Game.Editor.Level
{
    /// <summary>
    /// 视口拖动编辑目标时上报的一帧更新。
    /// </summary>
    /// <remarks>
    /// <see cref="Position"/> 是拖动目标的绝对世界坐标（已含抓取偏移, 因此拖动不会跳变）;
    /// <see cref="Delta"/> 是相对上一帧的位移, 供需要增量语义的操作（例如整体平移区域）使用。
    /// 窗口据此调用 <see cref="LevelEditController"/> 的对应方法, 视口自身不修改关卡数据。
    /// </remarks>
    public readonly struct ViewportDragUpdate
    {
        /// <summary>被拖动的编辑目标。</summary>
        public LevelSelection Selection { get; }

        /// <summary>目标的世界坐标。</summary>
        public Vector2 Position { get; }

        /// <summary>相对上一帧的世界位移。</summary>
        public Vector2 Delta { get; }

        /// <summary>创建一帧拖动更新。</summary>
        /// <param name="selection">被拖动的编辑目标。</param>
        /// <param name="position">目标的世界坐标。</param>
        /// <param name="delta">相对上一帧的世界位移。</param>
        public ViewportDragUpdate(LevelSelection selection, Vector2 position, Vector2 delta)
        {
            Selection = selection;
            Position = position;
            Delta = delta;
        }
    }

    /// <summary>
    /// 在区域边上插入顶点的请求。
    /// </summary>
    /// <remarks>
    /// <see cref="Position"/> 是鼠标位置在该边上的投影, 使新顶点落在原边上;
    /// <see cref="InsertIndex"/> 是该边终点顶点的下标, 直接作为插入位置即可把边一分为二。
    /// </remarks>
    public readonly struct ViewportVertexInsertRequest
    {
        /// <summary>区域种类。</summary>
        public ZoneKind ZoneKind { get; }

        /// <summary>区域下标。</summary>
        public int ZoneIndex { get; }

        /// <summary>插入位置; 等于该边终点顶点的下标。</summary>
        public int InsertIndex { get; }

        /// <summary>新顶点的世界坐标。</summary>
        public Vector2 Position { get; }

        /// <summary>创建插入顶点请求。</summary>
        /// <param name="zoneKind">区域种类。</param>
        /// <param name="zoneIndex">区域下标。</param>
        /// <param name="insertIndex">插入位置。</param>
        /// <param name="position">新顶点世界坐标。</param>
        public ViewportVertexInsertRequest(ZoneKind zoneKind, int zoneIndex, int insertIndex, Vector2 position)
        {
            ZoneKind = zoneKind;
            ZoneIndex = zoneIndex;
            InsertIndex = insertIndex;
            Position = position;
        }
    }
}
