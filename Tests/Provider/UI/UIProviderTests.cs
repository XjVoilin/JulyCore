using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Data.UI;
using JulyCore.Provider.Base;
using JulyCore.Provider.Performance;
using JulyCore.Provider.Resource;
using JulyCore.Provider.UI;
using JulyCore.Provider.UI.Animation;
using JulyGF.Tests.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace JulyGF.Tests.Provider.UI
{
    [TestFixture]
    public class UIProviderTests
    {
        private UIProvider _provider;
        private MockResourceProvider _resourceProvider;
        private FrameworkContext _context;

        [SetUp]
        public void SetUp()
        {
            _context = FrameworkContext.Instance;
            _resourceProvider = new MockResourceProvider();
            
            // 使用新的 DI 容器注册方式
            _context.Container.RegisterSingleton<IResourceProvider>(_resourceProvider);
            _context.ProviderService.Track(_resourceProvider);
            
            // UIProvider 现在需要构造函数参数
            _provider = new UIProvider(_resourceProvider, null);
            _context.Container.RegisterSingleton<IUIProvider>(_provider);
            _context.ProviderService.Track(_provider);
        }

        [TearDown]
        public void TearDown()
        {
            _provider?.Shutdown();
            _context.ProviderService.Clear();
            _context.Container.Clear();
            _provider = null;
            _resourceProvider = null;
        }

        [UnityTest]
        public IEnumerator OnInitAsync_WithResourceProvider_ShouldInitialize()
        {
            yield return _provider.InitAsync().ToCoroutine();

            Assert.IsTrue(_provider.IsInitialized);
        }

        [UnityTest]
        public IEnumerator OpenAsync_ValidOptions_ShouldOpenUI()
        {
            yield return _provider.InitAsync().ToCoroutine();
            var prefab = TestHelpers.CreateMockUIGameObject("TestUIPrefab");
            prefab.AddComponent<MockUIBase>();
            _resourceProvider.SetPrefab(typeof(MockUIBase), prefab);

            var options = new UIOpenOptions
            {
                WindowIdentifier = new WindowIdentifier(1, "MockUIBase"),
                Layer = UILayer.Normal,
                OpenAnimationType = UIAnimationType.None
            };

            var ui = _provider.OpenAsync(options).ToCoroutine();

            yield return ui;

            Assert.IsNotNull(ui);
        }

        [UnityTest]
        public IEnumerator OpenAsync_NullOptions_ShouldThrowArgumentNullException()
        {
            yield return _provider.InitAsync().ToCoroutine();

            var exception = Assert.ThrowsAsync<ArgumentNullException>(async () => { await _provider.OpenAsync(null); });

            yield return null;
        }

        [UnityTest]
        public IEnumerator CloseAsync_OpenedUI_ShouldCloseUI()
        {
            yield return _provider.InitAsync().ToCoroutine();
            var prefab = TestHelpers.CreateMockUIGameObject("TestUIPrefab");
            prefab.AddComponent<MockUIBase>();
            _resourceProvider.SetPrefab(typeof(MockUIBase), prefab);

            var options = new UIOpenOptions
            {
                WindowIdentifier = new WindowIdentifier(1, "MockUIBase"),
                Layer = UILayer.Normal,
                OpenAnimationType = UIAnimationType.None
            };

            UIBase ui = null;
            yield return _provider.OpenAsync(options).ToCoroutine(x => ui = x);
            yield return _provider.CloseAsync(new WindowIdentifier(1, "MockUIBase")).ToCoroutine();

            Assert.IsFalse(ui.IsOpened);
        }

        [UnityTest]
        public IEnumerator CloseAsync_NonExistentUI_ShouldNotThrow()
        {
            yield return _provider.InitAsync().ToCoroutine();

            yield return _provider.CloseAsync(new WindowIdentifier(1, "NonExistent")).ToCoroutine();
        }

        [Test]
        public void IsOpen_OpenedUI_ShouldReturnTrue()
        {
            _provider.InitAsync().GetAwaiter().GetResult();
            var prefab = TestHelpers.CreateMockUIGameObject("TestUIPrefab");
            prefab.AddComponent<MockUIBase>();
            _resourceProvider.SetPrefab(typeof(MockUIBase), prefab);

            var options = new UIOpenOptions
            {
                WindowIdentifier = new WindowIdentifier(1, "MockUIBase"),
                Layer = UILayer.Normal,
                OpenAnimationType = UIAnimationType.None
            };

            _provider.OpenAsync(options).GetAwaiter().GetResult();
            bool isOpen = _provider.IsOpen(new WindowIdentifier(1, "MockUIBase"));

            Assert.IsTrue(isOpen);
        }

        [Test]
        public void IsOpen_NonExistentUI_ShouldReturnFalse()
        {
            _provider.InitAsync().GetAwaiter().GetResult();

            bool isOpen = _provider.IsOpen(new WindowIdentifier(1, "NonExistent"));

            Assert.IsFalse(isOpen);
        }

        private class MockResourceProvider : ProviderBase, IResourceProvider
        {
            private Dictionary<Type, GameObject> _prefabs = new();

            public void SetPrefab(Type type, GameObject prefab)
            {
                _prefabs[type] = prefab;
            }

            public UniTask<T> LoadAsync<T>(string path, System.Threading.CancellationToken cancellationToken = default)
                where T : UnityEngine.Object
            {
                if (typeof(T) == typeof(GameObject))
                {
                    foreach (var kvp in _prefabs)
                    {
                        if (path.Contains(kvp.Key.Name))
                        {
                            return UniTask.FromResult(kvp.Value as T);
                        }
                    }
                }

                return UniTask.FromResult<T>(null);
            }

            public UniTask<ResourceHandle<T>> LoadWithHandleAsync<T>(string fileName, bool captureStackTrace = false,
                CancellationToken cancellationToken = default) where T : Object
            {
                throw new NotImplementedException();
            }

            public UniTask<bool> PreloadAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : Object
            {
                throw new NotImplementedException();
            }

            public UniTask<List<T>> LoadBatchAsync<T>(IEnumerable<string> fileNames, CancellationToken cancellationToken = default) where T : Object
            {
                throw new NotImplementedException();
            }

            public UniTask<T> LoadSubAssetAsync<T>(string fileName, string assetName, CancellationToken cancellationToken = default) where T : Object
            {
                throw new NotImplementedException();
            }

            public UniTask<List<T>> LoadAllSubAssetsAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : Object
            {
                throw new NotImplementedException();
            }

            public UniTask<bool> DownloadByTagWithRetryAsync(string tag, int maxRetries = 3, CancellationToken ct = default)
            {
                throw new NotImplementedException();
            }

            public bool HasAsset(string fileName)
            {
                throw new NotImplementedException();
            }

            public void Unload(Object obj)
            {
            }

            public T Load<T>(string path) where T : UnityEngine.Object
            {
                return LoadAsync<T>(path).GetAwaiter().GetResult();
            }

            public void Unload(string path)
            {
            }

            public void UnloadAll()
            {
            }

            public int GetRefCount(Object obj)
            {
                throw new NotImplementedException();
            }

            public ResourceStatistics GetStatistics()
            {
                throw new NotImplementedException();
            }

            public UniTask<Scene> LoadSceneAsync(string sceneName, LoadSceneMode loadSceneMode = LoadSceneMode.Single,
                CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public UniTask<bool> UnloadSceneAsync(string sceneName, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            protected override LogChannel LogChannel { get; }
        }
    }
}
