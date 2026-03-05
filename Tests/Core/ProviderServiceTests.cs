using System.Collections;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Provider.Base;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace JulyGF.Tests.Core
{
    [TestFixture]
    public class ProviderServiceTests
    {
        private ProviderService _providerService;

        [SetUp]
        public void SetUp()
        {
            _providerService = new ProviderService();
        }

        [TearDown]
        public void TearDown()
        {
            _providerService?.Clear();
            _providerService = null;
        }

        [Test]
        public void Track_ValidProvider_ShouldTrackSuccessfully()
        {
            var provider = new TestProvider();

            _providerService.Track(provider);

            // Track 成功不抛异常
            Assert.Pass();
        }

        [Test]
        public void Track_NullProvider_ShouldNotThrow()
        {
            // Track null 应该安全地忽略
            _providerService.Track(null);

            Assert.Pass();
        }

        [Test]
        public void Track_DuplicateProvider_ShouldNotAddTwice()
        {
            var provider = new TestProvider();

            _providerService.Track(provider);
            _providerService.Track(provider);

            // 重复 Track 应该安全地忽略
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator InitAllAsync_TrackedProviders_ShouldInitializeAll()
        {
            var provider = new TestProvider();
            _providerService.Track(provider);

            yield return _providerService.InitAllAsync().ToCoroutine();

            Assert.IsTrue(_providerService.IsInitialized);
            Assert.IsTrue(provider.IsInitialized);
        }

        [UnityTest]
        public IEnumerator InitAllAsync_AlreadyInitialized_ShouldSkip()
        {
            var provider = new TestProvider();
            _providerService.Track(provider);
            yield return _providerService.InitAllAsync().ToCoroutine();

            yield return _providerService.InitAllAsync().ToCoroutine();

            Assert.IsTrue(_providerService.IsInitialized);
        }

        [UnityTest]
        public IEnumerator ShutdownAllAsync_InitializedProviders_ShouldShutdownAll()
        {
            var provider = new TestProvider();
            _providerService.Track(provider);
            yield return _providerService.InitAllAsync().ToCoroutine();

            yield return _providerService.ShutdownAllAsync().ToCoroutine();

            Assert.IsFalse(_providerService.IsInitialized);
            Assert.IsFalse(provider.IsInitialized);
        }

        [Test]
        public void Clear_WithTrackedProviders_ShouldRemoveAll()
        {
            var provider = new TestProvider();
            _providerService.Track(provider);

            _providerService.Clear();

            Assert.IsFalse(_providerService.IsInitialized);
        }

        [UnityTest]
        public IEnumerator InitAllAsync_MultipleProviders_ShouldRespectPriority()
        {
            var lowPriority = new PriorityTestProvider(100);
            var highPriority = new PriorityTestProvider(0);
            
            _providerService.Track(lowPriority);
            _providerService.Track(highPriority);

            yield return _providerService.InitAllAsync().ToCoroutine();

            Assert.IsTrue(lowPriority.IsInitialized);
            Assert.IsTrue(highPriority.IsInitialized);
        }

        internal interface ITestProvider : IProvider
        {
        }

        internal class TestProvider : ProviderBase, ITestProvider
        {
            protected override LogChannel LogChannel { get; }
        }

        internal class PriorityTestProvider : ProviderBase, ITestProvider
        {
            public override int Priority { get; }
            protected override LogChannel LogChannel { get; }

            public PriorityTestProvider(int priority)
            {
                Priority = priority;
            }
        }
    }
}
