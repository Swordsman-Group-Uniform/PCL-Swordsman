using PCL.PluginSdk;

namespace PCL.PluginHost;

/// <summary>
///     宿主事件总线：**宿主 publish、插件 subscribe**。
///     <para>
///         规则：
///         <list type="bullet">
///             <item>订阅需要插件清单里声明 <c>events</c> 能力，否则返回空订阅 + 记一条警告；</item>
///             <item>每个订阅者各自 try/catch，一个插件抛异常不影响其它插件与宿主；</item>
///             <item>插件卸载 / 重扫时 <see cref="Clear" /> 一次性退订全部；</item>
///             <item>事件类型按「可赋值」匹配（订阅 <c>object</c> 能收到全部，别这么干）。</item>
///         </list>
///     </para>
/// </summary>
public sealed class PluginEventBus
{
    private readonly List<Subscription> _subscriptions = [];
    private readonly object _lock = new();

    /// <summary>一条订阅。</summary>
    private sealed class Subscription
    {
        public required Type EventType { get; init; }
        public required Action<object> Handler { get; init; }
        public required string PluginId { get; init; }
        public bool IsDisposed { get; set; }
    }

    /// <summary>给某个插件用的订阅门面。</summary>
    private sealed class Subscriber : IPluginEventBus
    {
        private readonly PluginEventBus _bus;
        private readonly string _pluginId;
        private readonly bool _canSubscribe;
        private readonly IPluginLogger _log;

        public Subscriber(PluginEventBus bus, string pluginId, bool canSubscribe, IPluginLogger log)
        {
            _bus = bus;
            _pluginId = pluginId;
            _canSubscribe = canSubscribe;
            _log = log;
        }

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            if (handler is null)
                return EmptyDisposable.Instance;

            if (!_canSubscribe)
            {
                _log.Warn($"插件 {_pluginId} 没有声明 events 能力，订阅 {typeof(TEvent).Name} 被忽略");
                return EmptyDisposable.Instance;
            }

            return _bus.Add(typeof(TEvent), payload => handler((TEvent)payload), _pluginId, _log);
        }
    }

    private sealed class EmptyDisposable : IDisposable
    {
        public static readonly EmptyDisposable Instance = new();

        public void Dispose()
        {
        }
    }

    /// <summary>为某个插件创建订阅门面（由 <see cref="PluginContext" /> 调用）。</summary>
    internal IPluginEventBus CreateSubscriber(string pluginId, bool canSubscribe, IPluginLogger log) =>
        new Subscriber(this, pluginId, canSubscribe, log);

    private IDisposable Add(Type eventType, Action<object> handler, string pluginId, IPluginLogger log)
    {
        var subscription = new Subscription { EventType = eventType, Handler = handler, PluginId = pluginId };
        lock (_lock) _subscriptions.Add(subscription);

        return new SubscriptionHandle(this, subscription, log);
    }

    private sealed class SubscriptionHandle : IDisposable
    {
        private readonly PluginEventBus _bus;
        private readonly Subscription _subscription;
        private readonly IPluginLogger _log;

        public SubscriptionHandle(PluginEventBus bus, Subscription subscription, IPluginLogger log)
        {
            _bus = bus;
            _subscription = subscription;
            _log = log;
        }

        public void Dispose() => _bus.Remove(_subscription, _log);
    }

    private void Remove(Subscription subscription, IPluginLogger? log)
    {
        subscription.IsDisposed = true;
        lock (_lock) _subscriptions.Remove(subscription);
        _ = log; // 日志仅在异常时用到，保留参数便于以后加诊断
    }

    /// <summary>发布一个事件给所有订阅者（同类型或基类匹配）。</summary>
    public void Publish<TEvent>(TEvent payload) where TEvent : class
    {
        if (payload is null) return;

        Subscription[] snapshot;
        lock (_lock) snapshot = _subscriptions.ToArray();
        if (snapshot.Length == 0) return;

        foreach (var subscription in snapshot)
        {
            if (subscription.IsDisposed) continue;
            if (!subscription.EventType.IsInstanceOfType(payload)) continue;

            try
            {
                subscription.Handler(payload);
            }
            catch (Exception ex)
            {
                // 插件自己的异常不许冒到发布方
                PublishFailure?.Invoke(subscription.PluginId, ex);
            }
        }
    }

    /// <summary>订阅者抛异常时的回调（宿主用来写日志）。</summary>
    public Action<string, Exception>? PublishFailure { get; set; }

    /// <summary>清空全部订阅（插件卸载 / 重扫时调用）。</summary>
    public void Clear()
    {
        lock (_lock)
        {
            foreach (var subscription in _subscriptions)
                subscription.IsDisposed = true;
            _subscriptions.Clear();
        }
    }

    /// <summary>当前订阅数（诊断用）。</summary>
    public int Count
    {
        get
        {
            lock (_lock) return _subscriptions.Count;
        }
    }
}
