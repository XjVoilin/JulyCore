using System;
using System.Collections.Generic;
using System.Reflection;

namespace JulyCore.Data.HotUpdate
{
    /// <summary>
    /// 热更新阶段
    /// </summary>
    public enum HotUpdateStage
    {
        /// <summary>
        /// 准备阶段
        /// </summary>
        Preparing,

        /// <summary>
        /// 加载AOT元数据
        /// </summary>
        LoadingAOTMetadata,

        /// <summary>
        /// 加载热更新程序集
        /// </summary>
        LoadingHotUpdateAssemblies,

        /// <summary>
        /// 执行入口方法
        /// </summary>
        ExecutingEntry,

        /// <summary>
        /// 完成
        /// </summary>
        Completed,

        /// <summary>
        /// 失败
        /// </summary>
        Failed
    }

    /// <summary>
    /// 热更新配置
    /// </summary>
    [Serializable]
    public class HotUpdateConfig
    {
        /// <summary>
        /// 热更新程序集名称列表（不含.dll后缀）
        /// </summary>
        public List<string> HotUpdateAssemblyNames { get; set; } = new List<string>();

        /// <summary>
        /// AOT元数据程序集名称列表（用于补充泛型等元数据，不含.dll后缀）
        /// </summary>
        public List<string> AOTMetaAssemblyNames { get; set; } = new List<string>();

        /// <summary>
        /// 热更新DLL资源路径前缀（如 "Assets/HotUpdateDlls/"）
        /// </summary>
        public string HotUpdateDllPathPrefix { get; set; } = "HotUpdateDlls/";

        /// <summary>
        /// AOT元数据DLL资源路径前缀
        /// </summary>
        public string AOTMetaDllPathPrefix { get; set; } = "AOTMetaDlls/";

        /// <summary>
        /// 热更新入口类全名（如 "HotUpdate.GameEntry"）
        /// </summary>
        public string EntryClassName { get; set; }

        /// <summary>
        /// 热更新入口方法名（如 "Start"）
        /// </summary>
        public string EntryMethodName { get; set; } = "Start";

        /// <summary>
        /// 是否在编辑器模式下跳过热更新加载
        /// </summary>
        public bool SkipInEditor { get; set; } = true;
    }

    /// <summary>
    /// 热更新加载结果
    /// </summary>
    [Serializable]
    public class HotUpdateResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 已加载的热更新程序集
        /// </summary>
        public List<Assembly> LoadedAssemblies { get; set; } = new List<Assembly>();

        /// <summary>
        /// AOT元数据加载结果（程序集名称 -> 是否成功）
        /// </summary>
        public Dictionary<string, bool> AOTMetaLoadResults { get; set; } = new Dictionary<string, bool>();

        public static HotUpdateResult Success(List<Assembly> assemblies)
        {
            return new HotUpdateResult
            {
                IsSuccess = true,
                LoadedAssemblies = assemblies ?? new List<Assembly>()
            };
        }

        public static HotUpdateResult Failure(string error)
        {
            return new HotUpdateResult
            {
                IsSuccess = false,
                ErrorMessage = error
            };
        }
    }

    /// <summary>
    /// 热更新进度回调
    /// </summary>
    [Serializable]
    public class HotUpdateProgress
    {
        /// <summary>
        /// 当前阶段
        /// </summary>
        public HotUpdateStage Stage { get; set; }

        /// <summary>
        /// 当前进度（0-1）
        /// </summary>
        public float Progress { get; set; }

        /// <summary>
        /// 当前正在处理的项目名称
        /// </summary>
        public string CurrentItem { get; set; }

        /// <summary>
        /// 描述信息
        /// </summary>
        public string Description { get; set; }
    }
}

