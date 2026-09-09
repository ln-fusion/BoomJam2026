namespace Game.Foundation
{
    /// <summary>
    /// 角色形象稳定 ID，与立绘资源目录中的标识对应。
    /// </summary>
    [System.Serializable]
    public sealed class AppearanceId : StrongId<AppearanceId>
    {
        /// <summary>创建角色形象稳定标识。</summary>
        /// <param name="value">稳定 ID（如 official.appearance.hani.casual）。</param>
        public AppearanceId(string value)
            : base(value) { }
    }
}
