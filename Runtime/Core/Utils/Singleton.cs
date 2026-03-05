namespace JulyCore.Core
{
    /// <summary>
    /// 普通单例基类
    /// </summary>
    public abstract class Singleton<T> where T : Singleton<T>, new()
    {
        private static T _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new T();
                            _instance.OnInit();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 初始化回调
        /// </summary>
        protected virtual void OnInit() { }

        /// <summary>
        /// 销毁单例
        /// </summary>
        public static void Dispose()
        {
            if (_instance != null)
            {
                _instance.OnDispose();
                _instance = null;
            }
        }

        /// <summary>
        /// 销毁回调
        /// </summary>
        protected virtual void OnDispose() { }
    }
}

