using System;
using JulyCore.Core;
using NUnit.Framework;

namespace JulyGF.Tests.Core
{
    /// <summary>
    /// 依赖注入容器测试
    /// </summary>
    [TestFixture]
    public class DependencyContainerTests
    {
        private DependencyContainer _container;

        [SetUp]
        public void SetUp()
        {
            _container = new DependencyContainer();
        }

        [TearDown]
        public void TearDown()
        {
            _container.Clear();
            _container = null;
        }

        #region 单例注册测试

        [Test]
        public void RegisterSingleton_ValidInstance_ShouldRegisterSuccessfully()
        {
            // Arrange
            var service = new TestService();

            // Act
            _container.RegisterSingleton<ITestService>(service);

            // Assert
            var resolved = _container.Resolve<ITestService>();
            Assert.IsNotNull(resolved);
            Assert.AreSame(service, resolved);
        }

        [Test]
        public void RegisterSingleton_NullInstance_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
            {
                _container.RegisterSingleton<ITestService>(null);
            });
        }

        [Test]
        public void Resolve_RegisteredSingleton_ShouldReturnSameInstance()
        {
            // Arrange
            var service = new TestService();
            _container.RegisterSingleton<ITestService>(service);

            // Act
            var resolved1 = _container.Resolve<ITestService>();
            var resolved2 = _container.Resolve<ITestService>();

            // Assert
            Assert.AreSame(resolved1, resolved2);
            Assert.AreSame(service, resolved1);
        }

        [Test]
        public void Resolve_UnregisteredService_ShouldThrowJulyException()
        {
            // Act & Assert
            Assert.Throws<JulyException>(() =>
            {
                _container.Resolve<ITestService>();
            });
        }

        #endregion

        #region 工厂注册测试

        [Test]
        public void RegisterFactory_ValidFactory_ShouldRegisterSuccessfully()
        {
            // Arrange
            int callCount = 0;
            _container.RegisterFactory<ITestService>(() =>
            {
                callCount++;
                return new TestService();
            });

            // Act
            var service1 = _container.Resolve<ITestService>();
            var service2 = _container.Resolve<ITestService>();

            // Assert
            Assert.IsNotNull(service1);
            Assert.IsNotNull(service2);
            Assert.AreNotSame(service1, service2); // 瞬态服务应该返回不同实例
            Assert.AreEqual(2, callCount);
        }

        [Test]
        public void RegisterTransient_ValidFactory_ShouldCreateNewInstanceEachTime()
        {
            // Arrange
            _container.RegisterTransient<ITestService>(container => new TestService());

            // Act
            var service1 = _container.Resolve<ITestService>();
            var service2 = _container.Resolve<ITestService>();

            // Assert
            Assert.AreNotSame(service1, service2);
        }

        [Test]
        public void RegisterFactory_NullFactory_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
            {
                _container.RegisterFactory<ITestService>(null);
            });
        }

        #endregion

        #region TryResolve测试

        [Test]
        public void TryResolve_RegisteredService_ShouldReturnTrue()
        {
            // Arrange
            var service = new TestService();
            _container.RegisterSingleton<ITestService>(service);

            // Act
            bool success = _container.TryResolve<ITestService>(out var resolved);

            // Assert
            Assert.IsTrue(success);
            Assert.IsNotNull(resolved);
            Assert.AreSame(service, resolved);
        }

        [Test]
        public void TryResolve_UnregisteredService_ShouldReturnFalse()
        {
            // Act
            bool success = _container.TryResolve<ITestService>(out var resolved);

            // Assert
            Assert.IsFalse(success);
            Assert.IsNull(resolved);
        }

        #endregion

        #region IsRegistered测试

        [Test]
        public void IsRegistered_RegisteredService_ShouldReturnTrue()
        {
            // Arrange
            var service = new TestService();
            _container.RegisterSingleton<ITestService>(service);

            // Act
            bool isRegistered = _container.IsRegistered<ITestService>();

            // Assert
            Assert.IsTrue(isRegistered);
        }

        [Test]
        public void IsRegistered_UnregisteredService_ShouldReturnFalse()
        {
            // Act
            bool isRegistered = _container.IsRegistered<ITestService>();

            // Assert
            Assert.IsFalse(isRegistered);
        }

        #endregion

        #region Clear测试

        [Test]
        public void Clear_WithRegisteredServices_ShouldRemoveAllServices()
        {
            // Arrange
            var service = new TestService();
            _container.RegisterSingleton<ITestService>(service);

            // Act
            _container.Clear();

            // Assert
            Assert.IsFalse(_container.IsRegistered<ITestService>());
            Assert.Throws<JulyException>(() => _container.Resolve<ITestService>());
        }

        #endregion

        #region 构造函数注入测试

        [Test]
        public void RegisterTransient_WithConstructorInjection_ShouldResolveDependencies()
        {
            // Arrange
            _container.RegisterSingleton<ITestService>(new TestService());
            _container.RegisterTransient<ITestServiceWithDependency, TestServiceWithDependency>();

            // Act
            var service = _container.Resolve<ITestServiceWithDependency>();

            // Assert
            Assert.IsNotNull(service);
            Assert.IsNotNull(service.Dependency);
        }

        [Test]
        public void RegisterTransient_WithNestedDependencies_ShouldResolveAll()
        {
            // Arrange
            _container.RegisterSingleton<ITestService>(new TestService());
            _container.RegisterTransient<ITestServiceWithDependency, TestServiceWithDependency>();
            _container.RegisterTransient<ITestServiceWithNestedDependency, TestServiceWithNestedDependency>();

            // Act
            var service = _container.Resolve<ITestServiceWithNestedDependency>();

            // Assert
            Assert.IsNotNull(service);
            Assert.IsNotNull(service.NestedDependency);
            Assert.IsNotNull(service.NestedDependency.Dependency);
        }

        [Test]
        public void RegisterTransient_CircularDependency_ShouldThrowException()
        {
            // Arrange
            _container.RegisterTransient<ICircularServiceA, CircularServiceA>();
            _container.RegisterTransient<ICircularServiceB, CircularServiceB>();

            // Act & Assert
            Assert.Throws<JulyException>(() =>
            {
                _container.Resolve<ICircularServiceA>();
            });
        }

        [Test]
        public void RegisterTransient_MissingDependency_ShouldThrowException()
        {
            // Arrange
            _container.RegisterTransient<ITestServiceWithDependency, TestServiceWithDependency>();

            // Act & Assert
            Assert.Throws<JulyException>(() =>
            {
                _container.Resolve<ITestServiceWithDependency>();
            });
        }

        #endregion

        #region 测试接口和实现

        public interface ITestService
        {
            string Name { get; }
        }

        public class TestService : ITestService
        {
            public string Name => "TestService";
        }

        public interface ITestServiceWithDependency
        {
            ITestService Dependency { get; }
        }

        public class TestServiceWithDependency : ITestServiceWithDependency
        {
            public ITestService Dependency { get; }

            public TestServiceWithDependency(ITestService dependency)
            {
                Dependency = dependency;
            }
        }

        public interface ITestServiceWithNestedDependency
        {
            ITestServiceWithDependency NestedDependency { get; }
        }

        public class TestServiceWithNestedDependency : ITestServiceWithNestedDependency
        {
            public ITestServiceWithDependency NestedDependency { get; }

            public TestServiceWithNestedDependency(ITestServiceWithDependency nestedDependency)
            {
                NestedDependency = nestedDependency;
            }
        }

        public interface ICircularServiceA
        {
            ICircularServiceB Dependency { get; }
        }

        public class CircularServiceA : ICircularServiceA
        {
            public ICircularServiceB Dependency { get; }

            public CircularServiceA(ICircularServiceB dependency)
            {
                Dependency = dependency;
            }
        }

        public interface ICircularServiceB
        {
            ICircularServiceA Dependency { get; }
        }

        public class CircularServiceB : ICircularServiceB
        {
            public ICircularServiceA Dependency { get; }

            public CircularServiceB(ICircularServiceA dependency)
            {
                Dependency = dependency;
            }
        }

        #endregion
    }
}

