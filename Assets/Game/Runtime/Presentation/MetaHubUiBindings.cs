using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>MetaHub 预制体的页面切换引用。</summary>
    public sealed class MetaHubUiBindings : MonoBehaviour
    {
        /// <summary>地图页面根节点。</summary>
        public GameObject MapPage;
        /// <summary>档案页面根节点。</summary>
        public GameObject ArchivePage;
        /// <summary>人员页面根节点。</summary>
        public GameObject CharacterPage;
        /// <summary>休息室页面根节点。</summary>
        public GameObject LoungePage;
        /// <summary>页面切换按钮。</summary>
        public Button[] NavigationButtons;

        /// <summary>检查 MetaHub 是否绑定了页面和至少四个导航按钮。</summary>
        public bool IsComplete => MapPage != null && ArchivePage != null &&
            CharacterPage != null && LoungePage != null && NavigationButtons != null &&
            NavigationButtons.Length >= 4;
    }
}
