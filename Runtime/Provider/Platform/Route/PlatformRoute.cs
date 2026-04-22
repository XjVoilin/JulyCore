using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace JulyCore.Provider.Platform
{
    public class PlatformRoute : IPlatformRoute
    {
        private readonly List<IPlatformRouteHandler> _handlers = new();
        private bool _isReady;
        private RouteContext _pending;
        private RouteContext _lastRouted;

        public RouteContext ColdContext { get; private set; }
        public RouteContext LatestContext { get; private set; }

        public void AddHandler(IPlatformRouteHandler handler)
        {
            if (handler == null) return;
            _handlers.Add(handler);
        }

        public void RemoveHandler(IPlatformRouteHandler handler)
        {
            if (handler == null) return;
            _handlers.Remove(handler);
        }

        public void Clear()
        {
            _handlers.Clear();
            _pending = null;
            _lastRouted = null;
        }

        public void Dispatch(RouteContext ctx)
        {
            if (ctx == null) return;

            LatestContext = ctx;
            if (ctx.IsColdStart)
                ColdContext = ctx;

            if (!_isReady)
            {
                _pending = ctx;
                GF.Log($"[PlatformRoute] 暂存 Pending: {ctx}");
                return;
            }

            if (!ctx.IsColdStart && IsDuplicate(ctx))
            {
                GF.Log($"[PlatformRoute] 重复参数，跳过: {ctx}");
                return;
            }

            RouteAsync(ctx).Forget(ex =>
                GF.LogError($"[PlatformRoute] 路由异常: {ex}"));
        }

        public void MarkReady()
        {
            if (_isReady) return;
            _isReady = true;

            if (_pending == null) return;

            var pending = _pending;
            _pending = null;
            GF.Log($"[PlatformRoute] Ready, 分发 Pending: {pending}");
            RouteAsync(pending).Forget(ex =>
                GF.LogError($"[PlatformRoute] Pending 路由异常: {ex}"));
        }

        private async UniTask RouteAsync(RouteContext ctx)
        {
            foreach (var handler in _handlers)
            {
                if (!handler.Match(ctx)) continue;

                try
                {
                    GF.Log($"[PlatformRoute] 命中 Handler: {handler.GetType().Name}");
                    await handler.HandleAsync(ctx);
                    _lastRouted = ctx;
                    return;
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    GF.LogError($"[PlatformRoute] Handler 异常: {handler.GetType().Name}, {ex}");
                    return;
                }
            }

            if (ctx.Query.Count > 0 || !string.IsNullOrEmpty(ctx.SceneId))
                GF.Log($"[PlatformRoute] 无 Handler 匹配: {ctx}");
        }

        private bool IsDuplicate(RouteContext ctx)
        {
            if (_lastRouted == null) return false;
            if (ctx.Query.Count != _lastRouted.Query.Count) return false;

            foreach (var kv in ctx.Query)
            {
                if (!_lastRouted.Query.TryGetValue(kv.Key, out var val) || val != kv.Value)
                    return false;
            }
            return true;
        }
    }
}
