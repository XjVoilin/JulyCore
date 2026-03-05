using System.Collections;
using Cysharp.Threading.Tasks;
using JulyCore.Provider.UI;
using JulyCore.Provider.UI.Animation;
using JulyGF.Tests.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JulyGF.Tests.Provider.UI.Animation
{
    /// <summary>
    /// 缩放动画策略测试
    /// </summary>
    [TestFixture]
    public class ScaleAnimationStrategyTests
    {
        private ScaleAnimationStrategy _strategy;
        private GameObject _uiGameObject;
        private MockUIBase _ui;

        [SetUp]
        public void SetUp()
        {
            _strategy = new ScaleAnimationStrategy();
            _uiGameObject = TestHelpers.CreateMockUIGameObject("TestUI");
            _ui = _uiGameObject.AddComponent<MockUIBase>();
        }

        [TearDown]
        public void TearDown()
        {
            TestHelpers.DestroyGameObject(_uiGameObject);
            _strategy = null;
        }

        [Test]
        public void IsSupported_ValidUI_ShouldReturnTrue()
        {
            // Act
            bool isSupported = _strategy.IsSupported(_ui);

            // Assert
            Assert.IsTrue(isSupported);
        }

        [Test]
        public void IsSupported_NullUI_ShouldReturnFalse()
        {
            // Act
            bool isSupported = _strategy.IsSupported(null);

            // Assert
            Assert.IsFalse(isSupported);
        }

        [UnityTest]
        public IEnumerator PlayOpenAnimationAsync_ValidUI_ShouldScaleFromZeroToOne()
        {
            // Arrange
            _ui.transform.localScale = Vector3.zero;

            // Act
            yield return _strategy.PlayOpenAnimationAsync(new UIInfo() { UI = _ui }).ToCoroutine();

            // Assert
            Assert.GreaterOrEqual(_ui.transform.localScale.x, 0.99f, "Scale应该接近1");
            Assert.GreaterOrEqual(_ui.transform.localScale.y, 0.99f, "Scale应该接近1");
            Assert.GreaterOrEqual(_ui.transform.localScale.z, 0.99f, "Scale应该接近1");
        }

        [UnityTest]
        public IEnumerator PlayCloseAnimationAsync_ValidUI_ShouldScaleFromOneToZero()
        {
            // Act
            yield return _strategy.PlayCloseAnimationAsync(new UIInfo() { UI = _ui }).ToCoroutine();
            
            // Assert
            Assert.LessOrEqual(_ui.transform.localScale.x, 0.5f, "Scale应该接近0.5");
            Assert.LessOrEqual(_ui.transform.localScale.y, 0.5f, "Scale应该接近0.5");
            Assert.LessOrEqual(_ui.transform.localScale.z, 0.5f, "Scale应该接近0.5");
        }

        [UnityTest]
        public IEnumerator PlayOpenAnimationAsync_NullUI_ShouldNotThrow()
        {
            // Act & Assert
            yield return _strategy.PlayOpenAnimationAsync(null).ToCoroutine();
        }

        [UnityTest]
        public IEnumerator PlayCloseAnimationAsync_NullUI_ShouldNotThrow()
        {
            // Act & Assert
            yield return _strategy.PlayCloseAnimationAsync(null).ToCoroutine();
        }
    }
}

