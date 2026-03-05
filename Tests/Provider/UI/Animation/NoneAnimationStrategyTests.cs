using System.Collections;
using JulyCore.Provider.UI;
using JulyCore.Provider.UI.Animation;
using JulyGF.Tests.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JulyGF.Tests.Provider.UI.Animation
{
    /// <summary>
    /// 无动画策略测试
    /// </summary>
    [TestFixture]
    public class NoneAnimationStrategyTests
    {
        private NoneAnimationStrategy _strategy;
        private MockUIBase _ui;

        [SetUp]
        public void SetUp()
        {
            _strategy = new NoneAnimationStrategy();
            var go = TestHelpers.CreateMockUIGameObject("TestUI");
            _ui = go.AddComponent<MockUIBase>();
        }

        [TearDown]
        public void TearDown()
        {
            TestHelpers.DestroyGameObject(_ui?.gameObject);
            _strategy = null;
        }

        [Test]
        public void IsSupported_AnyUI_ShouldReturnTrue()
        {
            // Act
            bool isSupported = _strategy.IsSupported(_ui);

            // Assert
            Assert.IsTrue(isSupported);
        }

        [Test]
        public void IsSupported_NullUI_ShouldReturnTrue()
        {
            // Act
            bool isSupported = _strategy.IsSupported(null);

            // Assert
            Assert.IsTrue(isSupported, "None策略应该总是支持");
        }

        [UnityTest]
        public IEnumerator PlayOpenAnimationAsync_ShouldCompleteImmediately()
        {
            // Arrange
            float startTime = Time.time;

            // Act
            yield return _strategy.PlayOpenAnimationAsync(new UIInfo() { UI = _ui });

            // Assert
            float elapsed = Time.time - startTime;
            Assert.Less(elapsed, 0.1f, "无动画应该立即完成");
        }

        [UnityTest]
        public IEnumerator PlayCloseAnimationAsync_ShouldCompleteImmediately()
        {
            // Arrange
            float startTime = Time.time;

            // Act
            yield return _strategy.PlayCloseAnimationAsync(new UIInfo() { UI = _ui });

            // Assert
            float elapsed = Time.time - startTime;
            Assert.Less(elapsed, 0.1f, "无动画应该立即完成");
        }
    }
}