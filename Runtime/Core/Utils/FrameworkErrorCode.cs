namespace JulyCore.Core
{
    /// <summary>
    /// 框架统一错误码
    /// 
    /// 【错误码规范】
    /// - 0: 成功
    /// - 1xxx: 通用错误
    /// - 2xxx: Module 相关错误
    /// - 3xxx: Provider 相关错误
    /// - 4xxx: 资源相关错误
    /// - 5xxx: 网络相关错误
    /// - 6xxx: UI 相关错误
    /// - 7xxx: 数据相关错误
    /// - 8xxx: 配置相关错误
    /// - 9xxx: 业务相关错误（预留给项目扩展）
    /// </summary>
    public enum FrameworkErrorCode
    {
        #region 成功 (0)

        /// <summary>
        /// 操作成功
        /// </summary>
        Success = 0,

        #endregion

        #region 通用错误 (1xxx)

        /// <summary>
        /// 未知错误
        /// </summary>
        Unknown = 1000,

        /// <summary>
        /// 参数无效
        /// </summary>
        InvalidArgument = 1001,

        /// <summary>
        /// 空引用
        /// </summary>
        NullReference = 1002,

        /// <summary>
        /// 操作超时
        /// </summary>
        Timeout = 1003,

        /// <summary>
        /// 操作被取消
        /// </summary>
        Cancelled = 1004,

        /// <summary>
        /// 状态无效
        /// </summary>
        InvalidState = 1005,

        /// <summary>
        /// 未初始化
        /// </summary>
        NotInitialized = 1006,

        /// <summary>
        /// 已初始化
        /// </summary>
        AlreadyInitialized = 1007,

        /// <summary>
        /// 不支持的操作
        /// </summary>
        NotSupported = 1008,

        #endregion

        #region Module 相关错误 (2xxx)

        /// <summary>
        /// Module 未找到
        /// </summary>
        ModuleNotFound = 2000,

        /// <summary>
        /// Module 未初始化
        /// </summary>
        ModuleNotInitialized = 2001,

        /// <summary>
        /// Module 初始化失败
        /// </summary>
        ModuleInitFailed = 2002,

        /// <summary>
        /// Module 依赖错误
        /// </summary>
        ModuleDependencyError = 2003,

        /// <summary>
        /// Module 循环依赖
        /// </summary>
        ModuleCircularDependency = 2004,

        #endregion

        #region Provider 相关错误 (3xxx)

        /// <summary>
        /// Provider 未找到
        /// </summary>
        ProviderNotFound = 3000,

        /// <summary>
        /// Provider 未初始化
        /// </summary>
        ProviderNotInitialized = 3001,

        /// <summary>
        /// Provider 初始化失败
        /// </summary>
        ProviderInitFailed = 3002,

        #endregion

        #region 资源相关错误 (4xxx)

        /// <summary>
        /// 资源未找到
        /// </summary>
        ResourceNotFound = 4000,

        /// <summary>
        /// 资源加载失败
        /// </summary>
        ResourceLoadFailed = 4001,

        /// <summary>
        /// 资源类型错误
        /// </summary>
        ResourceTypeMismatch = 4002,

        /// <summary>
        /// 资源已释放
        /// </summary>
        ResourceReleased = 4003,

        #endregion

        #region 网络相关错误 (5xxx)

        /// <summary>
        /// 网络连接失败
        /// </summary>
        NetworkConnectionFailed = 5000,

        /// <summary>
        /// 网络已断开
        /// </summary>
        NetworkDisconnected = 5001,

        /// <summary>
        /// 网络请求失败
        /// </summary>
        NetworkRequestFailed = 5002,

        /// <summary>
        /// 网络请求超时
        /// </summary>
        NetworkTimeout = 5003,

        /// <summary>
        /// 消息发送失败
        /// </summary>
        MessageSendFailed = 5004,

        #endregion

        #region UI 相关错误 (6xxx)

        /// <summary>
        /// UI 未找到
        /// </summary>
        UINotFound = 6000,

        /// <summary>
        /// UI 打开失败
        /// </summary>
        UIOpenFailed = 6001,

        /// <summary>
        /// UI 预制体加载失败
        /// </summary>
        UIPrefabLoadFailed = 6002,

        /// <summary>
        /// UI 类型错误
        /// </summary>
        UITypeMismatch = 6003,

        #endregion

        #region 数据相关错误 (7xxx)

        /// <summary>
        /// 数据序列化失败
        /// </summary>
        SerializeFailed = 7000,

        /// <summary>
        /// 数据反序列化失败
        /// </summary>
        DeserializeFailed = 7001,

        /// <summary>
        /// 数据保存失败
        /// </summary>
        SaveFailed = 7002,

        /// <summary>
        /// 数据加载失败
        /// </summary>
        LoadFailed = 7003,

        /// <summary>
        /// 数据加密失败
        /// </summary>
        EncryptFailed = 7004,

        /// <summary>
        /// 数据解密失败
        /// </summary>
        DecryptFailed = 7005,

        #endregion

        #region 配置相关错误 (8xxx)

        /// <summary>
        /// 配置未找到
        /// </summary>
        ConfigNotFound = 8000,

        /// <summary>
        /// 配置格式错误
        /// </summary>
        ConfigFormatError = 8001,

        /// <summary>
        /// 配置值无效
        /// </summary>
        ConfigInvalidValue = 8002,

        #endregion
    }
}

