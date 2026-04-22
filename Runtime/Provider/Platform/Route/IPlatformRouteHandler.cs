using Cysharp.Threading.Tasks;

namespace JulyCore.Provider.Platform
{
    /// <summary>
    /// 平台路由处理器。由 PlatformSystem 在 OnInitialize 中集中注册。
    /// 注册顺序即匹配优先级：上面先判，命中即停。
    /// 其它业务 System（MiniGameSystem 等）不应注册平台路由 Handler。
    /// </summary>
    public interface IPlatformRouteHandler
    {
        /// <summary>纯参数匹配，无副作用；不看运行时状态。</summary>
        bool Match(RouteContext ctx);

        /// <summary>
        /// 执行业务逻辑。同步场景请显式 <c>return UniTask.CompletedTask</c>，
        /// 不要使用 <c>async</c> 关键字（避免多余的 async state machine）。
        /// </summary>
        UniTask HandleAsync(RouteContext ctx);
    }
}
