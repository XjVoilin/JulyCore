using JulyCore.Provider.UI;
using UnityEngine;

namespace JulyGF.Tests.Utils
{
    /// <summary>
    /// Mock UI基类
    /// 用于测试的UI对象
    /// </summary>
    public class MockUIBase : UIBase
    {
        public int OnOpenCallCount { get; private set; }
        public int OnCloseCallCount { get; private set; }

        protected override void OnOpen()
        {
            OnOpenCallCount++;
        }

        protected override void OnClose()
        {
            OnCloseCallCount++;
        }

        /// <summary>
        /// 重置Mock状态
        /// </summary>
        public void Reset()
        {
            OnOpenCallCount = 0;
            OnCloseCallCount = 0;
        }
    }
}

