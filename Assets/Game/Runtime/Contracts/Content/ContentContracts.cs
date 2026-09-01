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
        Ugc,
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
        InvalidPayload,
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

        /// <summary>Unlock rule evaluated before the level can be entered.</summary>
        public UnlockRequirementData UnlockRequirement;
    }

    /// <summary>Describes the completion facts required to unlock a level.</summary>
    [Serializable]
    public sealed class UnlockRequirementData
    {
        /// <summary>Combines required facts with all or any semantics.</summary>
        public UnlockRequirementMode Mode;

        /// <summary>Stable level IDs whose completion facts are required.</summary>
        public List<string> RequiredLevelIds = new List<string>();
    }

    /// <summary>Combines multiple level completion requirements.</summary>
    public enum UnlockRequirementMode
    {
        /// <summary>No prerequisite is required.</summary>
        None,

        /// <summary>Every listed prerequisite must be complete.</summary>
        All,

        /// <summary>At least one listed prerequisite must be complete.</summary>
        Any,
    }

    /// <summary>Groups ordered level summaries into a selectable map.</summary>
    [Serializable]
    public sealed class MapDefinition
    {
        /// <summary>Content compatibility metadata for this map.</summary>
        public ContentHeader Header;

        /// <summary>Stable map identifier.</summary>
        public string MapId;

        /// <summary>Localization key for the map name.</summary>
        public string DisplayNameKey;

        /// <summary>Order among official maps.</summary>
        public int SortOrder;

        /// <summary>Levels belonging to this map.</summary>
        public List<LevelSummary> Levels = new List<LevelSummary>();
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

        /// <summary>Unlock rule copied into the level summary.</summary>
        public UnlockRequirementData UnlockRequirement;

        /// <summary>由完整定义派生的关卡选择摘要。</summary>
        public LevelSummary Summary =>
            new LevelSummary
            {
                LevelId = LevelId,
                MapId = MapId,
                DisplayNameKey = DisplayNameKey,
                SortOrder = SortOrder,
                UnlockRequirement = UnlockRequirement,
            };
    }

    /// <summary>
    /// 剧情节点类型。
    /// </summary>
    public enum StoryNodeType
    {
        /// <summary>播放一段剧情文本后进入后续节点。</summary>
        Dialogue,

        /// <summary>Waits for a player choice before continuing.</summary>
        Choice,

        /// <summary>Jumps to another node without presentation.</summary>
        Goto,

        /// <summary>剧情序列结束。</summary>
        End,

        /// <summary>显示或刷新角色立绘（使用 SpeakerCharacterId 与 AppearanceOverride），随后自动推进。</summary>
        ShowCharacter,

        /// <summary>显示全屏 CG（使用 AssetId），随后自动推进。</summary>
        ShowCg,

        /// <summary>切换背景（使用 BackgroundId），随后自动推进。</summary>
        SetBackground,

        /// <summary>隐藏指定角色的立绘（使用 SpeakerCharacterId），随后自动推进。</summary>
        HideCharacter,

        /// <summary>移动角色立绘到指定位置（使用 SpeakerCharacterId 与 CharacterPosition），随后自动推进。</summary>
        MoveCharacter,

        /// <summary>播放音乐或音效（使用 AudioId 与 AudioKind），随后自动推进。</summary>
        PlayAudio,

        /// <summary>播放屏幕效果（使用 EffectType），随后自动推进。</summary>
        ScreenEffect,

        /// <summary>可跳过的等待（使用 WaitSeconds），倒计时结束后自动推进。</summary>
        Wait,
    }

    /// <summary>角色立绘在剧情画面中的位置。</summary>
    public enum StoryCharacterPosition
    {
        /// <summary>画面左侧。</summary>
        Left,

        /// <summary>画面中间。</summary>
        Center,

        /// <summary>画面右侧。</summary>
        Right,
    }

    /// <summary>剧情屏幕效果类型。</summary>
    public enum StoryScreenEffectType
    {
        /// <summary>无效果；用于占位。</summary>
        None,

        /// <summary>白屏闪烁。</summary>
        WhiteFlash,

        /// <summary>红屏闪烁。</summary>
        RedFlash,

        /// <summary>屏幕抖动。</summary>
        Shake,

        /// <summary>黑屏遮罩。</summary>
        Blackout,

        /// <summary>模糊效果占位。</summary>
        Blur,
    }

    /// <summary>演出音频资源的类别。</summary>
    public enum StoryAudioKind
    {
        /// <summary>背景音乐。</summary>
        Music,

        /// <summary>一次性音效。</summary>
        Sfx,
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

        /// <summary>说话人本地化键；C11 以前为占位，C16 起由节点提供。</summary>
        public string SpeakerKey;

        /// <summary>说话角色稳定标识；为空时表示无角色。</summary>
        public string SpeakerCharacterId;

        /// <summary>显式覆盖的形象稳定标识；为空时使用角色当前默认形象。</summary>
        public string AppearanceOverride;

        /// <summary>演出资源稳定标识（ShowCg 节点为 CG 图，其余节点可为空）。</summary>
        public string AssetId;

        /// <summary>背景资源稳定标识（SetBackground 节点使用）。</summary>
        public string BackgroundId;

        /// <summary>角色立绘位置（MoveCharacter 节点使用；默认左侧）。</summary>
        public StoryCharacterPosition CharacterPosition = StoryCharacterPosition.Left;

        /// <summary>角色表情稳定标识；无差分时可为空。</summary>
        public string ExpressionId;

        /// <summary>屏幕效果类型（ScreenEffect 节点使用；默认无效果）。</summary>
        public StoryScreenEffectType EffectType = StoryScreenEffectType.None;

        /// <summary>音频资源稳定标识（PlayAudio 节点使用）。</summary>
        public string AudioId;

        /// <summary>音频类别（PlayAudio 节点使用）。</summary>
        public StoryAudioKind AudioKind = StoryAudioKind.Sfx;

        /// <summary>等待秒数（Wait 节点使用；0 表示无等待）。</summary>
        public float WaitSeconds;

        /// <summary>下一节点标识；结束节点可为空。</summary>
        public string NextNodeId;

        /// <summary>Available choices when this is a choice node.</summary>
        public List<StoryChoiceDefinition> Choices = new List<StoryChoiceDefinition>();
    }

    /// <summary>One selectable branch of a choice node.</summary>
    [Serializable]
    public sealed class StoryChoiceDefinition
    {
        /// <summary>Stable choice identifier.</summary>
        public string ChoiceId;

        /// <summary>Localization key for the choice label.</summary>
        public string TextKey;

        /// <summary>Target node reached after selecting this choice.</summary>
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

        /// <summary>Stable ID of the first node to execute.</summary>
        public string StartNodeId = "start";

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

        /// <summary>角色可用的形象稳定标识列表；C16 起为剧情默认形象提供查询源。</summary>
        public List<string> AppearanceIds = new List<string>();

        /// <summary>角色默认形象稳定标识；不在 <see cref="AppearanceIds"/> 中时按无效处理。</summary>
        public string DefaultAppearanceId;
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

        /// <summary>Attempts to resolve a map by stable ID.</summary>
        /// <param name="mapId">Map stable identifier.</param>
        /// <param name="definition">Resolved map or null.</param>
        /// <returns>True when the map exists.</returns>
        bool TryGetMap(MapId mapId, out MapDefinition definition);

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
        /// <summary>Gets a map definition by stable ID.</summary>
        /// <param name="mapId">Map stable identifier.</param>
        /// <returns>The map or null when it is not known.</returns>
        MapDefinition GetMap(MapId mapId);

        /// <summary>Gets all official maps in display order.</summary>
        /// <returns>An immutable ordered map list.</returns>
        IReadOnlyList<MapDefinition> GetMaps();

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

    /// <summary>
    /// 角色当前形象查询接口：为剧情默认立绘和人员页面提供角色形象来源。
    /// </summary>
    /// <remarks>
    /// 查询不执行资源加载，只回答"该角色当前使用哪个形象"；资源本体由
    /// <see cref="ICharacterAssetRegistry"/> 按形象 ID 解析。人员当前形象成为剧情
    /// 未显式指定形象时的默认来源（技术设计文档 §8.6）。
    /// </remarks>
    public interface ICharacterAppearanceQuery
    {
        /// <summary>获取角色默认（当前）形象稳定标识。</summary>
        /// <param name="characterId">角色稳定标识。</param>
        /// <returns>默认形象标识；角色无可用形象时为 null。</returns>
        AppearanceId GetDefaultAppearance(CharacterId characterId);

        /// <summary>获取角色全部可用形象标识列表。</summary>
        /// <param name="characterId">角色稳定标识。</param>
        /// <returns>按声明顺序排列的形象标识只读列表；无形象时为空列表。</returns>
        IReadOnlyList<AppearanceId> GetAppearances(CharacterId characterId);
    }

    /// <summary>
    /// 角色立绘资源注册表：按角色、形象和表情三元组解析立绘精灵。
    /// </summary>
    /// <remarks>
    /// 剧情与人员页面共用同一注册表，避免各自维护重复资源引用（技术设计文档 §8.1）。
    /// </remarks>
    public interface ICharacterAssetRegistry
    {
        /// <summary>按角色、形象与表情获取立绘精灵。</summary>
        /// <param name="characterId">角色稳定标识。</param>
        /// <param name="appearanceId">形象稳定标识。</param>
        /// <param name="expressionId">表情稳定标识；无差分时可为 null。</param>
        /// <returns>立绘精灵；资源缺失时返回 null。</returns>
        Sprite GetPortrait(CharacterId characterId, AppearanceId appearanceId, ExpressionId expressionId);
    }

    /// <summary>
    /// 剧情演出资源源：按稳定 ID 解析背景与 CG 精灵。
    /// </summary>
    /// <remarks>
    /// 由 Presentation 侧适配器包装 <see cref="IAssetResolver.GetSprite"/>,
    /// 保持剧情表现层不直接依赖具体资源系统。
    /// </remarks>
    public interface IStoryStageAssetSource
    {
        /// <summary>按稳定 ID 获取背景精灵；不存在时返回 null。</summary>
        /// <param name="backgroundId">背景资源稳定标识。</param>
        /// <returns>背景精灵；资源缺失时返回 null。</returns>
        Sprite GetBackground(string backgroundId);

        /// <summary>按稳定 ID 获取 CG 精灵；不存在时返回 null。</summary>
        /// <param name="assetId">CG 资源稳定标识。</param>
        /// <returns>CG 精灵；资源缺失时返回 null。</returns>
        Sprite GetCg(string assetId);
    }
}
