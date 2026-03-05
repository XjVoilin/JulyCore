using JulyCore.Core;

namespace JulyGF.Tests.Utils
{
    /// <summary>
    /// 测试事件类
    /// 用于测试事件总线
    /// </summary>
    public class TestEvent : IEvent
    {
        public string Message { get; set; }
        public int Value { get; set; }

        public TestEvent(string message = "", int value = 0)
        {
            Message = message;
            Value = value;
        }
    }

    /// <summary>
    /// 测试事件类2
    /// 用于测试不同类型的事件
    /// </summary>
    public class TestEvent2 : IEvent
    {
        public string Data { get; set; }
    }
}

