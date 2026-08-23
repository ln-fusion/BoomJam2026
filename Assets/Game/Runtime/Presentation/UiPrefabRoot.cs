using UnityEngine;

namespace Game.Presentation
{
    /// <summary>界面预制体根标记，供运行时和编辑器校验预制体用途。</summary>
    public sealed class UiPrefabRoot : MonoBehaviour
    {
        [SerializeField] private UiScreenId screenId;
        [SerializeField] private int contractVersion = 1;

        /// <summary>该预制体实现的界面类型。</summary>
        public UiScreenId ScreenId => screenId;

        /// <summary>绑定契约版本；字段变化时递增。</summary>
        public int ContractVersion => contractVersion;
    }
}
