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
    [TestFixture]
    public class AnimatorAnimationStrategyTests
    {
        private AnimatorAnimationStrategy _strategy;
        private GameObject _uiGameObject;
        private MockUIBase _ui;
        private Animator _animator;

        [SetUp]
        public void SetUp()
        {
            _strategy = new AnimatorAnimationStrategy();
            _uiGameObject = TestHelpers.CreateMockUIGameObject("TestUI");
            _ui = _uiGameObject.AddComponent<MockUIBase>();
            _animator = _uiGameObject.AddComponent<Animator>();
        }

        [TearDown]
        public void TearDown()
        {
            TestHelpers.DestroyGameObject(_uiGameObject);
            _strategy = null;
        }

        [Test]
        public void IsSupported_NullUI_ShouldReturnFalse()
        {
            bool isSupported = _strategy.IsSupported(null);

            Assert.IsFalse(isSupported);
        }

        [Test]
        public void IsSupported_UIWithoutAnimator_ShouldReturnFalse()
        {
            Object.DestroyImmediate(_animator);

            bool isSupported = _strategy.IsSupported(_ui);

            Assert.IsFalse(isSupported);
        }

        [Test]
        public void IsSupported_UIWithAnimatorWithoutController_ShouldReturnFalse()
        {
            _animator.runtimeAnimatorController = null;

            bool isSupported = _strategy.IsSupported(_ui);

            Assert.IsFalse(isSupported);
        }

        [UnityTest]
        public IEnumerator PlayOpenAnimationAsync_NullUI_ShouldNotThrow()
        {
            yield return _strategy.PlayOpenAnimationAsync(null).ToCoroutine();
        }

        [UnityTest]
        public IEnumerator PlayCloseAnimationAsync_NullUI_ShouldNotThrow()
        {
            yield return _strategy.PlayCloseAnimationAsync(null).ToCoroutine();
        }
    }
}

