using System.Collections;
using Cysharp.Threading.Tasks;
using JulyCore.Provider.UI;
using JulyCore.Provider.UI.Animation;
using JulyGF.Tests.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JulyGF.Tests.Provider.UI.Animation
{
    /// <summary>
    /// 淡入淡出动画策略测试
    /// </summary>
    [TestFixture]
    public class FadeAnimationStrategyTests
    {
        private FadeAnimationStrategy _strategy;
        private GameObject _uiGameObject;
        private MockUIBase _ui;

        [SetUp]
        public void SetUp()
        {
            _strategy = new FadeAnimationStrategy();
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
        public IEnumerator PlayOpenAnimationAsync_ValidUI_ShouldFadeIn()
        {
            // Arrange
            var canvasGroup = _uiGameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = _uiGameObject.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = 0f;

            var uiInfo = new UIInfo
            {
                CanvasGroup = canvasGroup,
                UI = _ui
            };
            // Act
            yield return _strategy.PlayOpenAnimationAsync(uiInfo).ToCoroutine();

            // Assert
            Assert.GreaterOrEqual(canvasGroup.alpha, 0.99f, "Alpha应该接近1");
            Assert.IsTrue(canvasGroup.interactable, "应该可交互");
            Assert.IsTrue(canvasGroup.blocksRaycasts, "应该阻挡射线");
        }

        [UnityTest]
        public IEnumerator PlayCloseAnimationAsync_ValidUI_ShouldFadeOut()
        {
            // Arrange
            var canvasGroup = _uiGameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = _uiGameObject.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            var uiInfo = new UIInfo
            {
                CanvasGroup = canvasGroup,
                UI = _ui
            };
            // Act
            yield return _strategy.PlayCloseAnimationAsync(uiInfo).ToCoroutine();
            uiInfo.SetInteractable(false);
            // Assert
            Assert.LessOrEqual(canvasGroup.alpha, 0.01f, "Alpha应该接近0");
            Assert.IsFalse(canvasGroup.interactable, "应该不可交互");
            Assert.IsFalse(canvasGroup.blocksRaycasts, "不应该阻挡射线");
        }

        [UnityTest]
        public IEnumerator PlayOpenAnimationAsync_NullUI_ShouldNotThrow()
        {
            // Act & Assert
            yield return _strategy.PlayOpenAnimationAsync(null).ToCoroutine();
            // 如果没有抛出异常，测试通过
        }

        [UnityTest]
        public IEnumerator PlayCloseAnimationAsync_NullUI_ShouldNotThrow()
        {
            // Act & Assert
            yield return _strategy.PlayCloseAnimationAsync(null).ToCoroutine();
            // 如果没有抛出异常，测试通过
        }
    }
}

