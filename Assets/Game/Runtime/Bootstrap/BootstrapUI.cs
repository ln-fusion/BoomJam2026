#nullable enable
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Game.Bootstrap
{
    /// <summary>
    /// 场景 UI 输入分发器：程序化创建 EventSystem 与输入模块，保证真实鼠标/键盘可操作 UI.
    /// </summary>
    /// <remarks>
    /// 项目启用新 Input System（activeInputHandler=2），旧的 StandaloneInputModule 已废弃，
    /// 必须使用 <see cref="InputSystemUIInputModule"/>。场景中无手搭 EventSystem，
    /// 由本组件在运行时挂载（跟随 GameRoot 驻留 00_Bootstrap，跨场景存活）。
    /// 若场景中已存在 EventSystem，则跳过创建，避免重复实例。
    /// </remarks>
    public sealed class BootstrapUI : MonoBehaviour
    {
        /// <summary>
        /// 确保存在 EventSystem；已存在时跳过（幂等）.
        /// </summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            var go = new GameObject("[EventSystem]");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            Object.DontDestroyOnLoad(go);
        }
    }
}
