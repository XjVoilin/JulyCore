namespace JulyCore.Core
{
    /// <summary>
    /// 框架相关的只读内容集中管理
    /// </summary>
    public static class Frameworkconst
    {
        public static readonly string FrameworkName = "JulyGF";

        // 日志标签
        public static readonly string TagFrameworkContext = "[FrameworkContext]";
        public static readonly string TagModuleService = "[ModuleService]";
        public static readonly string TagProviderService = "[ProviderService]";
        public static readonly string TagJulyGameEntry = "[JulyGameEntry]";
        public static readonly string TagEventBus = "[EventBus]";
        public static readonly string TagDependencyContainer = "[DependencyContainer]";

        // 模块优先级（数值越小优先级越高）
        public const int PriorityHotUpdateModule = 1;   // 热更新模块（必须最先初始化）
        public const int PriorityResourceModule = 3;    // 资源模块（需较早初始化，其他模块可能依赖）
        public const int PriorityConfigModule = 5;
        public const int PriorityTimeModule = 8;           // 时间模块（定时器需要尽早初始化）
        public const int PriorityLocalizationModule = 9;   // 多语言模块（UI等模块可能依赖）
        public const int PrioritySerializeModule = 10;
        public const int PrioritySaveModule = 15;
        public const int PriorityNetworkModule = 20;
        public const int PriorityAnalyticsModule = 21;     // 数据统计模块（依赖NetworkModule和SerializeModule）
        public const int PriorityFsmModule = 22;           // 状态机模块
        public const int PriorityPoolModule = 25;           // 对象池模块
        public const int PriorityAdModule = 31;
        public const int PriorityAudioModule = 32;
        public const int PrioritySceneModule = 35;
        public const int PriorityUIModule = 40;
        public const int PriorityABTestModule = 41;          // AB测试模块（在任务模块之前，用于功能开关）
        public const int PriorityTaskModule = 42;           // 任务模块
        public const int PriorityActivityModule = 43;       // 活动模块（在任务模块之后）
        public const int PriorityRedDotModule = 44;         // 红点模块（在活动模块之后，便于联动）
        public const int PriorityPerformanceModule = 45;  // 性能监控模块（在UIModule之后）
        public const int PriorityGuideModule = 50;        // 引导模块（在UI之后）
        public const int PriorityCombatModule = 60;       // 战斗模块

        // Provider 优先级（数值越小优先级越高，越先初始化）
        // 基础服务层（无依赖）
        public const int PrioritySerializeProvider = 10;      // 序列化（最基础）
        public const int PriorityEncryptionProvider = 11;     // 加密（可能被其他 Provider 使用）
        public const int PriorityTimeProvider = 12;           // 时间
        
        // 资源层
        public const int PriorityResourceProvider = 20;       // 资源加载（UI、Audio 等依赖）
        public const int PriorityHotUpdateProvider = 21;      // 热更新（依赖资源）
        public const int PriorityConfigProvider = 22;         // 配置（依赖资源）
        
        // 功能服务层
        public const int PriorityPoolProvider = 30;           // 对象池
        public const int PrioritySaveProvider = 31;           // 存储
        public const int PriorityLocalizationProvider = 32;   // 本地化
        public const int PriorityPerformanceProvider = 33;    // 性能监控
        
        // 业务支撑层
        public const int PriorityUIProvider = 40;             // UI（依赖资源、对象池）
        public const int PriorityAdProvider = 40;              // 广告（独立，无框架内依赖）
        public const int PriorityAudioProvider = 41;          // 音频（依赖资源、对象池）
        public const int PrioritySceneProvider = 42;          // 场景
        
        // 业务扩展层
        public const int PriorityNetworkProvider = 50;        // 网络
        public const int PriorityAnalyticsProvider = 51;      // 数据分析
        public const int PriorityTaskProvider = 52;           // 任务
        public const int PriorityActivityProvider = 53;       // 活动
        public const int PriorityRedDotProvider = 54;         // 红点
        public const int PriorityABTestProvider = 55;         // AB测试
        public const int PriorityGuideProvider = 56;          // 引导
        public const int PriorityCombatProvider = 57;         // 战斗
        
        #region SaveKey
        
        /// <summary>
        /// 新手引导存档Key
        /// </summary>
        public const string GuideSaveKey = "guide_progress";
        
        /// <summary>
        /// 活动数据存档Key
        /// </summary>
        public const string ActivitySaveKey = "activity_data";
        
        #endregion
        
        /// <summary>
        /// 标记为脏时,如果同时携带的信号级别是:Medium  那么如果当前总共被标记的个数的阈值超过了这个值,就会被保存
        /// </summary>
        public const int MediumDirtyCount = 3;
    }
}
