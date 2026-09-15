using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.Story;
using Game.Foundation;
using Game.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>验证剧情历史覆盖层的渲染、滚动结构与输入阻塞行为。</summary>
    public sealed class StoryHistoryPanelTests
    {
        private static readonly StoryId StoryId = new StoryId("official.story.test");

        /// <summary>历史渲染实际文本而非本地化键。</summary>
        [Test]
        public void History_RendersLocalizedText()
        {
            var root = new GameObject("StoryHistoryTextTest", typeof(RectTransform));
            try
            {
                var panel = root.AddComponent<StoryDialoguePanel>();
                panel.SetLocalization(new FakeLocalizationService());
                panel.AppendDialogueHistory(
                    StoryId,
                    new StoryNodeId("start"),
                    "official.character.hani",
                    "story.speaker.hani",
                    "story.text.hello"
                );

                OpenHistory(root, panel);

                string text = FindComponent<Text>(root, "HistoryText").text;
                Assert.That(text, Does.Contain("Hanī"), "历史应显示解析后的说话人文本");
                Assert.That(text, Does.Contain("你好"), "历史应显示解析后的正文文本");
                Assert.That(text, Does.Not.Contain("story.text.hello"), "历史不应显示本地化键");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>选项记录以带前缀的独立行渲染, 并标记为玩家选择。</summary>
        [Test]
        public void History_RendersChoiceLine()
        {
            var root = new GameObject("StoryHistoryChoiceTest", typeof(RectTransform));
            try
            {
                var panel = root.AddComponent<StoryDialoguePanel>();
                panel.SetLocalization(new FakeLocalizationService());
                panel.AppendChoiceHistory(StoryId, new StoryNodeId("choice"), "left", "story.c06.left");

                OpenHistory(root, panel);

                string text = FindComponent<Text>(root, "HistoryText").text;
                Assert.That(text, Does.Contain("> " + "向左"), "选项应作为带前缀的独立行");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>历史打开时点击不推进剧情, 关闭后恢复推进。</summary>
        [Test]
        public void HistoryOpen_BlocksAdvance()
        {
            var root = new GameObject("StoryHistoryInputTest", typeof(RectTransform));
            try
            {
                var panel = root.AddComponent<StoryDialoguePanel>();
                bool advanced = false;
                panel.ShowDialogue(
                    new StoryDialogueView("story.speaker.hani", "story.text.hello"),
                    () => advanced = true
                );

                OpenHistory(root, panel);
                Assert.That(panel.IsHistoryOpen, Is.True, "点击 History 按钮后覆盖层应打开");
                Assert.That(
                    FindComponent<Button>(root, "Continue").interactable,
                    Is.False,
                    "历史打开时应禁用继续按钮"
                );
                Assert.That(
                    FindComponent<Button>(root, "Skip").interactable,
                    Is.False,
                    "历史打开时应禁用跳过按钮"
                );

                // 打字机未结束时首次点击只补全文本, 第二次点击才推进；
                // 因此历史打开状态下连续两次点击都不应触发推进回调。
                panel.OnPointerClick(null);
                panel.OnPointerClick(null);
                Assert.That(advanced, Is.False, "历史打开时点击不应推进剧情");

                OpenHistory(root, panel);
                Assert.That(panel.IsHistoryOpen, Is.False, "再次点击 History 按钮应关闭覆盖层");
                panel.OnPointerClick(null);
                panel.OnPointerClick(null);
                Assert.That(advanced, Is.True, "关闭历史后点击应恢复推进");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>历史覆盖层具备垂直滚动结构, 长记录可滚动查看。</summary>
        [Test]
        public void History_HasScrollStructure()
        {
            var root = new GameObject("StoryHistoryScrollTest", typeof(RectTransform));
            try
            {
                var panel = root.AddComponent<StoryDialoguePanel>();
                panel.AppendDialogueHistory(
                    StoryId,
                    new StoryNodeId("start"),
                    null,
                    null,
                    "story.text.hello"
                );

                var scroll = FindComponent<ScrollRect>(root, "HistoryView");
                Assert.That(scroll, Is.Not.Null, "HistoryView 应挂载 ScrollRect");
                Assert.That(scroll.vertical, Is.True);
                Assert.That(scroll.horizontal, Is.False);
                Assert.That(scroll.content, Is.Not.Null, "ScrollRect 应绑定 Content");
                Assert.That(
                    scroll.content.GetComponent<Text>(),
                    Is.SameAs(FindComponent<Text>(root, "HistoryText")),
                    "Content 应就是历史文本"
                );
                Assert.That(scroll.content.GetComponent<ContentSizeFitter>(), Is.Not.Null);
                Assert.That(
                    FindComponent<RectMask2D>(root, "HistoryViewport"),
                    Is.Not.Null,
                    "Viewport 应裁剪超出区域"
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>切换语言后历史仍保持当时所见的文本快照。</summary>
        [Test]
        public void History_KeepsTextSnapshotAcrossLocaleChange()
        {
            var root = new GameObject("StoryHistoryLocaleTest", typeof(RectTransform));
            try
            {
                var localization = new FakeLocalizationService();
                var panel = root.AddComponent<StoryDialoguePanel>();
                panel.SetLocalization(localization);
                panel.AppendDialogueHistory(
                    StoryId,
                    new StoryNodeId("start"),
                    null,
                    null,
                    "story.text.hello"
                );

                RunSync(() => localization.SetLocaleAsync("ja", CancellationToken.None));
                OpenHistory(root, panel);

                string text = FindComponent<Text>(root, "HistoryText").text;
                Assert.That(text, Does.Contain("你好"), "历史应保持切换语言前所见的文本");
                Assert.That(text, Does.Not.Contain("こんにちは"), "历史不应按新语言重新解析");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>清空剧情后历史覆盖层关闭且内容重置。</summary>
        [Test]
        public void Clear_ResetsHistoryOverlay()
        {
            var root = new GameObject("StoryHistoryClearTest", typeof(RectTransform));
            try
            {
                var panel = root.AddComponent<StoryDialoguePanel>();
                panel.SetLocalization(new FakeLocalizationService());
                panel.AppendDialogueHistory(
                    StoryId,
                    new StoryNodeId("start"),
                    null,
                    null,
                    "story.text.hello"
                );
                OpenHistory(root, panel);

                panel.Clear();

                Assert.That(panel.IsHistoryOpen, Is.False, "清空后历史覆盖层应关闭");
                Assert.That(
                    FindComponent<Text>(root, "HistoryText").text,
                    Is.Empty,
                    "清空后历史文本应为空"
                );
                Assert.That(
                    FindComponent<Button>(root, "Continue").interactable,
                    Is.True,
                    "清空后输入阻塞应解除"
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>点击 History 按钮切换历史覆盖层并断言其状态。</summary>
        /// <param name="root">面板根节点。</param>
        /// <param name="panel">待检查的面板。</param>
        private static void OpenHistory(GameObject root, StoryDialoguePanel panel)
        {
            Button button = FindComponent<Button>(root, "History");
            Assert.That(button, Is.Not.Null, "面板应包含 History 按钮");
            button.onClick.Invoke();
        }

        /// <summary>同步等待异步本地化切换完成。</summary>
        /// <param name="operation">异步操作。</param>
        private static void RunSync(Func<Task> operation)
        {
            Task.Run(operation).GetAwaiter().GetResult();
        }

        /// <summary>在面板层级内按名称查找组件。</summary>
        /// <typeparam name="T">组件类型。</typeparam>
        /// <param name="root">查找根节点。</param>
        /// <param name="name">节点名称。</param>
        /// <returns>找到的组件；否则为 null。</returns>
        private static T FindComponent<T>(GameObject root, string name)
            where T : Component
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                    return child.GetComponent<T>();
            return null;
        }

        /// <summary>返回按 Locale 分派文本的内存本地化替身, 用于验证文本快照语义。</summary>
        private sealed class FakeLocalizationService : ILocalizationService
        {
            private static readonly Dictionary<string, string> Empty =
                new Dictionary<string, string>(StringComparer.Ordinal);
            private static readonly Dictionary<string, Dictionary<string, string>> Texts =
                new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["zh-CN"] = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["story.speaker.hani"] = "Hanī",
                        ["story.text.hello"] = "你好",
                        ["story.c06.left"] = "向左",
                    },
                    ["ja"] = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["story.speaker.hani"] = "ハニー",
                        ["story.text.hello"] = "こんにちは",
                        ["story.c06.left"] = "左へ",
                    },
                };

            private string _currentLocaleCode = "zh-CN";

            /// <summary>当前替身 Locale。</summary>
            public string CurrentLocaleCode => _currentLocaleCode;

            /// <summary>Locale 变更事件。</summary>
            public event Action<string> LocaleChanged;

            /// <summary>同步切换替身 Locale。</summary>
            /// <param name="localeCode">目标 Locale 代码。</param>
            /// <param name="cancellationToken">取消令牌。</param>
            /// <returns>切换结果；未知 Locale 返回失败。</returns>
            public Task<Result> SetLocaleAsync(string localeCode, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(localeCode))
                    return Task.FromResult(
                        Result.Failure(ErrorCode.LocaleUnsupported, "Locale code is required.")
                    );

                string normalizedCode = localeCode.Trim();
                if (string.Equals(_currentLocaleCode, normalizedCode, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(Result.Success());
                if (!Texts.ContainsKey(normalizedCode))
                    return Task.FromResult(
                        Result.Failure(ErrorCode.LocaleUnsupported, "Locale is not supported.")
                    );

                _currentLocaleCode = normalizedCode;
                LocaleChanged?.Invoke(normalizedCode);
                return Task.FromResult(Result.Success());
            }

            /// <summary>按当前 Locale 解析键, 缺失键回退为键名。</summary>
            /// <param name="key">稳定本地化键。</param>
            /// <param name="arguments">未使用的格式化参数。</param>
            /// <returns>本地化文本或键名。</returns>
            public string Get(LocalizationKey key, params object[] arguments)
            {
                string value = key?.Value;
                if (string.IsNullOrEmpty(value))
                    return string.Empty;
                Dictionary<string, string> table = Texts.TryGetValue(_currentLocaleCode, out var found)
                    ? found
                    : Empty;
                return table.TryGetValue(value, out string text) ? text : value;
            }
        }
    }
}
