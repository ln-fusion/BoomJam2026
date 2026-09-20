using System;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Level
{
    /// <summary>
    /// 单行文本输入对话框; 用于在编辑器窗口中收集稳定的内容 ID。
    /// </summary>
    /// <remarks>
    /// <see cref="EditorWindow.ShowModalUtility"/> 在 Unity 2022.3 可用,
    /// 但需要派生自 EditorWindow 才能获得模态行为。本窗口通过静态字段回传结果,
    /// 因此同一时刻只支持一个输入会话。
    /// </remarks>
    public sealed class PromptWindow : EditorWindow
    {
        private string _value = string.Empty;
        private string _message = string.Empty;
        private string _result;
        private bool _confirmed;

        /// <summary>弹出输入窗口并阻塞到用户确认或取消。</summary>
        /// <param name="title">窗口标题。</param>
        /// <param name="message">输入框上方的说明文本。</param>
        /// <param name="initialValue">输入框初始值。</param>
        /// <returns>用户确认时返回输入文本; 取消时返回 null。</returns>
        public static string Show(string title, string message, string initialValue)
        {
            var window = CreateInstance<PromptWindow>();
            window.titleContent = new GUIContent(title);
            window._message = message ?? string.Empty;
            window._value = initialValue ?? string.Empty;
            window.minSize = new Vector2(360f, 130f);
            window.maxSize = new Vector2(640f, 130f);
            window.ShowModalUtility();
            return window._confirmed ? window._result : null;
        }

        /// <summary>绘制输入界面。</summary>
        private void OnGUI()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(_message, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(4f);
            _value = EditorGUILayout.TextField(_value);
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("确定"))
            {
                _result = _value;
                _confirmed = true;
                Close();
            }
            if (GUILayout.Button("取消"))
            {
                _confirmed = false;
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
