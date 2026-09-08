using Game.Contracts.Content;
using Game.Foundation;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 剧情演出资源源适配器：把 <see cref="IAssetResolver"/> 转换为背景与 CG 查询端口。
    /// </summary>
    public sealed class StoryAssetSource : IStoryStageAssetSource
    {
        private readonly IAssetResolver _assetResolver;

        /// <summary>创建演出资源源。</summary>
        /// <param name="assetResolver">官方资源解析器；为 null 时查询全部返回 null。</param>
        public StoryAssetSource(IAssetResolver assetResolver)
        {
            _assetResolver = assetResolver;
        }

        /// <inheritdoc/>
        public Sprite GetBackground(string backgroundId)
        {
            return string.IsNullOrWhiteSpace(backgroundId) || _assetResolver == null
                ? null
                : _assetResolver.GetSprite(new SpriteId(backgroundId));
        }

        /// <inheritdoc/>
        public Sprite GetCg(string assetId)
        {
            return string.IsNullOrWhiteSpace(assetId) || _assetResolver == null
                ? null
                : _assetResolver.GetSprite(new SpriteId(assetId));
        }
    }
}
