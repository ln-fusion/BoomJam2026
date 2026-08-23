namespace Game.Foundation
{
    /// <summary>
    /// 关卡稳定 ID，对应内容目录中关卡资源的标识。
    /// </summary>
    [System.Serializable]
    public sealed class LevelId : StrongId<LevelId>
    {
        /// <summary>创建关卡稳定标识。</summary>
        /// <param name="value">稳定 ID（如 official.level.factory_001）。</param>
        public LevelId(string value)
            : base(value) { }
    }
}
