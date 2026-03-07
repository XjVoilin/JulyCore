using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Base;
using JulyCore.Provider.Resource;
using JulyCore.Provider.Scene.Events;
using UnityEngine.SceneManagement;

namespace JulyCore.Module.Scene
{
    /// <summary>
    /// 场景模块
    /// 提供场景加载、卸载、切换等管理功能
    /// 直接使用 IResourceProvider 加载场景，支持场景生命周期事件
    /// </summary>
    internal class SceneModule : ModuleBase, IModuleDependency
    {
        private IResourceProvider _resourceProvider;
        private IEventBus _eventBus;

        protected override LogChannel LogChannel => LogChannel.Scene;

        // 当前场景名称
        private string _currentSceneName;

        // 场景历史栈（用于返回上一场景）
        private readonly Stack<string> _sceneStack = new();

        /// <summary>
        /// 模块执行优先级
        /// </summary>
        public override int Priority => Frameworkconst.PrioritySceneModule;

        /// <summary>
        /// 获取模块依赖
        /// </summary>
        public IEnumerable<Type> GetDependencies()
        {
            return Array.Empty<Type>();
        }

        /// <summary>
        /// 当前场景名称
        /// </summary>
        public string CurrentSceneName => _currentSceneName;

        /// <summary>
        /// 初始化Module
        /// </summary>
        protected override UniTask OnInitAsync()
        {
            try
            {
                // 获取资源提供者（用于加载场景）
                _resourceProvider = GetProvider<IResourceProvider>();
                if (_resourceProvider == null)
                {
                    throw new JulyException($"[{Name}] 未找到 IResourceProvider，请先注册 IResourceProvider");
                }

                // 获取事件总线
                _eventBus = EventBus;

                // 记录当前场景
                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.IsValid())
                {
                    _currentSceneName = activeScene.name;
                }

                Log($"[{Name}] 场景模块初始化完成，当前场景: {_currentSceneName ?? "无"}");
                return base.OnInitAsync();
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 场景模块初始化失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 异步加载场景
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="loadSceneMode">加载模式</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载的场景</returns>
        internal async UniTask<UnityEngine.SceneManagement.Scene> LoadSceneAsync(
            string sceneName, 
            LoadSceneMode loadSceneMode = LoadSceneMode.Single, 
            CancellationToken cancellationToken = default)
        {
            EnsureProvider();

            // 发布场景加载开始事件
            _eventBus.Publish(new SceneLoadStartEvent
            {
                SceneName = sceneName,
                LoadMode = loadSceneMode
            });

            try
            {
                var scene = await _resourceProvider.LoadSceneAsync(sceneName, loadSceneMode, cancellationToken);

                if (loadSceneMode == LoadSceneMode.Single)
                {
                    _currentSceneName = sceneName;
                }

                _eventBus.Publish(new SceneLoadCompleteEvent
                {
                    SceneName = sceneName,
                    Scene = scene,
                    LoadMode = loadSceneMode
                });

                Log($"[{Name}] 场景 {sceneName} 加载完成");
                return scene;
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 场景 {sceneName} 加载失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 异步卸载场景
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否卸载成功</returns>
        internal async UniTask<bool> UnloadSceneAsync(string sceneName, CancellationToken cancellationToken = default)
        {
            EnsureProvider();

            // 发布场景卸载开始事件
            _eventBus.Publish(new SceneUnloadStartEvent
            {
                SceneName = sceneName
            });

            try
            {
                // 直接使用 IResourceProvider 卸载场景
                var success = await _resourceProvider.UnloadSceneAsync(sceneName, cancellationToken);

                // 如果卸载的是当前场景，清空当前场景名称
                if (success && _currentSceneName == sceneName)
                {
                    _currentSceneName = null;
                }

                // 发布场景卸载完成事件
                _eventBus.Publish(new SceneUnloadCompleteEvent
                {
                    SceneName = sceneName,
                    Success = success
                });

                Log($"[{Name}] 场景 {sceneName} 卸载{(success ? "成功" : "失败")}");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 场景 {sceneName} 卸载失败: {ex.Message}");

                // 即使失败也发布事件
                _eventBus.Publish(new SceneUnloadCompleteEvent
                {
                    SceneName = sceneName,
                    Success = false
                });

                throw;
            }
        }

        /// <summary>
        /// 异步切换场景（卸载当前场景并加载新场景）
        /// 将当前场景压入场景栈，支持 GoBackAsync 返回
        /// </summary>
        /// <param name="sceneName">目标场景名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载的场景</returns>
        internal async UniTask<UnityEngine.SceneManagement.Scene> SwitchSceneAsync(
            string sceneName, 
            CancellationToken cancellationToken = default)
        {
            EnsureProvider();

            var fromSceneName = _currentSceneName;

            _eventBus.Publish(new SceneSwitchStartEvent
            {
                FromSceneName = fromSceneName ?? string.Empty,
                ToSceneName = sceneName
            });

            try
            {
                if (!string.IsNullOrEmpty(fromSceneName) && fromSceneName != sceneName)
                {
                    _sceneStack.Push(fromSceneName);
                }

                // LoadSceneMode.Single 会自动卸载旧场景，无需手动 Unload
                var scene = await LoadSceneAsync(sceneName, LoadSceneMode.Single, cancellationToken);

                _eventBus.Publish(new SceneSwitchCompleteEvent
                {
                    FromSceneName = fromSceneName ?? string.Empty,
                    ToSceneName = sceneName,
                    Scene = scene
                });

                Log($"[{Name}] 场景切换完成: {fromSceneName ?? "无"} -> {sceneName}");
                return scene;
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 场景切换失败: {fromSceneName ?? "无"} -> {sceneName}, 错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 返回上一场景（从场景栈中弹出）
        /// 不会将当前场景压入栈，避免无限增长
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载的场景，如果没有上一场景则返回null</returns>
        internal async UniTask<UnityEngine.SceneManagement.Scene?> GoBackAsync(CancellationToken cancellationToken = default)
        {
            if (_sceneStack.Count == 0)
            {
                LogWarning($"[{Name}] 场景栈为空，无法返回上一场景");
                return null;
            }

            var previousSceneName = _sceneStack.Pop();
            var fromSceneName = _currentSceneName;

            _eventBus.Publish(new SceneSwitchStartEvent
            {
                FromSceneName = fromSceneName ?? string.Empty,
                ToSceneName = previousSceneName
            });

            try
            {
                var scene = await LoadSceneAsync(previousSceneName, LoadSceneMode.Single, cancellationToken);

                _eventBus.Publish(new SceneSwitchCompleteEvent
                {
                    FromSceneName = fromSceneName ?? string.Empty,
                    ToSceneName = previousSceneName,
                    Scene = scene
                });

                Log($"[{Name}] 返回场景: {fromSceneName ?? "无"} -> {previousSceneName}");
                return scene;
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 返回场景失败: {fromSceneName ?? "无"} -> {previousSceneName}, 错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取场景（通过 SceneManager）
        /// </summary>
        public bool TryGetScene(string sceneName, out UnityEngine.SceneManagement.Scene scene)
        {
            scene = default;
            if (string.IsNullOrEmpty(sceneName))
            {
                return false;
            }

            scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        /// <summary>
        /// 清空场景栈
        /// </summary>
        internal void ClearSceneStack()
        {
            _sceneStack.Clear();
            Log($"[{Name}] 场景栈已清空");
        }

        /// <summary>
        /// 关闭Module
        /// </summary>
        protected override async UniTask OnShutdownAsync()
        {
            // 清空场景栈
            _sceneStack.Clear();

            _resourceProvider = null;
            _eventBus = null;
            _currentSceneName = null;

            Log($"[{Name}] 场景模块已关闭");
            await base.OnShutdownAsync();
        }

        private void EnsureProvider()
        {
            if (_resourceProvider == null)
            {
                throw new InvalidOperationException($"[{Name}] 资源提供者未初始化");
            }
        }
    }
}
