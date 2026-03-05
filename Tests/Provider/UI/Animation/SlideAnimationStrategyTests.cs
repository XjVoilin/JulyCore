using System.Collections;
using Cysharp.Threading.Tasks;
using JulyCore.Data.UI;
using JulyCore.Provider.UI.Animation;
using JulyGF.Tests.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JulyGF.Tests.Provider.UI.Animation
{
    [TestFixture]
    public class SlideAnimationStrategyTests
    {
        private SlideAnimationStrategy _strategy;
        private GameObject _uiGameObject;
        private MockUIBase _ui;
        private RectTransform _rectTransform;

        [SetUp]
        public void SetUp()
        {
            _uiGameObject = TestHelpers.CreateMockUIGameObject("TestUI");
            _rectTransform = _uiGameObject.GetComponent<RectTransform>();
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
            _strategy = new SlideAnimationStrategy(UIAnimationType.SlideFromTop);

            bool isSupported = _strategy.IsSupported(_ui);

            Assert.IsTrue(isSupported);
        }

        [Test]
        public void IsSupported_NullUI_ShouldReturnFalse()
        {
            _strategy = new SlideAnimationStrategy(UIAnimationType.SlideFromTop);

            bool isSupported = _strategy.IsSupported(null);

            Assert.IsFalse(isSupported);
        }

        [UnityTest]
        public IEnumerator PlayOpenAnimationAsync_NullUI_ShouldNotThrow()
        {
            _strategy = new SlideAnimationStrategy(UIAnimationType.SlideFromTop);

            yield return _strategy.PlayOpenAnimationAsync(null).ToCoroutine();
        }

        [UnityTest]
        public IEnumerator PlayCloseAnimationAsync_NullUI_ShouldNotThrow()
        {
            _strategy = new SlideAnimationStrategy(UIAnimationType.SlideFromTop);

            yield return _strategy.PlayCloseAnimationAsync(null).ToCoroutine();
        }
    }
}

