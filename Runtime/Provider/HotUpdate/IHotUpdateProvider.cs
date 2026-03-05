using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Data.HotUpdate;

namespace JulyCore.Provider.HotUpdate
{
    /// <summary>
    /// 热更新提供者接口
    /// 纯技术执行层：负责程序集加载、元数据补充、入口执行
    /// 不包含业务逻辑，不维护业务状态
    /// </summary>
    public interface IHotUpdateProvider : IProvider
    {
        /// <summary>
        /// 是否已加载热更新程序集
        /// </summary>
        bool IsHotUpdateLoaded { get; }

        /// <summary>
        /// 已加载的热更新程序集列表
        /// </summary>
        IReadOnlyList<Assembly> LoadedAssemblies { get; }

        /// <summary>
        /// 加载热更新（包括AOT元数据和热更新程序集）
        /// </summary>
        /// <param name="config">热更新配置</param>
        /// <param name="progressCallback">进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载结果</returns>
        UniTask<HotUpdateResult> LoadHotUpdateAsync(
            HotUpdateConfig config,
            Action<HotUpdateProgress> progressCallback = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 仅加载AOT补充元数据
        /// </summary>
        /// <param name="aotAssemblyNames">AOT程序集名称列表</param>
        /// <param name="pathPrefix">资源路径前缀</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载结果（程序集名称 -> 是否成功）</returns>
        UniTask<Dictionary<string, bool>> LoadAOTMetadataAsync(
            List<string> aotAssemblyNames,
            string pathPrefix,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 仅加载热更新程序集
        /// </summary>
        /// <param name="assemblyNames">程序集名称列表</param>
        /// <param name="pathPrefix">资源路径前缀</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>已加载的程序集列表</returns>
        UniTask<List<Assembly>> LoadAssembliesAsync(
            List<string> assemblyNames,
            string pathPrefix,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 执行热更新入口方法
        /// </summary>
        /// <param name="entryClassName">入口类全名</param>
        /// <param name="entryMethodName">入口方法名</param>
        /// <param name="parameters">方法参数</param>
        /// <returns>是否执行成功</returns>
        bool ExecuteEntry(string entryClassName, string entryMethodName, object[] parameters = null);

        /// <summary>
        /// 从已加载的热更新程序集中获取类型
        /// </summary>
        /// <param name="typeFullName">类型全名</param>
        /// <returns>类型，未找到返回null</returns>
        Type GetHotUpdateType(string typeFullName);

        /// <summary>
        /// 从已加载的热更新程序集中获取所有类型
        /// </summary>
        /// <param name="predicate">筛选条件</param>
        /// <returns>符合条件的类型列表</returns>
        List<Type> GetHotUpdateTypes(Func<Type, bool> predicate = null);
    }
}

