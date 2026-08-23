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

        /// <summary>创建领域事件总线。</summary>
        /// <param name="logger">用于记录订阅者异常；为 null 时使用丢弃日志实现。</param>
        public DomainEventBus(IGameLogger logger)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// 订阅指定事件类型，返回释放即退订的句柄.
        /// </summary>
        /// <typeparam name="T">需要订阅的领域事件类型。</typeparam>
        /// <param name="handler">收到事件时调用的处理器。</param>
        /// <returns>释放时取消本次订阅的句柄。</returns>
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
        /// <typeparam name="T">需要发布的领域事件类型。</typeparam>
        /// <param name="domainEvent">发布给该类型所有有效订阅者的事件。</param>
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

        /// <summary>
        /// 单个订阅的可释放句柄，同时持有订阅者回调和所属列表。
        /// </summary>
        private sealed class SubscriptionEntry : IDisposable
        {
            private readonly List<SubscriptionEntry> _owner;

            /// <summary>订阅者回调对象。</summary>
            internal object Handler { get; }

            /// <summary>该订阅是否已经释放。</summary>
            internal bool IsDisposed { get; private set; }

            /// <summary>创建订阅句柄。</summary>
            /// <param name="owner">所属订阅列表。</param>
            /// <param name="handler">订阅者回调。</param>
            internal SubscriptionEntry(List<SubscriptionEntry> owner, object handler)
            {
                _owner = owner;
                Handler = handler;
            }

            /// <summary>取消当前订阅；重复调用安全。</summary>
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
