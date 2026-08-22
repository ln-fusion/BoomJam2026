namespace Game.Foundation
{
    /// <summary>
    /// 档案条目稳定 ID：对应档案内容目录中的稳定标识.
    /// </summary>
    [System.Serializable]
    public sealed class ArchiveEntryId : StrongId<ArchiveEntryId>
    {
        /// <summary>创建档案条目稳定标识。</summary>
        /// <param name="value">稳定 ID（如 official.archive.char_hani_01）。</param>
        public ArchiveEntryId(string value)
            : base(value) { }
    }
}
