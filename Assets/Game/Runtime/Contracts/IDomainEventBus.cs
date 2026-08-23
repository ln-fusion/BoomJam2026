#nullable enable
using System;

namespace Game.Contracts
{
    /// <summary>
    /// 领域事件标记接口：只发布已经发生或已经提交的不可变事实。
    /// </summary>
    /// <remarks>
    /// 参见技术设计文档 §13.2：不得把 GameObject、Collider2D 或 View 引用放进跨模块事件。
    /// </remarks>
    public interface IDomainEvent { }

    /// <summary>
    /// 领域事件总线：订阅已提交事实；订阅返回句柄，释放句柄即取消订阅。
    /// </summary>
    /// <remarks>
    /// 领域事件总线；订阅者异常会被隔离并记录，卸载场景时释放订阅句柄。
    /// </remarks>
    public interface IDomainEventBus
    {
        /// <summary>
        /// 订阅指定事件类型。
        /// </summary>
        /// <typeparam name="T">事件类型，须实现 <see cref="IDomainEvent"/></typeparam>
        /// <param name="handler">事件回调</param>
        /// <returns>订阅句柄；调用 <see cref="IDisposable.Dispose"/> 取消订阅</returns>
        IDisposable Subscribe<T>(Action<T> handler)
            where T : IDomainEvent;

        /// <summary>发布事件给所有订阅者（同步，按订阅顺序）.</summary>
        /// <typeparam name="T">事件类型，须实现 <see cref="IDomainEvent"/></typeparam>
        /// <param name="domainEvent">已提交的不可变事件实例</param>
        void Publish<T>(T domainEvent)
            where T : IDomainEvent;
    }
}
