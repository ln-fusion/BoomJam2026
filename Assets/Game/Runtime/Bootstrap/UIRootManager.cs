#nullable enable
using System;
using System.Collections.Generic;
using Game.Contracts;
using Game.Flow;
using Game.Foundation;
using Game.Presentation;
using Game.Progression;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// 场景 UI 根管理器：监听场景激活事件，动态加载场景对应 UI 根.
    /// </summary>
    /// <remarks>
    /// UI 根随功能场景卸载自动销毁（父对象挂在场景 GameObject 下）;
    /// 订阅句柄在 OnDestroy 释放.
    /// </remarks>
    public sealed class UIRootManager : IDisposable
    {
        private readonly IGameFlowService _flow;
        private readonly IDomainEventBus _eventBus;
        private readonly ISettingsService _settings;
        private readonly ILocalizationService? _localization;
        private readonly IClock _clock;

        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        public UIRootManager(
            IGameFlowService flow,
            IDomainEventBus eventBus,
            ISettingsService settings,
            ILocalizationService? localization,
            IClock clock
        )
        {
            if (flow == null)
                throw new ArgumentNullException(nameof(flow));
            if (eventBus == null)
                throw new ArgumentNullException(nameof(eventBus));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (clock == null)
                throw new ArgumentNullException(nameof(clock));

            _flow = flow;
            _eventBus = eventBus;
            _settings = settings;
            _localization = localization;
            _clock = clock;

            _subscriptions.Add(eventBus.Subscribe<SceneActivatedEvent>(OnSceneActivated));
        }

        private void OnSceneActivated(SceneActivatedEvent evt)
        {
            if (evt.SceneName == SceneNames.StartMenu)
            {
                // 构建 StartMenu UI 根（挂在 Bootstrap 场景下，随场景加载创建）
                var root = new GameObject("[StartMenuUI]");
                var component = root.AddComponent<StartMenuRoot>();
                component.Initialize(_flow, _eventBus, _settings, _localization);
            }
            else if (evt.SceneName == SceneNames.MetaHub)
            {
                var root = new GameObject("[MetaHubUI]");
                var component = root.AddComponent<MetaHubRoot>();
                component.Initialize(_flow, _eventBus, new EmptyProgressQuery(), _clock);
            }
        }

        public void Dispose()
        {
            foreach (var sub in _subscriptions)
            {
                sub.Dispose();
            }

            _subscriptions.Clear();
        }
    }
}
