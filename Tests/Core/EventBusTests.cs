using System;
using System.Collections.Generic;
using JulyCore.Core;
using JulyCore.Core.Config;
using JulyGF.Tests.Utils;
using NUnit.Framework;

namespace JulyGF.Tests.Core
{
    /// <summary>
    /// 事件总线测试
    /// </summary>
    [TestFixture]
    public class EventBusTests
    {
        private EventBus _eventBus;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus(new EventBusConfig());
        }

        [TearDown]
        public void TearDown()
        {
            _eventBus = null;
        }

        #region 同步事件测试

        [Test]
        public void Subscribe_ValidHandler_ShouldAddToHandlers()
        {
            // Arrange
            bool handlerCalled = false;
            Action<TestEvent> handler = evt => handlerCalled = true;

            // Act
            _eventBus.Subscribe(handler, this);

            // Assert
            _eventBus.Publish(new TestEvent());
            Assert.IsTrue(handlerCalled);
        }

        [Test]
        public void Subscribe_NullHandler_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => { _eventBus.Subscribe<TestEvent>(null, this); });
        }

        [Test]
        public void Publish_WithSubscribedHandler_ShouldInvokeHandler()
        {
            // Arrange
            TestEvent receivedEvent = null;
            var testEvent = new TestEvent("Test", 42);
            _eventBus.Subscribe<TestEvent>(evt => receivedEvent = evt, this);

            // Act
            _eventBus.Publish(testEvent);

            // Assert
            Assert.IsNotNull(receivedEvent);
            Assert.AreEqual("Test", receivedEvent.Message);
            Assert.AreEqual(42, receivedEvent.Value);
        }

        [Test]
        public void Publish_MultipleHandlers_ShouldInvokeAllHandlers()
        {
            // Arrange
            int callCount = 0;
            _eventBus.Subscribe<TestEvent>(evt => callCount++, this);
            _eventBus.Subscribe<TestEvent>(evt => callCount++, this);
            _eventBus.Subscribe<TestEvent>(evt => callCount++, this);

            // Act
            _eventBus.Publish(new TestEvent());

            // Assert
            Assert.AreEqual(3, callCount);
        }

        [Test]
        public void Unsubscribe_ExistingHandler_ShouldRemoveHandler()
        {
            // Arrange
            int callCount = 0;
            Action<TestEvent> handler = evt => callCount++;
            _eventBus.Subscribe(handler, this);

            // Act
            _eventBus.Unsubscribe(handler);
            _eventBus.Publish(new TestEvent());

            // Assert
            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void Unsubscribe_NonExistentHandler_ShouldNotThrow()
        {
            // Arrange
            Action<TestEvent> handler = evt => { };

            // Act & Assert
            Assert.DoesNotThrow(() => { _eventBus.Unsubscribe(handler); });
        }

        [Test]
        public void Publish_DifferentEventTypes_ShouldOnlyInvokeMatchingHandlers()
        {
            // Arrange
            int testEvent1Count = 0;
            int testEvent2Count = 0;
            _eventBus.Subscribe<TestEvent>(evt => testEvent1Count++, this);
            _eventBus.Subscribe<TestEvent2>(evt => testEvent2Count++, this);

            // Act
            _eventBus.Publish(new TestEvent());
            _eventBus.Publish(new TestEvent2());

            // Assert
            Assert.AreEqual(1, testEvent1Count);
            Assert.AreEqual(1, testEvent2Count);
        }

        #endregion

        #region 异常处理测试

        [Test]
        public void Publish_HandlerThrowsException_ShouldNotAffectOtherHandlers()
        {
            // Arrange
            int successCount = 0;
            _eventBus.Subscribe<TestEvent>(evt => throw new Exception("Test exception"), this);
            _eventBus.Subscribe<TestEvent>(evt => successCount++, this);

            // Act & Assert
            // 应该不会抛出异常，其他处理器应该正常执行
            Assert.DoesNotThrow(() => { _eventBus.Publish(new TestEvent()); });
            Assert.AreEqual(1, successCount);
        }

        #endregion

        #region 优先级测试

        [Test]
        public void Subscribe_WithPriority_ShouldExecuteInPriorityOrder()
        {
            // Arrange
            var executionOrder = new List<int>();
            _eventBus.Subscribe<TestEvent>(evt => executionOrder.Add(3), this, 3);
            _eventBus.Subscribe<TestEvent>(evt => executionOrder.Add(1), this, 1);
            _eventBus.Subscribe<TestEvent>(evt => executionOrder.Add(2), this, 2);

            // Act
            _eventBus.Publish(new TestEvent());

            // Assert
            Assert.AreEqual(3, executionOrder.Count);
            Assert.AreEqual(1, executionOrder[0]); // 优先级1先执行
            Assert.AreEqual(2, executionOrder[1]); // 优先级2其次
            Assert.AreEqual(3, executionOrder[2]); // 优先级3最后
        }

        [Test]
        public void Subscribe_SamePriority_ShouldExecuteInSubscriptionOrder()
        {
            // Arrange
            var executionOrder = new List<int>();
            _eventBus.Subscribe<TestEvent>(evt => executionOrder.Add(1), this, 0);
            _eventBus.Subscribe<TestEvent>(evt => executionOrder.Add(2), this, 0);
            _eventBus.Subscribe<TestEvent>(evt => executionOrder.Add(3), this, 0);

            // Act
            _eventBus.Publish(new TestEvent());

            // Assert
            Assert.AreEqual(3, executionOrder.Count);
            // 相同优先级按订阅顺序执行
            Assert.AreEqual(1, executionOrder[0]);
            Assert.AreEqual(2, executionOrder[1]);
            Assert.AreEqual(3, executionOrder[2]);
        }

        #endregion

        #region 批量取消订阅测试

        [Test]
        public void UnsubscribeAll_WithMultipleSubscriptions_ShouldRemoveAll()
        {
            // Arrange
            int callCount = 0;
            _eventBus.Subscribe<TestEvent>(evt => callCount++, this);
            _eventBus.Subscribe<TestEvent>(evt => callCount++, this);
            _eventBus.Subscribe<TestEvent2>(evt => callCount++, this);

            // Act
            _eventBus.UnsubscribeAll(this);
            _eventBus.Publish(new TestEvent());
            _eventBus.Publish(new TestEvent2());

            // Assert
            Assert.AreEqual(0, callCount);
        }

        #endregion
    }
}
