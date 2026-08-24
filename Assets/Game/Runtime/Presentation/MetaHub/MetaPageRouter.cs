#nullable enable
using System;
using Game.Contracts;

namespace Game.Presentation
{
    /// <summary>
    /// MetaHub 页面路由（Presentation 内部）：页面 ID 与字符串互转（存档持久化用）.
    /// </summary>
    public static class MetaPageRouter
    {
        /// <summary>从存档字符串解析页面；未知值回退 Map.</summary>
        public static MetaPageId FromString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return MetaPageId.Map;

            if (
                Enum.TryParse<MetaPageId>(value, ignoreCase: true, out var page)
                && Enum.IsDefined(typeof(MetaPageId), page)
            )
            {
                return page;
            }

            return MetaPageId.Map;
        }

        /// <summary>页面转存档字符串（小写）.</summary>
        public static string ToString(MetaPageId page) => page.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// MetaHub 页面 Presenter：持有当前页，页面切换回调.
    /// </summary>
    public sealed class MetaPagePresenter
    {
        public MetaPageId CurrentPage { get; private set; }

        /// <summary>页面切换时触发（新页为参数）.</summary>
        public event Action<MetaPageId>? OnPageChanged;

        public MetaPagePresenter()
        {
        }

        /// <summary>切换页面.</summary>
        public void Select(MetaPageId page)
        {
            if (page == CurrentPage)
                return;

            CurrentPage = page;
            OnPageChanged?.Invoke(page);
        }

        /// <summary>恢复上次页面（解析失败回退 Map）. </summary>
        public void Restore(string lastPageId) => Select(MetaPageRouter.FromString(lastPageId));
    }
}
