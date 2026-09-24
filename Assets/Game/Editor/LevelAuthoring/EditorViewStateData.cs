using System;
using System.Collections.Generic;

namespace Game.Editor.Level
{
    /// <summary>
    /// 视图状态数据; 用于保存关卡编辑器视口快照, 不属于关卡玩法内容。
    /// </summary>
    /// <remarks>
    /// 选中项按 <see cref="SelectedKind"/> 解释。C20 只保存对象稳定 ID,
    /// C21/C22 起扩展到区域、区域顶点与世界边界角; 旧文件缺少种类字段时
    /// 按"非空 <see cref="SelectedObjectId"/> 即对象选中"还原（见
    /// <see cref="ResolveSelection"/>）。
    /// </remarks>
    [Serializable]
    public sealed class EditorViewStateData
    {
        /// <summary>视口中心 X 坐标。</summary>
        public float ViewCenterX;

        /// <summary>视口中心 Y 坐标。</summary>
        public float ViewCenterY;

        /// <summary>视口缩放值。</summary>
        public float Zoom = 1f;

        /// <summary>选中项种类; 缺省为无选中。</summary>
        public LevelSelectionKind SelectedKind = LevelSelectionKind.None;

        /// <summary>当前选中对象稳定 ID; 为空表示无选中。仅对象选中时使用。</summary>
        public string SelectedObjectId;

        /// <summary>选中区域所属种类; 仅区域相关选中时使用。</summary>
        public ZoneKind SelectedZoneKind = ZoneKind.Deployable;

        /// <summary>选中区域下标; 无区域选中时为 -1。</summary>
        public int SelectedZoneIndex = -1;

        /// <summary>选中顶点下标; 非顶点选中时为 -1。</summary>
        public int SelectedVertexIndex = -1;

        /// <summary>选中的世界边界角; 仅世界边界选中时使用。</summary>
        public WorldBoundsCorner SelectedCorner = WorldBoundsCorner.LeftBottom;

        /// <summary>是否处于未保存修改状态（不持久化, 仅会话内使用）。</summary>
        [NonSerialized]
        public bool IsDirty;

        /// <summary>把选中状态写入本对象。</summary>
        /// <param name="selection">待保存的选中项。</param>
        public void CaptureSelection(LevelSelection selection)
        {
            SelectedKind = selection.Kind;
            SelectedObjectId = selection.ObjectId ?? string.Empty;
            SelectedZoneKind = selection.ZoneKind;
            SelectedZoneIndex = selection.ZoneIndex;
            SelectedVertexIndex = selection.VertexIndex;
            SelectedCorner = selection.Corner;
        }

        /// <summary>还原选中状态; 兼容只写了 <see cref="SelectedObjectId"/> 的旧文件。</summary>
        /// <returns>还原出的选中项。</returns>
        public LevelSelection ResolveSelection()
        {
            if (SelectedKind == LevelSelectionKind.None)
            {
                // C20 写出的文件没有种类字段, 只按对象 ID 还原; 其余情况视为无选中。
                return string.IsNullOrEmpty(SelectedObjectId)
                    ? LevelSelection.None
                    : LevelSelection.StageObject(SelectedObjectId);
            }
            switch (SelectedKind)
            {
                case LevelSelectionKind.StageObject:
                    return LevelSelection.StageObject(SelectedObjectId);
                case LevelSelectionKind.SpawnPoint:
                    return LevelSelection.SpawnPoint();
                case LevelSelectionKind.GoalPoint:
                    return LevelSelection.GoalPoint();
                case LevelSelectionKind.Zone:
                    return LevelSelection.Zone(SelectedZoneKind, SelectedZoneIndex);
                case LevelSelectionKind.ZoneVertex:
                    return LevelSelection.ZoneVertex(SelectedZoneKind, SelectedZoneIndex, SelectedVertexIndex);
                case LevelSelectionKind.WorldBoundsCorner:
                    return LevelSelection.WorldBounds(SelectedCorner);
                default:
                    return LevelSelection.None;
            }
        }
    }
}
