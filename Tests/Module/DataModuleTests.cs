using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Data;
using JulyCore.Provider.Base;
using JulyCore.Provider.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JulyGF.Tests.Module
{
    [TestFixture]
    public class DataModuleTests
    {
        private SerializeModule _module;
        private MockSerializeProvider _provider;
        private FrameworkContext _context;

        [SetUp]
        public void SetUp()
        {
            _context = FrameworkContext.Instance;
            _provider = new MockSerializeProvider();
            
            _context.Registry.Register<ISerializeProvider>(_provider);
            _context.ProviderService.Track(_provider);
            _module = new SerializeModule();
        }

        [TearDown]
        public void TearDown()
        {
            _module?.ShutdownAsync().GetAwaiter().GetResult();
            _module?.Dispose();
            _context.ProviderService.Clear();
            _context.Registry.Clear();
            _module = null;
            _provider = null;
        }

        [UnityTest]
        public IEnumerator OnInitAsync_WithProvider_ShouldInitialize()
        {
            yield return _module.InitAsync().ToCoroutine();

            Assert.IsTrue(_module.IsInitialized);
        }

        [Test]
        public void Serialize_ValidData_ShouldReturnBytes()
        {
            _module.InitAsync().GetAwaiter().GetResult();
            var testData = new TestData { Value = 42, Name = "Test" };

            var bytes = _module.Serialize(testData);

            Assert.IsNotNull(bytes);
            Assert.Greater(bytes.Length, 0);
        }

        [Test]
        public void Serialize_WithoutProvider_ShouldThrowInvalidOperationException()
        {
            var module = new SerializeModule();
            var testData = new TestData { Value = 42 };

            Assert.Throws<InvalidOperationException>(() => { module.Serialize(testData); });
        }

        [Test]
        public void Deserialize_ValidBytes_ShouldReturnData()
        {
            _module.InitAsync().GetAwaiter().GetResult();
            var testData = new TestData { Value = 42, Name = "Test" };
            var bytes = _module.Serialize(testData);

            var result = _module.Deserialize<TestData>(bytes);

            Assert.IsNotNull(result);
            Assert.AreEqual(42, result.Value);
            Assert.AreEqual("Test", result.Name);
        }

        [Test]
        public void Deserialize_EmptyBytes_ShouldReturnDefault()
        {
            _module.InitAsync().GetAwaiter().GetResult();

            var result = _module.Deserialize<TestData>(new byte[0]);

            Assert.IsNull(result);
        }

        [Test]
        public void Deserialize_NullBytes_ShouldReturnDefault()
        {
            _module.InitAsync().GetAwaiter().GetResult();

            var result = _module.Deserialize<TestData>(null);

            Assert.IsNull(result);
        }

        [Test]
        public void Deserialize_WithoutProvider_ShouldThrowInvalidOperationException()
        {
            var module = new SerializeModule();
            var bytes = new byte[] { 1, 2, 3 };

            Assert.Throws<InvalidOperationException>(() => { module.Deserialize<TestData>(bytes); });
        }

        [UnityTest]
        public IEnumerator SerializeAsync_ValidData_ShouldReturnBytes()
        {
            yield return _module.InitAsync().ToCoroutine();
            var testData = new TestData { Value = 42 };

            byte[] bytes = null;
            yield return _module.SerializeAsync(testData).ToCoroutine(x => bytes = x);

            Assert.IsNotNull(bytes);
            Assert.Greater(bytes.Length, 0);
        }

        [UnityTest]
        public IEnumerator DeserializeAsync_ValidBytes_ShouldReturnData()
        {
            yield return _module.InitAsync().ToCoroutine();
            var testData = new TestData { Value = 42, Name = "Test" };
            byte[] bytes = null;
            yield return _module.SerializeAsync(testData).ToCoroutine(x => bytes = x);

            Assert.IsNotNull(bytes);
            TestData result = null;
            yield return _module.DeserializeAsync<TestData>(bytes).ToCoroutine(x => result = x);

            Assert.IsNotNull(result);
            Assert.AreEqual(42, result.Value);
            Assert.AreEqual("Test", result.Name);
        }

        [UnityTest]
        public IEnumerator DeserializeAsync_EmptyBytes_ShouldReturnDefault()
        {
            yield return _module.InitAsync().ToCoroutine();

            TestData result = null;
            yield return _module.DeserializeAsync<TestData>(Array.Empty<byte>()).ToCoroutine(x => result = x);

            Assert.IsNull(result);
        }

        [Serializable]
        private class TestData
        {
            public int Value;
            public string Name;
        }

        private class MockSerializeProvider : ProviderBase, ISerializeProvider
        {
            public byte[] Serialize<T>(T data)
            {
                var json = JsonUtility.ToJson(data);
                return System.Text.Encoding.UTF8.GetBytes(json);
            }

            public T Deserialize<T>(byte[] bytes)
            {
                if (bytes == null || bytes.Length == 0)
                {
                    return default(T);
                }

                var json = System.Text.Encoding.UTF8.GetString(bytes);
                return JsonUtility.FromJson<T>(json);
            }

            public UniTask<byte[]> SerializeAsync<T>(T data,
                System.Threading.CancellationToken cancellationToken = default)
            {
                return UniTask.FromResult(Serialize(data));
            }

            public UniTask<T> DeserializeAsync<T>(byte[] bytes,
                System.Threading.CancellationToken cancellationToken = default)
            {
                return UniTask.FromResult(Deserialize<T>(bytes));
            }

            public string SerializeToJson(object data)
            {
                return JsonUtility.ToJson(data);
            }

            public object DeserializeFromJson(string json, System.Type type)
            {
                return JsonUtility.FromJson(json, type);
            }

            protected override LogChannel LogChannel { get; }
        }
    }
}
