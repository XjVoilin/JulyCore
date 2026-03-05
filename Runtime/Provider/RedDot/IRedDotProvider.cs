using System.Collections.Generic;
using JulyCore.Core;
using JulyCore.Data.RedDot;

namespace JulyCore.Provider.RedDot
{
    /// <summary>
    /// 红点存储提供者接口
    /// 纯技术层：仅负责红点树结构存储、节点CRUD操作
    /// 不包含业务逻辑（如刷新策略、系统关联）
    /// 
    /// 用户可实现此接口来自定义存储方式（如：服务器同步等）
    /// </summary>
    public interface IRedDotProvider : IProvider
    {
        #region 节点存储（CRUD）

        /// <summary>
        /// 存储节点
        /// </summary>
        /// <param name="key">节点Key</param>
        /// <param name="parentKey">父节点Key（null表示根节点）</param>
        /// <param name="type">红点类型</param>
        /// <returns>是否存储成功</returns>
        bool Store(string key, string parentKey = null, RedDotType type = RedDotType.Normal);

        /// <summary>
        /// 批量存储节点
        /// </summary>
        /// <param name="nodes">节点配置列表</param>
        void StoreBatch(IEnumerable<(string Key, string ParentKey, RedDotType Type)> nodes);

        /// <summary>
        /// 删除节点（包括子节点）
        /// </summary>
        /// <param name="key">节点Key</param>
        /// <returns>是否删除成功</returns>
        bool Remove(string key);

        /// <summary>
        /// 清空所有节点
        /// </summary>
        void Clear();

        #endregion

        #region 节点查询

        /// <summary>
        /// 获取节点
        /// </summary>
        /// <param name="key">节点Key</param>
        /// <returns>节点数据，不存在返回null</returns>
        RedDotNode Get(string key);

        /// <summary>
        /// 检查节点是否存在
        /// </summary>
        /// <param name="key">节点Key</param>
        /// <returns>是否存在</returns>
        bool Exists(string key);

        /// <summary>
        /// 获取所有节点
        /// </summary>
        /// <returns>节点列表</returns>
        List<RedDotNode> GetAll();

        /// <summary>
        /// 获取所有根节点
        /// </summary>
        /// <returns>根节点列表</returns>
        List<RedDotNode> GetRootNodes();

        /// <summary>
        /// 获取子节点
        /// </summary>
        /// <param name="key">父节点Key</param>
        /// <returns>子节点列表</returns>
        List<RedDotNode> GetChildren(string key);

        /// <summary>
        /// 获取所有叶子节点
        /// </summary>
        /// <returns>叶子节点列表</returns>
        List<RedDotNode> GetLeafNodes();

        /// <summary>
        /// 获取节点路径（从根到当前节点）
        /// </summary>
        /// <param name="key">节点Key</param>
        /// <returns>路径Key列表</returns>
        List<string> GetPath(string key);

        #endregion

        #region 红点值操作

        /// <summary>
        /// 设置节点红点数量
        /// </summary>
        /// <param name="key">节点Key</param>
        /// <param name="count">数量</param>
        /// <returns>变更信息列表（包含受影响的所有父节点）</returns>
        List<RedDotChangeInfo> SetCount(string key, int count);

        /// <summary>
        /// 批量设置节点红点数量（优化：避免重复计算父节点）
        /// </summary>
        /// <param name="counts">节点Key和数量的字典</param>
        /// <returns>变更信息列表（已去重）</returns>
        List<RedDotChangeInfo> SetCountBatch(Dictionary<string, int> counts);

        /// <summary>
        /// 获取节点红点数量（包含子节点汇总）
        /// </summary>
        /// <param name="key">节点Key</param>
        /// <returns>数量</returns>
        int GetCount(string key);

        /// <summary>
        /// 使节点缓存失效
        /// </summary>
        /// <param name="key">节点Key</param>
        void InvalidateCache(string key);

        /// <summary>
        /// 使所有缓存失效
        /// </summary>
        void InvalidateAllCache();

        /// <summary>
        /// 重新计算节点值
        /// </summary>
        /// <param name="key">节点Key</param>
        /// <returns>计算后的值</returns>
        int Recalculate(string key);

        /// <summary>
        /// 设置节点启用状态
        /// 禁用后该节点及其子节点的 GetCount 返回 0
        /// </summary>
        /// <param name="key">节点Key</param>
        /// <param name="enabled">是否启用</param>
        void SetEnabled(string key, bool enabled);

        /// <summary>
        /// 获取节点启用状态（包括检查父节点）
        /// </summary>
        /// <param name="key">节点Key</param>
        /// <returns>是否启用</returns>
        bool GetEnabled(string key);

        /// <summary>
        /// 设置全局启用状态（影响所有红点）
        /// </summary>
        /// <param name="enabled">是否启用</param>
        void SetAllEnabled(bool enabled);

        /// <summary>
        /// 获取全局启用状态
        /// </summary>
        /// <returns>是否启用</returns>
        bool GetAllEnabled();

        #endregion

        #region 数据导入导出

        /// <summary>
        /// 导出红点状态（叶子节点的值）
        /// </summary>
        /// <returns>状态数据</returns>
        Dictionary<string, int> Export();

        /// <summary>
        /// 导入红点状态
        /// </summary>
        /// <param name="stateData">状态数据</param>
        void Import(Dictionary<string, int> stateData);

        #endregion
    }
}
