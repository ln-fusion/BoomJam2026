using System;
using System.Collections.Generic;
using Game.Foundation;
using UnityEngine;

namespace Game.Contracts.Content
{
    /// <summary>
    /// 内容来源类别，区分随安装包发布的官方内容和用户生成内容。
    /// </summary>
    public enum ContentSource
    {
        /// <summary>随官方安装包或补丁发布的受控内容。</summary>
        Official,
        /// <summary>玩家或创意工坊提供的用户生成内容；首发仅保留边界。</summary>
        Ugc
    }

    /// <summary>
    /// 内容包头信息，记录格式版本、来源、兼容版本和载荷校验信息。
    /// </summary>
    [Serializable]
    public sealed class ContentHeader
    {
        /// <summary>内容包的稳定标识。</summary>
        public string ContentId;
        /// <summary>内容来源，官方目录默认为 <see cref="ContentSource.Official"/>。</summary>
        public ContentSource Source = ContentSource.Official;
        /// <summary>内容数据格式版本；当前官方运行时只接受版本 1。</summary>
        public int FormatVersion = 1;
        /// <summary>内容修订号，用于成绩、兼容性和补丁诊断。</summary>
        public int ContentRevision = 1;
        /// <summary>允许读取该内容的最低游戏版本。</summary>
        public string MinGameVersion;
        /// <summary>该内容最后验证通过的游戏版本。</summary>
        public string MaxTestedGameVersion;
        /// <summary>内容载荷的 SHA-256 摘要，供加载校验使用。</summary>
        public string PayloadSha256;
    }

    /// <summary>
    /// 内容兼容性检查结果。
    /// </summary>
    public enum ContentCompatibility
    {
        /// <summary>内容头满足当前运行时的最小兼容要求。</summary>
        Compatible,
        /// <summary>内容头缺失，无法判断内容包来源或版本。</summary>
        MissingHeader,
        /// <summary>内容格式版本不被当前运行时支持。</summary>
        UnsupportedFormat,
        /// <summary>内容来源不符合当前服务期望。</summary>
        WrongSource,
        /// <summary>内容头或载荷缺少必需字段。</summary>
        InvalidPayload
    }

    /// <summary>
    /// 关卡选择界面使用的轻量级关卡摘要。
    /// </summary>
    [Serializable]
    public sealed class LevelSummary
    {
        /// <summary>关卡稳定标识。</summary>
        public string LevelId;
        /// <summary>关卡所属地图稳定标识。</summary>
        public string MapId;
        /// <summary>本地化显示名称键。</summary>
        public string DisplayNameKey;
        /// <summary>同一地图内的显示排序值。</summary>
        public int SortOrder;
    }

    /// <summary>
    /// 运行时关卡定义的最小数据模型。
    /// </summary>
    /// <remarks>
    /// 当前 C04/C05 阶段只保存关卡目录和显示所需字段，玩法实体数据会在后续关卡编辑器阶段扩展。
    /// </remarks>
    [Serializable]
    public sealed class LevelDefinition
    {
        /// <summary>关卡所属内容包头信息。</summary>
        public ContentHeader Header;
        /// <summary>关卡稳定标识。</summary>
        public string LevelId;
        /// <summary>关卡所属地图稳定标识。</summary>
        public string MapId;
        /// <summary>本地化显示名称键。</summary>
        public string DisplayNameKey;
        /// <summary>同一地图内的显示排序值。</summary>
        public int SortOrder;
        /// <summary>由完整定义派生的关卡选择摘要。</summary>
        public LevelSummary Summary => new LevelSummary
        {
            LevelId = LevelId,
            MapId = MapId,
            DisplayNameKey = DisplayNameKey,
            SortOrder = SortOrder
        };
    }

    /// <summary>
    /// 剧情节点类型。
    /// </summary>
    public enum StoryNodeType
    {
        /// <summary>播放一段剧情文本后进入后续节点。</summary>
        Dialogue,
        /// <summary>剧情序列结束。</summary>
        End
    }

    /// <summary>
    /// 剧情定义中的单个节点。
    /// </summary>
    [Serializable]
    public sealed class StoryNodeDefinition
    {
        /// <summary>节点在当前剧情内的稳定标识。</summary>
        public string NodeId;
        /// <summary>节点类型。</summary>
        public StoryNodeType Type;
        /// <summary>本地化剧情文本键。</summary>
        public string TextKey;
        /// <summary>下一节点标识；结束节点可为空。</summary>
        public string NextNodeId;
    }

    /// <summary>
    /// 可播放剧情的运行时定义。
    /// </summary>
    [Serializable]
    public sealed class StoryDefinition
    {
        /// <summary>剧情所属内容包头信息。</summary>
        public ContentHeader Header;
        /// <summary>剧情稳定标识。</summary>
        public string StoryId;
        /// <summary>按编辑器生成顺序保存的剧情节点集合。</summary>
        public List<StoryNodeDefinition> Nodes = new List<StoryNodeDefinition>();
    }

    /// <summary>
    /// 角色定义的占位模型；后续会扩展名称、立绘和剧情默认形象等字段。
    /// </summary>
    [Serializable]
    public sealed class CharacterDefinition
    {
        /// <summary>角色稳定标识。</summary>
        public string CharacterId;
    }

    /// <summary>
    /// 档案条目定义的占位模型；后续会扩展标题、正文、解锁规则和资源引用。
    /// </summary>
    [Serializable]
    public sealed class ArchiveEntryDefinition
    {
        /// <summary>档案条目稳定标识。</summary>
        public string EntryId;
    }

    /// <summary>
    /// 内容提供者接口，按稳定 ID 暴露某一来源的关卡与剧情定义。
    /// </summary>
    public interface IContentProvider
    {
        /// <summary>该提供者负责的内容来源。</summary>
        ContentSource Source { get; }
        /// <summary>尝试按稳定 ID 获取关卡定义。</summary>
        /// <param name="levelId">关卡稳定标识。</param>
        /// <param name="definition">找到时返回关卡定义；未找到时为 null。</param>
        /// <returns>找到关卡定义返回 true，否则返回 false。</returns>
        bool TryGetLevel(LevelId levelId, out LevelDefinition definition);
        /// <summary>尝试按稳定 ID 获取剧情定义。</summary>
        /// <param name="storyId">剧情稳定标识。</param>
        /// <param name="definition">找到时返回剧情定义；未找到时为 null。</param>
        /// <returns>找到剧情定义返回 true，否则返回 false。</returns>
        bool TryGetStory(StoryId storyId, out StoryDefinition definition);
    }

    /// <summary>
    /// 官方运行时内容查询服务，供地图、剧情、档案和人员页面读取只读内容定义。
    /// </summary>
    public interface IContentService
    {
        /// <summary>获取指定关卡定义；不存在时返回 null。</summary>
        /// <param name="levelId">关卡稳定标识。</param>
        /// <returns>关卡定义；不存在时为 null。</returns>
        LevelDefinition GetLevel(LevelId levelId);
        /// <summary>获取指定剧情定义；不存在时返回 null。</summary>
        /// <param name="storyId">剧情稳定标识。</param>
        /// <returns>剧情定义；不存在时为 null。</returns>
        StoryDefinition GetStory(StoryId storyId);
        /// <summary>获取指定角色定义；不存在时返回 null。</summary>
        /// <param name="characterId">角色稳定标识。</param>
        /// <returns>角色定义；不存在时为 null。</returns>
        CharacterDefinition GetCharacter(CharacterId characterId);
        /// <summary>获取指定档案条目定义；不存在时返回 null。</summary>
        /// <param name="entryId">档案条目稳定标识。</param>
        /// <returns>档案条目定义；不存在时为 null。</returns>
        ArchiveEntryDefinition GetArchiveEntry(ArchiveEntryId entryId);
        /// <summary>获取某个地图下的关卡摘要列表。</summary>
        /// <param name="mapId">地图稳定标识。</param>
        /// <returns>按排序值排列的关卡摘要只读列表。</returns>
        IReadOnlyList<LevelSummary> GetLevelsForMap(MapId mapId);
        /// <summary>检查内容头是否可由当前官方内容服务读取。</summary>
        /// <param name="header">内容头信息。</param>
        /// <returns>兼容性检查结果。</returns>
        ContentCompatibility CheckCompatibility(ContentHeader header);
    }

    /// <summary>
    /// 官方资源解析接口，使用稳定资源 ID 获取 Unity 资源对象。
    /// </summary>
    /// <remarks>
    /// 技术设计文档要求运行时通过受控 Registry 获取资源，不直接散落 <c>Resources.Load</c> 字符串。
    /// </remarks>
    public interface IAssetResolver
    {
        /// <summary>获取指定预制体资源；不存在时返回 null。</summary>
        /// <param name="id">预制体稳定标识。</param>
        /// <returns>预制体资源；不存在时为 null。</returns>
        GameObject GetPrefab(PrefabId id);
        /// <summary>获取指定 UI 预制体资源；不存在时返回 null。</summary>
        /// <param name="id">UI 预制体稳定标识。</param>
        /// <returns>UI 预制体资源；不存在时为 null。</returns>
        GameObject GetUiPrefab(UiPrefabId id);
        /// <summary>获取指定精灵资源；不存在时返回 null。</summary>
        /// <param name="id">精灵资源稳定标识。</param>
        /// <returns>精灵资源；不存在时为 null。</returns>
        Sprite GetSprite(SpriteId id);
        /// <summary>获取指定音频资源；不存在时返回 null。</summary>
        /// <param name="id">音频资源稳定标识。</param>
        /// <returns>音频资源；不存在时为 null。</returns>
        AudioClip GetAudio(AudioId id);
    }
}
