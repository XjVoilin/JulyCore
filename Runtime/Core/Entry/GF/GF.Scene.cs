using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Module.Scene;
using UnityEngine.SceneManagement;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 场景相关操作
        /// 
        /// 【功能说明】
        /// - 场景加载/卸载/切换
        /// - 场景栈管理（支持返回上一场景）
        /// - 场景生命周期事件（通过 EventBus 发布）
        /// 
        /// 【使用示例】
        /// // 加载场景
        /// await GF.Scene.LoadAsync("MainScene");
        /// 
        /// // 切换场景（自动卸载当前场景）
        /// await GF.Scene.SwitchAsync("BattleScene");
        /// 
        /// // 返回上一场景
        /// await GF.Scene.GoBackAsync();
        /// </summary>
        public static class Scene
        {
            private static SceneModule _module;

            private static SceneModule Module
            {
                get
                {
                    _module ??= GetModule<SceneModule>();
                    return _module;
                }
            }

            /// <summary>
            /// 当前场景名称
            /// </summary>
            public static string CurrentSceneName => Module.CurrentSceneName;

            #region 场景加载

            /// <summary>
            /// 异步加载场景
            /// </summary>
            /// <param name="sceneName">场景名称</param>
            /// <param name="loadSceneMode">加载模式（Single: 替换当前场景，Additive: 叠加场景）</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>加载的场景</returns>
            public static UniTask<UnityEngine.SceneManagement.Scene> LoadAsync(
                string sceneName,
                LoadSceneMode loadSceneMode = LoadSceneMode.Single,
                CancellationToken cancellationToken = default)
            {
                return Module.LoadSceneAsync(sceneName, loadSceneMode, cancellationToken);
            }

            /// <summary>
            /// 异步卸载场景
            /// </summary>
            /// <param name="sceneName">场景名称</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>是否卸载成功</returns>
            public static UniTask<bool> UnloadAsync(
                string sceneName,
                CancellationToken cancellationToken = default)
            {
                return Module.UnloadSceneAsync(sceneName, cancellationToken);
            }

            #endregion

            #region 场景切换

            /// <summary>
            /// 异步切换场景（卸载当前场景并加载新场景）
            /// 会将当前场景压入场景栈，支持 GoBackAsync 返回
            /// </summary>
            /// <param name="sceneName">目标场景名称</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>加载的场景</returns>
            public static UniTask<UnityEngine.SceneManagement.Scene> SwitchAsync(
                string sceneName,
                CancellationToken cancellationToken = default)
            {
                return Module.SwitchSceneAsync(sceneName, cancellationToken);
            }

            /// <summary>
            /// 返回上一场景（从场景栈中弹出）
            /// </summary>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>加载的场景，如果没有上一场景则返回 null</returns>
            public static UniTask<UnityEngine.SceneManagement.Scene?> GoBackAsync(
                CancellationToken cancellationToken = default)
            {
                return Module.GoBackAsync(cancellationToken);
            }

            /// <summary>
            /// 清空场景栈
            /// </summary>
            public static void ClearSceneStack()
            {
                Module.ClearSceneStack();
            }

            #endregion

            #region 场景查询

            /// <summary>
            /// 尝试获取已加载的场景
            /// </summary>
            /// <param name="sceneName">场景名称</param>
            /// <param name="scene">输出场景</param>
            /// <returns>场景是否已加载</returns>
            public static bool TryGetScene(string sceneName, out UnityEngine.SceneManagement.Scene scene)
            {
                return Module.TryGetScene(sceneName, out scene);
            }

            #endregion
        }
    }
}

