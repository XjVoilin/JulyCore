// using System;
// using System.Collections;
// using System.Collections.Generic;
// using Cysharp.Threading.Tasks;
// using JulyCore.Core;
// using JulyCore.Module.Config;
// using JulyCore.Provider.Base;
// using JulyCore.Provider.Config;
// using NUnit.Framework;
// using UnityEngine.TestTools;
//
// namespace JulyGF.Tests.Module
// {
//     [TestFixture]
//     public class ConfigModuleTests
//     {
//         private ConfigModule _module;
//         private MockConfigProvider _provider;
//         private FrameworkContext _context;
//
//         [SetUp]
//         public void SetUp()
//         {
//             _context = FrameworkContext.Instance;
//             _provider = new MockConfigProvider();
//             _context.ProviderService.RegisterProvider(_provider);
//             _module = new ConfigModule();
//         }
//
//         [TearDown]
//         public void TearDown()
//         {
//             _module?.ShutdownAsync().GetAwaiter().GetResult();
//             _module?.Dispose();
//             _context.ProviderService.Clear();
//             _module = null;
//             _provider = null;
//         }
//
//         [UnityTest]
//         public IEnumerator OnInitAsync_WithProvider_ShouldInitialize()
//         {
//             yield return _module.InitAsync().ToCoroutine();
//
//             Assert.IsTrue(_module.IsInitialized);
//         }
//
//         [UnityTest]
//         public IEnumerator LoadConfigAsync_ValidConfig_ShouldLoad()
//         {
//             yield return _module.InitAsync().ToCoroutine();
//             var testData = new TestConfigData { Value = 42 };
//             _provider.SetConfig("TestConfig", testData);
//
//
//             TestConfigData result = null;
//             yield return _module.LoadConfigAsync<TestConfigData>("TestConfig").ToCoroutine(x => result = x);
//
//             Assert.IsNotNull(result);
//             Assert.AreEqual(42, result.Value);
//         }
//
//         [Test]
//         public void GetConfig_LoadedConfig_ShouldReturnConfig()
//         {
//             _module.InitAsync().GetAwaiter().GetResult();
//             var testData = new TestConfigData { Value = 42 };
//             _provider.SetConfig("TestConfig", testData);
//             _module.LoadConfigAsync<TestConfigData>("TestConfig").GetAwaiter().GetResult();
//
//             var result = _module.GetConfig<TestConfigData>("TestConfig");
//
//             Assert.IsNotNull(result);
//             Assert.AreEqual(42, result.Value);
//         }
//
//         [Test]
//         public void GetConfig_UnloadedConfig_ShouldThrowJulyException()
//         {
//             _module.InitAsync().GetAwaiter().GetResult();
//
//             Assert.Throws<JulyException>(() => { _module.GetConfig<TestConfigData>("NonExistentConfig"); });
//         }
//
//         [Test]
//         public void GetEntry_ValidEntry_ShouldReturnValue()
//         {
//             _module.InitAsync().GetAwaiter().GetResult();
//             var dict = new Dictionary<int, string> { { 1, "Value1" }, { 2, "Value2" } };
//             _provider.SetConfig("TestDict", dict);
//             _module.LoadConfigAsync<Dictionary<int, string>>("TestDict").GetAwaiter().GetResult();
//
//             var result = _module.GetEntry<int, string>("TestDict", 1);
//
//             Assert.AreEqual("Value1", result);
//         }
//
//         [Test]
//         public void GetEntry_InvalidKey_ShouldThrowJulyException()
//         {
//             _module.InitAsync().GetAwaiter().GetResult();
//             var dict = new Dictionary<int, string> { { 1, "Value1" } };
//             _provider.SetConfig("TestDict", dict);
//             _module.LoadConfigAsync<Dictionary<int, string>>("TestDict").GetAwaiter().GetResult();
//
//             Assert.Throws<JulyException>(() => { _module.GetEntry<int, string>("TestDict", 999); });
//         }
//
//         [Test]
//         public void TryGetEntry_ValidEntry_ShouldReturnTrue()
//         {
//             _module.InitAsync().GetAwaiter().GetResult();
//             var dict = new Dictionary<int, string> { { 1, "Value1" } };
//             _provider.SetConfig("TestDict", dict);
//             _module.LoadConfigAsync<Dictionary<int, string>>("TestDict").GetAwaiter().GetResult();
//
//             bool success = _module.TryGetEntry<int, string>("TestDict", 1, out var value);
//
//             Assert.IsTrue(success);
//             Assert.AreEqual("Value1", value);
//         }
//
//         [Test]
//         public void TryGetEntry_InvalidKey_ShouldReturnFalse()
//         {
//             _module.InitAsync().GetAwaiter().GetResult();
//             var dict = new Dictionary<int, string> { { 1, "Value1" } };
//             _provider.SetConfig("TestDict", dict);
//             _module.LoadConfigAsync<Dictionary<int, string>>("TestDict").GetAwaiter().GetResult();
//
//             bool success = _module.TryGetEntry<int, string>("TestDict", 999, out var value);
//
//             Assert.IsFalse(success);
//             Assert.IsNull(value);
//         }
//
//         [UnityTest]
//         public IEnumerator OnShutdownAsync_ShouldClearCache()
//         {
//             yield return _module.InitAsync().ToCoroutine();
//             var testData = new TestConfigData { Value = 42 };
//             _provider.SetConfig("TestConfig", testData);
//             yield return _module.LoadConfigAsync<TestConfigData>("TestConfig").ToCoroutine();
//
//             yield return _module.ShutdownAsync().ToCoroutine();
//
//             Assert.IsTrue(_provider.CacheCleared);
//         }
//
//         [Serializable]
//         private class TestConfigData
//         {
//             public int Value;
//         }
//
//         private class MockConfigProvider : ProviderBase, IConfigProvider
//         {
//             private Dictionary<string, object> _configs = new Dictionary<string, object>();
//             public bool CacheCleared { get; private set; }
//
//             public void SetConfig<T>(string key, T value)
//             {
//                 _configs[key] = value;
//             }
//
//             public UniTask<T> LoadAsync<T>(string configKey, string resourcePath = null,
//                 System.Threading.CancellationToken cancellationToken = default)
//             {
//                 if (_configs.TryGetValue(configKey, out var value) && value is T)
//                 {
//                     return UniTask.FromResult((T)value);
//                 }
//
//                 throw new JulyException($"Config {configKey} not found");
//             }
//
//             public T Get<T>(string configKey)
//             {
//                 if (_configs.TryGetValue(configKey, out var value) && value is T)
//                 {
//                     return (T)value;
//                 }
//
//                 throw new JulyException($"Config {configKey} not found");
//             }
//
//             public bool TryGet<T>(string configKey, out T value)
//             {
//                 value = default(T);
//                 if (_configs.TryGetValue(configKey, out var obj) && obj is T)
//                 {
//                     value = (T)obj;
//                     return true;
//                 }
//
//                 return false;
//             }
//
//             public void ClearCache(string configKey = null)
//             {
//                 if (string.IsNullOrEmpty(configKey))
//                 {
//                     _configs.Clear();
//                     CacheCleared = true;
//                 }
//                 else
//                 {
//                     _configs.Remove(configKey);
//                 }
//             }
//         }
//     }
// }