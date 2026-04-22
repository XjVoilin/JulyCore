namespace JulyCore.Provider.Platform
{
    /// <summary>
    /// 平台路由管线。
    /// AOT 层 <see cref="ILifecycleService"/> 投递 <see cref="RouteContext"/>，
    /// 业务层（PlatformSystem）集中注册 <see cref="IPlatformRouteHandler"/>。
    /// </summary>
    public interface IPlatformRoute
    {
        RouteContext ColdContext { get; }
        RouteContext LatestContext { get; }

        void AddHandler(IPlatformRouteHandler handler);
        void RemoveHandler(IPlatformRouteHandler handler);
        void Clear();

        void MarkReady();
        void Dispatch(RouteContext ctx);
    }
}
