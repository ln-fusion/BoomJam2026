using System;
using System.Collections.Generic;

namespace Game.Editor.Level
{
    /// <summary>
    /// 视图状态数据; 用于保存关卡编辑器视口快照, 不属于关卡玩法内容。
    /// </summary>
    [Serializable]
    public sealed class EditorViewStateData
    {
        /// <summary>视口中心 X 坐标。</summary>
        public float ViewCenterX;

        /// <summary>视口中心 Y 坐标。</summary>
        public float ViewCenterY;

        /// <summary>视口缩放值。</summary>
        public float Zoom = 1f;

        /// <summary>当前选中对象稳定 ID; 为空表示无选中。</summary>
        public string SelectedObjectId;

        /// <summary>是否处于未保存修改状态（不持久化, 仅会话内使用）。</summary>
        [NonSerialized]
        public bool IsDirty;
    }
}
