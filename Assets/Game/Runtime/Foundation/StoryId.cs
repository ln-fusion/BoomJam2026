namespace Game.Foundation
{
    /// <summary>
    /// 剧情稳定 ID：对应剧情资源目录中的稳定标识.
    /// </summary>
    [System.Serializable]
    public sealed class StoryId : StrongId<StoryId>
    {
        /// <summary>创建剧情稳定标识。</summary>
        /// <param name="value">稳定 ID（如 official.story.prologue）。</param>
        public StoryId(string value)
            : base(value) { }
    }
}
