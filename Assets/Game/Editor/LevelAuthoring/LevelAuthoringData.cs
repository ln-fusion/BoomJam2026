using System;
using Game.Contracts.Content;

namespace Game.Editor.Level
{
    /// <summary>
    /// 关卡 Authoring 数据; 包含关卡定义与 Editor-only 的视口元数据。
    /// </summary>
    [Serializable]
    public sealed class LevelAuthoringData
    {
        /// <summary>Authoring 文件格式版本; 当前为 1。</summary>
        public int FormatVersion = 1;

        /// <summary>内容修订号; 每次保存递增, 参与编译摘要。</summary>
        public int ContentRevision = 1;

        /// <summary>编写中的关卡定义。</summary>
        public LevelDefinition Definition;

        /// <summary>编辑器视口状态; 编译时排除, 不进入 Generated。</summary>
        public EditorViewStateData EditorViewState;
    }
}
