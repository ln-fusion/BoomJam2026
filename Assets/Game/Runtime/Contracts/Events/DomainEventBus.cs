using System;
using System.Collections.Generic;
using Game.Contracts.Logging;

namespace Game.Contracts.Events
{
    public sealed class DomainEventBus : IDomainEventBus
    {
        private readonly object _gate = new object();
        private readonly Dictionary<Type, List<Subscription>> _subscriptions =
            new Dictionary<Type, List<Subscription>>();
        private readonly IGameLogger _logger;

        public DomainEventBus(IGameLogger logger = null)
        {
            _logger = logger ?? NullGameLogger.Instance;
        }

        public IDisposable Subscribe<T>(Action<T> handler) where T : IDomainEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var subscription = new Subscription(typeof(T), value => handler((T)value), Remove);
            lock (_gate)
            {
                if (!_subscriptions.TryGetValue(typeof(T), out List<Subscription> handlers))
                {
                    handlers = new List<Subscription>();
                    _subscriptions.Add(typeof(T), handlers);
                }

                handlers.Add(subscription);
            }

            return subscription;
        }

        public void Publish<T>(T domainEvent) where T : IDomainEvent
        {
            if ((object)domainEvent == null)
                throw new ArgumentNullException(nameof(domainEvent));

            Subscription[] snapshot;
            lock (_gate)
            {
                if (!_subscriptions.TryGetValue(typeof(T), out List<Subscription> handlers))
                    return;

                snapshot = handlers.ToArray();
            }

            foreach (Subscription subscription in snapshot)
            {
                if (subscription.IsDisposed)
                    continue;

                try
                {
                    subscription.Invoke(domainEvent);
                }
                catch (Exception exception)
                {
                    _logger.Write(LogLevel.Error, "A domain event subscriber failed.",
                        LogContext.Empty.With("eventType", typeof(T).FullName), exception);
                }
            }
        }

        private void Remove(Subscription subscription)
        {
            lock (_gate)
            {
                if (!_subscriptions.TryGetValue(subscription.EventType,
                        out List<Subscription> handlers))
                    return;

                handlers.Remove(subscription);
                if (handlers.Count == 0)
                    _subscriptions.Remove(subscription.EventType);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly Action<object> _handler;
            private Action<Subscription> _remove;

            public Type EventType { get; }
            public bool IsDisposed => _remove == null;

            public Subscription(Type eventType, Action<object> handler,
                Action<Subscription> remove)
            {
                EventType = eventType;
                _handler = handler;
                _remove = remove;
            }

            public void Invoke(object value)
            {
                _handler(value);
            }

            public void Dispose()
            {
                Action<Subscription> remove = _remove;
                if (remove == null)
                    return;

                _remove = null;
                remove(this);
            }
        }
    }
}
