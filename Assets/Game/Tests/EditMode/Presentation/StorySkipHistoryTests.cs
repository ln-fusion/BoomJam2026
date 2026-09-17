using Game.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>验证整段跳过也会把实际经过的节点记入剧情历史。</summary>
    public sealed class StorySkipHistoryTests
    {
        /// <summary>跳过会把经过的对白与自动选择的选项补入历史, 未选分支不出现。</summary>
        [Test]
        public void Skip_RecordsVisitedNodesIntoHistory()
        {
            var root = new GameObject("StorySkipHistoryTest", typeof(RectTransform));
            try
            {
                var presenter = root.AddComponent<StoryScenePresenter>();
                // 不注入运行时服务: 使用官方测试剧情目录, 且不触发剧情结束后的返回跳转。
                presenter.Initialize();
                Assert.That(
                    root.GetComponentInChildren<StoryDialoguePanel>(true),
                    Is.Not.Null,
                    "表现器应创建剧情面板"
                );

                Button skip = FindComponent<Button>(root, "Skip");
                Assert.That(skip, Is.Not.Null, "面板应包含跳过按钮");
                skip.onClick.Invoke(); // 首次点击进入二次确认
                skip.onClick.Invoke(); // 再次点击执行整段跳过

                string text = FindComponent<Text>(root, "HistoryText").text;
                Assert.That(
                    text,
                    Does.Contain("story.c06.start"),
                    "跳过前已显示的对白应保留在历史中"
                );
                Assert.That(
                    text,
                    Does.Contain("story.c06.merge"),
                    "跳过期间经过的对白应补入历史"
                );
                Assert.That(
                    text,
                    Does.Contain("story.c06.left"),
                    "跳过自动选择的选项应记入历史"
                );
                Assert.That(
                    text,
                    Does.Not.Contain("story.c06.right"),
                    "未选择的分支不应出现在历史中"
                );
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>跳过结束后历史仍可查看, 直到下一次剧情开始才重置。</summary>
        [Test]
        public void Skip_HistorySurvivesUntilNextSession()
        {
            var root = new GameObject("StorySkipHistoryLifetimeTest", typeof(RectTransform));
            try
            {
                var presenter = root.AddComponent<StoryScenePresenter>();
                presenter.Initialize();
                var panel = root.GetComponentInChildren<StoryDialoguePanel>(true);

                Button skip = FindComponent<Button>(root, "Skip");
                skip.onClick.Invoke();
                skip.onClick.Invoke();

                Assert.That(
                    FindComponent<Text>(root, "HistoryText").text,
                    Is.Not.Empty,
                    "跳过结束后历史应仍可回看"
                );

                panel.ResetHistory();
                Assert.That(
                    FindComponent<Text>(root, "HistoryText").text,
                    Is.Empty,
                    "下一次剧情开始时历史应被重置"
                );
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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
    }
}
