namespace Game.Foundation
{
    /// <summary>
    /// 角色稳定 ID：对应角色/立绘资源目录中的稳定标识.
    /// </summary>
    [System.Serializable]
    public sealed class CharacterId : StrongId<CharacterId>
    {
        /// <param name="value">稳定 ID（如 official.character.hani）</param>
        public CharacterId(string value)
            : base(value) { }
    }
}
