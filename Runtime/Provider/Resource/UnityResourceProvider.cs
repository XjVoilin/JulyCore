using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Provider.Base;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JulyCore.Provider.Resource
{
    /// <summary>
    /// Unity Resources 资源提供者实现
    /// 使用 Unity 内置的 Resources.Load 进行资源加载
    /// 作为框架的默认资源提供者，适用于简单项目或原型开发
    /// 
    /// 生产环境建议使用 YooAssetResourceProvider 或其他 AssetBundle 方案
    /// </summary>
    public class UnityResourceProvider : ProviderBase, IResourceProvider
    {
        public override int Priority => Frameworkconst.PriorityResourceProvider;
        protected override LogChannel LogChannel => LogChannel.Resource;

        protected override UniTask OnInitAsync()
        {
            return UniTask.CompletedTask;
        }

        #region 资源加载

        public async UniTask<ResourceHandle<T>> LoadAssetAsync<T>(string fileName, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(fileName))
            {
                LogWarning($"[{Name}] 资源路径不能为空");
                return null;
            }

            var path = NormalizePath(fileName);

            try
            {
                var request = Resources.LoadAsync<T>(path);
                await request.ToUniTask(cancellationToken: cancellationToken);

                if (request.asset == null)
                {
                    LogWarning($"[{Name}] 资源加载失败: {path}");
                    return null;
                }

                var resource = request.asset as T;
                if (resource == null)
                {
                    LogWarning($"[{Name}] 资源类型不匹配: {path}");
                    return null;
                }

                return new ResourceHandle<T>(resource, () =>
                {
                    // GameObject 不能用 Resources.UnloadAsset 释放
                    if (!(resource is GameObject))
                    {
                        Resources.UnloadAsset(resource);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                LogWarning($"[{Name}] 资源加载已取消: {path}");
                return null;
            }
            catch (Exception ex)
            {
                GF.LogException(ex);
                return null;
            }
        }

        public bool HasAsset(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            var path = NormalizePath(fileName);

            var resource = Resources.Load(path);
            if (resource != null)
            {
                Resources.UnloadAsset(resource);
                return true;
            }

            return false;
        }

        public UniTask<bool> DownloadByTagWithRetryAsync(string tag, int maxRetries = 3, CancellationToken ct = default)
        {
            return UniTask.FromResult(false);
        }

        #endregion

        #region 场景加载

        public async UniTask<UnityEngine.SceneManagement.Scene> LoadSceneAsync(
            string sceneName,
            LoadSceneMode loadSceneMode = LoadSceneMode.Single,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                throw new ArgumentException("场景名称不能为空", nameof(sceneName));
            }

            var existingScene = SceneManager.GetSceneByName(sceneName);
            if (existingScene.IsValid() && existingScene.isLoaded)
            {
                LogWarning($"[{Name}] 场景 {sceneName} 已加载，直接返回");
                return existingScene;
            }

            var asyncOperation = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
            if (asyncOperation == null)
            {
                throw new JulyException($"[{Name}] 场景 {sceneName} 加载失败（场景不存在或路径错误）");
            }

            asyncOperation.allowSceneActivation = true;

            while (!asyncOperation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    LogWarning($"[{Name}] 场景 {sceneName} 加载被取消");
                    throw new OperationCanceledException("场景加载被取消", cancellationToken);
                }

                await UniTask.Yield();
            }

            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid())
            {
                throw new JulyException($"[{Name}] 场景 {sceneName} 加载后无效");
            }

            return scene;
        }

        public async UniTask<bool> UnloadSceneAsync(string sceneName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                throw new ArgumentException("场景名称不能为空", nameof(sceneName));
            }

            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                LogWarning($"[{Name}] 场景 {sceneName} 未加载，无需卸载");
                return false;
            }

            var asyncOperation = SceneManager.UnloadSceneAsync(scene);
            if (asyncOperation == null)
            {
                LogWarning($"[{Name}] 场景 {sceneName} 卸载失败");
                return false;
            }

            while (!asyncOperation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    LogWarning($"[{Name}] 场景 {sceneName} 卸载被取消");
                    throw new OperationCanceledException("场景卸载被取消", cancellationToken);
                }

                await UniTask.Yield();
            }

            return true;
        }

        #endregion

        #region Private Methods

        private string NormalizePath(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            var path = System.IO.Path.ChangeExtension(input, null);

            const string resourcesPrefix = "Resources/";
            if (path.StartsWith(resourcesPrefix, StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(resourcesPrefix.Length);
            }

            path = path.Replace('\\', '/');
            return path;
        }

        #endregion
    }
}
