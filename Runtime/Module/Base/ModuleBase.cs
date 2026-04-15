using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Core.Config;

namespace JulyCore.Module.Base
{
    /// <summary>
    /// Module 抽象基类
    /// 提供通用的生命周期管理：InitAsync → Update → Shutdown
    /// </summary>
    public abstract class ModuleBase : IModule, IPriority
    {
        private bool _isInitialized;
        private FrameworkContext _context;

        public string Name => GetType().Name;
        public bool IsInitialized => _isInitialized;
        public virtual int Priority => 0;
        protected abstract LogChannel LogChannel { get; }

        #region 受控服务访问

        protected IEventBus EventBus => _context?.EventBus;
        protected FrameworkConfig FrameworkConfig => _context.FrameworkConfig;

        protected T GetProvider<T>() where T : IProvider
        {
            var result = _context.Registry.Resolve<T>();
            if (result == null)
            {
                var type = typeof(T);
                throw new JulyException($"[{Name}] 需要{type}，请先注册");
            }
            return result;
        }

        protected TCapability GetCapability<TCapability>() where TCapability : class, ICapability
        {
            var result = _context.ModuleService.GetModuleByCapability<TCapability>();
            if (result == null)
            {
                var type = typeof(TCapability);
                throw new JulyException($"[{Name}] 需要能力 {type.Name}，请确保对应的 Module 已注册并实现了该接口");
            }
            return result;
        }

        #endregion

        #region 模块日志方法

        protected void Log(object message)
        {
            JLogger.LogChannel(LogChannel, Name, message);
        }

        protected void LogWarning(object message)
        {
            JLogger.LogChannelWarning(Name, message);
        }

        protected void LogError(object message)
        {
            JLogger.LogChannelError(Name, message);
        }

        #endregion

        #region 生命周期

        public UniTask InitAsync()
        {
            if (_isInitialized)
            {
                JLogger.LogWarning($"[{Name}] Module已经初始化，跳过重复初始化");
                return UniTask.CompletedTask;
            }

            _context = FrameworkContext.Instance;
            var task = OnInitAsync();

            if (task.Status == UniTaskStatus.Succeeded)
            {
                _isInitialized = true;
                return UniTask.CompletedTask;
            }

            return AwaitInitAsync(task);
        }

        private async UniTask AwaitInitAsync(UniTask initTask)
        {
            await initTask;
            _isInitialized = true;
        }

        public void Shutdown()
        {
            if (!_isInitialized) return;
            _isInitialized = false;
            OnShutdown();
        }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            if (!_isInitialized) return;
            OnUpdate(elapseSeconds, realElapseSeconds);
        }

        #endregion

        #region 子类钩子

        protected virtual UniTask OnInitAsync() => UniTask.CompletedTask;
        protected virtual void OnShutdown() { }
        protected virtual void OnUpdate(float elapseSeconds, float realElapseSeconds) { }

        #endregion
    }
}
