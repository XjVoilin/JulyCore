using System;
using System.Collections;
using JulyCore.Provider.UI;
using JulyCore.Provider.UI.Pool;
using JulyGF.Tests.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JulyGF.Tests.Provider.UI.Pool
{
    /// <summary>
    /// UI对象池测试
    /// </summary>
    [TestFixture]
    public class UIPoolTests
    {
        private UIPool _pool;
        private GameObject _prefab;
        private Transform _parent;

        [SetUp]
        public void SetUp()
        {
            _pool = new UIPool(maxSizePerType: 2);
            _prefab = TestHelpers.CreateMockUIGameObject("UIPrefab");
            _prefab.AddComponent<MockUIBase>();
            _parent = new GameObject("Parent").transform;
        }

        [TearDown]
        public void TearDown()
        {
            _pool.ClearAllPools();
            TestHelpers.DestroyGameObject(_prefab);
            TestHelpers.DestroyGameObject(_parent.gameObject);
            _pool = null;
        }

        [Test]
        public void RegisterPrefab_ValidPrefab_ShouldRegisterSuccessfully()
        {
            // Act
            _pool.RegisterPrefab(typeof(MockUIBase), _prefab);

            // Assert
            var ui = _pool.GetOrCreate(typeof(MockUIBase), _parent) as MockUIBase;
            Assert.IsNotNull(ui);
        }

        [Test]
        public void RegisterPrefab_NullPrefab_ShouldNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                _pool.RegisterPrefab(typeof(MockUIBase), null);
            });
        }

        [Test]
        public void GetOrCreate_RegisteredPrefab_ShouldCreateInstance()
        {
            // Arrange
            _pool.RegisterPrefab(typeof(MockUIBase), _prefab);

            // Act
            var ui = _pool.GetOrCreate(typeof(MockUIBase), _parent) as MockUIBase;

            // Assert
            Assert.IsNotNull(ui);
            Assert.IsInstanceOf<MockUIBase>(ui);
            Assert.AreEqual(_parent, ui.transform.parent);
            Assert.IsTrue(ui.gameObject.activeSelf);
        }

        [Test]
        public void GetOrCreate_UnregisteredPrefab_ShouldReturnNull()
        {
            // Act
            var ui = _pool.GetOrCreate(typeof(MockUIBase), _parent);

            // Assert
            Assert.IsNull(ui);
        }

        [Test]
        public void ReturnToPool_ValidUI_ShouldReturnToPool()
        {
            // Arrange
            _pool.RegisterPrefab(typeof(MockUIBase), _prefab);
            var ui1 = _pool.GetOrCreate(typeof(MockUIBase), _parent) as MockUIBase;
            Assert.IsNotNull(ui1);

            // Act
            _pool.ReturnToPool(typeof(MockUIBase), ui1);
            var ui2 = _pool.GetOrCreate(typeof(MockUIBase), _parent) as MockUIBase;

            _pool.ReturnToPool(typeof(MockUIBase), ui2);
            // Assert
            Assert.IsNotNull(ui2);
            Assert.AreSame(ui1, ui2, "应该返回同一个实例");
            Assert.IsFalse(ui2.gameObject.activeSelf, "回收后应该处于非激活状态");
        }

        [UnityTest]
        public IEnumerator ReturnToPool_UIExceedsMaxSize_ShouldDestroyInstance()
        {
            _pool = new UIPool(maxSizePerType: 1);
            _pool.RegisterPrefab(typeof(MockUIBase), _prefab);

            var ui1 = _pool.GetOrCreate(typeof(MockUIBase), _parent) as MockUIBase;
            var ui2 = _pool.GetOrCreate(typeof(MockUIBase), _parent) as MockUIBase;

            _pool.ReturnToPool(typeof(MockUIBase), ui1);
            _pool.ReturnToPool(typeof(MockUIBase), ui2);

            yield return null; // 等一帧

            Assert.IsTrue(ui2 == null);
        }

        [Test]
        public void ClearPool_WithPooledInstances_ShouldDestroyAll()
        {
            // Arrange
            _pool.RegisterPrefab(typeof(MockUIBase), _prefab);
            var ui1 = _pool.GetOrCreate(typeof(MockUIBase), _parent) as MockUIBase;
            var ui2 = _pool.GetOrCreate(typeof(MockUIBase), _parent) as MockUIBase;
            _pool.ReturnToPool(typeof(MockUIBase), ui1);
            _pool.ReturnToPool(typeof(MockUIBase), ui2);

            // Act
            _pool.ClearPool(typeof(MockUIBase));

            // Assert
            var ui3 = _pool.GetOrCreate(typeof(MockUIBase), _parent) as MockUIBase;
            Assert.IsNotNull(ui3);
            Assert.AreNotSame(ui1, ui3, "应该创建新实例");
        }

        [Test]
        public void ClearAllPools_WithMultipleTypes_ShouldClearAll()
        {
            // Arrange
            _pool.RegisterPrefab(typeof(MockUIBase), _prefab);
            var ui = _pool.GetOrCreate(typeof(MockUIBase), _parent) as MockUIBase;
            _pool.ReturnToPool(typeof(MockUIBase), ui);

            // Act
            _pool.ClearAllPools();

            _pool.RegisterPrefab(typeof(MockUIBase), _prefab);
            // Assert
            var newUI = _pool.GetOrCreate(typeof(MockUIBase), _parent) as MockUIBase;
            Assert.IsNotNull(newUI);
            Assert.AreNotSame(ui, newUI);
        }
    }
}

