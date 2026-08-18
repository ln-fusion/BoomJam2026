using System;
using System.Collections.Generic;
using Game.Contracts;
using Game.Foundation;

namespace Game.Flow
{
    /// <summary>
    /// 领域事件总线默认实现：按类型维护订阅表，发布时隔离订阅者异常.
    /// </summary>
    /// <remarks>
    /// 订阅者异常必须被隔离并记录，不能回滚已经提交的事务（技术设计文档 §13.2）.
    /// 所有操作假设单线程调用（Unity 主线程）；如需跨线程再引入同步.
    /// </remarks>
    public sealed class DomainEventBus : IDomainEventBus
    {
        private readonly Dictionary<Type, List<SubscriptionEntry>> _subscriptions =
            new Dictionary<Type, List<SubscriptionEntry>>();

        private readonly IGameLogger _logger;

        /// <param name="logger">用于记录订阅者异常；为 null 时静默（不推荐，仅测试）</param>
        public DomainEventBus(IGameLogger logger)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// 订阅指定事件类型，返回释放即退订的句柄.
        /// </summary>
        public IDisposable Subscribe<T>(Action<T> handler)
            where T : IDomainEvent
        {
            if (handler is null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var type = typeof(T);
            if (!_subscriptions.TryGetValue(type, out var list))
            {
                list = new List<SubscriptionEntry>();
                _subscriptions[type] = list;
            }

            var entry = new SubscriptionEntry(list, handler);
            list.Add(entry);
            return entry;
        }

        /// <summary>
        /// 发布事件：按订阅顺序调用，每个订阅者异常隔离并记录错误日志.
        /// </summary>
        public void Publish<T>(T domainEvent)
            where T : IDomainEvent
        {
            if (domainEvent is null)
            {
                throw new ArgumentNullException(nameof(domainEvent));
            }

            var type = typeof(T);
            if (!_subscriptions.TryGetValue(type, out var list) || list.Count == 0)
            {
                return;
            }

            // 快照迭代：回调中可能退订/新增，避免修改集合时枚举异常
            var snapshot = list.ToArray();
            foreach (var entry in snapshot)
            {
                if (entry.IsDisposed)
                {
                    continue;
                }

                try
                {
                    ((Action<T>)entry.Handler)(domainEvent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(LogContext.Empty, $"[DomainEventBus] 事件处理异常,type={type.Name}: {ex}");
                }
            }
        }

        private sealed class SubscriptionEntry : IDisposable
        {
            private readonly List<SubscriptionEntry> _owner;

            internal object Handler { get; }

            internal bool IsDisposed { get; private set; }

            internal SubscriptionEntry(List<SubscriptionEntry> owner, object handler)
            {
                _owner = owner;
                Handler = handler;
            }

            public void Dispose()
            {
                if (IsDisposed)
                {
                    return;
                }

                IsDisposed = true;
                // 单线程模型下直接移除安全；Publish 走快照迭代不受影响
                _owner.Remove(this);
            }
        }
    }
}
