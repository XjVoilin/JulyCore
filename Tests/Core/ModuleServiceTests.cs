using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Base;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace JulyGF.Tests.Core
{
    [TestFixture]
    public class ModuleServiceTests
    {
        private ModuleService _moduleService;

        [SetUp]
        public void SetUp()
        {
            _moduleService = new ModuleService();
        }

        [TearDown]
        public void TearDown()
        {
            _moduleService?.Clear();
            _moduleService = null;
        }

        [Test]
        public void RegisterModule_ValidModule_ShouldRegisterSuccessfully()
        {
            var module = new TestModule();

            _moduleService.RegisterModule(module);

            Assert.IsTrue(_moduleService.HasModule<TestModule>());
            var retrieved = _moduleService.GetModule<TestModule>();
            Assert.AreSame(module, retrieved);
        }

        [Test]
        public void RegisterModule_NullModule_ShouldThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                _moduleService.RegisterModule((IModule)null);
            });
        }

        [Test]
        public void RegisterModule_DuplicateModule_ShouldThrowJulyException()
        {
            var module = new TestModule();
            _moduleService.RegisterModule(module);

            Assert.Throws<JulyException>(() =>
            {
                _moduleService.RegisterModule(module);
            });
        }

        [Test]
        public void RegisterModule_Generic_ShouldCreateAndRegister()
        {
            _moduleService.RegisterModule<TestModule>();

            Assert.IsTrue(_moduleService.HasModule<TestModule>());
            var module = _moduleService.GetModule<TestModule>();
            Assert.IsNotNull(module);
        }

        [Test]
        public void GetModule_RegisteredModule_ShouldReturnModule()
        {
            var module = new TestModule();
            _moduleService.RegisterModule(module);

            var retrieved = _moduleService.GetModule<TestModule>();

            Assert.IsNotNull(retrieved);
            Assert.AreSame(module, retrieved);
        }

        [Test]
        public void GetModule_UnregisteredModule_ShouldReturnNull()
        {
            var module = _moduleService.GetModule<TestModule>();

            Assert.IsNull(module);
        }

        [Test]
        public void TryGetModule_RegisteredModule_ShouldReturnTrue()
        {
            var module = new TestModule();
            _moduleService.RegisterModule(module);

            bool success = _moduleService.TryGetModule<TestModule>(out var retrieved);

            Assert.IsTrue(success);
            Assert.IsNotNull(retrieved);
            Assert.AreSame(module, retrieved);
        }

        [Test]
        public void TryGetModule_UnregisteredModule_ShouldReturnFalse()
        {
            bool success = _moduleService.TryGetModule<TestModule>(out var module);

            Assert.IsFalse(success);
            Assert.IsNull(module);
        }

        [Test]
        public void HasModule_RegisteredModule_ShouldReturnTrue()
        {
            var module = new TestModule();
            _moduleService.RegisterModule(module);

            bool hasModule = _moduleService.HasModule<TestModule>();

            Assert.IsTrue(hasModule);
        }

        [Test]
        public void HasModule_UnregisteredModule_ShouldReturnFalse()
        {
            bool hasModule = _moduleService.HasModule<TestModule>();

            Assert.IsFalse(hasModule);
        }

        [UnityTest]
        public IEnumerator InitAsync_RegisteredModules_ShouldInitializeAll()
        {
            var module1 = new TestModule();
            var module2 = new TestModule2();
            _moduleService.RegisterModule(module1);
            _moduleService.RegisterModule(module2);

            yield return _moduleService.InitAllAsync().ToCoroutine();

            Assert.IsTrue(_moduleService.IsInitialized);
            Assert.IsTrue(module1.IsInitialized);
            Assert.IsTrue(module2.IsInitialized);
        }

        [UnityTest]
        public IEnumerator InitAsync_AlreadyInitialized_ShouldSkip()
        {
            var module = new TestModule();
            _moduleService.RegisterModule(module);
            yield return _moduleService.InitAllAsync().ToCoroutine();

            yield return _moduleService.InitAllAsync().ToCoroutine();

            Assert.IsTrue(_moduleService.IsInitialized);
        }

        [UnityTest]
        public IEnumerator Shutdown_InitializedModules_ShouldShutdownAll()
        {
            var module = new TestModule();
            _moduleService.RegisterModule(module);
            yield return _moduleService.InitAllAsync().ToCoroutine();

            _moduleService.Shutdown();

            Assert.IsFalse(_moduleService.IsInitialized);
            Assert.IsFalse(module.IsInitialized);
        }

        [UnityTest]
        public IEnumerator Update_InitializedModule_ShouldCallUpdate()
        {
            var module = new TestModule();
            _moduleService.RegisterModule(module);
            yield return _moduleService.InitAllAsync().ToCoroutine();

            _moduleService.Update(0.1f, 0.1f);

            Assert.Greater(module.UpdateCallCount, 0);
        }

        [Test]
        public void Clear_WithRegisteredModules_ShouldRemoveAll()
        {
            var module = new TestModule();
            _moduleService.RegisterModule(module);

            _moduleService.Clear();

            Assert.IsFalse(_moduleService.HasModule<TestModule>());
        }

        [UnityTest]
        public IEnumerator InitAsync_WithDependency_ShouldInitializeInOrder()
        {
            var moduleA = new TestModuleA();
            var moduleB = new TestModuleB(); // B depends on A
            _moduleService.RegisterModule(moduleB);
            _moduleService.RegisterModule(moduleA);

            yield return _moduleService.InitAllAsync().ToCoroutine();

            Assert.IsTrue(moduleA.IsInitialized);
            Assert.IsTrue(moduleB.IsInitialized);
            // A should be initialized before B
            Assert.Less(moduleA.InitOrder, moduleB.InitOrder);
        }

        [UnityTest]
        public IEnumerator InitAsync_WithMissingDependency_ShouldThrowException()
        {
            var moduleB = new TestModuleB(); // B depends on A, but A is not registered
            _moduleService.RegisterModule(moduleB);

            JulyException exception = null;
            yield return UniTask.Create(async () =>
            {
                try
                {
                    await _moduleService.InitAllAsync();
                }
                catch (JulyException ex)
                {
                    exception = ex;
                }
            }).ToCoroutine();

            Assert.IsNotNull(exception);
        }

        #region Test Modules

        private static int _initCounter = 0;

        private class TestModule : ModuleBase
        {
            public int UpdateCallCount { get; private set; } 
            public override int Priority { get; } = 10;
            protected override LogChannel LogChannel { get; }

            protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
            {
                UpdateCallCount++;
            }
        }

        private class TestModule2 : ModuleBase
        {
            public override int Priority { get; } = 5;
            protected override LogChannel LogChannel { get; }
        }

        private class TestModuleA : ModuleBase
        {
            public int InitOrder { get; private set; }
            public override int Priority { get; } = 1;
            protected override LogChannel LogChannel { get; }

            protected override UniTask OnInitAsync()
            {
                InitOrder = _initCounter++;
                return UniTask.CompletedTask;
            }
        }

        private class TestModuleB : ModuleBase, IModuleDependency
        {
            public int InitOrder { get; private set; }
            public override int Priority { get; } = 2;
            protected override LogChannel LogChannel { get; }

            public IEnumerable<Type> GetDependencies()
            {
                return new[] { typeof(TestModuleA) };
            }

            protected override UniTask OnInitAsync()
            {
                InitOrder = _initCounter++;
                return UniTask.CompletedTask;
            }
        }

        #endregion
    }
}
